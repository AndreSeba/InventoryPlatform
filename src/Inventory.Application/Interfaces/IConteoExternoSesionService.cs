using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IConteoExternoSesionService
{
    Task<IReadOnlyList<ConteoExternoSesionResumenDto>> ListarAsync(int paisId, CancellationToken ct);
    Task<ConteoExternoSesionDetalleDto> ObtenerAsync(int id, int paisId, CancellationToken ct);
    Task<ConteoExternoSesionResumenDto> CrearAsync(CrearConteoExternoSesionDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task RegistrarConteoAsync(int lineaId, int numeroConteo, RegistrarConteoExternoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task ActualizarObservacionAsync(int lineaId, ActualizarObservacionConteoExternoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    // Transcripción ágil: escribe la cantidad FINAL de la línea (ver la regla en el servicio).
    Task RegistrarCantidadFinalAsync(int lineaId, ConteoExternoCantidadFinalDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    // Producto sobrante: catálogo (si es nuevo) + existencia 0 + línea de la sesión, todo o nada.
    Task<ConteoExternoLineaDto> AgregarSobranteAsync(int sesionId, AgregarSobranteConteoExternoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ConteoExternoSesionResumenDto> CerrarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct);

    // Siempre sobre TODAS las líneas de la sesión, nunca sobre un filtro de pantalla.
    Task<ConteoExternoSesionKpiDto> ObtenerKpisAsync(int sesionId, int paisId, CancellationToken ct);

    // Una fila por sesión — para el historial multi-sesión.
    Task<IReadOnlyList<ConteoExternoSesionKpiDto>> ObtenerHistorialKpisAsync(int paisId, CancellationToken ct);

    Task<(byte[] Contenido, string NombreArchivo)> ExportarKpisExcelAsync(int sesionId, int paisId, CancellationToken ct);
}
