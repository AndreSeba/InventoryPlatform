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
            _ when ErrorDeBaseConocido(exception) is { } conocido => conocido,
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

    // Red de seguridad: si una validación se escapó y la base rechazó el dato (texto más largo
    // que la columna, regla CHECK, clave duplicada, bloqueo cruzado), es un problema del dato
    // enviado, no un fallo del servidor. Se identifica por el número de error de SQL Server.
    private static (int, string, string)? ErrorDeBaseConocido(Exception exception)
    {
        var raiz = exception.GetBaseException();
        if (raiz.GetType().Name != "SqlException") return null;

        var numero = raiz.GetType().GetProperty("Number")?.GetValue(raiz) as int?;
        return numero switch
        {
            2601 or 2627 => (StatusCodes.Status409Conflict, "Error de negocio", "Ya existe un registro con esos datos."),
            2628 or 8152 => (StatusCodes.Status400BadRequest, "Solicitud inválida", "Algún texto supera el largo permitido."),
            547 => (StatusCodes.Status400BadRequest, "Solicitud inválida", "Algún dato está fuera de rango o hace referencia a algo que no existe."),
            1205 => (StatusCodes.Status409Conflict, "Error de negocio", "Otra persona estaba modificando lo mismo en este momento. Intentá de nuevo."),
            _ => null,
        };
    }
}
