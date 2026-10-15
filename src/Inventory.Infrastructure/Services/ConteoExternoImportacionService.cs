using System.Globalization;
using ClosedXML.Excel;
using Inventory.Application;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

// Módulo de conteo independiente — importación desde Excel (Fase 5). A diferencia de
// /conteos (que importa SU propia hoja, con layout fijo), este Excel viene de un sistema
// externo con columnas que pueden venir en cualquier orden — por eso la vista previa +
// mapeo, en vez de asumir un formato.
public class ConteoExternoImportacionService : IConteoExternoImportacionService
{
    private const int MaxFilasVistaPrevia = 10;

    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ConteoExternoImportacionService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public Task<VistaPreviaImportacionConteoExternoDto> VistaPreviaAsync(byte[] archivo, CancellationToken ct)
    {
        using var libro = AbrirLibro(archivo);
        var hoja = PrimeraHoja(libro);
        var rangoUsado = hoja.RangeUsed()
            ?? throw new ArchivoInvalidoException("El archivo no tiene filas con datos.");

        var totalFilas = rangoUsado.RowCount();
        var totalColumnas = rangoUsado.ColumnCount();

        var encabezados = new List<string>();
        for (var c = 1; c <= totalColumnas; c++)
        {
            var texto = hoja.Cell(1, c).GetString().Trim();
            encabezados.Add(texto.Length > 0 ? texto : $"Columna {c}");
        }

        var filas = new List<IReadOnlyList<string>>();
        for (var f = 2; f <= Math.Min(totalFilas, MaxFilasVistaPrevia + 1); f++)
        {
            var fila = new List<string>();
            for (var c = 1; c <= totalColumnas; c++)
                fila.Add(hoja.Cell(f, c).GetString().Trim());
            filas.Add(fila);
        }

        return Task.FromResult(new VistaPreviaImportacionConteoExternoDto(encabezados, filas, Math.Max(0, totalFilas - 1)));
    }

