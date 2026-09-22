using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class UnidadService : IUnidadService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public UnidadService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static object Snapshot(Unidad u) => new { u.CodigoUnidad, u.Nombre, u.Activo };

    public async Task<IReadOnlyList<UnidadDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct)
    {
        var query = _db.Unidades.AsNoTracking().Include(u => u.Pais).Where(u => u.PaisId == paisId);
        if (!incluirInactivas) query = query.Where(u => u.Activo);

        return await query.OrderBy(u => u.CodigoUnidad)
            .Select(u => new UnidadDto(u.Id, u.CodigoUnidad, u.Nombre, u.Activo, u.PaisId, u.Pais!.Nombre))
            .ToListAsync(ct);
    }

    public async Task<UnidadDto> CrearAsync(CrearUnidadDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = dto.CodigoUnidad.Trim().ToUpperInvariant();

        // Único POR PAÍS, no global — dos países pueden repetir un código de unidad.
        var yaExiste = await _db.Unidades.AnyAsync(u => u.PaisId == paisId && u.CodigoUnidad == codigo && u.Activo, ct);
        if (yaExiste)
            throw new CodigoUnidadDuplicadoException(codigo);

        var unidad = new Unidad { CodigoUnidad = codigo, Nombre = dto.Nombre.Trim(), PaisId = paisId, Activo = true };
        _db.Unidades.Add(unidad);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Unidad), unidad.CodigoUnidad, "Crear", null, _auditoria.Capturar(Snapshot(unidad)), paisId, usuario, null, ct);

        await _db.Entry(unidad).Reference(u => u.Pais).LoadAsync(ct);
        return new UnidadDto(unidad.Id, unidad.CodigoUnidad, unidad.Nombre, unidad.Activo, unidad.PaisId, unidad.Pais!.Nombre);
    }

    // Solo Nombre y Activo: CodigoUnidad es inmutable porque viaja dentro de
    // Producto.ClaveProducto (ver comentario en Unidad.cs).
    public async Task<UnidadDto> ActualizarAsync(int id, ActualizarUnidadDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var unidad = await _db.Unidades.Include(u => u.Pais).FirstOrDefaultAsync(u => u.Id == id && u.PaisId == paisId, ct)
            ?? throw new UnidadNoEncontradaException(id);

        var anterior = _auditoria.Capturar(Snapshot(unidad));

        unidad.Nombre = dto.Nombre.Trim();
        unidad.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Unidad), unidad.CodigoUnidad, "Actualizar", anterior, _auditoria.Capturar(Snapshot(unidad)), paisId, usuario, null, ct);

        return new UnidadDto(unidad.Id, unidad.CodigoUnidad, unidad.Nombre, unidad.Activo, unidad.PaisId, unidad.Pais!.Nombre);
    }
}
