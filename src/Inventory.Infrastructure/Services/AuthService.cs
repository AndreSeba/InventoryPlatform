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

        var usuario = await _db.Usuarios
            .Include(u => u.Rol!).ThenInclude(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct)
            ?? throw new CredencialesInvalidasException();

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
            throw new CredencialesInvalidasException();

        if (!usuario.Activo)
            throw new UsuarioInactivoException();

        // Un login = un país (ver JwtTokenService) — cualquier usuario puede elegir
        // cualquier país activo del selector, no es un atributo fijo del Usuario.
        var pais = await _db.Paises.FirstOrDefaultAsync(p => p.Id == dto.PaisId && p.Activo, ct)
            ?? throw new PaisNoEncontradoException(dto.PaisId);

        var permisos = usuario.Rol!.RolPermisos.Select(rp => rp.Permiso!.Codigo).ToList();
        var (token, expiraEn) = _tokenService.GenerarToken(usuario, permisos, pais.Id);

        usuario.UltimoLoginEn = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var usuarioDto = new UsuarioDto(
            usuario.Id, usuario.Email, usuario.NombreCompleto, usuario.RolId, usuario.Rol.Nombre,
            usuario.Activo, usuario.CreadoEn, usuario.UltimoLoginEn, permisos
        );

        return new LoginResultDto(token, expiraEn, usuarioDto, pais.Id, pais.Nombre, pais.CodigoIso);
    }
}
