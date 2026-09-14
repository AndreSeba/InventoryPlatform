using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Security;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class RolService : IRolService
{
    private readonly InventoryDbContext _db;

    public RolService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<RolDto>> ListarAsync(CancellationToken ct)
    {
        var roles = await _db.Roles.AsNoTracking()
            .Include(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .Where(r => r.Activo)
            .OrderBy(r => r.Nombre)
            .ToListAsync(ct);

        return roles.Select(ARolDto).ToList();
    }

    public async Task<RolDto> CrearAsync(CrearRolDto dto, CancellationToken ct)
    {
        var nombre = dto.Nombre.Trim();

        var yaExiste = await _db.Roles.AnyAsync(r => r.Nombre == nombre && r.Activo, ct);
        if (yaExiste)
            throw new NombreRolDuplicadoException(nombre);

        var permisos = await ResolverPermisosAsync(dto.PermisoCodigos, ct);

        var rol = new Rol { Nombre = nombre, Descripcion = dto.Descripcion, Activo = true };
        rol.RolPermisos = permisos.Select(p => new RolPermiso { Permiso = p }).ToList();

        _db.Roles.Add(rol);
        await _db.SaveChangesAsync(ct);

        return ARolDto(rol);
    }

    public async Task<RolDto> ActualizarAsync(int id, ActualizarRolDto dto, CancellationToken ct)
    {
        var rol = await _db.Roles.Include(r => r.RolPermisos)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new RolNoEncontradoException(id);

        var permisos = await ResolverPermisosAsync(dto.PermisoCodigos, ct);

        rol.Nombre = dto.Nombre.Trim();
        rol.Descripcion = dto.Descripcion;
        rol.Activo = dto.Activo;

        _db.RolPermisos.RemoveRange(rol.RolPermisos);
        rol.RolPermisos = permisos.Select(p => new RolPermiso { RolId = rol.Id, PermisoId = p.Id }).ToList();

        await _db.SaveChangesAsync(ct);

        rol.RolPermisos = rol.RolPermisos.Select(rp => { rp.Permiso = permisos.First(p => p.Id == rp.PermisoId); return rp; }).ToList();
        return ARolDto(rol);
    }

    public IReadOnlyList<PermisoDto> ListarPermisosDisponibles() =>
        Permisos.Catalogo.Select(p => new PermisoDto(p.Codigo, p.Modulo, p.Descripcion)).ToList();

    private async Task<List<Permiso>> ResolverPermisosAsync(IReadOnlyList<string> codigos, CancellationToken ct)
    {
        var distintos = codigos.Distinct().ToList();
        var permisos = await _db.Permisos.Where(p => distintos.Contains(p.Codigo)).ToListAsync(ct);

        var faltante = distintos.FirstOrDefault(c => permisos.All(p => p.Codigo != c));
        if (faltante is not null)
            throw new PermisoInvalidoException(faltante);

        return permisos;
    }

    private static RolDto ARolDto(Rol r) => new(
        r.Id, r.Nombre, r.Descripcion, r.Activo,
        r.RolPermisos.Select(rp => rp.Permiso?.Codigo ?? string.Empty).Where(c => c.Length > 0).ToList()
    );
}
