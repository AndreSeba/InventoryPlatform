using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly InventoryDbContext _db;
    private readonly JwtTokenService _tokenService;

    public AuthService(InventoryDbContext db, JwtTokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        // Un login = un país, y ahora también un Usuario de ESE país (Usuario pasó a ser
        // por país — el mismo email puede existir como cuentas distintas en países
        // distintos). El país se valida primero porque el lookup de Usuario ya depende de él.
        var pais = await _db.Paises.FirstOrDefaultAsync(p => p.Id == dto.PaisId && p.Activo, ct)
            ?? throw new PaisNoEncontradoException(dto.PaisId);

        var usuario = await _db.Usuarios
            .Include(u => u.Rol!).ThenInclude(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.PaisId == pais.Id, ct)
            ?? throw new CredencialesInvalidasException(); // mismo error genérico que
                                                             // contraseña incorrecta — no
                                                             // revela que el email existe
                                                             // en OTRO país.

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
            throw new CredencialesInvalidasException();

        if (!usuario.Activo)
            throw new UsuarioInactivoException();

        var permisos = usuario.Rol!.RolPermisos.Select(rp => rp.Permiso!.Codigo).ToList();
        var (token, expiraEn) = _tokenService.GenerarToken(usuario, permisos, pais.Id);

        usuario.UltimoLoginEn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var usuarioDto = new UsuarioDto(
            usuario.Id, usuario.Email, usuario.NombreCompleto, usuario.RolId, usuario.Rol.Nombre,
            usuario.Activo, usuario.CreadoEn, usuario.UltimoLoginEn, permisos,
            usuario.PaisId, pais.Nombre
        );

        return new LoginResultDto(token, expiraEn, usuarioDto, pais.Id, pais.Nombre, pais.CodigoIso);
    }
}
