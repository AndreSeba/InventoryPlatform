using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/inicio")]
[Authorize]
public class InicioController : ControllerBase
{
    private readonly IResumenService _resumenService;

    public InicioController(IResumenService resumenService)
    {
        _resumenService = resumenService;
    }

    [HttpGet("resumen")]
    [Authorize(Policy = Permisos.InicioVer)]
    [ProducesResponseType(typeof(ResumenInicioDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResumenInicioDto>> Resumen(CancellationToken ct)
        => Ok(await _resumenService.ObtenerResumenInicioAsync(User.ObtenerPaisId(), ct));
}
