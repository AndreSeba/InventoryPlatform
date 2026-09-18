using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Inventory.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Infrastructure.Security;

public class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiraEn) GenerarToken(Usuario usuario, IEnumerable<string> permisos, int paisId, string paisNombre, string paisCodigoIso)
    {
        var expiraEn = DateTime.UtcNow.AddMinutes(_options.ExpiracionMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(ClaimTypes.Name, usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol!.Nombre),
            // País elegido en el login (ver AuthService/Home.razor) — un login = un país,
            // no hay forma de cambiarlo sin volver a loguearse (ver ObtenerPaisId).
            new("pais", paisId.ToString()),
            // Nombre/código van embebidos acá (no solo el Id) para que el frontend pueda
            // reconstruir la sesión leyendo únicamente la cookie con el JWT tras un F5,
            // sin una llamada extra a /api/paises — ver AuthState.RestaurarDesdeToken.
            new("pais_nombre", paisNombre),
            new("pais_codigo", paisCodigoIso),
        };
        claims.AddRange(permisos.Select(p => new Claim("permiso", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credenciales = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiraEn,
            signingCredentials: credenciales
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEn);
    }
}
