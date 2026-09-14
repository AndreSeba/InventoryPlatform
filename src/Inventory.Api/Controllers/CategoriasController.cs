using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/categorias")]
[Authorize]
public class CategoriasController : ControllerBase
{
    private readonly ICategoriaService _categoriaService;

    public CategoriasController(ICategoriaService categoriaService) => _categoriaService = categoriaService;

    [HttpGet]
    [Authorize(Policy = Permisos.CategoriasVer)]
    public async Task<ActionResult<IReadOnlyList<CategoriaDto>>> Listar([FromQuery] bool incluirInactivas, CancellationToken ct)
        => Ok(await _categoriaService.ListarAsync(incluirInactivas, ct));

    [HttpPost]
    [Authorize(Policy = Permisos.CategoriasCrear)]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoriaDto>> Crear([FromBody] CrearCategoriaDto dto, CancellationToken ct)
    {
        var creada = await _categoriaService.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Listar), creada);
    }
}
