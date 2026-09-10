using System.Net.Http.Json;
using Inventory.Application.Dtos;

namespace Inventory.Web.Services;

public class ConteoApiClient
{
    private readonly HttpClient _http;

    public ConteoApiClient(HttpClient http) => _http = http;

    public async Task<List<ConteoDto>> ListarPorSesionAsync(string sesionConteo, CancellationToken ct = default)
    {
        var respuesta = await _http.GetAsync($"api/conteos/sesion/{Uri.EscapeDataString(sesionConteo)}", ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return await respuesta.Content.ReadFromJsonAsync<List<ConteoDto>>(cancellationToken: ct) ?? [];
    }

    public async Task<ConteoDto> RegistrarAsync(RegistrarConteoDto dto, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/conteos", dto, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return (await respuesta.Content.ReadFromJsonAsync<ConteoDto>(cancellationToken: ct))!;
    }
}
