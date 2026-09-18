using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class PaisService : IPaisService
{
    private readonly InventoryDbContext _db;

    public PaisService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<PaisDto>> ListarAsync(bool incluirInactivos, CancellationToken ct)
    {
        var query = _db.Paises.AsNoTracking();
        if (!incluirInactivos) query = query.Where(p => p.Activo);

        return await query.OrderBy(p => p.Nombre)
            .Select(p => new PaisDto(p.Id, p.Nombre, p.CodigoIso, p.Activo))
            .ToListAsync(ct);
    }

    public async Task<PaisDto> CrearAsync(CrearPaisDto dto, CancellationToken ct)
    {
        var codigoIso = dto.CodigoIso.Trim().ToUpperInvariant();

        var yaExiste = await _db.Paises.AnyAsync(p => p.CodigoIso == codigoIso && p.Activo, ct);
        if (yaExiste)
            throw new CodigoPaisDuplicadoException(codigoIso);

        var pais = new Pais { Nombre = dto.Nombre, CodigoIso = codigoIso, Activo = true };
        _db.Paises.Add(pais);
        await _db.SaveChangesAsync(ct);

        return new PaisDto(pais.Id, pais.Nombre, pais.CodigoIso, pais.Activo);
    }

    public async Task<PaisDto> ActualizarAsync(int id, ActualizarPaisDto dto, CancellationToken ct)
    {
        var pais = await _db.Paises.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new PaisNoEncontradoException(id);

        pais.Nombre = dto.Nombre;
        pais.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);
        return new PaisDto(pais.Id, pais.Nombre, pais.CodigoIso, pais.Activo);
    }
}
