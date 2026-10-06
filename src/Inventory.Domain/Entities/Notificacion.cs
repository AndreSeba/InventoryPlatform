using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

// Una notificación para UNA persona (campanita). Todas se guardan; NotificacionService las genera a partir del
// estado real del sistema y las identifica con `Clave`: la misma Clave para la misma persona es siempre la misma
// notificación (se actualiza en vez de duplicarse), y cuando la condición deja de cumplirse se marca Resuelta.
public class Notificacion
{
    public long Id { get; set; }

    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // Ej. «sol-pend:42», «inv-sin-stock», «pres-mora:17». Única por persona mientras no esté resuelta.
    public string Clave { get; set; } = string.Empty;

    public CategoriaNotificacion Categoria { get; set; }
    public SeveridadNotificacion Severidad { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    // Ruta del frontend a la que lleva la notificación (ej. «/solicitudes/42»).
    public string? Url { get; set; }

    // Para las notificaciones agregadas («N productos sin stock»): si el número SUBE, vuelve a quedar sin leer.
    public int? Valor { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaLeida { get; set; }

    // La condición que la originó ya no se cumple (solicitud resuelta, stock repuesto…): sale de la campanita.
    public bool Resuelta { get; set; }
    public DateTime? FechaResolucion { get; set; }
}
