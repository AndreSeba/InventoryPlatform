namespace Inventory.Domain.Enums;

// Pendiente: el solicitante avisó que va a devolver y el operario todavía no recibió el material.
// Recibido: el operario registró la entrada (queda ligada al aviso). Cancelado: el solicitante se arrepintió.
public enum EstadoAvisoDevolucion
{
    Pendiente = 1,
    Recibido = 2,
    Cancelado = 3,
}
