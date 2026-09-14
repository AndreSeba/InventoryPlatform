using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(IUsuarioService usuarioService) => _usuarioService = usuarioService;

    [HttpGet]
    [Authorize(Policy = Permisos.UsuariosGestionar)]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar(CancellationToken ct)
        => Ok(await _usuarioService.ListarAsync(ct));

    [HttpPost]
    [Authorize(Policy = Permisos.UsuariosGestionar)]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Crear([FromBody] CrearUsuarioDto dto, CancellationToken ct)
    {
        var creado = await _usuarioService.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Listar), creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.UsuariosGestionar)]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Actualizar(int id, [FromBody] ActualizarUsuarioDto dto, CancellationToken ct)
        => Ok(await _usuarioService.ActualizarAsync(id, dto, ct));
}
