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
    public string Entidad { get; set; } = string.Empty;
    public string EntidadId { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string? Motivo { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}
