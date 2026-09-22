using System.Text.Json;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly InventoryDbContext _db;

    public AuditoriaService(InventoryDbContext db) => _db = db;

    public string Capturar(object snapshot) => JsonSerializer.Serialize(snapshot);

    public async Task RegistrarAsync(
        string entidad, string entidadId, string accion,
        string? valorAnteriorJson, string? valorNuevoJson,
        int paisId, UsuarioActuante usuario, string? motivo, CancellationToken ct)
    {
        _db.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuario.Id,
            UsuarioNombre = usuario.Nombre,
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            ValorAnterior = valorAnteriorJson,
            ValorNuevo = valorNuevoJson,
            PaisId = paisId,
            Motivo = motivo,
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditoriaDto>> ListarAsync(FiltroAuditoriaDto filtro, int paisId, CancellationToken ct)
    {
        var query = _db.Auditorias.AsNoTracking().Where(a => a.PaisId == paisId);

        if (!string.IsNullOrWhiteSpace(filtro.Entidad))
            query = query.Where(a => a.Entidad == filtro.Entidad);
        if (!string.IsNullOrWhiteSpace(filtro.Accion))
            query = query.Where(a => a.Accion == filtro.Accion);
        if (filtro.UsuarioId is not null)
            query = query.Where(a => a.UsuarioId == filtro.UsuarioId);
        if (filtro.Desde is not null)
            query = query.Where(a => a.FechaHora >= filtro.Desde.Value);
        if (filtro.Hasta is not null)
            query = query.Where(a => a.FechaHora <= filtro.Hasta.Value);

        return await query.OrderByDescending(a => a.FechaHora)
            .Select(a => new AuditoriaDto(
                a.Id, a.FechaHora, a.UsuarioId, a.UsuarioNombre, a.Entidad, a.EntidadId,
                a.Accion, a.ValorAnterior, a.ValorNuevo, a.Motivo, a.CorrelationId))
            .ToListAsync(ct);
    }

    public async Task<CatalogoAuditoriaDto> ObtenerCatalogoAsync(int paisId, CancellationToken ct)
    {
        var entidades = await _db.Auditorias.Where(a => a.PaisId == paisId)
            .Select(a => a.Entidad).Distinct().OrderBy(e => e).ToListAsync(ct);
        var acciones = await _db.Auditorias.Where(a => a.PaisId == paisId)
            .Select(a => a.Accion).Distinct().OrderBy(a => a).ToListAsync(ct);

        return new CatalogoAuditoriaDto(entidades, acciones);
    }
}
