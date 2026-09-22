namespace Inventory.Domain.Entities;

public class Auditoria
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    // Snapshot del nombre al momento de la operación (denormalización DELIBERADA,
    // ver CLAUDE.md): el FK de arriba dice QUIÉN fue y sigue siendo válido aunque
    // la persona cambie de nombre; esto dice con qué nombre se firmó entonces.
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    // País de la SESIÓN del actor al momento de la acción (mismo criterio que cada
    // servicio país-scoped: no una propiedad "natural" de la entidad auditada, sino
    // el país desde el que se hizo el cambio) — así el listado se puede filtrar igual
    // que todo lo demás en el sistema, sin que un admin de un país vea la auditoría de otro.
    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public string Entidad { get; set; } = string.Empty;
    public string EntidadId { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string? Motivo { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
