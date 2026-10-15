using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

// Módulo de conteo independiente — importación desde Excel de un sistema externo (Fase 5).
// Dos pasos: vista previa (devuelve encabezados + unas filas de muestra para que el usuario
// mapee las columnas) y confirmar (aplica el mapeo contra el archivo completo). El archivo
// se manda dos veces (una por paso) a propósito — no se guarda nada server-side entre medio.
[ApiController]
[Route("api/conteo-externo/importacion")]
[Authorize]
public class ConteoExternoImportacionController : ControllerBase
{
    private const long MaxBytesArchivo = 10 * 1024 * 1024;

    private readonly IConteoExternoImportacionService _importacionService;

    public ConteoExternoImportacionController(IConteoExternoImportacionService importacionService) => _importacionService = importacionService;

    // Sin [Consumes] a propósito, mismo motivo que /conteos/{id}/importar: con él, la
    // autorización no se evalúa antes de rechazar el content-type.
    [HttpPost("vista-previa")]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [RequestSizeLimit(MaxBytesArchivo + 1_000_000)]
    [ProducesResponseType(typeof(VistaPreviaImportacionConteoExternoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VistaPreviaImportacionConteoExternoDto>> VistaPrevia(IFormFile archivo, CancellationToken ct)
    {
        var datos = await LeerArchivoAsync(archivo, ct);
        return Ok(await _importacionService.VistaPreviaAsync(datos, ct));
    }

    [HttpPost("confirmar")]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [RequestSizeLimit(MaxBytesArchivo + 1_000_000)]
    [ProducesResponseType(typeof(ImportacionConteoExternoResultadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportacionConteoExternoResultadoDto>> Confirmar(IFormFile archivo, [FromForm] MapeoColumnasConteoExternoDto mapeo, CancellationToken ct)
    {
        var datos = await LeerArchivoAsync(archivo, ct);
        var resultado = await _importacionService.ImportarAsync(datos, mapeo, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return Ok(resultado);
    }

    private static async Task<byte[]> LeerArchivoAsync(IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            throw new ArchivoInvalidoException("Subí el archivo de Excel a importar.");
        if (archivo.Length > MaxBytesArchivo)
            throw new ArchivoInvalidoException("El archivo no puede superar los 10 MB.");

        using var stream = new MemoryStream();
        await archivo.CopyToAsync(stream, ct);
        return stream.ToArray();
    }
}
