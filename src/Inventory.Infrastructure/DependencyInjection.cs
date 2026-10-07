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

        services.AddMemoryCache();
        services.Configure<Controles.ControlesOptions>(configuration.GetSection(Controles.ControlesOptions.SectionName));
        services.Configure<Controles.FuncionesOptions>(configuration.GetSection(Controles.FuncionesOptions.SectionName));
        services.Configure<Controles.NotificacionesOptions>(configuration.GetSection(Controles.NotificacionesOptions.SectionName));

        // Correo saliente (apagado por defecto): la cola se llena desde NotificacionService y la vacía un servicio
        // en segundo plano de la API (EnvioCorreoHostedService).
        services.Configure<Correo.CorreoOptions>(configuration.GetSection(Correo.CorreoOptions.SectionName));
        services.AddSingleton<Correo.ColaCorreo>();
        services.AddSingleton<ICorreoSaliente>(sp => sp.GetRequiredService<Correo.ColaCorreo>());
        services.AddSingleton<Correo.EnviadorSmtp>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IRolService, RolService>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();

        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IUnidadService, UnidadService>();
        services.AddScoped<IPaisService, PaisService>();
        services.AddScoped<IAlmacenService, AlmacenService>();
        services.AddScoped<IAreaService, AreaService>();
        services.AddScoped<IUbicacionService, UbicacionService>();
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<IMovimientoService, MovimientoService>();
        services.AddScoped<IResumenService, ResumenService>();
        services.AddScoped<ISolicitudService, SolicitudService>();
        services.AddScoped<IConteoService, ConteoService>();
        services.AddScoped<IRevisionAccesoService, RevisionAccesoService>();
        services.AddScoped<IDevolucionService, DevolucionService>();
        services.AddScoped<IFirmaService, FirmaService>();
        services.AddScoped<INotificacionService, NotificacionService>();
        services.AddScoped<ISolicitudNotificationService, LoggingSolicitudNotificationService>();

        return services;
    }
}
