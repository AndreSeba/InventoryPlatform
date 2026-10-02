using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Inventory.Infrastructure.Security;

// Lo que la API necesita saber de un usuario en cada pedido: si sigue activo y qué permisos
// tiene HOY. El JWT lleva una copia de los permisos de cuando se logueó; sin comparar contra
// esto, un usuario desactivado seguía operando hasta 8 horas y un cambio de permisos no
// aplicaba hasta volver a loguearse.
public record EstadoSesionUsuario(bool Activo, string Permisos);

public static class SesionUsuarioCache
{
    // Corta a propósito: acota la consulta extra por pedido sin dejar la baja de un usuario
    // sin efecto por mucho tiempo. Las ediciones de usuario/rol invalidan la entrada al instante.
    private static readonly TimeSpan Vida = TimeSpan.FromSeconds(30);

    private static string Clave(int usuarioId) => $"sesion-usuario:{usuarioId}";

    public static void Invalidar(IMemoryCache cache, int usuarioId) => cache.Remove(Clave(usuarioId));

    public static string Normalizar(IEnumerable<string> permisos) =>
        string.Join('|', permisos.OrderBy(p => p, StringComparer.Ordinal));

    public static async Task<EstadoSesionUsuario?> ObtenerAsync(IMemoryCache cache, InventoryDbContext db, int usuarioId, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(Clave(usuarioId), async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = Vida;
            var datos = await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => new
                {
                    Activo = u.Activo && u.Rol!.Activo,
                    Permisos = u.Rol!.RolPermisos.Select(rp => rp.Permiso!.Codigo).ToList(),
                })
                .FirstOrDefaultAsync(ct);
            return datos is null ? null : new EstadoSesionUsuario(datos.Activo, Normalizar(datos.Permisos));
        });
    }
}
