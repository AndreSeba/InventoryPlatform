using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

public class Ubicacion
{
    public int Id { get; set; }
    public TipoUbicacion TipoUbicacion { get; set; }
    public string Nro { get; set; } = string.Empty;
    public string Lado { get; set; } = string.Empty;

    // Solo aplica a RACK — un MUEBLE no lleva nivel (regla CK_Ubicacion_Nivel de la guía v4).
    public string? Nivel { get; set; }

    // Generado por el servicio al crear/editar (equivalente al flujo "INV Generar
    // codigo ubicacion" de Power Automate): RACK -> Lado-Nro-Nivel, MUEBLE -> M-{Nro}-{Lado}.
    public string CodigoUbicacion { get; set; } = string.Empty;

    // Contenedor real de esta ubicación (propio o de un proveedor externo, en un país
    // determinado) — CodigoUbicacion es único POR Almacén, ya no global (ver
    // AlmacenConfiguration/UbicacionConfiguration).
    public int AlmacenId { get; set; }
    public Almacen? Almacen { get; set; }

    public bool Activo { get; set; } = true;
}
