namespace Inventory.Domain.Entities;

// Un conteo numerado en una ubicación puntual. Una línea puede tener hasta MaxConteos
// "números de conteo" (reconteos), y cada número puede sumar varias ubicaciones libres
// (p. ej. "UB1", "UB2" repartidas en la planta). El "conteo vigente" de una línea es el
// número más alto que tenga algún registro — los números anteriores NO se combinan con
// el vigente (ver ConteoExternoSesionService).
public class ConteoExternoRegistro
{
    public const int MaxConteos = 4;
    public const string SinUbicacion = "S/U";
    public const int LargoMaxUbicacion = 40;

    public int Id { get; set; }

    public int ConteoExternoLineaId { get; set; }
    public ConteoExternoLinea? ConteoExternoLinea { get; set; }

    public int NumeroConteo { get; set; }
    public string Ubicacion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }

    public int ContadoPorId { get; set; }
    public Usuario? ContadoPor { get; set; }
    public string ContadoPorNombre { get; set; } = string.Empty;
    public DateTime FechaConteo { get; set; } = DateTime.UtcNow;
}
