using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

public class Movimiento
{
    public int Id { get; set; }
    public string NumeroMovimiento { get; set; } = string.Empty;

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public TipoMovimiento TipoMovimiento { get; set; }
    public int Cantidad { get; set; }

    // Con signo: +Cantidad en Entrada/AjustePositivo, -Cantidad en Salida/AjusteNegativo.
    // Es la base de SUM() para el stock — nunca se acepta un valor que no siga esta regla
    // (impuesto por MovimientoService, más el CHECK de base CK_Movimiento_Signo).
    public int CantidadEfectiva { get; set; }

    // Obligatoria en los 4 tipos (a diferencia del v3 original, donde el rack solo
    // era obligatorio en Entrada/Salida) — así lo definió la guía v4 (sección 5.8).
    public int UbicacionId { get; set; }
    public Ubicacion? Ubicacion { get; set; }

    // Préstamo/salida con retorno esperado — solo aplica a TipoMovimiento = Salida.
    public bool Retorna { get; set; }
    public string? UbicacionExterna { get; set; }
    public DateOnly? FechaRetornoEsperada { get; set; }

    // Autorreferencia: la devolución (Entrada) apunta a la Salida que la originó.
    public int? MovimientoOrigenId { get; set; }
    public Movimiento? MovimientoOrigen { get; set; }

    // Solo en Salida: si la salida cubre una línea de una solicitud aprobada,
    // acumula CantidadEntregada en esa línea (ver SolicitudService).
    public int? SolicitudDetalleId { get; set; }
    public SolicitudDetalle? SolicitudDetalle { get; set; }

    public int RegistradoPorId { get; set; }
    public Usuario? RegistradoPor { get; set; }
    // Snapshot del nombre al momento de la operación (denormalización DELIBERADA,
    // ver CLAUDE.md): el FK de arriba dice QUIÉN fue y sigue siendo válido aunque
    // la persona cambie de nombre; esto dice con qué nombre se firmó entonces.
    public string RegistradoPorNombre { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime FechaMovimiento { get; set; } = DateTime.UtcNow;
}
