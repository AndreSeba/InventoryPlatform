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

    public UsuarioService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct)
    {
        var usuarios = await _db.Usuarios.AsNoTracking()
            .Include(u => u.Rol!).ThenInclude(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync(ct);

        return usuarios.Select(AUsuarioDto).ToList();
    }

    public async Task<UsuarioDto> CrearAsync(CrearUsuarioDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var yaExiste = await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == email && u.Activo, ct);
        if (yaExiste)
            throw new EmailDuplicadoException(email);

        var rol = await _db.Roles.Include(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(r => r.Id == dto.RolId && r.Activo, ct)
            ?? throw new RolNoEncontradoException(dto.RolId);

        if (dto.Password.Length < 8)
            throw new ArgumentOutOfRangeException(nameof(dto.Password), "La contraseña debe tener al menos 8 caracteres.");

        var usuario = new Usuario
        {
            Email = email,
            NombreCompleto = dto.NombreCompleto,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
            RolId = rol.Id,
            Activo = true,
            CreadoEn = DateTime.UtcNow,
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(ct);

        usuario.Rol = rol;
        return AUsuarioDto(usuario);
    }

    public async Task<UsuarioDto> ActualizarAsync(int id, ActualizarUsuarioDto dto, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new UsuarioNoEncontradoException(id);

        var rol = await _db.Roles.Include(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(r => r.Id == dto.RolId && r.Activo, ct)
            ?? throw new RolNoEncontradoException(dto.RolId);

        usuario.NombreCompleto = dto.NombreCompleto;
        usuario.RolId = rol.Id;
        usuario.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        usuario.Rol = rol;
        return AUsuarioDto(usuario);
    }

    private static UsuarioDto AUsuarioDto(Usuario u) => new(
        u.Id, u.Email, u.NombreCompleto, u.RolId, u.Rol?.Nombre ?? string.Empty,
        u.Activo, u.CreadoEn, u.UltimoLoginEn,
        u.Rol?.RolPermisos.Select(rp => rp.Permiso!.Codigo).ToList() ?? []
    );
}
