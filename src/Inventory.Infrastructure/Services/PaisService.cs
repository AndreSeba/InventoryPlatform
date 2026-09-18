using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Security;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Seed;
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
        await _db.SaveChangesAsync(ct); // necesita pais.Id antes de sembrar roles/usuario

        await SembrarRolesYAdminInicialAsync(pais, ct);

        return new PaisDto(pais.Id, pais.Nombre, pais.CodigoIso, pais.Activo);
    }

    // Sin esto un país recién creado queda inusable: sin roles no se puede crear ningún
    // usuario, y sin usuario nadie puede loguearse para gestionarlo (ver plan
    // "Usuarios y Roles por país" — el mismo bootstrap que corre DatabaseSeeder en frío,
    // acá en caliente para que el país quede utilizable sin reiniciar la API).
    private async Task SembrarRolesYAdminInicialAsync(Pais pais, CancellationToken ct)
    {
        var rolAdmin = new Rol { Nombre = RolesPorDefecto.Administrador, PaisId = pais.Id, Activo = true };
        var rolOperador = new Rol { Nombre = RolesPorDefecto.Operador, PaisId = pais.Id, Activo = true };
        var rolConsulta = new Rol { Nombre = RolesPorDefecto.Consulta, PaisId = pais.Id, Activo = true };
        var rolSolicitante = new Rol { Nombre = RolesPorDefecto.Solicitante, PaisId = pais.Id, Activo = true };
        _db.Roles.AddRange(rolAdmin, rolOperador, rolConsulta, rolSolicitante);
        await _db.SaveChangesAsync(ct); // necesita los Id de cada Rol antes de mapear RolPermiso

        var permisoIdPorCodigo = await _db.Permisos.ToDictionaryAsync(p => p.Codigo, p => p.Id, ct);
        _db.RolPermisos.AddRange(
            Permisos.Catalogo.Select(p => new RolPermiso { RolId = rolAdmin.Id, PermisoId = permisoIdPorCodigo[p.Codigo] })
                .Concat(RolesPorDefecto.PermisosOperador.Select(c => new RolPermiso { RolId = rolOperador.Id, PermisoId = permisoIdPorCodigo[c] }))
                .Concat(RolesPorDefecto.PermisosConsulta.Select(c => new RolPermiso { RolId = rolConsulta.Id, PermisoId = permisoIdPorCodigo[c] }))
                .Concat(RolesPorDefecto.PermisosSolicitante.Select(c => new RolPermiso { RolId = rolSolicitante.Id, PermisoId = permisoIdPorCodigo[c] }))
        );

        _db.Usuarios.Add(new Usuario
        {
            Email = DatabaseSeeder.EmailAdminInicial,
            NombreCompleto = "Administrador",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DatabaseSeeder.PasswordAdminInicial, workFactor: 12),
            RolId = rolAdmin.Id,
            PaisId = pais.Id,
            Activo = true,
            CreadoEn = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
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
