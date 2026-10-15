using Inventory.Application;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class ConteoExternoUnidadService : IConteoExternoUnidadService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ConteoExternoUnidadService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static object Snapshot(ConteoExternoUnidad u) => new { u.Codigo, u.DecimalesPermitidos, u.Activo };

    public async Task<IReadOnlyList<ConteoExternoUnidadDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct)
    {
        var query = _db.ConteoExternoUnidades.AsNoTracking().Include(u => u.Pais).Where(u => u.PaisId == paisId);
        if (!incluirInactivas) query = query.Where(u => u.Activo);

        return await query.OrderBy(u => u.Codigo)
            .Select(u => new ConteoExternoUnidadDto(u.Id, u.Codigo, u.DecimalesPermitidos, u.Activo, u.PaisId, u.Pais!.Nombre))
            .ToListAsync(ct);
    }

    public async Task<ConteoExternoUnidadDto> CrearAsync(CrearConteoExternoUnidadDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = Validacion.Texto(dto.Codigo, 10, "El código de la unidad").ToUpperInvariant();
        if (dto.DecimalesPermitidos < 0 || dto.DecimalesPermitidos > 4)
            throw new ValidacionException("Los decimales permitidos deben estar entre 0 y 4.");

        var yaExiste = await _db.ConteoExternoUnidades.AnyAsync(u => u.PaisId == paisId && u.Codigo == codigo && u.Activo, ct);
        if (yaExiste)
            throw new CodigoConteoExternoUnidadDuplicadoException(codigo);

        var unidad = new ConteoExternoUnidad { Codigo = codigo, DecimalesPermitidos = dto.DecimalesPermitidos, PaisId = paisId, Activo = true };
        _db.ConteoExternoUnidades.Add(unidad);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoUnidad), unidad.Codigo, "Crear", null, _auditoria.Capturar(Snapshot(unidad)), paisId, usuario, null, ct);

        await _db.Entry(unidad).Reference(u => u.Pais).LoadAsync(ct);
        return new ConteoExternoUnidadDto(unidad.Id, unidad.Codigo, unidad.DecimalesPermitidos, unidad.Activo, unidad.PaisId, unidad.Pais!.Nombre);
    }

    // Solo DecimalesPermitidos y Activo: el Código es inmutable — una vez que Existencias
    // (Fase 2) empiece a asignarle esta unidad a productos, renombrarla los dejaría
    // apuntando a un código que no existe.
    public async Task<ConteoExternoUnidadDto> ActualizarAsync(int id, ActualizarConteoExternoUnidadDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var unidad = await _db.ConteoExternoUnidades.Include(u => u.Pais)
            .FirstOrDefaultAsync(u => u.Id == id && u.PaisId == paisId, ct)
            ?? throw new ConteoExternoUnidadNoEncontradaException(id);

        var anterior = _auditoria.Capturar(Snapshot(unidad));

        if (dto.DecimalesPermitidos < 0 || dto.DecimalesPermitidos > 4)
            throw new ValidacionException("Los decimales permitidos deben estar entre 0 y 4.");

        unidad.DecimalesPermitidos = dto.DecimalesPermitidos;
        unidad.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoUnidad), unidad.Codigo, "Actualizar", anterior, _auditoria.Capturar(Snapshot(unidad)), paisId, usuario, null, ct);

        return new ConteoExternoUnidadDto(unidad.Id, unidad.Codigo, unidad.DecimalesPermitidos, unidad.Activo, unidad.PaisId, unidad.Pais!.Nombre);
    }
}
