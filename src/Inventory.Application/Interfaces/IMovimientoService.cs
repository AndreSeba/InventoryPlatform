using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IMovimientoService
{
    Task<MovimientoResultadoDto> RegistrarEntradaAsync(RegistrarEntradaDto dto, string usuarioId, CancellationToken ct);
    Task<MovimientoResultadoDto> RegistrarSalidaAsync(RegistrarSalidaDto dto, string usuarioId, CancellationToken ct);
    Task<MovimientoResultadoDto> RegistrarAjusteAsync(RegistrarAjusteDto dto, string usuarioId, CancellationToken ct);
    Task<MovimientoResultadoDto> RegistrarDevolucionAsync(RegistrarDevolucionDto dto, string usuarioId, CancellationToken ct);
    Task<IReadOnlyList<MovimientoDto>> ListarPorProductoAsync(int productoId, CancellationToken ct);
    Task<IReadOnlyList<MovimientoDto>> ListarAsync(DateTime? desde, DateTime? hasta, CancellationToken ct);
    Task<IReadOnlyList<MovimientoDto>> ListarPrestamosPendientesAsync(CancellationToken ct);
    Task<(byte[] Contenido, string NombreArchivo)> GenerarExcelAsync(GenerarMovimientosExcelDto dto, CancellationToken ct);
}
