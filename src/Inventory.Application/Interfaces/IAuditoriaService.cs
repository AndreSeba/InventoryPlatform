using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

// Punto único de escritura/lectura de Auditoria — todos los servicios que mutan datos
// lo inyectan en vez de tocar _db.Auditorias directo, para que Entidad/Accion/PaisId
// queden siempre con la misma forma (ver AuditoriaService).
public interface IAuditoriaService
{
    // Serializa un snapshot (un objeto anónimo con los campos de negocio relevantes,
    // NUNCA la entidad de EF completa ni un DTO con binarios) a JSON consistente, para
    // usar como ValorAnterior/ValorNuevo. Se llama dos veces con la MISMA forma de objeto
    // (antes y después de mutar) para que el frontend pueda diffear campo por campo.
    string Capturar(object snapshot);

    // Encola el registro de auditoría (Auditorias.Add) y hace su propio SaveChanges —
    // así el registro queda escrito pase lo que pase después en el método que llama,
    // sin depender de que ese método haga otro SaveChanges más adelante.
    Task RegistrarAsync(
        string entidad, string entidadId, string accion,
        string? valorAnteriorJson, string? valorNuevoJson,
        int paisId, UsuarioActuante usuario, string? motivo, CancellationToken ct);

    Task<IReadOnlyList<AuditoriaDto>> ListarAsync(FiltroAuditoriaDto filtro, int paisId, CancellationToken ct);

    // Entidades/Acciones realmente presentes en la tabla PARA ESE país — alimenta los
    // <select> de filtro sin hardcodear una lista fija en el frontend, que se iría
    // desactualizando cada vez que se audite un módulo nuevo.
    Task<CatalogoAuditoriaDto> ObtenerCatalogoAsync(int paisId, CancellationToken ct);
}
