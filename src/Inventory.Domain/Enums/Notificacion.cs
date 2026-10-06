namespace Inventory.Domain.Enums;

// Pendiente: trabajo que le toca a la persona (se cierra solo cuando el estado real cambia).
// Resultado: algo que le pasó a lo que esa persona pidió (aprobada, rechazada, entregada, recibida).
// Inventario: alertas de existencias, vencimientos y préstamos. Control: revisión de accesos, conteos, cuentas.
public enum CategoriaNotificacion
{
    Pendiente = 1,
    Resultado = 2,
    Inventario = 3,
    Control = 4,
}

public enum SeveridadNotificacion
{
    Info = 1,
    Aviso = 2,
    Alerta = 3,
}
