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
    // (deja lugar arriba para el título y los datos de sesión/ubicación/fecha) — el
    // importador lee con este mismo número, así que si se mueve acá hay que moverlo allá.
    private const int FilaEncabezado = 5;

    public async Task<(byte[] Contenido, string NombreArchivo, string SesionConteo)> GenerarHojaConteoAsync(GenerarHojaConteoDto dto, CancellationToken ct)
    {
        var ubicacion = await _db.Ubicaciones.FirstOrDefaultAsync(u => u.Id == dto.UbicacionId && u.Activo, ct)
            ?? throw new UbicacionNoEncontradaException(dto.UbicacionId);

        var query = _db.Productos.AsNoTracking().Where(p => p.Activo);
        if (dto.ProductoIds is { Count: > 0 })
            query = query.Where(p => dto.ProductoIds.Contains(p.Id));
        else if (dto.CategoriaId is not null)
            query = query.Where(p => p.CategoriaId == dto.CategoriaId);

        var productos = await query.OrderBy(p => p.Nombre).ToListAsync(ct);
        if (productos.Count == 0)
            throw new ArchivoInvalidoException("No hay productos activos para generar la hoja de conteo con ese filtro.");

        var productoIds = productos.Select(p => p.Id).ToList();
        var existencias = await _db.Movimientos
            .Where(m => m.UbicacionId == dto.UbicacionId && productoIds.Contains(m.ProductoId))
            .GroupBy(m => m.ProductoId)
            .Select(g => new { ProductoId = g.Key, Existencia = g.Sum(m => m.CantidadEfectiva) })
            .ToDictionaryAsync(x => x.ProductoId, x => x.Existencia, ct);

        var sesionConteo = $"CONTEO-{ubicacion.CodigoUbicacion}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Conteo");

        hoja.Cell(1, 1).Value = "Hoja de Conteo Físico";
        hoja.Range(1, 1, 1, 6).Merge();
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        hoja.Cell(2, 1).Value = "Sesión:";
        hoja.Cell(2, 1).Style.Font.Bold = true;
        hoja.Cell(2, 2).Value = sesionConteo;
        hoja.Cell(2, 4).Value = "Ubicación:";
        hoja.Cell(2, 4).Style.Font.Bold = true;
        hoja.Cell(2, 5).Value = ubicacion.CodigoUbicacion;
        hoja.Cell(3, 1).Value = "Fecha:";
        hoja.Cell(3, 1).Style.Font.Bold = true;
        hoja.Cell(3, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        hoja.Cell(FilaEncabezado, 1).Value = "ProductoId";
        hoja.Cell(FilaEncabezado, 2).Value = "Código";
        hoja.Cell(FilaEncabezado, 3).Value = "Nombre";
        hoja.Cell(FilaEncabezado, 4).Value = "Unidad";
        hoja.Cell(FilaEncabezado, 5).Value = "Existencia Sistema";
        hoja.Cell(FilaEncabezado, 6).Value = "Cantidad Contada";
        var rangoEncabezado = hoja.Range(FilaEncabezado, 1, FilaEncabezado, 6);
        rangoEncabezado.Style.Font.Bold = true;
        rangoEncabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        rangoEncabezado.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        var fila = FilaEncabezado + 1;
        foreach (var p in productos)
        {
            hoja.Cell(fila, 1).Value = p.Id;
            hoja.Cell(fila, 2).Value = p.CodigoProducto;
            hoja.Cell(fila, 3).Value = p.Nombre;
            hoja.Cell(fila, 4).Value = p.UnidadMedida;
            hoja.Cell(fila, 5).Value = existencias.TryGetValue(p.Id, out var ex) ? ex : 0m;
            hoja.Range(fila, 1, fila, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            fila++;
        }

        hoja.Column(1).Hide();
        hoja.Columns(2, 6).AdjustToContents();
        hoja.SheetView.FreezeRows(FilaEncabezado);
        hoja.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        hoja.PageSetup.FitToPages(1, 0);
        hoja.PageSetup.Margins.SetLeft(1.0).SetRight(1.0).SetTop(1.0).SetBottom(1.0);

        using var stream = new MemoryStream();
        libro.SaveAs(stream);

        var nombreArchivo = $"ConteoFisico_{ubicacion.CodigoUbicacion}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
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
            var ubicacionCodigo = hoja.Cell(2, 5).GetString().Trim();
            if (string.IsNullOrWhiteSpace(sesionConteo) || string.IsNullOrWhiteSpace(ubicacionCodigo))
                throw new ArchivoInvalidoException("El archivo no tiene el formato de la hoja de conteo generada por el sistema.");

            var ubicacion = await _db.Ubicaciones.FirstOrDefaultAsync(u => u.CodigoUbicacion == ubicacionCodigo && u.Activo, ct)
                ?? throw new ArchivoInvalidoException($"No existe (o está inactiva) la ubicación '{ubicacionCodigo}' de esta hoja.");

            var contados = new List<(int ProductoId, decimal Cantidad)>();
            var fila = FilaEncabezado + 1;
            while (!hoja.Cell(fila, 1).IsEmpty())
            {
                var celdaCantidad = hoja.Cell(fila, 6);
                if (!celdaCantidad.IsEmpty())
                {
                    var productoId = hoja.Cell(fila, 1).GetValue<int>();
                    var cantidad = celdaCantidad.GetValue<decimal>();
                    if (cantidad < 0)
                        throw new ArchivoInvalidoException($"La cantidad contada en la fila {fila} no puede ser negativa.");
                    contados.Add((productoId, cantidad));
                }
                fila++;
            }

            if (contados.Count == 0)
                throw new ArchivoInvalidoException("No se cargó ninguna cantidad contada en el archivo.");

            var conDiferencia = new List<ConteoDto>();
            foreach (var (productoId, cantidad) in contados)
            {
                var ultimoNumero = await _db.Conteos
                    .Where(c => c.SesionConteo == sesionConteo && c.ProductoId == productoId && c.UbicacionId == ubicacion.Id)
                    .OrderByDescending(c => c.NumeroConteo)
                    .Select(c => (int?)c.NumeroConteo)
                    .FirstOrDefaultAsync(ct);

                var registrado = await RegistrarAsync(
                    new RegistrarConteoDto(sesionConteo, productoId, ubicacion.Id, (ultimoNumero ?? 0) + 1, cantidad),
                    usuario, ct);

                if (registrado.Diferencia != 0)
                    conDiferencia.Add(registrado);
            }

            return new ImportarHojaConteoResultadoDto(sesionConteo, contados.Count, conDiferencia);
        }
    }
}
