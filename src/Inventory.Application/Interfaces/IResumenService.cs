using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IResumenService
{
    Task<ResumenInicioDto> ObtenerResumenInicioAsync(int paisId, CancellationToken ct);
}
