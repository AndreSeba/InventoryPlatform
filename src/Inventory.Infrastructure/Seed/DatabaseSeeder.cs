using Inventory.Domain.Entities;
using Inventory.Domain.Security;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Seed;

// Crea un usuario Administrador inicial por cada país que todavía no tenga ningún
// usuario. No puede ir como HasData (necesita BCrypt en tiempo de ejecución) — se llama
// una vez al arrancar la API (ver Program.cs). Usuario/Rol pasaron a ser por país —
// Bolivia y Perú ya traen sus 4 roles sembrados vía migración (RolConfiguration), así
// que acá solo falta el usuario. Un país agregado en caliente desde /paises no pasa por
// acá — lo siembra PaisService.CrearAsync directo (no hace falta reiniciar la API).
// Credenciales de arranque documentadas en el CLAUDE.md del repo — cambiar la
// contraseña real apenas haya un despliegue con datos de verdad.
public static class DatabaseSeeder
{
    public const string EmailAdminInicial = "admin@inventario.local";
    public const string PasswordAdminInicial = "Cambiar123!";

    public static async Task SeedAdminInicialAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        var paises = await db.Paises.Where(p => p.Activo).ToListAsync();
        foreach (var pais in paises)
        {
            var hayUsuarios = await db.Usuarios.AnyAsync(u => u.PaisId == pais.Id);
            if (hayUsuarios)
                continue;

            var rolAdmin = await db.Roles.FirstOrDefaultAsync(r => r.PaisId == pais.Id && r.Nombre == RolesPorDefecto.Administrador && r.Activo);
            if (rolAdmin is null)
            {
                logger.LogWarning("País {Pais} no tiene rol Administrador sembrado — no se pudo crear su usuario inicial.", pais.Nombre);
                continue;
            }

            db.Usuarios.Add(new Usuario
            {
                Email = EmailAdminInicial,
                NombreCompleto = "Administrador",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordAdminInicial, workFactor: 12),
                RolId = rolAdmin.Id,
                PaisId = pais.Id,
                Activo = true,
                CreadoEn = DateTime.UtcNow,
            });

            logger.LogWarning(
                "Usuario admin inicial creado para {Pais}: {Email} / contraseña por defecto — cambiarla antes de usar en producción.",
                pais.Nombre, EmailAdminInicial);
        }

        await db.SaveChangesAsync();
    }
}
