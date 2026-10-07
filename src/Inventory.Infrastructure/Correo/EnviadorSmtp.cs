using System.Net;
using Inventory.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Inventory.Infrastructure.Correo;

// Arma y envía un correo por SMTP (MailKit). Una conexión por correo: el volumen es de unos pocos al minuto.
public class EnviadorSmtp
{
    private readonly CorreoOptions _o;
    private readonly ILogger<EnviadorSmtp> _logger;

    public EnviadorSmtp(IOptions<CorreoOptions> opciones, ILogger<EnviadorSmtp> logger)
    {
        _o = opciones.Value;
        _logger = logger;
    }

    public async Task EnviarAsync(MensajeCorreo m, CancellationToken ct)
    {
        var redirigido = !string.IsNullOrWhiteSpace(_o.DestinatarioDePrueba);
        var destino = redirigido ? _o.DestinatarioDePrueba.Trim() : m.ParaEmail;
        var remitente = string.IsNullOrWhiteSpace(_o.RemitenteEmail) ? _o.Usuario : _o.RemitenteEmail;

        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(_o.RemitenteNombre, remitente));
        mensaje.To.Add(MailboxAddress.Parse(destino));
        // En modo prueba el asunto dice a quién le habría llegado en producción.
        mensaje.Subject = redirigido ? $"[Para {m.ParaNombre}] {m.Asunto}" : m.Asunto;

        var enlace = string.IsNullOrWhiteSpace(m.Url) ? null : _o.UrlBaseWeb.TrimEnd('/') + m.Url;
        var cuerpo = new BodyBuilder
        {
            HtmlBody = PlantillaCorreo.Html(m.Titulo, m.Mensaje, enlace, m.ParaNombre, redirigido ? m.ParaEmail : null),
            TextBody = PlantillaCorreo.Texto(m.Titulo, m.Mensaje, enlace, m.ParaNombre),
        };
        mensaje.Body = cuerpo.ToMessageBody();

        using var cliente = new SmtpClient { Timeout = 30000 };
        var seguridad = _o.Seguridad.Trim().ToLowerInvariant() switch
        {
            "ssl" => SecureSocketOptions.SslOnConnect,
            "ninguna" => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls,
        };
        await cliente.ConnectAsync(_o.Host, _o.Puerto, seguridad, ct);
        await cliente.AuthenticateAsync(new NetworkCredential(_o.Usuario, _o.Clave), ct);
        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(true, ct);

        _logger.LogInformation("Correo enviado a {Destino} — {Asunto}", destino, mensaje.Subject);
    }
}
