namespace Inventory.Domain.Entities;

// Catálogo de unidades de medida. Reemplaza al CHECK fijo
// CK_Producto_Unidad IN ('UNI','CAJA','PQTS') que tenía la base: las unidades ahora
// se dan de alta desde /unidades sin tocar código ni migrar la base.
//
// Producto guarda el CÓDIGO (no un FK) a propósito: ese mismo código va embebido en
// ClaveProducto ("{CodigoProducto}-{CodigoUnidad}", regla de la guía v4), así que ya
// es una clave natural denormalizada por diseño. Por eso CodigoUnidad es INMUTABLE
// una vez creada la unidad (ActualizarUnidadDto no lo expone) — si se pudiera
// renombrar, los productos ya creados quedarían apuntando a un código que no existe.
// Para "cambiar" un código: desactivar la unidad y crear una nueva.
public class Unidad
{
    public int Id { get; set; }

    public string CodigoUnidad { get; set; } = string.Empty; // UNI, CAJA, KG, L...
    public string Nombre { get; set; } = string.Empty;       // "Unidad", "Caja", "Kilogramo"
    public bool Activo { get; set; } = true;
}
