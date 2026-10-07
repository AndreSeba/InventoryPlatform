using Inventory.Api.Security;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Inventory.Infrastructure.Correo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Controllers;

// Para comprobar la configuración del correo sin esperar a que ocurra un aviso real.
[ApiController]
[Route("api/correo")]
[Authorize]
public class CorreoController : ControllerBase
{
    private readonly EnviadorSmtp _enviador;
    private readonly CorreoOptions _opciones;

    public CorreoController(EnviadorSmtp enviador, IOptions<CorreoOptions> opciones)
    {
        _enviador = enviador;
        _opciones = opciones.Value;
    }

    // Manda un correo de prueba a Correo:DestinatarioDePrueba (o, si no hay, al email del usuario que lo pide).
    [HttpPost("prueba")]
    [Authorize(Policy = Permisos.UsuariosGestionar)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Prueba(CancellationToken ct)
    {
        if (!_opciones.Habilitado)
            throw new ValidacionException("El correo está apagado: poné Correo:Habilitado en true.");
        if (string.IsNullOrWhiteSpace(_opciones.Usuario) || string.IsNullOrWhiteSpace(_opciones.Clave))
            throw new ValidacionException("Falta configurar Correo:Usuario y Correo:Clave.");

        var usuario = User.ObtenerUsuarioActuante();
        var destino = string.IsNullOrWhiteSpace(_opciones.DestinatarioDePrueba) ? _opciones.Usuario : _opciones.DestinatarioDePrueba;
        try
        {
            await _enviador.EnviarAsync(new MensajeCorreo(destino, usuario.Nombre, "Prueba de correo del sistema de inventario",
                "El correo funciona", "Si estás leyendo esto, el sistema ya puede avisar por correo (solicitudes, entregas, devoluciones).", "/inicio"), ct);
        }
        catch (Exception ex)
        {
            throw new ValidacionException($"No se pudo enviar el correo: {ex.Message}");
        }
        return Ok(new { enviadoA = destino });
    }
}
