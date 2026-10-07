using Inventory.Infrastructure.Correo;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Servicios;

// Saca los correos de la cola y los envía por SMTP, de a uno. Si el correo está apagado no hace nada. Un correo que
// falla se registra en el log y se descarta (con un reintento): nunca debe tirar abajo la API ni trabar la cola.
public class EnvioCorreoHostedService : BackgroundService
{
    private readonly ColaCorreo _cola;
    private readonly EnviadorSmtp _enviador;
    private readonly CorreoOptions _opciones;
    private readonly ILogger<EnvioCorreoHostedService> _logger;

    public EnvioCorreoHostedService(ColaCorreo cola, EnviadorSmtp enviador, IOptions<CorreoOptions> opciones, ILogger<EnvioCorreoHostedService> logger)
    {
        _cola = cola;
        _enviador = enviador;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.Habilitado)
        {
            _logger.LogInformation("Correo apagado (Correo:Habilitado = false): los avisos solo salen por la campanita.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_opciones.Usuario) || string.IsNullOrWhiteSpace(_opciones.Clave))
        {
            _logger.LogWarning("Correo habilitado pero falta Correo:Usuario o Correo:Clave: no se enviará nada hasta configurarlos.");
            return;
        }

        _logger.LogInformation("Correo habilitado: {Host}:{Puerto} como {Usuario}{Prueba}.", _opciones.Host, _opciones.Puerto, _opciones.Usuario,
            string.IsNullOrWhiteSpace(_opciones.DestinatarioDePrueba) ? "" : $", TODO se redirige a {_opciones.DestinatarioDePrueba}");

        var redirigido = !string.IsNullOrWhiteSpace(_opciones.DestinatarioDePrueba);

        await foreach (var mensaje in _cola.Lector.ReadAllAsync(stoppingToken))
        {
            // Los usuarios de demostración con email ficticio (@inventario.local, dominio inexistente) no tienen buzón:
            // se saltean en vez de fallar en cada envío. Con DestinatarioDePrueba todo se redirige igual.
            if (!redirigido && mensaje.ParaEmail.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Correo no enviado a {Para}: email ficticio de demostración.", mensaje.ParaEmail);
                continue;
            }

            for (var intento = 1; intento <= 2; intento++)
            {
                try
                {
                    await _enviador.EnviarAsync(mensaje, stoppingToken);
                    break;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    if (intento == 2)
                        _logger.LogError(ex, "No se pudo enviar el correo «{Asunto}» a {Para}.", mensaje.Asunto, mensaje.ParaEmail);
                    else
                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
            }
        }
    }
}
