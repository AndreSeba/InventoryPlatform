using Inventory.Application.Interfaces;
using Inventory.Infrastructure.Persistence;
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

        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IAreaService, AreaService>();
        services.AddScoped<IUbicacionService, UbicacionService>();
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<IMovimientoService, MovimientoService>();
        services.AddScoped<ISolicitudService, SolicitudService>();
        services.AddScoped<IConteoService, ConteoService>();

        return services;
    }
}
