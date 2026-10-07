namespace Inventory.Infrastructure.Controles;

// Sección «Notificaciones» de appsettings.
public class NotificacionesOptions
{
    public const string SectionName = "Notificaciones";

    // Una notificación leída (o abierta con un clic) sigue a la vista este tiempo y después sale de la campanita.
    // Las de categoría «Pendiente» (algo por hacer: aprobar, entregar, recibir) NO se ocultan por leerlas: siguen hasta
    // que se resuelven, porque si no una tarea sin hacer desaparecería de la vista. 0 = se ocultan al instante.
    public int HorasVisiblesTrasLeer { get; set; } = 24;
}
