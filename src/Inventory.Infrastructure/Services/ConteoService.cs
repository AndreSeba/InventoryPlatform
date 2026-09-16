using ClosedXML.Excel;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class ConteoService : IConteoService
{
    private readonly InventoryDbContext _db;

    public ConteoService(InventoryDbContext db) => _db = db;

    public async Task<ConteoDto> RegistrarAsync(RegistrarConteoDto dto, UsuarioActuante usuario, CancellationToken ct)
    {
        if (dto.NumeroConteo < 1)
            throw new ArgumentOutOfRangeException(nameof(dto.NumeroConteo), "El número de conteo debe ser 1 o mayor.");
        if (dto.CantidadContada < 0)
            throw new ArgumentOutOfRangeException(nameof(dto.CantidadContada), "La cantidad contada no puede ser negativa.");

        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == dto.ProductoId && p.Activo, ct)
            ?? throw new ProductoNoEncontradoException(dto.ProductoId);

        var ubicacion = await _db.Ubicaciones.FirstOrDefaultAsync(u => u.Id == dto.UbicacionId && u.Activo, ct)
            ?? throw new UbicacionNoEncontradaException(dto.UbicacionId);

        // UQ_Conteo_Sesion (guía v3/v4): permite reconteos, no duplicar el mismo número.
        var yaExiste = await _db.Conteos.AnyAsync(c =>
            c.SesionConteo == dto.SesionConteo && c.ProductoId == dto.ProductoId &&
            c.UbicacionId == dto.UbicacionId && c.NumeroConteo == dto.NumeroConteo, ct);
        if (yaExiste)
            throw new ConteoDuplicadoException(dto.SesionConteo, dto.NumeroConteo);

        var conteo = new Conteo
        {
            SesionConteo = dto.SesionConteo,
            ProductoId = producto.Id,
            UbicacionId = ubicacion.Id,
            NumeroConteo = dto.NumeroConteo,
            CantidadContada = dto.CantidadContada,
            ContadoPorId = usuario.Id,
            ContadoPorNombre = usuario.Nombre,
            FechaConteo = DateTime.UtcNow,
        };

        _db.Conteos.Add(conteo);
        await _db.SaveChangesAsync(ct);

        var existenciaSistema = await _db.Movimientos
            .Where(m => m.ProductoId == producto.Id && m.UbicacionId == ubicacion.Id)
            .SumAsync(m => (decimal?)m.CantidadEfectiva, ct) ?? 0m;

        return new ConteoDto(
            conteo.Id, conteo.SesionConteo, conteo.ProductoId, producto.Nombre, conteo.UbicacionId,
            ubicacion.CodigoUbicacion, conteo.NumeroConteo, conteo.CantidadContada, conteo.ContadoPorId, conteo.ContadoPorNombre,
            conteo.FechaConteo, existenciaSistema, conteo.CantidadContada - existenciaSistema
        );
    }

    public async Task<IReadOnlyList<ConteoDto>> ListarPorSesionAsync(string sesionConteo, CancellationToken ct)
    {
        var conteos = await _db.Conteos.AsNoTracking()
            .Include(c => c.Producto).Include(c => c.Ubicacion)
            .Where(c => c.SesionConteo == sesionConteo)
            .OrderBy(c => c.Producto!.Nombre).ThenBy(c => c.NumeroConteo)
            .ToListAsync(ct);

        if (conteos.Count == 0) return [];

        var claves = conteos.Select(c => (c.ProductoId, c.UbicacionId)).Distinct().ToList();
        var existencias = new Dictionary<(int, int), decimal>();
        foreach (var (productoId, ubicacionId) in claves)
        {
            existencias[(productoId, ubicacionId)] = await _db.Movimientos
                .Where(m => m.ProductoId == productoId && m.UbicacionId == ubicacionId)
                .SumAsync(m => (decimal?)m.CantidadEfectiva, ct) ?? 0m;
        }

        return conteos.Select(c =>
        {
            var existenciaSistema = existencias[(c.ProductoId, c.UbicacionId)];
            return new ConteoDto(
                c.Id, c.SesionConteo, c.ProductoId, c.Producto!.Nombre, c.UbicacionId, c.Ubicacion!.CodigoUbicacion,
                c.NumeroConteo, c.CantidadContada, c.ContadoPorId, c.ContadoPorNombre, c.FechaConteo, existenciaSistema, c.CantidadContada - existenciaSistema
            );
        }).ToList();
    }

    // Fila donde arranca el encabezado de la tabla de productos en la hoja generada
    // (deja lugar arriba para el título y los datos de sesión/fecha) — el importador lee
    // con este mismo número, así que si se mueve acá hay que moverlo allá.
    private const int FilaEncabezado = 5;
    private const int ColumnasExcel = 9;

    // Sin ubicación como filtro de entrada (2026-09-15, pedido del operario): se eligen
    // PRODUCTOS, y la hoja arma una fila por cada (producto, ubicación) donde ese producto
    // tiene stock ahora mismo — un producto guardado en 3 racks genera 3 filas. Reusa el
    // mismo criterio de "dónde tiene stock" que ProductoService.ListarUbicacionesConStockAsync,
    // pero de una sola consulta agrupada para todos los productos elegidos a la vez.
    public async Task<(byte[] Contenido, string NombreArchivo, string SesionConteo)> GenerarHojaConteoAsync(GenerarHojaConteoDto dto, CancellationToken ct)
    {
        // La hoja se arma SIEMPRE sobre productos elegidos uno por uno (2026-09-16). Antes,
        // si no venía selección, caía a "todos los productos activos" (o a una categoría
        // entera), que es justo lo que no se quiere: un conteo físico se hace sobre un
        // conjunto acotado, y una hoja con el catálogo completo es inmanejable en papel.
        if (dto.ProductoIds is not { Count: > 0 })
            throw new SeleccionDeProductosVaciaException();

        var idsPedidos = dto.ProductoIds.Distinct().ToList();

        var productos = await _db.Productos.AsNoTracking().Include(p => p.Categoria)
            .Where(p => p.Activo && idsPedidos.Contains(p.Id))
            .OrderBy(p => p.Nombre)
            .ToListAsync(ct);

        // Si alguno de los ids pedidos no existe o está inactivo, avisar en vez de
        // generar en silencio una hoja más corta que lo que el usuario eligió.
        if (productos.Count != idsPedidos.Count)
        {
            var faltantes = idsPedidos.Except(productos.Select(p => p.Id)).ToList();
            throw new ArchivoInvalidoException(
                $"No se puede generar la hoja: {faltantes.Count} de los productos seleccionados ya no existen o están inactivos.");
        }

        var productoIds = productos.Select(p => p.Id).ToList();

        var filas = await _db.Movimientos.AsNoTracking()
            .Where(m => productoIds.Contains(m.ProductoId))
            .GroupBy(m => new { m.ProductoId, m.UbicacionId, m.Ubicacion!.CodigoUbicacion })
            .Where(g => g.Sum(m => m.CantidadEfectiva) > 0)
            .Select(g => new { g.Key.ProductoId, g.Key.UbicacionId, g.Key.CodigoUbicacion, Existencia = g.Sum(m => m.CantidadEfectiva) })
            .ToListAsync(ct);

        if (filas.Count == 0)
            throw new ArchivoInvalidoException("Ninguno de los productos elegidos tiene stock en alguna ubicación.");

        var productosPorId = productos.ToDictionary(p => p.Id);
        var sesionConteo = $"CONTEO-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Conteo");

        hoja.Cell(1, 1).Value = "Hoja de Conteo Físico";
        hoja.Range(1, 1, 1, ColumnasExcel).Merge();
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        hoja.Cell(2, 1).Value = "Sesión:";
        hoja.Cell(2, 1).Style.Font.Bold = true;
        hoja.Cell(2, 2).Value = sesionConteo;
        hoja.Cell(3, 1).Value = "Fecha:";
        hoja.Cell(3, 1).Style.Font.Bold = true;
        hoja.Cell(3, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

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
        foreach (var f in filas.OrderBy(x => productosPorId[x.ProductoId].Nombre).ThenBy(x => x.CodigoUbicacion))
        {
            var producto = productosPorId[f.ProductoId];
            hoja.Cell(fila, 1).Value = f.ProductoId;
            hoja.Cell(fila, 2).Value = f.UbicacionId;
            hoja.Cell(fila, 3).Value = producto.CodigoProducto;
            hoja.Cell(fila, 4).Value = producto.Nombre;
            hoja.Cell(fila, 5).Value = producto.Categoria?.CodigoCategoria ?? "";
            hoja.Cell(fila, 6).Value = producto.UnidadMedida;
            hoja.Cell(fila, 7).Value = f.CodigoUbicacion;
            hoja.Cell(fila, 8).Value = f.Existencia;
            hoja.Cell(fila, 8).Style.NumberFormat.Format = "#,##0.00";

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
        hoja.SheetView.ShowGridLines = false;
        hoja.PageSetup.ShowGridlines = false;

        hoja.SheetView.FreezeRows(FilaEncabezado);
        hoja.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        hoja.PageSetup.FitToPages(1, 0);
        hoja.PageSetup.Margins.SetLeft(0.6).SetRight(0.6).SetTop(0.7).SetBottom(0.7);
        hoja.PageSetup.CenterHorizontally = true;
        // El encabezado se repite en cada página impresa: sin esto, de la hoja 2 en
        // adelante no se sabe qué columna es cuál.
        hoja.PageSetup.SetRowsToRepeatAtTop(FilaEncabezado, FilaEncabezado);
        hoja.PageSetup.Footer.Right.AddText("Página ").AddText(XLHFPredefinedText.PageNumber)
            .AddText(" de ").AddText(XLHFPredefinedText.NumberOfPages);

        using var stream = new MemoryStream();
        libro.SaveAs(stream);

        var nombreArchivo = $"ConteoFisico_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        return (stream.ToArray(), nombreArchivo, sesionConteo);
    }

    public async Task<ImportarHojaConteoResultadoDto> ImportarHojaConteoAsync(Stream archivo, UsuarioActuante usuario, CancellationToken ct)
    {
        XLWorkbook libro;
        try
        {
            libro = new XLWorkbook(archivo);
        }
        catch (Exception)
        {
            throw new ArchivoInvalidoException("El archivo no es un Excel válido (.xlsx).");
        }

        using (libro)
        {
            var hoja = libro.Worksheets.FirstOrDefault()
                ?? throw new ArchivoInvalidoException("El archivo no tiene ninguna hoja.");

            var sesionConteo = hoja.Cell(2, 2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(sesionConteo))
                throw new ArchivoInvalidoException("El archivo no tiene el formato de la hoja de conteo generada por el sistema.");

            // La ubicación viaja por FILA (columna oculta 2), no en el encabezado — cada
            // fila puede ser de una ubicación distinta, porque la hoja se arma por producto
            // y no por ubicación única.
            var contados = new List<(int ProductoId, int UbicacionId, decimal Cantidad)>();
            var fila = FilaEncabezado + 1;
            while (!hoja.Cell(fila, 1).IsEmpty())
            {
                var celdaCantidad = hoja.Cell(fila, ColumnasExcel);
                if (!celdaCantidad.IsEmpty())
                {
                    var productoId = hoja.Cell(fila, 1).GetValue<int>();
                    var ubicacionId = hoja.Cell(fila, 2).GetValue<int>();
                    var cantidad = celdaCantidad.GetValue<decimal>();
                    if (cantidad < 0)
                        throw new ArchivoInvalidoException($"La cantidad contada en la fila {fila} no puede ser negativa.");
                    contados.Add((productoId, ubicacionId, cantidad));
                }
                fila++;
            }

            if (contados.Count == 0)
                throw new ArchivoInvalidoException("No se cargó ninguna cantidad contada en el archivo.");

            var conDiferencia = new List<ConteoDto>();
            foreach (var (productoId, ubicacionId, cantidad) in contados)
            {
                var ultimoNumero = await _db.Conteos
                    .Where(c => c.SesionConteo == sesionConteo && c.ProductoId == productoId && c.UbicacionId == ubicacionId)
                    .OrderByDescending(c => c.NumeroConteo)
                    .Select(c => (int?)c.NumeroConteo)
                    .FirstOrDefaultAsync(ct);

                var registrado = await RegistrarAsync(
                    new RegistrarConteoDto(sesionConteo, productoId, ubicacionId, (ultimoNumero ?? 0) + 1, cantidad),
                    usuario, ct);

                if (registrado.Diferencia != 0)
                    conDiferencia.Add(registrado);
            }

            return new ImportarHojaConteoResultadoDto(sesionConteo, contados.Count, conDiferencia);
        }
    }
}
