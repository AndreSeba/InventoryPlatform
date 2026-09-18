namespace Inventory.Domain.Entities;

public class SolicitudDetalle
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public Solicitud? Solicitud { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int CantidadSolicitada { get; set; }
    public int? CantidadAprobada { get; set; } // null hasta que se aprueba; <= CantidadSolicitada

    // Cache acumulado desde los Movimiento de Salida ligados a esta línea — lo
    // actualiza SolicitudService.RegistrarEntregaAsync en la misma transacción
    // que crea el movimiento (equivalente al flujo "INV Acumular entregado").
    public int CantidadEntregada { get; set; }

    // Solo tiene sentido en una Solicitud de Salida (validado en SolicitudService,
    // no puede ser un CHECK porque Tipo vive en la tabla Solicitud) — mismo dato que
    // ya existe en Movimiento (Retorna/UbicacionExterna/FechaRetornoEsperada), cargado
    // acá al armar el carrito para que sobreviva el circuito de aprobación y se copie
    // tal cual al Movimiento de Salida cuando se entrega la línea.
    public bool Retorna { get; set; }
    public string? UbicacionExterna { get; set; }
    public DateOnly? FechaRetornoEsperada { get; set; }

    public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
}
