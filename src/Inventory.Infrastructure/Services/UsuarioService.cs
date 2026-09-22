using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class UsuarioService : IUsuarioService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public UsuarioService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // Nunca PasswordHash acá — auditoría no es lugar para guardar ni un hash.
    private static object Snapshot(Usuario u) => new { u.Email, u.NombreCompleto, u.RolId, u.Activo };

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(int paisId, CancellationToken ct)
    {
        var usuarios = await _db.Usuarios.AsNoTracking()
            .Include(u => u.Rol!).ThenInclude(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .Include(u => u.Pais)
            .Where(u => u.PaisId == paisId)
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync(ct);

        return usuarios.Select(AUsuarioDto).ToList();
    }

    public async Task<UsuarioDto> CrearAsync(CrearUsuarioDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        // Único POR PAÍS, no global — el mismo email puede existir en otro país.
        var yaExiste = await _db.Usuarios.AnyAsync(u => u.PaisId == paisId && u.Email.ToLower() == email && u.Activo, ct);
        if (yaExiste)
            throw new EmailDuplicadoException(email);

        var rol = await _db.Roles.Include(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(r => r.Id == dto.RolId && r.PaisId == paisId && r.Activo, ct)
            ?? throw new RolNoEncontradoException(dto.RolId);

        if (dto.Password.Length < 8)
            throw new ArgumentOutOfRangeException(nameof(dto.Password), "La contraseña debe tener al menos 8 caracteres.");

        var pais = await _db.Paises.FirstOrDefaultAsync(p => p.Id == paisId, ct);

        var nuevoUsuario = new Usuario
        {
            Email = email,
            NombreCompleto = dto.NombreCompleto,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
            RolId = rol.Id,
            PaisId = paisId,
            Activo = true,
            CreadoEn = DateTime.UtcNow,
        };

        _db.Usuarios.Add(nuevoUsuario);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Usuario), nuevoUsuario.Id.ToString(), "Crear", null, _auditoria.Capturar(Snapshot(nuevoUsuario)), paisId, usuario, null, ct);

        nuevoUsuario.Rol = rol;
        nuevoUsuario.Pais = pais;
        return AUsuarioDto(nuevoUsuario);
    }

    public async Task<UsuarioDto> ActualizarAsync(int id, ActualizarUsuarioDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var entidad = await _db.Usuarios.Include(u => u.Pais).FirstOrDefaultAsync(u => u.Id == id && u.PaisId == paisId, ct)
            ?? throw new UsuarioNoEncontradoException(id);

        var anterior = _auditoria.Capturar(Snapshot(entidad));

        var rol = await _db.Roles.Include(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(r => r.Id == dto.RolId && r.PaisId == paisId && r.Activo, ct)
            ?? throw new RolNoEncontradoException(dto.RolId);

        entidad.NombreCompleto = dto.NombreCompleto;
        entidad.RolId = rol.Id;
        entidad.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Usuario), entidad.Id.ToString(), "Actualizar", anterior, _auditoria.Capturar(Snapshot(entidad)), paisId, usuario, null, ct);

        entidad.Rol = rol;
        return AUsuarioDto(entidad);
    }

    private static UsuarioDto AUsuarioDto(Usuario u) => new(
        u.Id, u.Email, u.NombreCompleto, u.RolId, u.Rol?.Nombre ?? string.Empty,
        u.Activo, u.CreadoEn, u.UltimoLoginEn,
        u.Rol?.RolPermisos.Select(rp => rp.Permiso!.Codigo).ToList() ?? [],
        u.PaisId, u.Pais?.Nombre ?? string.Empty
    );
}
