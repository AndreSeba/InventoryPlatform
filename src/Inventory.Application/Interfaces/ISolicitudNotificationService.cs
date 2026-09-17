using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

// Seam para el aviso por correo al encargado de categoría — el envío real (SMTP/API)
// todavía no tiene proveedor elegido (pendiente, ver CLAUDE.md). Mientras tanto,
// LoggingSolicitudNotificationService solo loguea. Conectar el proveedor real es
// reemplazar esa clase + la línea de DI en DependencyInjection.cs, nada más.
public interface ISolicitudNotificationService
{
    Task NotificarNuevaSolicitudAsync(SolicitudDto solicitud, IReadOnlyList<EncargadoNotificacionDto> grupos, CancellationToken ct);
}
