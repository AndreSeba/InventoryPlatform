using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/auditoria")]
[Authorize]
public class AuditoriaController : ControllerBase
{
    private readonly IAuditoriaService _auditoriaService;

    public AuditoriaController(IAuditoriaService auditoriaService) => _auditoriaService = auditoriaService;

    [HttpGet]
    [Authorize(Policy = Permisos.AuditoriaVer)]
    public async Task<ActionResult<IReadOnlyList<AuditoriaDto>>> Listar([FromQuery] FiltroAuditoriaDto filtro, CancellationToken ct)
        => Ok(await _auditoriaService.ListarAsync(filtro, User.ObtenerPaisId(), ct));

    [HttpGet("catalogo")]
    [Authorize(Policy = Permisos.AuditoriaVer)]
    public async Task<ActionResult<CatalogoAuditoriaDto>> ObtenerCatalogo(CancellationToken ct)
        => Ok(await _auditoriaService.ObtenerCatalogoAsync(User.ObtenerPaisId(), ct));
}
