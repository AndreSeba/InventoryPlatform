using ClosedXML.Excel;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class ConteoService : IConteoService
{
    // Topes que vuelven 400 a lo que antes era un 500 de truncado/constraint en la base.
    private const int MaxProductosPorConteo = 1000;
    private const int MaxCantidadContada = 100_000_000;
    private const int MaxEvidenciasPorConteo = 10;
    private const int MaxBytesEvidencia = 10 * 1024 * 1024;
    private const int MaxLargoNombreArchivo = 200;

    private const string ContentTypeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ConteoService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // ---------------------------------------------------------------- lectura

    public async Task<IReadOnlyList<ConteoResumenDto>> ListarAsync(EstadoConteo? estado, int paisId, CancellationToken ct)
    {
        var consulta = _db.SesionesConteo.AsNoTracking().Where(s => s.PaisId == paisId);
        if (estado is not null) consulta = consulta.Where(s => s.Estado == estado);

        return await consulta
            .OrderByDescending(s => s.FechaCreacion).ThenByDescending(s => s.Id)
            .Take(500)
            .Select(s => new ConteoResumenDto(
                s.Id, s.Codigo, s.Nombre, s.Estado, s.FechaCreacion, s.CreadoPorNombre, s.FechaCierre, s.CerradoPorNombre,
                s.Lineas.Count(),
                s.Lineas.Count(l => l.CantidadContada != null),
                s.Lineas.Count(l => l.CantidadContada != null && l.CantidadContada != l.ExistenciaSistema),
                s.Evidencias.Count(),
                s.ConteoOrigenId, s.ConteoOrigen == null ? null : s.ConteoOrigen.Codigo))
            .ToListAsync(ct);
    }

    public async Task<ConteoDetalleDto> ObtenerAsync(int id, int paisId, CancellationToken ct)
    {
        var sesion = await _db.SesionesConteo.AsNoTracking().Include(s => s.ConteoOrigen)
            .FirstOrDefaultAsync(s => s.Id == id && s.PaisId == paisId, ct)
            ?? throw new ConteoNoEncontradoException(id);

        var lineas = await _db.SesionConteoLineas.AsNoTracking()
            .Include(l => l.Producto!).ThenInclude(p => p.Categoria)
            .Include(l => l.Ubicacion)
            .Where(l => l.SesionConteoId == id)
            .OrderBy(l => l.Producto!.Nombre).ThenBy(l => l.Ubicacion!.CodigoUbicacion)
            .ToListAsync(ct);

        // Proyección sin Datos: listar la evidencia nunca debe leer los bytes del archivo.
        var evidencias = await _db.SesionConteoEvidencias.AsNoTracking()
            .Where(e => e.SesionConteoId == id)
            .OrderBy(e => e.Id)
            .Select(e => new ConteoEvidenciaDto(e.Id, e.NombreArchivo, e.ContentType, e.TamanoBytes, e.SubidoPorNombre, e.FechaSubida))
            .ToListAsync(ct);

        var lineasDto = lineas.Select(l => new ConteoLineaDto(
            l.Id, l.ProductoId, l.Producto!.CodigoProducto, l.Producto.Nombre,
            l.Producto.Categoria?.CodigoCategoria ?? string.Empty, l.Producto.UnidadMedida,
            l.Producto.TieneImagen ? $"/api/productos/{l.ProductoId}/imagen" : null,
            l.UbicacionId, l.Ubicacion!.CodigoUbicacion, l.ExistenciaSistema,
            l.CantidadContada, l.CantidadContada is null ? null : l.CantidadContada - l.ExistenciaSistema,
            l.ContadoPorNombre, l.FechaConteo)).ToList();

        var resumen = new ConteoResumenDto(
            sesion.Id, sesion.Codigo, sesion.Nombre, sesion.Estado, sesion.FechaCreacion, sesion.CreadoPorNombre,
            sesion.FechaCierre, sesion.CerradoPorNombre,
            lineasDto.Count,
            lineasDto.Count(l => l.CantidadContada is not null),
            lineasDto.Count(l => l.Diferencia is not null && l.Diferencia != 0),
            evidencias.Count,
            sesion.ConteoOrigenId, sesion.ConteoOrigen?.Codigo);

        return new ConteoDetalleDto(resumen, sesion.Notas, sesion.MotivoCancelacion, lineasDto, evidencias);
    }

    // ---------------------------------------------------------------- crear

    public async Task<ConteoDetalleDto> CrearAsync(CrearConteoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var nombre = NormalizarTexto(dto.Nombre, 150, "El nombre");
        var notas = NormalizarTexto(dto.Notas, 500, "Las notas");

        // La hoja se arma SIEMPRE sobre productos elegidos uno por uno: un conteo físico se
        // hace sobre un conjunto acotado, y una hoja con el catálogo completo es inmanejable.
        if (dto.ProductoIds is not { Count: > 0 })
            throw new SeleccionDeProductosVaciaException();

        var idsPedidos = dto.ProductoIds.Distinct().ToList();
        if (idsPedidos.Count > MaxProductosPorConteo)
            throw new ConteoInvalidoException($"Un conteo admite hasta {MaxProductosPorConteo} productos. Dividilo en varios conteos.");

        var productos = await _db.Productos.AsNoTracking()
            .Where(p => p.Activo && p.PaisId == paisId && idsPedidos.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        // Si alguno de los ids pedidos no existe o está inactivo, avisar en vez de armar
        // en silencio un conteo más corto que lo que el usuario eligió.
        if (productos.Count != idsPedidos.Count)
        {
            var faltantes = idsPedidos.Except(productos).Count();
            throw new ConteoInvalidoException(
                $"No se puede crear el conteo: {faltantes} de los productos seleccionados ya no existen o están inactivos.");
        }

        var existencias = await ExistenciasPorUbicacionAsync(idsPedidos, soloConStock: true, ct);
        if (existencias.Count == 0)
            throw new ConteoInvalidoException("Ninguno de los productos elegidos tiene stock en alguna ubicación.");

        var lineas = existencias.Select(e => (e.Key.ProductoId, e.Key.UbicacionId, Existencia: e.Value)).ToList();
        return await CrearSesionAsync(nombre, notas, null, lineas, paisId, usuario, ct);
    }

    public async Task<ConteoDetalleDto> CrearReconteoAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var origen = await _db.SesionesConteo.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.PaisId == paisId, ct)
            ?? throw new ConteoNoEncontradoException(id);

        if (origen.Estado != EstadoConteo.Cerrado)
            throw new ConteoEstadoInvalidoException("Solo se puede abrir un reconteo desde un conteo cerrado.");

        var conDiferencia = await _db.SesionConteoLineas.AsNoTracking()
            .Where(l => l.SesionConteoId == id && l.CantidadContada != null && l.CantidadContada != l.ExistenciaSistema)
            .Select(l => new { l.ProductoId, l.UbicacionId })
            .ToListAsync(ct);

        if (conDiferencia.Count == 0)
            throw new ConteoInvalidoException("Este conteo no tiene diferencias: no hace falta un reconteo.");

        if (await _db.SesionesConteo.AnyAsync(s => s.ConteoOrigenId == id && s.Estado == EstadoConteo.EnCurso, ct))
            throw new ConteoEstadoInvalidoException("Ya hay un reconteo en curso de este conteo. Terminalo o cancelalo antes de abrir otro.");

        // La existencia se vuelve a medir AHORA: el reconteo compara contra lo que el
        // sistema dice hoy, no contra la foto vieja del conteo original.
        var productoIds = conDiferencia.Select(l => l.ProductoId).Distinct().ToList();
        var existencias = await ExistenciasPorUbicacionAsync(productoIds, soloConStock: false, ct);
        var lineas = conDiferencia
            .Select(l => (l.ProductoId, l.UbicacionId, Existencia: existencias.GetValueOrDefault((l.ProductoId, l.UbicacionId))))
            .ToList();

        try
        {
            return await CrearSesionAsync($"Reconteo de {origen.Codigo}", null, origen.Id, lineas, paisId, usuario, ct);
        }
        catch (DbUpdateException)
        {
            // Índice único filtrado: otro clic simultáneo ya abrió el reconteo.
            throw new ConteoEstadoInvalidoException("Ya hay un reconteo en curso de este conteo. Terminalo o cancelalo antes de abrir otro.");
        }
    }

    private async Task<ConteoDetalleDto> CrearSesionAsync(
        string? nombre, string? notas, int? origenId, List<(int ProductoId, int UbicacionId, int Existencia)> lineas,
        int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        SesionConteo sesion;
        await using (var tx = await _db.Database.BeginTransactionAsync(ct))
        {
            sesion = new SesionConteo
            {
                // Código provisional ÚNICO (no una constante compartida): dos altas simultáneas
                // no pueden chocar contra el índice único mientras esperan su Id definitivo.
                Codigo = "TMP-" + Guid.NewGuid().ToString("N"),
                PaisId = paisId,
                Nombre = nombre,
                Notas = notas,
                Estado = EstadoConteo.EnCurso,
                CreadoPorId = usuario.Id,
                CreadoPorNombre = usuario.Nombre,
                FechaCreacion = DateTime.UtcNow,
                ConteoOrigenId = origenId,
                Lineas = lineas.Select(l => new SesionConteoLinea
                {
                    ProductoId = l.ProductoId,
                    UbicacionId = l.UbicacionId,
                    ExistenciaSistema = l.Existencia,
                }).ToList(),
            };
            _db.SesionesConteo.Add(sesion);
            await _db.SaveChangesAsync(ct);

            sesion.Codigo = $"CONT-{DateTime.UtcNow:yyyy}-{sesion.Id:D6}";
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await _auditoria.RegistrarAsync(nameof(SesionConteo), sesion.Codigo, "Crear", null,
            _auditoria.Capturar(new { sesion.Codigo, sesion.Nombre, sesion.Estado, Lineas = lineas.Count, ConteoOrigenId = origenId }),
            paisId, usuario, null, ct);

        return await ObtenerAsync(sesion.Id, paisId, ct);
    }

    // ---------------------------------------------------------------- cantidades

    public async Task<ConteoDetalleDto> GuardarCantidadesAsync(int id, GuardarCantidadesDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (dto.Cantidades is not { Count: > 0 })
            throw new ConteoInvalidoException("No hay cantidades para guardar.");
        if (dto.Cantidades.Count > MaxProductosPorConteo * 5)
            throw new ConteoInvalidoException("Demasiadas líneas en una sola carga.");

        foreach (var c in dto.Cantidades)
            if (c.Cantidad is < 0 or > MaxCantidadContada)
                throw new ConteoInvalidoException($"La cantidad contada debe estar entre 0 y {MaxCantidadContada:N0}.");

        // Si una línea viene repetida, vale la última.
        var nuevas = dto.Cantidades.GroupBy(c => c.LineaId).ToDictionary(g => g.Key, g => g.Last().Cantidad);

        var (anterior, nuevo) = await EnCursoAsync(id, paisId, () => AplicarCantidadesAsync(id, nuevas, usuario, ct), ct);

        if (nuevo.Count > 0)
            await _auditoria.RegistrarAsync(nameof(SesionConteo), await CodigoAsync(id, ct), "Cargar cantidades",
                _auditoria.Capturar(anterior), _auditoria.Capturar(nuevo), paisId, usuario, null, ct);

        return await ObtenerAsync(id, paisId, ct);
    }

    private async Task<(Dictionary<string, int?> Anterior, Dictionary<string, int?> Nuevo)> AplicarCantidadesAsync(
        int sesionId, Dictionary<int, int?> nuevas, UsuarioActuante usuario, CancellationToken ct)
    {
        var ids = nuevas.Keys.ToList();
        var lineas = await _db.SesionConteoLineas
            .Include(l => l.Producto).Include(l => l.Ubicacion)
            .Where(l => l.SesionConteoId == sesionId && ids.Contains(l.Id))
            .ToListAsync(ct);

        if (lineas.Count != ids.Count)
            throw new ConteoInvalidoException("Alguna de las líneas no pertenece a este conteo.");

        var anterior = new Dictionary<string, int?>();
        var nuevo = new Dictionary<string, int?>();
        var ahora = DateTime.UtcNow;

        foreach (var l in lineas)
        {
            var cantidad = nuevas[l.Id];
            if (l.CantidadContada == cantidad) continue;

            var clave = $"{l.Producto!.CodigoProducto} @ {l.Ubicacion!.CodigoUbicacion}";
            anterior[clave] = l.CantidadContada;
            nuevo[clave] = cantidad;

            l.CantidadContada = cantidad;
            l.ContadoPorId = cantidad is null ? null : usuario.Id;
            l.ContadoPorNombre = cantidad is null ? null : usuario.Nombre;
            l.FechaConteo = cantidad is null ? null : ahora;
        }

        await _db.SaveChangesAsync(ct);
        return (anterior, nuevo);
    }

    // ---------------------------------------------------------------- Excel

    // Fila donde arranca el encabezado de la tabla de productos en la hoja generada
    // (deja lugar arriba para el título y los datos de sesión/fecha) — el importador lee
    // con este mismo número, así que si se mueve acá hay que moverlo allá.
    private const int FilaEncabezado = 5;
    private const int ColumnasExcel = 9;

    public async Task<(byte[] Contenido, string NombreArchivo)> GenerarHojaAsync(int id, int paisId, CancellationToken ct)
    {
        var sesion = await _db.SesionesConteo.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.PaisId == paisId, ct)
            ?? throw new ConteoNoEncontradoException(id);

        var lineas = await _db.SesionConteoLineas.AsNoTracking()
            .Include(l => l.Producto!).ThenInclude(p => p.Categoria)
            .Include(l => l.Ubicacion)
            .Where(l => l.SesionConteoId == id)
            .OrderBy(l => l.Producto!.Nombre).ThenBy(l => l.Ubicacion!.CodigoUbicacion)
            .ToListAsync(ct);

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Conteo");

        hoja.Cell(1, 1).Value = "Hoja de Conteo Físico";
        hoja.Range(1, 1, 1, ColumnasExcel).Merge();
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        hoja.Cell(2, 1).Value = "Sesión:";
        hoja.Cell(2, 1).Style.Font.Bold = true;
        hoja.Cell(2, 2).Value = sesion.Codigo;
        hoja.Cell(3, 1).Value = "Fecha:";
        hoja.Cell(3, 1).Style.Font.Bold = true;
        hoja.Cell(3, 2).Value = sesion.FechaCreacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        if (!string.IsNullOrWhiteSpace(sesion.Nombre))
        {
            hoja.Cell(4, 1).Value = "Nombre:";
            hoja.Cell(4, 1).Style.Font.Bold = true;
            hoja.Cell(4, 2).Value = sesion.Nombre;
        }

        string[] encabezados = ["ProductoId", "UbicacionId", "Código", "Producto", "Categoría", "Unidad", "Ubicación", "Existencia Sistema", "Cantidad Contada"];
        for (var col = 0; col < encabezados.Length; col++)
            hoja.Cell(FilaEncabezado, col + 1).Value = encabezados[col];

        var rangoEncabezado = hoja.Range(FilaEncabezado, 1, FilaEncabezado, ColumnasExcel);
        rangoEncabezado.Style.Font.Bold = true;
        rangoEncabezado.Style.Font.FontSize = 10;
        // Solo una regla debajo del encabezado. Sin recuadros: la hoja se imprime y se
        // llena a mano, y una cuadrícula completa la vuelve ilegible en papel.
        rangoEncabezado.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        rangoEncabezado.Style.Border.BottomBorderColor = XLColor.FromHtml("#334155");
        rangoEncabezado.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;

        var fila = FilaEncabezado + 1;
        foreach (var l in lineas)
        {
            hoja.Cell(fila, 1).Value = l.ProductoId;
            hoja.Cell(fila, 2).Value = l.UbicacionId;
            hoja.Cell(fila, 3).Value = l.Producto!.CodigoProducto;
            hoja.Cell(fila, 4).Value = l.Producto.Nombre;
            hoja.Cell(fila, 5).Value = l.Producto.Categoria?.CodigoCategoria ?? "";
            hoja.Cell(fila, 6).Value = l.Producto.UnidadMedida;
            hoja.Cell(fila, 7).Value = l.Ubicacion!.CodigoUbicacion;
            hoja.Cell(fila, 8).Value = l.ExistenciaSistema;
            hoja.Cell(fila, 8).Style.NumberFormat.Format = "#,##0";
            // Si ya se había cargado algo, la hoja baja con eso: se puede corregir y reimportar.
            if (l.CantidadContada is not null) hoja.Cell(fila, ColumnasExcel).Value = l.CantidadContada.Value;

            // Una sola línea fina abajo, como renglón para escribir la cantidad contada —
            // sin recuadros completos, que en papel vuelven la hoja ilegible.
            var rangoFila = hoja.Range(fila, 3, fila, ColumnasExcel);
            rangoFila.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            rangoFila.Style.Border.BottomBorderColor = XLColor.FromHtml("#94A3B8");
            rangoFila.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            hoja.Row(fila).Height = 22; // espacio para escribir a mano

            fila++;
        }

        var ultimaFila = fila - 1;

        hoja.Column(1).Hide();
        hoja.Column(2).Hide();
        hoja.Columns(3, ColumnasExcel - 1).AdjustToContents();
        hoja.Column(ColumnasExcel).Width = 18; // "Cantidad Contada": ancho fijo para escribir a mano
        hoja.Range(FilaEncabezado + 1, ColumnasExcel, ultimaFila, ColumnasExcel).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");

        // Sin cuadrícula: ni en pantalla ni al imprimir.
        hoja.ShowGridLines = false;
        hoja.PageSetup.ShowGridlines = false;

        hoja.SheetView.FreezeRows(FilaEncabezado);
        hoja.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        hoja.PageSetup.FitToPages(1, 0);
        hoja.PageSetup.Margins.SetLeft(0.6).SetRight(0.6).SetTop(0.7).SetBottom(0.7);
        hoja.PageSetup.CenterHorizontally = true;
        // El encabezado se repite en cada página impresa: sin esto, de la hoja 2 en
        // adelante no se sabe qué columna es cuál.
        hoja.PageSetup.SetRowsToRepeatAtTop(FilaEncabezado, FilaEncabezado);
        // No encadenar: AddText devuelve IXLRichString, que solo tiene AddText(string) —
        // las sobrecargas con XLHFPredefinedText/XLHFOccurrence son de IXLHFItem nada más.
        var piePagina = hoja.PageSetup.Footer.Right;
        piePagina.AddText("Página ", XLHFOccurrence.AllPages);
        piePagina.AddText(XLHFPredefinedText.PageNumber, XLHFOccurrence.AllPages);
        piePagina.AddText(" de ", XLHFOccurrence.AllPages);
        piePagina.AddText(XLHFPredefinedText.NumberOfPages, XLHFOccurrence.AllPages);

        using var stream = new MemoryStream();
        libro.SaveAs(stream);
        return (stream.ToArray(), $"{sesion.Codigo}.xlsx");
    }

    public async Task<ImportarHojaConteoResultadoDto> ImportarHojaAsync(
        int id, byte[] archivo, string nombreArchivo, bool adjuntarComoEvidencia, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = await _db.SesionesConteo.AsNoTracking()
            .Where(s => s.Id == id && s.PaisId == paisId).Select(s => s.Codigo).FirstOrDefaultAsync(ct)
            ?? throw new ConteoNoEncontradoException(id);

        var leidas = LeerHoja(archivo, codigo);

        var actualizadas = 0;
        var adjuntada = false;
        var (anterior, nuevo) = await EnCursoAsync(id, paisId, async () =>
        {
            var lineas = await _db.SesionConteoLineas
                .Include(l => l.Producto).Include(l => l.Ubicacion)
                .Where(l => l.SesionConteoId == id)
                .ToListAsync(ct);
            var porClave = lineas.ToDictionary(l => (l.ProductoId, l.UbicacionId));

            var desconocidas = leidas.Keys.Where(k => !porClave.ContainsKey(k)).ToList();
            if (desconocidas.Count > 0)
                throw new ArchivoInvalidoException($"El archivo trae {desconocidas.Count} fila(s) que no pertenecen a este conteo.");

            var cambiosPorLinea = leidas.ToDictionary(kv => porClave[kv.Key].Id, kv => (int?)kv.Value);
            var resultado = await AplicarCantidadesAsync(id, cambiosPorLinea, usuario, ct);
            actualizadas = leidas.Count;

            if (adjuntarComoEvidencia)
            {
                await AgregarEvidenciaAsync(id, nombreArchivo, archivo, usuario, ct);
                await _db.SaveChangesAsync(ct);
                adjuntada = true;
            }
            return resultado;
        }, ct);

        if (nuevo.Count > 0)
            await _auditoria.RegistrarAsync(nameof(SesionConteo), codigo, "Cargar cantidades",
                _auditoria.Capturar(anterior), _auditoria.Capturar(nuevo), paisId, usuario, "Importación de hoja Excel", ct);

        return new ImportarHojaConteoResultadoDto(actualizadas, adjuntada);
    }

    private static Dictionary<(int ProductoId, int UbicacionId), int> LeerHoja(byte[] archivo, string codigoEsperado)
    {
        XLWorkbook libro;
        try
        {
            libro = new XLWorkbook(new MemoryStream(archivo));
        }
        catch (Exception)
        {
            throw new ArchivoInvalidoException("El archivo no es un Excel válido (.xlsx).");
        }

        using (libro)
        {
            var hoja = libro.Worksheets.FirstOrDefault()
                ?? throw new ArchivoInvalidoException("El archivo no tiene ninguna hoja.");

            var codigoArchivo = hoja.Cell(2, 2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(codigoArchivo))
                throw new ArchivoInvalidoException("El archivo no tiene el formato de la hoja de conteo generada por el sistema.");
            if (!string.Equals(codigoArchivo, codigoEsperado, StringComparison.Ordinal))
                throw new ArchivoInvalidoException($"Este Excel es de otro conteo ({codigoArchivo}). Descargá la hoja de {codigoEsperado} desde este mismo conteo.");

            // La ubicación viaja por FILA (columna oculta 2), no en el encabezado — cada
            // fila puede ser de una ubicación distinta, porque la hoja se arma por producto.
            var contados = new Dictionary<(int, int), int>();
            var fila = FilaEncabezado + 1;
            while (!hoja.Cell(fila, 1).IsEmpty())
            {
                var celdaCantidad = hoja.Cell(fila, ColumnasExcel);
                if (!celdaCantidad.IsEmpty())
                {
                    if (!hoja.Cell(fila, 1).TryGetValue<int>(out var productoId) || !hoja.Cell(fila, 2).TryGetValue<int>(out var ubicacionId))
                        throw new ArchivoInvalidoException($"La fila {fila} no tiene los identificadores de la hoja generada por el sistema.");

                    // TryGetValue<int> redondea 3.5 a 4 en silencio: se pide un entero exacto.
                    if (!celdaCantidad.TryGetValue<double>(out var cantidad) || cantidad != Math.Floor(cantidad))
                        throw new ArchivoInvalidoException($"La cantidad contada de la fila {fila} debe ser un número entero.");
                    if (cantidad < 0 || cantidad > MaxCantidadContada)
                        throw new ArchivoInvalidoException($"La cantidad contada de la fila {fila} debe estar entre 0 y {MaxCantidadContada:N0}.");

                    contados[(productoId, ubicacionId)] = (int)cantidad;
                }
                fila++;
            }

            if (contados.Count == 0)
                throw new ArchivoInvalidoException("No se cargó ninguna cantidad contada en el archivo.");

            return contados;
        }
    }

    // ---------------------------------------------------------------- evidencia

    public async Task<ConteoEvidenciaDto> AdjuntarEvidenciaAsync(int id, string nombreArchivo, byte[] datos, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = await CodigoAsync(id, paisId, ct);

        var evidencia = await EnCursoAsync(id, paisId, async () =>
        {
            var e = await AgregarEvidenciaAsync(id, nombreArchivo, datos, usuario, ct);
            await _db.SaveChangesAsync(ct);
            return e;
        }, ct);

        await _auditoria.RegistrarAsync(nameof(SesionConteo), codigo, "Adjuntar evidencia", null,
            _auditoria.Capturar(new { evidencia.NombreArchivo, evidencia.ContentType, evidencia.TamanoBytes }),
            paisId, usuario, null, ct);

        return new ConteoEvidenciaDto(evidencia.Id, evidencia.NombreArchivo, evidencia.ContentType, evidencia.TamanoBytes, evidencia.SubidoPorNombre, evidencia.FechaSubida);
    }

    // No hace SaveChanges: lo guarda quien llama, dentro de su transacción.
    private async Task<SesionConteoEvidencia> AgregarEvidenciaAsync(int sesionId, string nombreArchivo, byte[] datos, UsuarioActuante usuario, CancellationToken ct)
    {
        if (datos.Length == 0)
            throw new ArchivoInvalidoException("El archivo está vacío.");
        if (datos.Length > MaxBytesEvidencia)
            throw new ArchivoInvalidoException("El archivo no puede superar los 10 MB.");

        var nombre = Path.GetFileName(nombreArchivo ?? string.Empty).Trim();
        if (nombre.Length == 0) nombre = "evidencia";
        if (nombre.Length > MaxLargoNombreArchivo)
        {
            var ext = Path.GetExtension(nombre);
            nombre = nombre[..(MaxLargoNombreArchivo - ext.Length)] + ext;
        }

        var contentType = DetectarTipo(datos, nombre)
            ?? throw new ArchivoInvalidoException("Formato no admitido. Subí una foto (JPG, PNG, WEBP), un PDF o un Excel (.xlsx).");

        var cantidad = await _db.SesionConteoEvidencias.CountAsync(e => e.SesionConteoId == sesionId, ct);
        if (cantidad >= MaxEvidenciasPorConteo)
            throw new ConteoInvalidoException($"Un conteo admite hasta {MaxEvidenciasPorConteo} archivos de evidencia.");

        var evidencia = new SesionConteoEvidencia
        {
            SesionConteoId = sesionId,
            NombreArchivo = nombre,
            ContentType = contentType,
            TamanoBytes = datos.Length,
            Datos = datos,
            SubidoPorId = usuario.Id,
            SubidoPorNombre = usuario.Nombre,
            FechaSubida = DateTime.UtcNow,
        };
        _db.SesionConteoEvidencias.Add(evidencia);
        return evidencia;
    }

    // El tipo se decide por los PRIMEROS BYTES del archivo, nunca por el Content-Type que
    // manda el cliente: un .exe renombrado a .png no debe entrar como imagen.
    private static string? DetectarTipo(byte[] d, string nombre)
    {
        if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47 && d[4] == 0x0D && d[5] == 0x0A && d[6] == 0x1A && d[7] == 0x0A)
            return "image/png";
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF)
            return "image/jpeg";
        if (d.Length >= 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P')
            return "image/webp";
        if (d.Length >= 5 && d[0] == '%' && d[1] == 'P' && d[2] == 'D' && d[3] == 'F' && d[4] == '-')
            return "application/pdf";
        if (d.Length >= 4 && d[0] == 0x50 && d[1] == 0x4B && d[2] == 0x03 && d[3] == 0x04
            && nombre.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return ContentTypeXlsx;
        return null;
    }

    public async Task EliminarEvidenciaAsync(int id, int evidenciaId, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = await CodigoAsync(id, paisId, ct);

        var nombre = await EnCursoAsync(id, paisId, async () =>
        {
            var e = await _db.SesionConteoEvidencias.FirstOrDefaultAsync(x => x.Id == evidenciaId && x.SesionConteoId == id, ct)
                ?? throw new ConteoNoEncontradoException(id);
            _db.SesionConteoEvidencias.Remove(e);
            await _db.SaveChangesAsync(ct);
            return e.NombreArchivo;
        }, ct);

        await _auditoria.RegistrarAsync(nameof(SesionConteo), codigo, "Eliminar evidencia",
            _auditoria.Capturar(new { NombreArchivo = nombre }), null, paisId, usuario, null, ct);
    }

    public async Task<(byte[] Datos, string ContentType, string NombreArchivo)> ObtenerEvidenciaAsync(int id, int evidenciaId, int paisId, CancellationToken ct)
    {
        var e = await _db.SesionConteoEvidencias.AsNoTracking()
            .Where(x => x.Id == evidenciaId && x.SesionConteoId == id && x.SesionConteo!.PaisId == paisId)
            .Select(x => new { x.Datos, x.ContentType, x.NombreArchivo })
            .FirstOrDefaultAsync(ct)
            ?? throw new ConteoNoEncontradoException(id);
        return (e.Datos, e.ContentType, e.NombreArchivo);
    }

    // ---------------------------------------------------------------- cerrar / cancelar

    public async Task<ConteoDetalleDto> CerrarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;

        // Una sola sentencia atómica: las condiciones (en curso, con evidencia, todo contado)
        // se evalúan en el mismo UPDATE que cambia el estado. Así ni dos cierres simultáneos
        // ni un cierre contra una carga de cantidades pueden colarse entre "chequear" y "cerrar".
        var filas = await _db.SesionesConteo
            .Where(s => s.Id == id && s.PaisId == paisId && s.Estado == EstadoConteo.EnCurso
                && s.Evidencias.Any() && !s.Lineas.Any(l => l.CantidadContada == null))
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Estado, EstadoConteo.Cerrado)
                .SetProperty(s => s.FechaCierre, ahora)
                .SetProperty(s => s.CerradoPorId, usuario.Id)
                .SetProperty(s => s.CerradoPorNombre, usuario.Nombre), ct);

        if (filas == 0)
        {
            var estado = await _db.SesionesConteo.AsNoTracking()
                .Where(s => s.Id == id && s.PaisId == paisId)
                .Select(s => new
                {
                    s.Codigo, s.Estado,
                    Evidencias = s.Evidencias.Count(),
                    SinContar = s.Lineas.Count(l => l.CantidadContada == null),
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new ConteoNoEncontradoException(id);

            if (estado.Estado != EstadoConteo.EnCurso)
                throw new ConteoEstadoInvalidoException($"El conteo {estado.Codigo} ya está {TextoEstado(estado.Estado)}.");
            if (estado.SinContar > 0)
                throw new ConteoEstadoInvalidoException($"Faltan {estado.SinContar} línea(s) por contar. Cargá todas las cantidades (un 0 también cuenta) antes de cerrar.");
            throw new ConteoEstadoInvalidoException("Adjuntá al menos un archivo de evidencia (foto del documento físico, PDF o Excel) antes de cerrar el conteo.");
        }

        var detalle = await ObtenerAsync(id, paisId, ct);
        await _auditoria.RegistrarAsync(nameof(SesionConteo), detalle.Resumen.Codigo, "Cerrar",
            _auditoria.Capturar(new { Estado = EstadoConteo.EnCurso }),
            _auditoria.Capturar(new { detalle.Resumen.Estado, detalle.Resumen.LineasConDiferencia, Evidencias = detalle.Resumen.CantidadEvidencias }),
            paisId, usuario, null, ct);
        return detalle;
    }

    public async Task<ConteoDetalleDto> CancelarAsync(int id, CancelarConteoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length < 3)
            throw new ConteoInvalidoException("Indicá el motivo de la cancelación.");
        if (motivo.Length > 500)
            throw new ConteoInvalidoException("El motivo no puede superar los 500 caracteres.");

        var ahora = DateTime.UtcNow;
        var filas = await _db.SesionesConteo
            .Where(s => s.Id == id && s.PaisId == paisId && s.Estado == EstadoConteo.EnCurso)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Estado, EstadoConteo.Cancelado)
                .SetProperty(s => s.MotivoCancelacion, motivo)
                .SetProperty(s => s.FechaCierre, ahora)
                .SetProperty(s => s.CerradoPorId, usuario.Id)
                .SetProperty(s => s.CerradoPorNombre, usuario.Nombre), ct);

        if (filas == 0)
            await LanzarPorEstadoAsync(id, paisId, ct);

        var detalle = await ObtenerAsync(id, paisId, ct);
        await _auditoria.RegistrarAsync(nameof(SesionConteo), detalle.Resumen.Codigo, "Cancelar",
            _auditoria.Capturar(new { Estado = EstadoConteo.EnCurso }),
            _auditoria.Capturar(new { detalle.Resumen.Estado }), paisId, usuario, motivo, ct);
        return detalle;
    }

    // ---------------------------------------------------------------- helpers

    // Ejecuta una modificación sobre un conteo EN CURSO. Primero toma el candado de la fila
    // con un UPDATE que solo matchea si sigue en curso: si otro pidió cerrarlo antes, esto
    // da 0 filas y se rechaza; si el cierre llega después, espera a que esta transacción
    // termine. Sin esto, se podría guardar una cantidad DESPUÉS de cerrado.
    private async Task<T> EnCursoAsync<T>(int id, int paisId, Func<Task<T>> accion, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var bloqueadas = await _db.SesionesConteo
            .Where(s => s.Id == id && s.PaisId == paisId && s.Estado == EstadoConteo.EnCurso)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Estado, EstadoConteo.EnCurso), ct);
        if (bloqueadas == 0)
            await LanzarPorEstadoAsync(id, paisId, ct);

        var resultado = await accion();
        await tx.CommitAsync(ct);
        return resultado;
    }

    // Se llama cuando una operación sobre un conteo EN CURSO no encontró filas: distingue
    // "no existe" (404) de "ya no está en curso" (409). Siempre lanza.
    private async Task LanzarPorEstadoAsync(int id, int paisId, CancellationToken ct)
    {
        var s = await _db.SesionesConteo.AsNoTracking()
            .Where(x => x.Id == id && x.PaisId == paisId)
            .Select(x => new { x.Codigo, x.Estado })
            .FirstOrDefaultAsync(ct)
            ?? throw new ConteoNoEncontradoException(id);
        throw new ConteoEstadoInvalidoException($"El conteo {s.Codigo} ya está {TextoEstado(s.Estado)}: no se puede modificar.");
    }

    private static string TextoEstado(EstadoConteo estado) => estado switch
    {
        EstadoConteo.Cerrado => "cerrado",
        EstadoConteo.Cancelado => "cancelado",
        _ => "en curso",
    };

    private Task<string> CodigoAsync(int id, CancellationToken ct) =>
        _db.SesionesConteo.AsNoTracking().Where(s => s.Id == id).Select(s => s.Codigo).FirstAsync(ct);

    private async Task<string> CodigoAsync(int id, int paisId, CancellationToken ct) =>
        await _db.SesionesConteo.AsNoTracking().Where(s => s.Id == id && s.PaisId == paisId).Select(s => s.Codigo).FirstOrDefaultAsync(ct)
        ?? throw new ConteoNoEncontradoException(id);

    // Existencia por (producto, ubicación) sumando CantidadEfectiva, igual que el resto del
    // sistema — el stock nunca se guarda, siempre se calcula.
    private async Task<Dictionary<(int ProductoId, int UbicacionId), int>> ExistenciasPorUbicacionAsync(
        List<int> productoIds, bool soloConStock, CancellationToken ct)
    {
        var consulta = _db.Movimientos.AsNoTracking()
            .Where(m => productoIds.Contains(m.ProductoId))
            .GroupBy(m => new { m.ProductoId, m.UbicacionId })
            .Select(g => new { g.Key.ProductoId, g.Key.UbicacionId, Existencia = g.Sum(m => m.CantidadEfectiva) });

        var filas = await consulta.ToListAsync(ct);
        return filas
            .Where(f => !soloConStock || f.Existencia > 0)
            .ToDictionary(f => (f.ProductoId, f.UbicacionId), f => f.Existencia);
    }

    private static string? NormalizarTexto(string? valor, int maximo, string campo)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        if (texto.Length > maximo)
            throw new ConteoInvalidoException($"{campo} no puede superar los {maximo} caracteres.");
        return texto;
    }
}
