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
    private readonly IAuditoriaService _auditoria;

    public AreaService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static object Snapshot(Area a) => new { a.CodigoArea, a.NombreArea, a.Activo };

    public async Task<IReadOnlyList<AreaDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct)
    {
        var query = _db.Areas.AsNoTracking().Include(a => a.Pais).Where(a => a.PaisId == paisId);
        if (!incluirInactivas) query = query.Where(a => a.Activo);

        return await query.OrderBy(a => a.NombreArea).Select(a => AAreaDto(a)).ToListAsync(ct);
    }

    public async Task<AreaDto> CrearAsync(CrearAreaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = dto.CodigoArea.Trim().ToUpperInvariant();

        // Único POR PAÍS, no global — dos países pueden repetir un código de área.
        var yaExiste = await _db.Areas.AnyAsync(a => a.PaisId == paisId && a.CodigoArea == codigo && a.Activo, ct);
        if (yaExiste)
            throw new CodigoAreaDuplicadoException(codigo);

        var area = new Area { CodigoArea = codigo, NombreArea = dto.NombreArea, PaisId = paisId, Activo = true };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Area), area.CodigoArea, "Crear", null, _auditoria.Capturar(Snapshot(area)), paisId, usuario, null, ct);

        await _db.Entry(area).Reference(a => a.Pais).LoadAsync(ct);
        return AAreaDto(area);
    }

    public async Task<AreaDto> ActualizarAsync(int id, ActualizarAreaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var area = await _db.Areas.Include(a => a.Pais).FirstOrDefaultAsync(a => a.Id == id && a.PaisId == paisId, ct)
            ?? throw new AreaNoEncontradaException(id);

        var anterior = _auditoria.Capturar(Snapshot(area));

        var codigo = dto.CodigoArea.Trim().ToUpperInvariant();

        var yaExiste = await _db.Areas.AnyAsync(a => a.PaisId == paisId && a.CodigoArea == codigo && a.Activo && a.Id != id, ct);
        if (yaExiste)
            throw new CodigoAreaDuplicadoException(codigo);

        area.CodigoArea = codigo;
        area.NombreArea = dto.NombreArea;
        area.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Area), area.CodigoArea, "Actualizar", anterior, _auditoria.Capturar(Snapshot(area)), paisId, usuario, null, ct);

        return AAreaDto(area);
    }

    private static AreaDto AAreaDto(Area a) => new(a.Id, a.CodigoArea, a.NombreArea, a.Activo, a.PaisId, a.Pais?.Nombre ?? string.Empty);
}
