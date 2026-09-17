using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Services;

// TODO: reemplazar por un envío real (SMTP/API) cuando se elija proveedor — cambiar esta
// clase (y la línea de DI en DependencyInjection.cs) por una que realmente mande el correo.
// Por ahora arma el asunto/cuerpo tal cual iría en el correo y lo deja en el log, para que
// el flujo completo (quién se entera, con qué contenido) sea verificable de punta a punta
// sin depender de tener un proveedor configurado.
public class LoggingSolicitudNotificationService : ISolicitudNotificationService
{
    private readonly ILogger<LoggingSolicitudNotificationService> _logger;

    public LoggingSolicitudNotificationService(ILogger<LoggingSolicitudNotificationService> logger) => _logger = logger;

    public Task NotificarNuevaSolicitudAsync(SolicitudDto solicitud, IReadOnlyList<EncargadoNotificacionDto> grupos, CancellationToken ct)
    {
        foreach (var grupo in grupos)
        {
            var asunto = solicitud.Tipo == TipoSolicitud.Entrada
                ? $"Aviso: va a llegar material — Solicitud {solicitud.NumeroSolicitud}"
                : $"Revisar solicitud de salida — {solicitud.NumeroSolicitud}";

            var cuerpo = solicitud.Tipo == TipoSolicitud.Entrada
                ? $"{solicitud.SolicitadoPor} avisó que va a llegar material del área {solicitud.AreaNombre}. Líneas: "
                    + string.Join(", ", grupo.Lineas.Select(l => $"{l.ProductoNombre} x{l.CantidadSolicitada:N0} {l.UnidadMedida}"))
                : $"{solicitud.SolicitadoPor} pidió material del área {solicitud.AreaNombre}. Revisá el stock y aprobá o rechazá en el sistema. Líneas: "
                    + string.Join(", ", grupo.Lineas.Select(l => $"{l.ProductoNombre} x{l.CantidadSolicitada:N0} {l.UnidadMedida}"));

            _logger.LogInformation(
                "Aviso de solicitud (sin enviar, sin proveedor de correo configurado) — Para: {EncargadoNombre} <{EncargadoEmail}> — Asunto: {Asunto} — Cuerpo: {Cuerpo}",
                grupo.EncargadoNombre, grupo.EncargadoEmail, asunto, cuerpo);
        }

        return Task.CompletedTask;
    }
}
