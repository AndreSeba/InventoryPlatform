namespace Inventory.Domain.Entities;

// Existencias PROPIAS del módulo de conteo independiente — nunca el stock calculado
// del resto del sistema (ver ConteoExternoUnidad.cs). Cargado a mano en esta Fase 2 —
// la importación desde Excel es la Fase 5.
//
// AlmacenCodigo es texto libre a propósito en esta fase: la Fase 6 agrega nombres de
// almacén (solo nombres, ver CLAUDE.md sobre por qué no hay costo por almacén). Acá
// alcanza con el código tal como viene del sistema de origen.
public class ConteoExternoExistencia
{
    public int Id { get; set; }

    public int ConteoExternoProductoId { get; set; }
    public ConteoExternoProducto? ConteoExternoProducto { get; set; }

    public string AlmacenCodigo { get; set; } = string.Empty;

    // Null = existencia sin lote (no todos los productos se manejan por lote).
    public string? Lote { get; set; }

    public decimal CantidadSistema { get; set; }

    // SIN costo propio, a propósito — decisión explícita del usuario: el costo es
    // ÚNICO por producto (ConteoExternoProducto.CostoReferencia), nunca por
    // almacén/lote. Un costo que varía por almacén puede hacer que un código "neteé 0
    // en Bs" sin "netear 0 en unidades" — rompe la lectura del IRA por código (ver
    // KpiConteoExternoCalculador). No reintroducir un costo acá sin repetir esa
    // discusión con el usuario.
    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
