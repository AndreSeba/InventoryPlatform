using Inventory.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Middleware;

// Sección 12 de la propuesta: la API nunca expone el detalle interno de una
// excepción no controlada — solo las DominioException (con .Status propio y
// mensaje pensado para mostrarse) devuelven su texto real al cliente.
public class ManejadorGlobalDeExcepciones : IExceptionHandler
{
    private readonly ILogger<ManejadorGlobalDeExcepciones> _logger;

    public ManejadorGlobalDeExcepciones(ILogger<ManejadorGlobalDeExcepciones> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, titulo, detalle) = exception switch
        {
            DominioException dominioEx => (dominioEx.Status, "Error de negocio", dominioEx.Message),
            ArgumentOutOfRangeException or ArgumentException => (StatusCodes.Status400BadRequest, "Solicitud inválida", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Error interno", "Ocurrió un error inesperado. Contactá al administrador si el problema persiste."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Error no controlado en {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = detalle,
            Instance = httpContext.Request.Path,
        }, ct);

        return true;
    }
}
