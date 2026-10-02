using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Inventory.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly InventoryDbContext _db;
    private readonly JwtTokenService _tokenService;
    private readonly IMemoryCache _cache;

    // Tras MaxIntentos fallos en la ventana, el login de ese email queda frenado hasta que
    // la ventana vence. Se cuenta también para emails que no existen, así no se puede usar
    // la respuesta para descubrir qué cuentas hay.
    private const int MaxIntentos = 5;
    private static readonly TimeSpan Ventana = TimeSpan.FromMinutes(10);

    private sealed record FalloLogin(int Cantidad, DateTime Vence);

    public AuthService(InventoryDbContext db, JwtTokenService tokenService, IMemoryCache cache)
    {
        _db = db;
        _tokenService = tokenService;
        _cache = cache;
    }

    private static string ClaveFallos(int paisId, string email) => $"login-fallos:{paisId}:{email}";

    private void RegistrarFallo(string clave)
    {
        var actual = _cache.TryGetValue(clave, out FalloLogin? f) && f is not null ? f : null;
        var vence = actual?.Vence ?? DateTime.UtcNow.Add(Ventana);
        _cache.Set(clave, new FalloLogin((actual?.Cantidad ?? 0) + 1, vence), new DateTimeOffset(vence, TimeSpan.Zero));
    }

    public async Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        // Un login = un país, y ahora también un Usuario de ESE país (Usuario pasó a ser
        // por país — el mismo email puede existir como cuentas distintas en países
        // distintos). El país se valida primero porque el lookup de Usuario ya depende de él.
        var pais = await _db.Paises.FirstOrDefaultAsync(p => p.Id == dto.PaisId && p.Activo, ct)
            ?? throw new PaisNoEncontradoException(dto.PaisId);

        var claveFallos = ClaveFallos(pais.Id, email);
        if (_cache.TryGetValue(claveFallos, out FalloLogin? previo) && previo is { Cantidad: >= MaxIntentos } && previo.Vence > DateTime.UtcNow)
            throw new LoginBloqueadoException((int)Math.Ceiling((previo.Vence - DateTime.UtcNow).TotalMinutes));

        var usuario = await _db.Usuarios
            .Include(u => u.Rol!).ThenInclude(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.PaisId == pais.Id, ct);

        // Mismo error genérico para "no existe" y "contraseña incorrecta" — no revela que el
        // email existe, ni siquiera en OTRO país.
        if (usuario is null || !BCrypt.Net.BCrypt.Verify(dto.Password ?? string.Empty, usuario.PasswordHash))
        {
            RegistrarFallo(claveFallos);
            throw new CredencialesInvalidasException();
        }

        _cache.Remove(claveFallos);

        if (!usuario.Activo)
            throw new UsuarioInactivoException();

        var permisos = usuario.Rol!.RolPermisos.Select(rp => rp.Permiso!.Codigo).ToList();
        var (token, expiraEn) = _tokenService.GenerarToken(usuario, permisos, pais.Id, pais.Nombre, pais.CodigoIso);

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
