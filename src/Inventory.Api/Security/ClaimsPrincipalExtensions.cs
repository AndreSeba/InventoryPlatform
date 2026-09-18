using System.Security.Claims;
using Inventory.Application.Dtos;

namespace Inventory.Api.Security;

public static class ClaimsPrincipalExtensions
{
    // ⚠️ El JWT se emite con el claim `sub` = usuario.Id (ver JwtTokenService), pero
    // Program.cs NO configura MapInboundClaims = false en AddJwtBearer, así que ASP.NET
    // aplica el mapeo por defecto y `sub` llega renombrado a ClaimTypes.NameIdentifier.
    // Buscar "sub" acá devuelve null.
    //
    // NO "arreglarlo" poniendo MapInboundClaims = false: eso también desactiva el mapeo
    // de ClaimTypes.Name y ClaimTypes.Role, y rompe en silencio User.Identity.Name.
    public static UsuarioActuante ObtenerUsuarioActuante(this ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        // Todos los controllers son [Authorize], así que el claim siempre está. Si no
        // está, es un bug de configuración y conviene que falle fuerte: el fallback
        // anterior escribía el usuario inventado "sistema" en el historial.
        if (!int.TryParse(idClaim, out var id))
            throw new InvalidOperationException(
                "El token no trae el id del usuario (claim 'sub' → ClaimTypes.NameIdentifier).");

        return new UsuarioActuante(id, principal.Identity?.Name ?? string.Empty);
    }

    // País elegido al loguearse (ver JwtTokenService/AuthService) — un login = un país,
    // así que todos los servicios país-específicos leen esto en vez de recibir un
    // ?paisId= a mano en cada llamada (fácil de olvidar, y no confiable como única
    // defensa). Mismo criterio que ObtenerUsuarioActuante: lanza si falta o no parsea,
    // sin caer a ningún país por default inventado.
    public static int ObtenerPaisId(this ClaimsPrincipal principal)
    {
        var paisClaim = principal.FindFirstValue("pais");

        if (!int.TryParse(paisClaim, out var paisId))
            throw new InvalidOperationException("El token no trae el país de la sesión (claim 'pais').");

        return paisId;
    }
}
