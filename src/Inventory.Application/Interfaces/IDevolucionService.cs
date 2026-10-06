using Inventory.Application.Dtos;
using Inventory.Domain.Enums;

namespace Inventory.Application.Interfaces;

public interface IDevolucionService
{
    // Préstamos que todavía no volvieron por completo: todos los del país, o solo los que pidió `solicitadoPorId`.
    Task<IReadOnlyList<PrestamoDto>> ListarPrestamosAsync(int paisId, int? solicitadoPorId, CancellationToken ct);

    // Avisos: todos los del país, o solo los que hizo `avisadoPorId`; opcionalmente filtrados por estado.
    Task<IReadOnlyList<AvisoDevolucionDto>> ListarAvisosAsync(int paisId, int? avisadoPorId, EstadoAvisoDevolucion? estado, CancellationToken ct);

    Task<AvisoDevolucionDto> AvisarAsync(CrearAvisoDevolucionDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<AvisoDevolucionDto> CancelarAvisoAsync(int id, CancelarAvisoDevolucionDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
