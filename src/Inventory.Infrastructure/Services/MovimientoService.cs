using ClosedXML.Excel;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class MovimientoService : IMovimientoService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public MovimientoService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // Un Movimiento nunca se edita ni se borra (ver CLAUDE.md) — solo hace falta el
    // snapshot de "nuevo" en el momento de crearlo, nunca un "anterior".
    private static object Snapshot(Movimiento m) => new
    {
        m.NumeroMovimiento, m.ProductoId, m.TipoMovimiento, m.Cantidad, m.UbicacionId,
        m.Retorna, m.UbicacionExterna, m.FechaRetornoEsperada, m.FechaVencimiento,
        m.MovimientoOrigenId, m.SolicitudDetalleId, m.Motivo,
    };

    public Task<MovimientoResultadoDto> RegistrarEntradaAsync(RegistrarEntradaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct) =>
        EjecutarAsync(async (producto, ubicacion, tx) =>
        {
            var detalle = await ValidarDetalleParaEntregaAsync(dto.SolicitudDetalleId, producto.Id, TipoSolicitud.Entrada, dto.Cantidad, ct);

            var movimiento = NuevoMovimiento(producto.Id, TipoMovimiento.Entrada, dto.Cantidad, dto.Cantidad, ubicacion.Id, usuario, dto.Motivo);
            movimiento.SolicitudDetalleId = detalle?.Id;
            movimiento.FechaVencimiento = dto.FechaVencimiento;

            await GuardarConNumeroAsync(movimiento, "MOV", ct);

            if (detalle is not null)
                await AcumularEntregaAsync(detalle, dto.Cantidad, ct);

            await _auditoria.RegistrarAsync(nameof(Movimiento), movimiento.NumeroMovimiento, "RegistrarEntrada", null, _auditoria.Capturar(Snapshot(movimiento)), paisId, usuario, null, ct);

            return movimiento;
        }, dto.ProductoId, dto.UbicacionId, paisId, ct);

    public Task<MovimientoResultadoDto> RegistrarSalidaAsync(RegistrarSalidaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct) =>
        EjecutarAsync(async (producto, ubicacion, tx) =>
        {
            if (dto.Cantidad <= 0)
                throw new ArgumentOutOfRangeException(nameof(dto.Cantidad), "La cantidad debe ser mayor a cero.");

            ValidarTextoOpcional(dto.Motivo, 2000, "El motivo");
            if (dto.Retorna)
            {
                // Un préstamo sin destino ni fecha de retorno no se puede reclamar después.
                if (string.IsNullOrWhiteSpace(dto.UbicacionExterna))
                    throw new ValidacionException("Indicá a dónde va el material (ubicación externa) para registrar un préstamo.");
                if (dto.UbicacionExterna.Trim().Length > 255)
                    throw new ValidacionException("La ubicación externa no puede superar los 255 caracteres.");
                if (dto.FechaRetornoEsperada is null)
                    throw new ValidacionException("Indicá la fecha de retorno esperada del préstamo.");
                if (dto.FechaRetornoEsperada.Value < DateOnly.FromDateTime(DateTime.Today))
                    throw new ValidacionException("La fecha de retorno esperada no puede estar en el pasado.");
            }

            var existenciaUbicacion = await CalcularExistenciaEnUbicacionAsync(producto.Id, ubicacion.Id, ct);
            if (dto.Cantidad > existenciaUbicacion)
                throw new StockInsuficienteException($"{producto.CodigoProducto} en {ubicacion.CodigoUbicacion}", existenciaUbicacion, dto.Cantidad);

            var detalle = await ValidarDetalleParaEntregaAsync(dto.SolicitudDetalleId, producto.Id, TipoSolicitud.Salida, dto.Cantidad, ct);

            var movimiento = NuevoMovimiento(producto.Id, TipoMovimiento.Salida, dto.Cantidad, -dto.Cantidad, ubicacion.Id, usuario, dto.Motivo);
            movimiento.Retorna = dto.Retorna;
            movimiento.UbicacionExterna = dto.Retorna ? dto.UbicacionExterna!.Trim() : null;
            movimiento.FechaRetornoEsperada = dto.Retorna ? dto.FechaRetornoEsperada : null;
            movimiento.SolicitudDetalleId = detalle?.Id;

            await GuardarConNumeroAsync(movimiento, "MOV", ct);

            if (detalle is not null)
                await AcumularEntregaAsync(detalle, dto.Cantidad, ct);

            await _auditoria.RegistrarAsync(nameof(Movimiento), movimiento.NumeroMovimiento, "RegistrarSalida", null, _auditoria.Capturar(Snapshot(movimiento)), paisId, usuario, null, ct);

            return movimiento;
        }, dto.ProductoId, dto.UbicacionId, paisId, ct);

    public Task<MovimientoResultadoDto> RegistrarAjusteAsync(RegistrarAjusteDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct) =>
        EjecutarAsync(async (producto, ubicacion, tx) =>
        {
            if (dto.Cantidad <= 0)
                throw new ArgumentOutOfRangeException(nameof(dto.Cantidad), "La cantidad debe ser mayor a cero.");

            // Un ajuste mueve stock sin documento de respaldo: el motivo es lo único que lo explica.
            if (string.IsNullOrWhiteSpace(dto.Motivo))
                throw new ValidacionException("Indicá el motivo del ajuste.");
            ValidarTextoOpcional(dto.Motivo, 2000, "El motivo");

            var tipo = dto.EsPositivo ? TipoMovimiento.AjustePositivo : TipoMovimiento.AjusteNegativo;
            var efectiva = dto.EsPositivo ? dto.Cantidad : -dto.Cantidad;

            if (!dto.EsPositivo)
            {
                var existenciaUbicacion = await CalcularExistenciaEnUbicacionAsync(producto.Id, ubicacion.Id, ct);
                if (dto.Cantidad > existenciaUbicacion)
                    throw new StockInsuficienteException($"{producto.CodigoProducto} en {ubicacion.CodigoUbicacion}", existenciaUbicacion, dto.Cantidad);
            }

            var movimiento = NuevoMovimiento(producto.Id, tipo, dto.Cantidad, efectiva, ubicacion.Id, usuario, dto.Motivo);
            await GuardarConNumeroAsync(movimiento, "MOV", ct);

            await _auditoria.RegistrarAsync(nameof(Movimiento), movimiento.NumeroMovimiento, dto.EsPositivo ? "RegistrarAjustePositivo" : "RegistrarAjusteNegativo", null, _auditoria.Capturar(Snapshot(movimiento)), paisId, usuario, dto.Motivo, ct);

            return movimiento;
        }, dto.ProductoId, dto.UbicacionId, paisId, ct);

    public async Task<MovimientoResultadoDto> RegistrarDevolucionAsync(RegistrarDevolucionDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (dto.Cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(dto.Cantidad), "La cantidad debe ser mayor a cero.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Filtrado por PaisId (vía el producto del movimiento origen): no se puede
        // devolver algo que no pertenece al país de la sesión.
        var origen = await _db.Movimientos.Include(m => m.Producto)
            .FirstOrDefaultAsync(m => m.Id == dto.MovimientoOrigenId && m.Producto!.PaisId == paisId, ct)
            ?? throw new MovimientoOrigenInvalidoException($"No existe el movimiento origen {dto.MovimientoOrigenId}.");

        // CK_Movimiento_OrigenSoloEntrada + la regla de negocio de la guía v4: una
        // devolución solo puede apuntar a una Salida marcada como préstamo (Retorna=1).
        if (origen.TipoMovimiento != TipoMovimiento.Salida || !origen.Retorna)
            throw new MovimientoOrigenInvalidoException("El movimiento origen debe ser una Salida con Retorna = true.");

        ValidarTextoOpcional(dto.Motivo, 2000, "El motivo");

        // Serializa con cualquier otro movimiento del producto: sin esto, varias devoluciones
        // simultáneas leen el mismo "ya devuelto" y se aceptan todas (devuelto > prestado).
        await BloquearProductoAsync(origen.ProductoId, paisId, ct);

        var yaDevuelto = await _db.Movimientos
            .Where(m => m.MovimientoOrigenId == origen.Id)
            .SumAsync(m => (int?)m.Cantidad, ct) ?? 0;

        if (yaDevuelto + dto.Cantidad > origen.Cantidad)
            throw new MovimientoOrigenInvalidoException(
                $"La devolución ({dto.Cantidad}) sumada a lo ya devuelto ({yaDevuelto}) supera la cantidad de la salida original ({origen.Cantidad}).");

        // Filtrado por PaisId (vía Almacen de la ubicación): el mismo criterio que
        // EjecutarAsync usa para Entrada/Salida/Ajuste — nunca se recibe stock en una
        // ubicación de otro país.
        var ubicacion = await _db.Ubicaciones.Include(u => u.Almacen)
            .FirstOrDefaultAsync(u => u.Id == dto.UbicacionId && u.Almacen!.PaisId == paisId && u.Activo, ct)
            ?? throw new UbicacionNoEncontradaException(dto.UbicacionId);

        var movimiento = NuevoMovimiento(origen.ProductoId, TipoMovimiento.Entrada, dto.Cantidad, dto.Cantidad, ubicacion.Id, usuario, dto.Motivo);
        movimiento.MovimientoOrigenId = origen.Id;

        await GuardarConNumeroAsync(movimiento, "MOV", ct);
        await _auditoria.RegistrarAsync(nameof(Movimiento), movimiento.NumeroMovimiento, "RegistrarDevolucion", null, _auditoria.Capturar(Snapshot(movimiento)), paisId, usuario, null, ct);
        await tx.CommitAsync(ct);

        var existenciaResultante = await CalcularExistenciaTotalAsync(origen.ProductoId, ct);
        return new MovimientoResultadoDto(movimiento.Id, movimiento.NumeroMovimiento, "CONFIRMADO", movimiento.FechaMovimiento, existenciaResultante);
    }

    public async Task<IReadOnlyList<MovimientoDto>> ListarPorProductoAsync(int productoId, int paisId, CancellationToken ct)
    {
        return await _db.Movimientos.AsNoTracking().Include(m => m.Producto)
            .Include(m => m.Ubicacion).ThenInclude(u => u!.Almacen)
            .Where(m => m.ProductoId == productoId && m.Producto!.PaisId == paisId)
            .OrderByDescending(m => m.FechaMovimiento)
            .Select(m => AMovimientoDto(m))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MovimientoDto>> ListarAsync(int paisId, DateTime? desde, DateTime? hasta, CancellationToken ct)
    {
        var query = _db.Movimientos.AsNoTracking().Include(m => m.Producto)
            .Include(m => m.Ubicacion).ThenInclude(u => u!.Almacen)
            .Include(m => m.SolicitudDetalle).ThenInclude(sd => sd!.Solicitud)
            .Where(m => m.Producto!.PaisId == paisId);

        if (desde is not null) query = query.Where(m => m.FechaMovimiento >= desde.Value);
        if (hasta is not null) query = query.Where(m => m.FechaMovimiento <= hasta.Value);

        return await query.OrderByDescending(m => m.FechaMovimiento).Select(m => AMovimientoDto(m)).ToListAsync(ct);
    }

    // Los N más nuevos, ya cortados en SQL — para el Inicio, que antes bajaba todos los
    // movimientos y se quedaba con cinco.
    public async Task<IReadOnlyList<MovimientoDto>> ListarRecientesAsync(int paisId, int cantidad, CancellationToken ct)
    {
        return await _db.Movimientos.AsNoTracking().Include(m => m.Producto)
            .Include(m => m.Ubicacion).ThenInclude(u => u!.Almacen)
            .Include(m => m.SolicitudDetalle).ThenInclude(sd => sd!.Solicitud)
            .Where(m => m.Producto!.PaisId == paisId)
            .OrderByDescending(m => m.FechaMovimiento)
            .Take(cantidad)
            .Select(m => AMovimientoDto(m))
            .ToListAsync(ct);
    }

    // Equivalente a vw_PrestamosPendientes de la guía v4: salidas con Retorna=1 que
    // todavía no tienen ninguna devolución que las cierre por completo.
    public async Task<IReadOnlyList<MovimientoDto>> ListarPrestamosPendientesAsync(int paisId, CancellationToken ct)
    {
        var salidas = await _db.Movimientos.AsNoTracking().Include(m => m.Producto)
            .Include(m => m.Ubicacion).ThenInclude(u => u!.Almacen)
            .Where(m => m.TipoMovimiento == TipoMovimiento.Salida && m.Retorna && m.Producto!.PaisId == paisId)
            .ToListAsync(ct);

        if (salidas.Count == 0) return [];

        var ids = salidas.Select(s => s.Id).ToList();
        var devueltoPorOrigen = await _db.Movimientos
            .Where(m => m.MovimientoOrigenId != null && ids.Contains(m.MovimientoOrigenId!.Value))
            .GroupBy(m => m.MovimientoOrigenId!.Value)
            .Select(g => new { OrigenId = g.Key, Total = g.Sum(m => m.Cantidad) })
            .ToDictionaryAsync(x => x.OrigenId, x => x.Total, ct);

        return salidas
            .Where(s => !devueltoPorOrigen.TryGetValue(s.Id, out var devuelto) || devuelto < s.Cantidad)
            .OrderBy(s => s.FechaRetornoEsperada)
            .Select(m => AMovimientoDto(m))
            .ToList();
    }

    // Fila donde arranca el encabezado de la tabla en la hoja generada — deja lugar arriba
    // para el título, los filtros aplicados y el resumen de totales.
    private const int FilaEncabezadoExcel = 8;
    private const int ColumnasExcel = 11;

    public async Task<(byte[] Contenido, string NombreArchivo)> GenerarExcelAsync(GenerarMovimientosExcelDto dto, int paisId, CancellationToken ct)
    {
        var query = _db.Movimientos.AsNoTracking()
            .Include(m => m.Producto).ThenInclude(p => p!.Categoria)
            .Include(m => m.Ubicacion)
            .Where(m => m.Producto!.PaisId == paisId);

        if (dto.Tipo is not null) query = query.Where(m => m.TipoMovimiento == dto.Tipo);
        if (dto.CategoriaId is not null) query = query.Where(m => m.Producto!.CategoriaId == dto.CategoriaId);

        var movimientos = await query.OrderByDescending(m => m.FechaMovimiento).ToListAsync(ct);

        string? categoriaTexto = null;
        if (dto.CategoriaId is not null)
        {
            categoriaTexto = movimientos.Count > 0
                ? movimientos[0].Producto!.Categoria!.CodigoCategoria
                : (await _db.Categorias.FirstOrDefaultAsync(c => c.Id == dto.CategoriaId, ct))?.CodigoCategoria ?? "—";
        }

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Movimientos");

        hoja.Cell(1, 1).Value = "Movimientos de Inventario";
        hoja.Range(1, 1, 1, ColumnasExcel).Merge();
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        hoja.Cell(2, 1).Value = "Generado:";
        hoja.Cell(2, 1).Style.Font.Bold = true;
        hoja.Cell(2, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        hoja.Cell(2, 4).Value = "Tipo:";
        hoja.Cell(2, 4).Style.Font.Bold = true;
        hoja.Cell(2, 5).Value = dto.Tipo is null ? "Todos" : EtiquetaTipo(dto.Tipo.Value);
        hoja.Cell(3, 1).Value = "Categoría:";
        hoja.Cell(3, 1).Style.Font.Bold = true;
        hoja.Cell(3, 2).Value = categoriaTexto ?? "Todas";

        hoja.Cell(5, 1).Value = "Resumen";
        hoja.Cell(5, 1).Style.Font.Bold = true;
        hoja.Cell(6, 1).Value =
            $"Total: {movimientos.Count}   |   Entradas: {movimientos.Count(m => m.TipoMovimiento == TipoMovimiento.Entrada)}" +
            $"   |   Salidas: {movimientos.Count(m => m.TipoMovimiento == TipoMovimiento.Salida)}" +
            $"   |   Ajustes +: {movimientos.Count(m => m.TipoMovimiento == TipoMovimiento.AjustePositivo)}" +
            $"   |   Ajustes -: {movimientos.Count(m => m.TipoMovimiento == TipoMovimiento.AjusteNegativo)}";
        hoja.Range(6, 1, 6, ColumnasExcel).Merge();

        string[] encabezados = ["Fecha", "N.º Movimiento", "Código", "Producto", "Categoría", "Tipo", "Cantidad", "Ubicación", "Retorna", "Registrado por", "Motivo"];
        for (var col = 0; col < encabezados.Length; col++)
            hoja.Cell(FilaEncabezadoExcel, col + 1).Value = encabezados[col];

        var rangoEncabezado = hoja.Range(FilaEncabezadoExcel, 1, FilaEncabezadoExcel, ColumnasExcel);
        rangoEncabezado.Style.Font.Bold = true;
        rangoEncabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        var fila = FilaEncabezadoExcel + 1;
        foreach (var m in movimientos)
        {
            hoja.Cell(fila, 1).Value = m.FechaMovimiento.ToLocalTime();
            hoja.Cell(fila, 1).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            hoja.Cell(fila, 2).Value = m.NumeroMovimiento;
            hoja.Cell(fila, 3).Value = m.Producto?.CodigoProducto ?? "";
            hoja.Cell(fila, 4).Value = m.Producto?.Nombre ?? "";
            hoja.Cell(fila, 5).Value = m.Producto?.Categoria?.CodigoCategoria ?? "";
            hoja.Cell(fila, 6).Value = EtiquetaTipo(m.TipoMovimiento);
            hoja.Cell(fila, 7).Value = m.Cantidad;
            hoja.Cell(fila, 7).Style.NumberFormat.Format = "#,##0";
            hoja.Cell(fila, 8).Value = m.Ubicacion?.CodigoUbicacion ?? "";
            hoja.Cell(fila, 9).Value = m.TipoMovimiento == TipoMovimiento.Salida ? (m.Retorna ? "Sí" : "No") : "";
            hoja.Cell(fila, 10).Value = m.RegistradoPorNombre;
            hoja.Cell(fila, 11).Value = m.Motivo ?? "";

            var (fondo, letra) = ColorTipo(m.TipoMovimiento);
            var celdaTipo = hoja.Cell(fila, 6);
            celdaTipo.Style.Fill.BackgroundColor = fondo;
            celdaTipo.Style.Font.FontColor = letra;
            celdaTipo.Style.Font.Bold = true;

            fila++;
        }

        var ultimaFila = fila - 1;
        var rangoTabla = hoja.Range(FilaEncabezadoExcel, 1, Math.Max(ultimaFila, FilaEncabezadoExcel), ColumnasExcel);
        rangoTabla.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rangoTabla.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        rangoEncabezado.SetAutoFilter();

        hoja.Columns(1, ColumnasExcel).AdjustToContents();
        hoja.SheetView.FreezeRows(FilaEncabezadoExcel);
        hoja.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        hoja.PageSetup.FitToPages(1, 0);
        hoja.PageSetup.SetRowsToRepeatAtTop(FilaEncabezadoExcel, FilaEncabezadoExcel);
        hoja.PageSetup.Margins.SetLeft(1.0).SetRight(1.0).SetTop(1.0).SetBottom(1.0);

        using var stream = new MemoryStream();
        libro.SaveAs(stream);

        var nombreArchivo = $"Movimientos_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        return (stream.ToArray(), nombreArchivo);
    }

    private static string EtiquetaTipo(TipoMovimiento t) => t switch
    {
        TipoMovimiento.Entrada => "Entrada",
        TipoMovimiento.Salida => "Salida",
        TipoMovimiento.AjustePositivo => "Ajuste +",
        TipoMovimiento.AjusteNegativo => "Ajuste -",
        _ => t.ToString(),
    };

    // Mismo criterio de color que los tags de la pantalla (verde = suma existencia, ver
    // Movimientos/Index.razor) — acá se usan 2 tonos más para distinguir Entrada de
    // Ajuste + y Salida de Ajuste -, algo que la UI no necesita porque ya separa las
    // columnas, pero en un Excel exportado ayuda a barrer la planilla de un vistazo.
    private static (XLColor Fondo, XLColor Letra) ColorTipo(TipoMovimiento t) => t switch
    {
        TipoMovimiento.Entrada => (XLColor.FromHtml("#DCFCE7"), XLColor.FromHtml("#166534")),
        TipoMovimiento.AjustePositivo => (XLColor.FromHtml("#DBEAFE"), XLColor.FromHtml("#1E40AF")),
        TipoMovimiento.Salida => (XLColor.FromHtml("#FEE2E2"), XLColor.FromHtml("#991B1B")),
        TipoMovimiento.AjusteNegativo => (XLColor.FromHtml("#FEF3C7"), XLColor.FromHtml("#92400E")),
        _ => (XLColor.White, XLColor.Black),
    };

    // ---- helpers ----

    private async Task<MovimientoResultadoDto> EjecutarAsync(
        Func<Producto, Ubicacion, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction, Task<Movimiento>> accion,
        int productoId, int ubicacionId, int paisId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Candado por producto, tomado ANTES de leer el stock: los movimientos del mismo
        // producto se hacen de a uno. Sin esto, 40 salidas simultáneas leen todas "hay 10",
        // se aceptan más de las que entran y el stock queda negativo (check-then-insert).
        await BloquearProductoAsync(productoId, paisId, ct);

        // Filtrando producto Y ubicación por el país de la sesión, ninguno de los dos
        // puede pertenecer a otro país — así se evita de raíz mover stock de un producto
        // de un país a una ubicación de otro, sin necesitar una validación cruzada aparte.
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == productoId && p.PaisId == paisId && p.Activo, ct)
            ?? throw new ProductoNoEncontradoException(productoId);

        var ubicacion = await _db.Ubicaciones.Include(u => u.Almacen)
            .FirstOrDefaultAsync(u => u.Id == ubicacionId && u.Almacen!.PaisId == paisId && u.Activo, ct)
            ?? throw new UbicacionNoEncontradaException(ubicacionId);

        var movimiento = await accion(producto, ubicacion, tx);
        await tx.CommitAsync(ct);

        var existenciaResultante = await CalcularExistenciaTotalAsync(productoId, ct);
        return new MovimientoResultadoDto(movimiento.Id, movimiento.NumeroMovimiento, "CONFIRMADO", movimiento.FechaMovimiento, existenciaResultante);
    }

    // UPDATE que no cambia nada pero toma el candado exclusivo de la fila hasta que termina
    // la transacción. 0 filas = el producto no existe, está inactivo o es de otro país.
    private async Task BloquearProductoAsync(int productoId, int paisId, CancellationToken ct)
    {
        var filas = await _db.Productos
            .Where(p => p.Id == productoId && p.PaisId == paisId && p.Activo)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.TieneImagen, p => p.TieneImagen), ct);
        if (filas == 0) throw new ProductoNoEncontradoException(productoId);
    }

    private static void ValidarTextoOpcional(string? texto, int maximo, string campo)
    {
        if (texto is not null && texto.Trim().Length > maximo)
            throw new ValidacionException($"{campo} no puede superar los {maximo} caracteres.");
    }

    private static Movimiento NuevoMovimiento(int productoId, TipoMovimiento tipo, int cantidad, int cantidadEfectiva, int ubicacionId, UsuarioActuante usuario, string? motivo) => new()
    {
        ProductoId = productoId,
        TipoMovimiento = tipo,
        Cantidad = cantidad,
        CantidadEfectiva = cantidadEfectiva,
        UbicacionId = ubicacionId,
        RegistradoPorId = usuario.Id,
        RegistradoPorNombre = usuario.Nombre,
        Motivo = motivo,
        FechaMovimiento = DateTime.UtcNow,
        // Provisorio ÚNICO (no una constante compartida): con "PENDIENTE" fijo, dos altas
        // simultáneas chocan contra el índice único mientras esperan su Id definitivo.
        NumeroMovimiento = "TMP-" + Guid.NewGuid().ToString("N"), // se reemplaza en GuardarConNumeroAsync
    };

    // El número definitivo necesita el Id autogenerado — se guarda en dos pasos,
    // mismo patrón que "INV Numerar solicitud" de la guía v4 (ahí el ID lo daba
    // SharePoint; acá lo da la IDENTITY de SQL Server).
    private async Task GuardarConNumeroAsync(Movimiento movimiento, string prefijo, CancellationToken ct)
    {
        _db.Movimientos.Add(movimiento);
        await _db.SaveChangesAsync(ct);

        movimiento.NumeroMovimiento = $"{prefijo}-{DateTime.UtcNow:yyyy}-{movimiento.Id:D6}";
        await _db.SaveChangesAsync(ct);
    }

    // Validación compartida por RegistrarEntradaAsync/RegistrarSalidaAsync cuando el
    // movimiento cierra una línea de Solicitud: la línea debe estar aprobada y la cantidad
    // no puede superar lo que todavía falta entregar (CantidadAprobada - CantidadEntregada).
    private async Task<SolicitudDetalle?> ValidarDetalleParaEntregaAsync(int? solicitudDetalleId, int productoId, TipoSolicitud tipoEsperado, int cantidad, CancellationToken ct)
    {
        if (solicitudDetalleId is null) return null;

        var solicitudId = await _db.SolicitudDetalles.Where(d => d.Id == solicitudDetalleId)
            .Select(d => (int?)d.SolicitudId).FirstOrDefaultAsync(ct)
            ?? throw new SolicitudEstadoInvalidoException($"No existe la línea de solicitud {solicitudDetalleId}.");

        // Candado sobre la SOLICITUD (no sobre la línea): las entregas de una misma solicitud
        // se hacen de a una, así dos entregas simultáneas de la misma línea no pasan ambas el
        // control de "lo pendiente", y AcumularEntregaAsync (que lee todas las líneas) no puede
        // cruzarse con otra entrega de la misma solicitud y trabarse. Orden de candados siempre
        // producto -> solicitud, así no hay ciclos.
        await _db.Solicitudes.Where(s => s.Id == solicitudId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Estado, s => s.Estado), ct);

        var detalle = await _db.SolicitudDetalles.Include(d => d.Solicitud)
            .FirstAsync(d => d.Id == solicitudDetalleId, ct);

        if (detalle.ProductoId != productoId)
            throw new SolicitudEstadoInvalidoException("El producto no coincide con el de la línea de la solicitud.");

        if (detalle.Solicitud!.Tipo != tipoEsperado)
            throw new SolicitudEstadoInvalidoException(tipoEsperado == TipoSolicitud.Salida
                ? "Esta solicitud es de entrada: se recibe con una entrada, no con una salida."
                : "Esta solicitud es de salida: se entrega con una salida, no con una entrada.");

        if (detalle.CantidadAprobada is null)
            throw new SolicitudEstadoInvalidoException("La línea de solicitud todavía no fue aprobada.");

        var pendiente = detalle.CantidadAprobada.Value - detalle.CantidadEntregada;
        if (cantidad > pendiente)
            throw new SolicitudEstadoInvalidoException($"La cantidad ({cantidad}) supera lo pendiente de entregar en la solicitud ({pendiente}).");

        return detalle;
    }

    private async Task AcumularEntregaAsync(SolicitudDetalle detalle, int cantidadEntregada, CancellationToken ct)
    {
        detalle.CantidadEntregada += cantidadEntregada;

        var solicitud = detalle.Solicitud ?? await _db.Solicitudes.Include(s => s.Detalles).FirstAsync(s => s.Id == detalle.SolicitudId, ct);
        var detalles = solicitud.Detalles.Count > 0 ? solicitud.Detalles : await _db.SolicitudDetalles.Where(d => d.SolicitudId == solicitud.Id).ToListAsync(ct);

        var completa = detalles.All(d => d.CantidadAprobada is not null && d.CantidadEntregada >= d.CantidadAprobada);
        solicitud.Estado = completa ? EstadoSolicitud.Entregada : EstadoSolicitud.EntregadaParcial;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<int> CalcularExistenciaTotalAsync(int productoId, CancellationToken ct) =>
        await _db.Movimientos.Where(m => m.ProductoId == productoId).SumAsync(m => (int?)m.CantidadEfectiva, ct) ?? 0;

    private async Task<int> CalcularExistenciaEnUbicacionAsync(int productoId, int ubicacionId, CancellationToken ct) =>
        await _db.Movimientos.Where(m => m.ProductoId == productoId && m.UbicacionId == ubicacionId)
            .SumAsync(m => (int?)m.CantidadEfectiva, ct) ?? 0;

    private static MovimientoDto AMovimientoDto(Movimiento m) => new(
        m.Id, m.NumeroMovimiento, m.ProductoId, m.Producto?.Nombre ?? string.Empty, m.TipoMovimiento, m.Cantidad,
        m.UbicacionId, m.Ubicacion?.CodigoUbicacion ?? string.Empty,
        m.Ubicacion?.Almacen?.Id ?? 0, m.Ubicacion?.Almacen?.Nombre ?? string.Empty,
        m.Retorna, m.UbicacionExterna, m.FechaRetornoEsperada, m.FechaVencimiento,
        m.MovimientoOrigenId, m.SolicitudDetalleId, m.SolicitudDetalle?.SolicitudId, m.SolicitudDetalle?.Solicitud?.NumeroSolicitud,
        m.RegistradoPorId, m.RegistradoPorNombre, m.Motivo, m.FechaMovimiento
    );
}
