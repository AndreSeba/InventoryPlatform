using System.Net.Http.Json;
using Inventory.Application.Dtos;

namespace Inventory.Web.Services;

public class UbicacionApiClient
{
    private readonly HttpClient _http;

    public UbicacionApiClient(HttpClient http) => _http = http;

    public async Task<List<UbicacionDto>> ListarAsync(bool incluirInactivas = false, CancellationToken ct = default)
    {
        var respuesta = await _http.GetAsync($"api/ubicaciones?incluirInactivas={incluirInactivas}", ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return await respuesta.Content.ReadFromJsonAsync<List<UbicacionDto>>(cancellationToken: ct) ?? [];
    }

    public async Task<UbicacionDto> CrearAsync(CrearUbicacionDto dto, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/ubicaciones", dto, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return (await respuesta.Content.ReadFromJsonAsync<UbicacionDto>(cancellationToken: ct))!;
    }
}
