namespace Inventory.Web.Services;

// Envuelve un rechazo real de la API (ProblemDetails con .Status propio) para
// que los componentes puedan distinguirlo de un error de red inesperado.
public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
