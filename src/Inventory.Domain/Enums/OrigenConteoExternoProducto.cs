namespace Inventory.Domain.Enums;

// De dónde salió un ConteoExternoProducto — informativo, no cambia ninguna regla.
public enum OrigenConteoExternoProducto
{
    BaseDatos = 1,  // carga inicial masiva (CSV/Excel semilla), si alguna vez se hace
    Manual = 2,     // alta uno por uno desde el catálogo del módulo
    Importado = 3,  // dado de alta al vuelo durante la importación de existencias/costos
}
