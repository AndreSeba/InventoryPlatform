namespace Inventory.Domain.Entities;

// Catálogo de nombres de almacén PROPIO del módulo de conteo independiente — Fase 6.
// SOLO para mostrar: el código de almacén que ya usan ConteoExternoExistencia/
// ConteoExternoLinea NUNCA se reemplaza por el nombre — sigue siendo la identidad
// estable para joins/reimportaciones, y el nombre se resuelve en el momento de
// mostrar (ver ConteoExternoExistenciaService/ConteoExternoSesionService).
public class ConteoExternoAlmacen
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public bool Activo { get; set; } = true;
}
