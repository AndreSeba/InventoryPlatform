using Inventory.Application.Exceptions;

namespace Inventory.Application;

// Validación de cantidades del módulo de conteo externo: rechaza negativos y cantidades
// con más decimales de los que la unidad permite (ver ConteoExternoUnidad.DecimalesPermitidos).
// Siempre decimal, nunca float.
public static class CantidadConteoExterno
{
    public static decimal Validar(decimal valor, int decimalesPermitidos, string campo)
    {
        if (valor < 0)
            throw new ValidacionException($"{campo} no puede ser negativa.");
        if (Math.Round(valor, decimalesPermitidos) != valor)
            throw new ValidacionException($"{campo} admite como máximo {decimalesPermitidos} decimal(es) para esta unidad.");
        return valor;
    }

    // Para cargar un conteo: null = sin contar (distinto de 0, que sí es un conteo real
    // en cero).
    public static decimal? ValidarOpcional(decimal? valor, int decimalesPermitidos, string campo)
        => valor is null ? null : Validar(valor.Value, decimalesPermitidos, campo);
}
