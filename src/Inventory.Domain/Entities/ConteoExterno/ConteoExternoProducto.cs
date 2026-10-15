using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

// Catálogo de productos PROPIO del módulo de conteo independiente — nunca el Producto
// general (ver ConteoExternoUnidad.cs para el motivo).
public class ConteoExternoProducto
{
    public int Id { get; set; }

    // Tal como se escribió — nunca se modifica para "normalizar" (ver CodigoNormalizado).
    public string Codigo { get; set; } = string.Empty;

    // Clave de comparación/deduplicación: sin ceros a la izquierda y en mayúsculas.
    // "10101105" y "010101105" son el mismo código (un Excel de origen puede comerse el
    // cero inicial al exportar) — ver CodigoConteoExterno.Normalizar(). Se guarda como
    // columna propia (no una propiedad calculada) para poder indexarla.
    public string CodigoNormalizado { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public string Familia { get; set; } = string.Empty; // se guarda en mayúsculas

    public OrigenConteoExternoProducto Origen { get; set; } = OrigenConteoExternoProducto.Manual;

    // Null hasta que se carga el primer lote de existencia (Fase 2) — ahí queda fija
    // ("la unidad es única por producto"). No se pide al dar de alta el producto a
    // propósito: pedirla antes produciría una unidad "adivinada".
    public string? UnidadCodigo { get; set; }

    // Costo de referencia, ÚNICO por producto (nunca por almacén/lote — ver
    // ConteoExternoExistencia.cs). Editable a mano, o se completa al importar costos.
    public decimal? CostoReferencia { get; set; }

    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
