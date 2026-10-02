using Inventory.Application.Exceptions;

namespace Inventory.Application;

// Validación de texto de entrada. Antes un nombre vacío entraba y uno demasiado largo
// terminaba en un 500 de truncado en la base; ahora ambos son un 400 con mensaje claro.
public static class Validacion
{
    // Obligatorio: sin espacios sobrantes, no vacío, con tope de largo.
    public static string Texto(string? valor, int maximo, string campo)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length == 0)
            throw new ValidacionException($"{campo} es obligatorio.");
        if (limpio.Length > maximo)
            throw new ValidacionException($"{campo} no puede superar los {maximo} caracteres.");
        return limpio;
    }

    // Opcional: vacío o solo espacios se guarda como null.
    public static string? TextoOpcional(string? valor, int maximo, string campo)
    {
        var limpio = valor?.Trim();
        if (string.IsNullOrEmpty(limpio)) return null;
        if (limpio.Length > maximo)
            throw new ValidacionException($"{campo} no puede superar los {maximo} caracteres.");
        return limpio;
    }
}
