using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record NotificacionDto(
    long Id, CategoriaNotificacion Categoria, SeveridadNotificacion Severidad,
    string Titulo, string Mensaje, string? Url, DateTime FechaCreacion, bool Leida
);

// NoLeidas cuenta todas las no leídas vigentes (puede ser más que Items.Count, que se corta en 50).
public record NotificacionesDto(int NoLeidas, IReadOnlyList<NotificacionDto> Items);
