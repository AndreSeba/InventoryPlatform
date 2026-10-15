using Inventory.Application;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class ConteoExternoProductoService : IConteoExternoProductoService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ConteoExternoProductoService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static object Snapshot(ConteoExternoProducto p) => new
    {
        p.Codigo, p.Nombre, p.Familia, p.Origen, p.UnidadCodigo, p.CostoReferencia, p.Activo
    };

    public async Task<IReadOnlyList<ConteoExternoProductoDto>> ListarAsync(int paisId, string? texto, string? familia, bool incluirInactivos, CancellationToken ct)
    {
        var query = _db.ConteoExternoProductos.AsNoTracking().Include(p => p.Pais).Where(p => p.PaisId == paisId);
        if (!incluirInactivos) query = query.Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var buscado = texto.Trim().ToUpperInvariant();
            query = query.Where(p => p.Codigo.ToUpper().Contains(buscado) || p.Nombre.ToUpper().Contains(buscado));
        }

        if (!string.IsNullOrWhiteSpace(familia))
        {
            var familiaBuscada = familia.Trim().ToUpperInvariant();
            query = query.Where(p => p.Familia == familiaBuscada);
        }

        return await query.OrderBy(p => p.Codigo)
            .Select(p => new ConteoExternoProductoDto(p.Id, p.Codigo, p.Nombre, p.Familia, p.Origen, p.UnidadCodigo, p.CostoReferencia, p.PaisId, p.Pais!.Nombre, p.Activo))
            .ToListAsync(ct);
    }

    // Buscador por código o nombre para el autocompletado (hasta `max` coincidencias). Si el texto es un
    // código exacto (con o sin ceros a la izquierda) ese producto va primero.
    public async Task<IReadOnlyList<ConteoExternoProductoBusquedaDto>> BuscarAsync(int paisId, string? texto, int max, CancellationToken ct)
    {
        var buscado = (texto ?? string.Empty).Trim().ToUpperInvariant();
        if (buscado.Length == 0) return [];
        var normalizado = CodigoConteoExterno.Normalizar(buscado);
        var tope = Math.Clamp(max, 1, 20);

        return await _db.ConteoExternoProductos.AsNoTracking()
            .Where(p => p.PaisId == paisId && p.Activo &&
                        (p.CodigoNormalizado == normalizado || p.Codigo.ToUpper().Contains(buscado) || p.Nombre.ToUpper().Contains(buscado)))
            .OrderByDescending(p => p.CodigoNormalizado == normalizado)
            .ThenBy(p => p.Codigo)
            .Take(tope)
            .Select(p => new ConteoExternoProductoBusquedaDto(p.Id, p.Codigo, p.Nombre, p.Familia))
            .ToListAsync(ct);
    }

    public async Task<ConteoExternoSugerenciasDto> ObtenerSugerenciasAsync(int id, int paisId, CancellationToken ct)
    {
        var producto = await _db.ConteoExternoProductos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.PaisId == paisId && p.Activo, ct)
            ?? throw new ConteoExternoProductoNoEncontradoException(id);

        var existencias = await _db.ConteoExternoExistencias.AsNoTracking()
            .Where(e => e.PaisId == paisId && e.ConteoExternoProductoId == id)
            .Select(e => new { e.AlmacenCodigo, e.Lote })
            .ToListAsync(ct);

        var habitual = existencias.GroupBy(e => e.AlmacenCodigo)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => g.Key).FirstOrDefault();
        var lotes = existencias.Select(e => e.Lote).Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l!).Distinct().OrderBy(l => l).ToList();

        return new ConteoExternoSugerenciasDto(producto.Id, producto.Codigo, producto.Nombre, producto.Familia,
            producto.UnidadCodigo, producto.CostoReferencia, habitual, lotes);
    }

    public async Task<IReadOnlyList<string>> ListarFamiliasAsync(int paisId, CancellationToken ct)
    {
        return await _db.ConteoExternoProductos.AsNoTracking()
            .Where(p => p.PaisId == paisId && p.Activo && p.Familia.Length > 0)
            .Select(p => p.Familia)
            .Distinct()
            .OrderBy(f => f)
            .ToListAsync(ct);
    }

    public async Task<ConteoExternoProductoDto> CrearAsync(CrearConteoExternoProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = ValidarCodigo(dto.Codigo);
        var nombre = Validacion.Texto(dto.Nombre, 200, "El nombre del producto");
        var familia = Validacion.TextoOpcional(dto.Familia, 100, "La familia") ?? string.Empty;
        familia = familia.ToUpperInvariant();

        var codigoNormalizado = CodigoConteoExterno.Normalizar(codigo);
        var yaExiste = await _db.ConteoExternoProductos.AnyAsync(p => p.PaisId == paisId && p.CodigoNormalizado == codigoNormalizado && p.Activo, ct);
        if (yaExiste)
            throw new CodigoConteoExternoProductoDuplicadoException(codigo);

        var producto = new ConteoExternoProducto
        {
            Codigo = codigo,
            CodigoNormalizado = codigoNormalizado,
            Nombre = nombre,
            Familia = familia,
            Origen = OrigenConteoExternoProducto.Manual,
            PaisId = paisId,
            Activo = true,
        };
        _db.ConteoExternoProductos.Add(producto);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoProducto), producto.Codigo, "Crear", null, _auditoria.Capturar(Snapshot(producto)), paisId, usuario, null, ct);

        await _db.Entry(producto).Reference(p => p.Pais).LoadAsync(ct);
        return new ConteoExternoProductoDto(producto.Id, producto.Codigo, producto.Nombre, producto.Familia, producto.Origen, producto.UnidadCodigo, producto.CostoReferencia, producto.PaisId, producto.Pais!.Nombre, producto.Activo);
    }

    // El Código nunca se edita acá — mover existencias y costos de un código a otro es
    // un caso que esta fase no necesita todavía.
    public async Task<ConteoExternoProductoDto> ActualizarAsync(int id, ActualizarConteoExternoProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var producto = await _db.ConteoExternoProductos.Include(p => p.Pais)
            .FirstOrDefaultAsync(p => p.Id == id && p.PaisId == paisId, ct)
            ?? throw new ConteoExternoProductoNoEncontradoException(id);

        var anterior = _auditoria.Capturar(Snapshot(producto));

        var nombre = Validacion.Texto(dto.Nombre, 200, "El nombre del producto");
        var familia = (Validacion.TextoOpcional(dto.Familia, 100, "La familia") ?? string.Empty).ToUpperInvariant();

        if (dto.CostoReferencia is < 0)
            throw new ValidacionException("El costo de referencia no puede ser negativo.");

        producto.Nombre = nombre;
        producto.Familia = familia;
        producto.CostoReferencia = dto.CostoReferencia;
        producto.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoProducto), producto.Codigo, "Actualizar", anterior, _auditoria.Capturar(Snapshot(producto)), paisId, usuario, null, ct);

        return new ConteoExternoProductoDto(producto.Id, producto.Codigo, producto.Nombre, producto.Familia, producto.Origen, producto.UnidadCodigo, producto.CostoReferencia, producto.PaisId, producto.Pais!.Nombre, producto.Activo);
    }

    // Validacion.Texto solo recorta espacios de los extremos; esto además rechaza
    // espacios INTERNOS en el código — un código con un espacio en medio normalmente es
    // un error de tipeo o de copiado del Excel de origen.
    private static string ValidarCodigo(string? valor)
    {
        var codigo = Validacion.Texto(valor, 30, "El código del producto");
        if (codigo.Any(char.IsWhiteSpace))
            throw new ValidacionException("El código del producto no puede contener espacios.");
        return codigo;
    }
}
