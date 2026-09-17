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

    public MovimientoService(InventoryDbContext db) => _db = db;

    public Task<MovimientoResultadoDto> RegistrarEntradaAsync(RegistrarEntradaDto dto, UsuarioActuante usuario, CancellationToken ct) =>
        EjecutarAsync(async (producto, ubicacion, tx) =>
        {
            var detalle = await ValidarDetalleParaEntregaAsync(dto.SolicitudDetalleId, dto.Cantidad, ct);

            var movimiento = NuevoMovimiento(producto.Id, TipoMovimiento.Entrada, dto.Cantidad, dto.Cantidad, ubicacion.Id, usuario, dto.Motivo);
            movimiento.SolicitudDetalleId = detalle?.Id;

            await GuardarConNumeroAsync(movimiento, "MOV", ct);

            if (detalle is not null)
                await AcumularEntregaAsync(detalle, dto.Cantidad, ct);

            return movimiento;
        }, dto.ProductoId, dto.UbicacionId, ct);

    public Task<MovimientoResultadoDto> RegistrarSalidaAsync(RegistrarSalidaDto dto, UsuarioActuante usuario, CancellationToken ct) =>
        EjecutarAsync(async (producto, ubicacion, tx) =>
        {
            if (dto.Cantidad <= 0)
                throw new ArgumentOutOfRangeException(nameof(dto.Cantidad), "La cantidad debe ser mayor a cero.");

            var existenciaUbicacion = await CalcularExistenciaEnUbicacionAsync(producto.Id, ubicacion.Id, ct);
            if (dto.Cantidad > existenciaUbicacion)
                throw new StockInsuficienteException($"{producto.CodigoProducto} en {ubicacion.CodigoUbicacion}", existenciaUbicacion, dto.Cantidad);

            var detalle = await ValidarDetalleParaEntregaAsync(dto.SolicitudDetalleId, dto.Cantidad, ct);

            var movimiento = NuevoMovimiento(producto.Id, TipoMovimiento.Salida, dto.Cantidad, -dto.Cantidad, ubicacion.Id, usuario, dto.Motivo);
            movimiento.Retorna = dto.Retorna;
            movimiento.UbicacionExterna = dto.Retorna ? dto.UbicacionExterna : null;
            movimiento.FechaRetornoEsperada = dto.Retorna ? dto.FechaRetornoEsperada : null;
            movimiento.SolicitudDetalleId = detalle?.Id;

            await GuardarConNumeroAsync(movimiento, "MOV", ct);

            if (detalle is not null)
                await AcumularEntregaAsync(detalle, dto.Cantidad, ct);

            return movimiento;
        }, dto.ProductoId, dto.UbicacionId, ct);

    public Task<MovimientoResultadoDto> RegistrarAjusteAsync(RegistrarAjusteDto dto, UsuarioActuante usuario, CancellationToken ct) =>
        EjecutarAsync(async (producto, ubicacion, tx) =>
        {
            if (dto.Cantidad <= 0)
                throw new ArgumentOutOfRangeException(nameof(dto.Cantidad), "La cantidad debe ser mayor a cero.");

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
            return movimiento;
        }, dto.ProductoId, dto.UbicacionId, ct);

    public async Task<MovimientoResultadoDto> RegistrarDevolucionAsync(RegistrarDevolucionDto dto, UsuarioActuante usuario, CancellationToken ct)
    {
        if (dto.Cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(dto.Cantidad), "La cantidad debe ser mayor a cero.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var origen = await _db.Movimientos.Include(m => m.Producto)
            .FirstOrDefaultAsync(m => m.Id == dto.MovimientoOrigenId, ct)
            ?? throw new MovimientoOrigenInvalidoException($"No existe el movimiento origen {dto.MovimientoOrigenId}.");

        // CK_Movimiento_OrigenSoloEntrada + la regla de negocio de la guía v4: una
        // devolución solo puede apuntar a una Salida marcada como préstamo (Retorna=1).
        if (origen.TipoMovimiento != TipoMovimiento.Salida || !origen.Retorna)
            throw new MovimientoOrigenInvalidoException("El movimiento origen debe ser una Salida con Retorna = true.");

        var yaDevuelto = await _db.Movimientos
            .Where(m => m.MovimientoOrigenId == origen.Id)
            .SumAsync(m => (int?)m.Cantidad, ct) ?? 0;

        if (yaDevuelto + dto.Cantidad > origen.Cantidad)
            throw new MovimientoOrigenInvalidoException(
                $"La devolución ({dto.Cantidad}) sumada a lo ya devuelto ({yaDevuelto}) supera la cantidad de la salida original ({origen.Cantidad}).");

        var ubicacion = await _db.Ubicaciones.FirstOrDefaultAsync(u => u.Id == dto.UbicacionId && u.Activo, ct)
            ?? throw new UbicacionNoEncontradaException(dto.UbicacionId);

        var movimiento = NuevoMovimiento(origen.ProductoId, TipoMovimiento.Entrada, dto.Cantidad, dto.Cantidad, ubicacion.Id, usuario, dto.Motivo);
        movimiento.MovimientoOrigenId = origen.Id;

        await GuardarConNumeroAsync(movimiento, "MOV", ct);
        await tx.CommitAsync(ct);

        var existenciaResultante = await CalcularExistenciaTotalAsync(origen.ProductoId, ct);
        return new MovimientoResultadoDto(movimiento.Id, movimiento.NumeroMovimiento, "CONFIRMADO", movimiento.FechaMovimiento, existenciaResultante);
    }

    public async Task<IReadOnlyList<MovimientoDto>> ListarPorProductoAsync(int productoId, CancellationToken ct)
    {
        return await _db.Movimientos.AsNoTracking().Include(m => m.Producto).Include(m => m.Ubicacion)
            .Where(m => m.ProductoId == productoId)
            .OrderByDescending(m => m.FechaMovimiento)
            .Select(m => AMovimientoDto(m))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MovimientoDto>> ListarAsync(DateTime? desde, DateTime? hasta, CancellationToken ct)
    {
        var query = _db.Movimientos.AsNoTracking().Include(m => m.Producto).Include(m => m.Ubicacion).AsQueryable();

        if (desde is not null) query = query.Where(m => m.FechaMovimiento >= desde.Value);
        if (hasta is not null) query = query.Where(m => m.FechaMovimiento <= hasta.Value);

        return await query.OrderByDescending(m => m.FechaMovimiento).Select(m => AMovimientoDto(m)).ToListAsync(ct);
    }

    // Equivalente a vw_PrestamosPendientes de la guía v4: salidas con Retorna=1 que
    // todavía no tienen ninguna devolución que las cierre por completo.
    public async Task<IReadOnlyList<MovimientoDto>> ListarPrestamosPendientesAsync(CancellationToken ct)
    {
        var salidas = await _db.Movimientos.AsNoTracking().Include(m => m.Producto).Include(m => m.Ubicacion)
            .Where(m => m.TipoMovimiento == TipoMovimiento.Salida && m.Retorna)
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

    public async Task<(byte[] Contenido, string NombreArchivo)> GenerarExcelAsync(GenerarMovimientosExcelDto dto, CancellationToken ct)
    {
        var query = _db.Movimientos.AsNoTracking()
            .Include(m => m.Producto).ThenInclude(p => p!.Categoria)
            .Include(m => m.Ubicacion)
            .AsQueryable();

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
        int productoId, int ubicacionId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == productoId && p.Activo, ct)
            ?? throw new ProductoNoEncontradoException(productoId);

        var ubicacion = await _db.Ubicaciones.FirstOrDefaultAsync(u => u.Id == ubicacionId && u.Activo, ct)
            ?? throw new UbicacionNoEncontradaException(ubicacionId);

        var movimiento = await accion(producto, ubicacion, tx);
        await tx.CommitAsync(ct);

        var existenciaResultante = await CalcularExistenciaTotalAsync(productoId, ct);
        return new MovimientoResultadoDto(movimiento.Id, movimiento.NumeroMovimiento, "CONFIRMADO", movimiento.FechaMovimiento, existenciaResultante);
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
        NumeroMovimiento = "PENDIENTE", // se reemplaza en GuardarConNumeroAsync una vez que hay Id
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
    private async Task<SolicitudDetalle?> ValidarDetalleParaEntregaAsync(int? solicitudDetalleId, int cantidad, CancellationToken ct)
    {
        if (solicitudDetalleId is null) return null;

        var detalle = await _db.SolicitudDetalles.Include(d => d.Solicitud)
            .FirstOrDefaultAsync(d => d.Id == solicitudDetalleId, ct)
            ?? throw new SolicitudEstadoInvalidoException($"No existe la línea de solicitud {solicitudDetalleId}.");

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
        m.UbicacionId, m.Ubicacion?.CodigoUbicacion ?? string.Empty, m.Retorna, m.UbicacionExterna, m.FechaRetornoEsperada,
        m.MovimientoOrigenId, m.SolicitudDetalleId, m.RegistradoPorId, m.RegistradoPorNombre, m.Motivo, m.FechaMovimiento
    );
}
