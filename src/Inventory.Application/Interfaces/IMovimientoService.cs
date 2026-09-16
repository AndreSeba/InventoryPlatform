using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IMovimientoService
{
    Task<MovimientoResultadoDto> RegistrarEntradaAsync(RegistrarEntradaDto dto, UsuarioActuante usuario, CancellationToken ct);
    Task<MovimientoResultadoDto> RegistrarSalidaAsync(RegistrarSalidaDto dto, UsuarioActuante usuario, CancellationToken ct);
    Task<MovimientoResultadoDto> RegistrarAjusteAsync(RegistrarAjusteDto dto, UsuarioActuante usuario, CancellationToken ct);
    Task<MovimientoResultadoDto> RegistrarDevolucionAsync(RegistrarDevolucionDto dto, UsuarioActuante usuario, CancellationToken ct);
    Task<IReadOnlyList<MovimientoDto>> ListarPorProductoAsync(int productoId, CancellationToken ct);
    Task<IReadOnlyList<MovimientoDto>> ListarAsync(DateTime? desde, DateTime? hasta, CancellationToken ct);
    Task<IReadOnlyList<MovimientoDto>> ListarPrestamosPendientesAsync(CancellationToken ct);
}
