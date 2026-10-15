namespace Inventory.Domain.Entities;

// Normalización de código para comparar/deduplicar. El código se GUARDA tal como se
// escribió; esto solo sirve para detectar duplicados (ceros a la izquierda, mayúsculas).
public static class CodigoConteoExterno
{
    public static string Normalizar(string? codigo)
    {
        var limpio = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        var sinCeros = limpio.TrimStart('0');
        // "0" o "000" no deberían existir como código, pero por las dudas no devolvemos "".
        return sinCeros.Length > 0 ? sinCeros : limpio;
    }
}
