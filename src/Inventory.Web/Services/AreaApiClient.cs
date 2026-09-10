using System.Net.Http.Json;
using Inventory.Application.Dtos;

namespace Inventory.Web.Services;

public class AreaApiClient
{
    private readonly HttpClient _http;

    public AreaApiClient(HttpClient http) => _http = http;

    public async Task<List<AreaDto>> ListarAsync(bool incluirInactivas = false, CancellationToken ct = default)
    {
        var respuesta = await _http.GetAsync($"api/areas?incluirInactivas={incluirInactivas}", ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return await respuesta.Content.ReadFromJsonAsync<List<AreaDto>>(cancellationToken: ct) ?? [];
    }

    public async Task<AreaDto> CrearAsync(CrearAreaDto dto, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/areas", dto, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return (await respuesta.Content.ReadFromJsonAsync<AreaDto>(cancellationToken: ct))!;
    }
}
