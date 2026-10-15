namespace Inventory.Domain.Entities;

// Catálogo de unidades PROPIO del módulo de conteo independiente — a propósito separado
// del Unidad general de productos/movimientos. Este módulo no toca Producto/Movimiento/
// SesionConteo, así que tampoco comparte su catálogo de unidades (ver CLAUDE.md,
// "Conteo Externo: módulo independiente de todo").
public class ConteoExternoUnidad
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty; // CAJ, PACK, KG, UNI...

    // Cuántos decimales admite una cantidad en esta unidad. 0 = solo enteros.
    public int DecimalesPermitidos { get; set; }

    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public bool Activo { get; set; } = true;
}
