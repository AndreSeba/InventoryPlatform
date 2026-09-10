namespace Inventory.Domain.Entities;

public class SolicitudDetalle
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public Solicitud? Solicitud { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public decimal CantidadSolicitada { get; set; }
    public decimal? CantidadAprobada { get; set; } // null hasta que se aprueba; <= CantidadSolicitada

    // Cache acumulado desde los Movimiento de Salida ligados a esta línea — lo
    // actualiza SolicitudService.RegistrarEntregaAsync en la misma transacción
    // que crea el movimiento (equivalente al flujo "INV Acumular entregado").
    public decimal CantidadEntregada { get; set; }

    public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
}
