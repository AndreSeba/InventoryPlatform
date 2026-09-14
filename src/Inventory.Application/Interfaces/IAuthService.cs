using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken ct);
}
