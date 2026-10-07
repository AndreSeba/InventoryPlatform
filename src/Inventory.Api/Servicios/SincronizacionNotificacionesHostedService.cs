using Inventory.Application.Interfaces;
using Inventory.Infrastructure.Correo;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Servicios;

// Sin esto las notificaciones (y sus correos) solo se generan cuando alguien tiene el sistema abierto, porque es
// la consulta de la campanita la que dispara la sincronización. Con el correo habilitado, este servicio revisa las
// novedades de cada país cada pocos segundos, para que el aviso salga aunque nadie esté mirando la pantalla.
public class SincronizacionNotificacionesHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly CorreoOptions _opciones;
    private readonly ILogger<SincronizacionNotificacionesHostedService> _logger;

    public SincronizacionNotificacionesHostedService(IServiceScopeFactory scopes, IOptions<CorreoOptions> opciones,
        ILogger<SincronizacionNotificacionesHostedService> logger)
    {
        _scopes = scopes;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.Habilitado) return;

        var cada = TimeSpan.FromSeconds(Math.Max(15, _opciones.SincronizarCadaSegundos));
        // Primera pasada con calma: deja arrancar la API (y aplicar migraciones) antes de tocar la base.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        using var temporizador = new PeriodicTimer(cada);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var paises = await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Paises.AsNoTracking()
                    .Where(p => p.Activo).Select(p => p.Id).ToListAsync(stoppingToken);
                var servicio = scope.ServiceProvider.GetRequiredService<INotificacionService>();
                foreach (var paisId in paises)
                    await servicio.SincronizarAsync(paisId, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron revisar las novedades para enviar los avisos por correo.");
            }
        } while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }
}
