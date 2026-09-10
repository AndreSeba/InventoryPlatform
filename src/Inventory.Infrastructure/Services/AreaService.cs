using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class AreaService : IAreaService
{
    private readonly InventoryDbContext _db;

    public AreaService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<AreaDto>> ListarAsync(bool incluirInactivas, CancellationToken ct)
    {
        var query = _db.Areas.AsNoTracking();
        if (!incluirInactivas) query = query.Where(a => a.Activo);

        return await query.OrderBy(a => a.NombreArea)
            .Select(a => new AreaDto(a.Id, a.CodigoArea, a.NombreArea, a.Activo))
            .ToListAsync(ct);
    }

    public async Task<AreaDto> CrearAsync(CrearAreaDto dto, CancellationToken ct)
    {
        var codigo = dto.CodigoArea.Trim().ToUpperInvariant();

        var yaExiste = await _db.Areas.AnyAsync(a => a.CodigoArea == codigo && a.Activo, ct);
        if (yaExiste)
            throw new CodigoAreaDuplicadoException(codigo);

        var area = new Area { CodigoArea = codigo, NombreArea = dto.NombreArea, Activo = true };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync(ct);

        return new AreaDto(area.Id, area.CodigoArea, area.NombreArea, area.Activo);
    }
}
