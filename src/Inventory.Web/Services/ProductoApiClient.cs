using System.Net.Http.Json;
using Inventory.Application.Dtos;

namespace Inventory.Web.Services;

// "Servicio de cliente" (sección 5.1): encapsula el HttpClient hacia la Web
// API, sin lógica de negocio propia. Los componentes .razor lo inyectan con
// @inject en vez de llamar a HttpClient directamente.
public class ProductoApiClient
{
    private readonly HttpClient _http;

    public ProductoApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ProductoDto>> ListarAsync(int? categoriaId = null, bool incluirInactivos = false, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (categoriaId is not null)
            query.Add($"categoriaId={categoriaId}");
        if (incluirInactivos)
            query.Add("incluirInactivos=true");

        var url = "api/productos" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        var respuesta = await _http.GetAsync(url, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);

        return await respuesta.Content.ReadFromJsonAsync<List<ProductoDto>>(cancellationToken: ct) ?? [];
    }

    public async Task<ProductoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var respuesta = await _http.GetAsync($"api/productos/{id}", ct);
        if (respuesta.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return await respuesta.Content.ReadFromJsonAsync<ProductoDto>(cancellationToken: ct);
    }

    public async Task<ProductoDto> CrearAsync(CrearProductoDto dto, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/productos", dto, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return (await respuesta.Content.ReadFromJsonAsync<ProductoDto>(cancellationToken: ct))!;
    }

    public async Task<ProductoDto> ActualizarAsync(int id, ActualizarProductoDto dto, CancellationToken ct = default)
    {
        var respuesta = await _http.PutAsJsonAsync($"api/productos/{id}", dto, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return (await respuesta.Content.ReadFromJsonAsync<ProductoDto>(cancellationToken: ct))!;
    }

    public async Task DesactivarAsync(int id, CancellationToken ct = default)
    {
        var respuesta = await _http.DeleteAsync($"api/productos/{id}", ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
    }
}
