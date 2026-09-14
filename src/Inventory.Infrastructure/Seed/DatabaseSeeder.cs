using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Seed;

// Crea el usuario Administrador inicial si la tabla Usuario está vacía. No puede ir
// como HasData (necesita BCrypt en tiempo de ejecución) — se llama una vez al arrancar
// la API (ver Program.cs). Credenciales de arranque documentadas en el CLAUDE.md del
// repo — cambiar la contraseña real apenas haya un despliegue con datos de verdad.
public static class DatabaseSeeder
{
    public const string EmailAdminInicial = "admin@inventario.local";
    public const string PasswordAdminInicial = "Cambiar123!";

    public static async Task SeedAdminInicialAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        var hayUsuarios = await db.Usuarios.AnyAsync();
        if (hayUsuarios)
            return;

        db.Usuarios.Add(new Usuario
        {
            Email = EmailAdminInicial,
            NombreCompleto = "Administrador",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(PasswordAdminInicial, workFactor: 12),
            RolId = RolPermisoConfiguration.RolAdministradorId,
            Activo = true,
            CreadoEn = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        logger.LogWarning(
            "Usuario admin inicial creado: {Email} / contraseña por defecto — cambiarla antes de usar en producción.",
            EmailAdminInicial);
    }
}
