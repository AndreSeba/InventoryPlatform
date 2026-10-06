using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface INotificacionService
{
    // Las notificaciones vigentes de la persona. Antes de listar, sincroniza las del país con el estado real del
    // sistema (con un freno de tiempo: no se recalcula en cada consulta).
    Task<NotificacionesDto> ListarAsync(int usuarioId, int paisId, CancellationToken ct);

    Task MarcarLeidaAsync(long id, int usuarioId, CancellationToken ct);
    Task<int> MarcarTodasLeidasAsync(int usuarioId, int paisId, CancellationToken ct);

    // Recalcula las notificaciones del país ya mismo (sin esperar el freno). Lo usan las pruebas.
    Task SincronizarAsync(int paisId, CancellationToken ct);
}
