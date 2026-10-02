using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRolService _rolService;

    public RolesController(IRolService rolService) => _rolService = rolService;

    // Listar roles y ver los permisos disponibles lo necesitan las pantallas de Usuarios (para
    // asignar un rol) y de Roles. Antes lo podía leer cualquier usuario logueado, incluso uno
    // sin ningún permiso.
    private const string VerRoles = Permisos.UsuariosGestionar + "|" + Permisos.RolesGestionar;

    [HttpGet]
    [Authorize(Policy = VerRoles)]
    public async Task<ActionResult<IReadOnlyList<RolDto>>> Listar(CancellationToken ct)
        => Ok(await _rolService.ListarAsync(User.ObtenerPaisId(), ct));

    [HttpGet("permisos-disponibles")]
    [Authorize(Policy = VerRoles)]
    public ActionResult<IReadOnlyList<PermisoDto>> ListarPermisosDisponibles()
        => Ok(_rolService.ListarPermisosDisponibles());

    [HttpPost]
    [Authorize(Policy = Permisos.RolesGestionar)]
    [ProducesResponseType(typeof(RolDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RolDto>> Crear([FromBody] CrearRolDto dto, CancellationToken ct)
    {
        var creado = await _rolService.CrearAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(Listar), creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.RolesGestionar)]
    [ProducesResponseType(typeof(RolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RolDto>> Actualizar(int id, [FromBody] ActualizarRolDto dto, CancellationToken ct)
        => Ok(await _rolService.ActualizarAsync(id, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));
}
