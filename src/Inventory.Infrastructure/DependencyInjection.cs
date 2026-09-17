using Inventory.Application.Interfaces;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Security;
using Inventory.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InventoryDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'InventoryDb' en la configuración.");

        services.AddDbContext<InventoryDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IRolService, RolService>();

        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IUnidadService, UnidadService>();
        services.AddScoped<IAreaService, AreaService>();
        services.AddScoped<IUbicacionService, UbicacionService>();
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<IMovimientoService, MovimientoService>();
        services.AddScoped<ISolicitudService, SolicitudService>();
        services.AddScoped<IConteoService, ConteoService>();
        services.AddScoped<ISolicitudNotificationService, LoggingSolicitudNotificationService>();

        return services;
    }
}
