namespace Inventory.Application.Interfaces;

// Un correo de aviso para UNA persona. `Url` es la ruta del sistema a la que lleva el botón del correo.
public record MensajeCorreo(string ParaEmail, string ParaNombre, string Asunto, string Titulo, string Mensaje, string? Url);

// Salida de correos del sistema. Encolar nunca bloquea ni lanza: el envío real (SMTP) lo hace un servicio en
// segundo plano, y si el correo está apagado o falla, el sistema sigue funcionando igual.
public interface ICorreoSaliente
{
    void Encolar(MensajeCorreo mensaje);
}
