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

    public UnidadService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<UnidadDto>> ListarAsync(bool incluirInactivas, CancellationToken ct)
    {
        var query = _db.Unidades.AsNoTracking();
        if (!incluirInactivas) query = query.Where(u => u.Activo);

        return await query.OrderBy(u => u.CodigoUnidad)
            .Select(u => new UnidadDto(u.Id, u.CodigoUnidad, u.Nombre, u.Activo))
            .ToListAsync(ct);
    }

    public async Task<UnidadDto> CrearAsync(CrearUnidadDto dto, CancellationToken ct)
    {
        var codigo = dto.CodigoUnidad.Trim().ToUpperInvariant();

        var yaExiste = await _db.Unidades.AnyAsync(u => u.CodigoUnidad == codigo && u.Activo, ct);
        if (yaExiste)
            throw new CodigoUnidadDuplicadoException(codigo);

        var unidad = new Unidad { CodigoUnidad = codigo, Nombre = dto.Nombre.Trim(), Activo = true };
        _db.Unidades.Add(unidad);
        await _db.SaveChangesAsync(ct);

        return new UnidadDto(unidad.Id, unidad.CodigoUnidad, unidad.Nombre, unidad.Activo);
    }

    // Solo Nombre y Activo: CodigoUnidad es inmutable porque viaja dentro de
    // Producto.ClaveProducto (ver comentario en Unidad.cs).
    public async Task<UnidadDto> ActualizarAsync(int id, ActualizarUnidadDto dto, CancellationToken ct)
    {
        var unidad = await _db.Unidades.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new UnidadNoEncontradaException(id);

        unidad.Nombre = dto.Nombre.Trim();
        unidad.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        return new UnidadDto(unidad.Id, unidad.CodigoUnidad, unidad.Nombre, unidad.Activo);
    }
}
