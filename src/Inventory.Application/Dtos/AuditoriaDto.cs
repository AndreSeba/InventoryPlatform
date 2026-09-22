namespace Inventory.Application.Dtos;

public record AuditoriaDto(
    int Id,
    DateTime FechaHora,
    int UsuarioId,
    string UsuarioNombre,
    string Entidad,
    string EntidadId,
    string Accion,
    string? ValorAnterior,
    string? ValorNuevo,
    string? Motivo,
    Guid CorrelationId
);

// Filtro por query string (GET /api/auditoria) — sin paginado server-side, mismo criterio
// que Movimientos/Solicitudes: la API devuelve la lista filtrada completa y el frontend
// pagina con <Pager/>.
public record FiltroAuditoriaDto(
    string? Entidad,
    string? Accion,
    int? UsuarioId,
    DateTime? Desde,
    DateTime? Hasta
);

// Catálogo de entidades/acciones auditadas — alimenta los <select> de filtro del frontend
// sin necesidad de otro endpoint (son valores fijos, conocidos de antemano).
public record CatalogoAuditoriaDto(IReadOnlyList<string> Entidades, IReadOnlyList<string> Acciones);