    public async Task<ImportacionConteoExternoResultadoDto> ImportarAsync(
        byte[] archivo, MapeoColumnasConteoExternoDto mapeo, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (mapeo.FilaInicioDatos < 1)
            throw new ValidacionException("La fila de inicio de datos debe ser 1 o mayor.");

        using var libro = AbrirLibro(archivo);
        var hoja = PrimeraHoja(libro);
        var ultimaFila = hoja.RangeUsed()?.RowCount() ?? 0;

        // Catálogos cargados una sola vez — este módulo nunca tiene miles de productos, y
        // así se evita un roundtrip a la base por cada fila del Excel.
        var productosPorCodigo = await _db.ConteoExternoProductos
            .Where(p => p.PaisId == paisId && p.Activo)
            .ToDictionaryAsync(p => p.CodigoNormalizado, ct);
        var unidadesPorCodigo = await _db.ConteoExternoUnidades
            .Where(u => u.PaisId == paisId && u.Activo)
            .ToDictionaryAsync(u => u.Codigo, ct);

        // Clave por REFERENCIA de producto (no por Id: un producto nuevo de esta misma
        // importación no tiene Id todavía, recién lo asigna el SaveChanges final) —
        // así tanto los existentes como los que se crean en esta misma pasada dedupen
        // correctamente contra una fila repetida del mismo archivo.
        var existenciasPorClave = new Dictionary<(ConteoExternoProducto Producto, string Almacen, string Lote), ConteoExternoExistencia>();
        var existenciasExistentes = await _db.ConteoExternoExistencias.Where(e => e.PaisId == paisId && e.Activo).ToListAsync(ct);
        var productosPorId = productosPorCodigo.Values.ToDictionary(p => p.Id);
        foreach (var existencia in existenciasExistentes)
            if (productosPorId.TryGetValue(existencia.ConteoExternoProductoId, out var producto))
                existenciasPorClave[(producto, existencia.AlmacenCodigo, existencia.Lote ?? string.Empty)] = existencia;

        var productosCreados = 0;
        var existenciasCreadas = 0;
        var existenciasActualizadas = 0;
        var filasOmitidas = 0;
        var advertencias = new List<string>();

        for (var f = mapeo.FilaInicioDatos; f <= ultimaFila; f++)
        {
            var codigoCrudo = hoja.Cell(f, mapeo.ColCodigo).GetString().Trim();
            if (codigoCrudo.Length == 0) continue; // fila en blanco, no es un error

            try
            {
                var codigoNormalizado = CodigoConteoExterno.Normalizar(codigoCrudo);
                if (!productosPorCodigo.TryGetValue(codigoNormalizado, out var producto))
                {
                    if (!mapeo.CrearProductosNuevos)
                    {
                        advertencias.Add($"Fila {f}: el código '{codigoCrudo}' no existe en el catálogo — omitida (no se pidió crear productos nuevos).");
                        filasOmitidas++;
                        continue;
                    }

                    var nombre = ObtenerTexto(hoja, f, mapeo.ColNombre);
                    var familia = ObtenerTexto(hoja, f, mapeo.ColFamilia)?.ToUpperInvariant() ?? string.Empty;

                    producto = new ConteoExternoProducto
                    {
                        Codigo = codigoCrudo,
                        CodigoNormalizado = codigoNormalizado,
                        Nombre = string.IsNullOrWhiteSpace(nombre) ? codigoCrudo : nombre,
                        Familia = familia,
                        Origen = OrigenConteoExternoProducto.Importado,
                        PaisId = paisId,
                        Activo = true,
                    };
                    _db.ConteoExternoProductos.Add(producto);
                    productosPorCodigo[codigoNormalizado] = producto;
                    productosCreados++;
                }

                var almacenCodigo = ObtenerTexto(hoja, f, mapeo.ColAlmacen);
                if (almacenCodigo is null)
                {
                    advertencias.Add($"Fila {f}: falta el código de almacén — omitida.");
                    filasOmitidas++;
                    continue;
                }

                var lote = ObtenerTexto(hoja, f, mapeo.ColLote);

                var codigoUnidadFila = ObtenerTexto(hoja, f, mapeo.ColUnidad)?.ToUpperInvariant();
                if (producto.UnidadCodigo is null)
                {
                    // La unidad se fija con el primer lote que trae este producto, igual que
                    // en el alta manual — sin unidad en el archivo no hay con qué fijarla.
                    if (codigoUnidadFila is null || !unidadesPorCodigo.ContainsKey(codigoUnidadFila))
                    {
                        advertencias.Add($"Fila {f}: el producto '{producto.Codigo}' todavía no tiene unidad y la fila no trae una unidad válida — omitida.");
                        filasOmitidas++;
                        continue;
                    }
                    producto.UnidadCodigo = codigoUnidadFila;
                }
                else if (codigoUnidadFila is not null && codigoUnidadFila != producto.UnidadCodigo)
                {
                    // Un archivo externo no siempre es consistente: se avisa, pero manda la
                    // unidad ya fijada del producto en vez de perder la fila.
                    advertencias.Add($"Fila {f}: la unidad '{codigoUnidadFila}' no coincide con la ya fijada para '{producto.Codigo}' ({producto.UnidadCodigo}) — se usó la del producto.");
                }

                var unidad = unidadesPorCodigo[producto.UnidadCodigo!];

                var textoCantidad = ObtenerTexto(hoja, f, mapeo.ColCantidad);
                if (!TryParseDecimal(textoCantidad, out var cantidadCruda))
                {
                    advertencias.Add($"Fila {f}: la cantidad '{textoCantidad}' no es un número válido — omitida.");
                    filasOmitidas++;
                    continue;
                }

                var cantidad = CantidadConteoExterno.Validar(cantidadCruda, unidad.DecimalesPermitidos, "La cantidad");

                // El costo solo se completa si el producto todavía no tiene uno — nunca
                // pisa un costo ya cargado a mano (ver Fase 1/2: costo único por producto).
                if (producto.CostoReferencia is null)
                {
                    var textoCosto = ObtenerTexto(hoja, f, mapeo.ColCosto);
                    if (textoCosto is not null && TryParseDecimal(textoCosto, out var costo) && costo >= 0)
                        producto.CostoReferencia = costo;
                }

                var clave = (producto, almacenCodigo, lote ?? string.Empty);
                if (existenciasPorClave.TryGetValue(clave, out var existencia))
                {
                    existencia.CantidadSistema = cantidad;
                    existenciasActualizadas++;
                }
                else
                {
                    var nueva = new ConteoExternoExistencia
                    {
                        ConteoExternoProducto = producto,
                        AlmacenCodigo = almacenCodigo,
                        Lote = lote,
                        CantidadSistema = cantidad,
                        PaisId = paisId,
                        Activo = true,
                    };
                    _db.ConteoExternoExistencias.Add(nueva);
                    existenciasPorClave[clave] = nueva;
                    existenciasCreadas++;
                }
            }
            catch (DominioException ex)
            {
                advertencias.Add($"Fila {f}: {ex.Message}");
                filasOmitidas++;
            }
        }

        await _db.SaveChangesAsync(ct);

        // Una importación completa es UNA fila de auditoría, no una por línea — mismo
        // criterio que la importación de hoja de /conteos.
        await _auditoria.RegistrarAsync(nameof(ConteoExternoExistencia), $"import-{DateTime.UtcNow:yyyyMMddHHmmss}", "Importar", null,
            _auditoria.Capturar(new { productosCreados, existenciasCreadas, existenciasActualizadas, filasOmitidas }), paisId, usuario, "Importación de Excel externo", ct);

        return new ImportacionConteoExternoResultadoDto(productosCreados, existenciasCreadas, existenciasActualizadas, filasOmitidas, advertencias);
    }

    private static bool TryParseDecimal(string? texto, out decimal valor)
        => decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor)
        || decimal.TryParse(texto, NumberStyles.Number, CultureInfo.CurrentCulture, out valor);

    private static string? ObtenerTexto(IXLWorksheet hoja, int fila, int? columna)
    {
        if (columna is null) return null;
        var texto = hoja.Cell(fila, columna.Value).GetString().Trim();
        return texto.Length == 0 ? null : texto;
    }

    private static XLWorkbook AbrirLibro(byte[] archivo)
    {
        try
        {
            return new XLWorkbook(new MemoryStream(archivo));
        }
        catch (Exception)
        {
            throw new ArchivoInvalidoException("El archivo no es un Excel válido (.xlsx).");
        }
    }

    private static IXLWorksheet PrimeraHoja(XLWorkbook libro)
        => libro.Worksheets.FirstOrDefault() ?? throw new ArchivoInvalidoException("El archivo no tiene ninguna hoja.");
}
