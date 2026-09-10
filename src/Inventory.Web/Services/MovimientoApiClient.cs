using System.Net.Http.Json;
using Inventory.Application.Dtos;

namespace Inventory.Web.Services;

public class MovimientoApiClient
{
    private readonly HttpClient _http;

    public MovimientoApiClient(HttpClient http) => _http = http;

    public async Task<List<MovimientoDto>> ListarPorProductoAsync(int productoId, CancellationToken ct = default)
    {
        var respuesta = await _http.GetAsync($"api/movimientos/producto/{productoId}", ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return await respuesta.Content.ReadFromJsonAsync<List<MovimientoDto>>(cancellationToken: ct) ?? [];
    }

    public async Task<List<MovimientoDto>> ListarPrestamosPendientesAsync(CancellationToken ct = default)
    {
        var respuesta = await _http.GetAsync("api/movimientos/prestamos-pendientes", ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return await respuesta.Content.ReadFromJsonAsync<List<MovimientoDto>>(cancellationToken: ct) ?? [];
    }

    public async Task<MovimientoResultadoDto> RegistrarEntradaAsync(RegistrarEntradaDto dto, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/movimientos/entradas", dto, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return (await respuesta.Content.ReadFromJsonAsync<MovimientoResultadoDto>(cancellationToken: ct))!;
    }

    public async Task<MovimientoResultadoDto> RegistrarSalidaAsync(RegistrarSalidaDto dto, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/movimientos/salidas", dto, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return (await respuesta.Content.ReadFromJsonAsync<MovimientoResultadoDto>(cancellationToken: ct))!;
    }

    public async Task<MovimientoResultadoDto> RegistrarAjusteAsync(RegistrarAjusteDto dto, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/movimientos/ajustes", dto, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return (await respuesta.Content.ReadFromJsonAsync<MovimientoResultadoDto>(cancellationToken: ct))!;
    }

    public async Task<MovimientoResultadoDto> RegistrarDevolucionAsync(RegistrarDevolucionDto dto, CancellationToken ct = default)
    {
        var respuesta = await _http.PostAsJsonAsync("api/movimientos/devoluciones", dto, ct);
        await ApiClientHelper.LanzarSiHayErrorAsync(respuesta, ct);
        return (await respuesta.Content.ReadFromJsonAsync<MovimientoResultadoDto>(cancellationToken: ct))!;
    }
}
