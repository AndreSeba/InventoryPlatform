using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IMovimientoService
{
    Task<MovimientoResultadoDto> RegistrarEntradaAsync(RegistrarEntradaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<MovimientoResultadoDto> RegistrarSalidaAsync(RegistrarSalidaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<MovimientoResultadoDto> RegistrarAjusteAsync(RegistrarAjusteDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<MovimientoResultadoDto> RegistrarDevolucionAsync(RegistrarDevolucionDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<IReadOnlyList<MovimientoDto>> ListarPorProductoAsync(int productoId, int paisId, CancellationToken ct);
    Task<IReadOnlyList<MovimientoDto>> ListarAsync(int paisId, DateTime? desde, DateTime? hasta, CancellationToken ct);
    Task<IReadOnlyList<MovimientoDto>> ListarPrestamosPendientesAsync(int paisId, CancellationToken ct);
    Task<(byte[] Contenido, string NombreArchivo)> GenerarExcelAsync(GenerarMovimientosExcelDto dto, int paisId, CancellationToken ct);
}
