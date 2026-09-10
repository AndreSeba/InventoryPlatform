using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Web.Services;

internal static class ApiClientHelper
{
    public static async Task LanzarSiHayErrorAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
        throw new ApiException((int)respuesta.StatusCode, problema?.Detail ?? "Ocurrió un error inesperado.");
    }
}
