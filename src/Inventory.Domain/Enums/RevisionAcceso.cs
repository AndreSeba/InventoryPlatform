namespace Inventory.Domain.Enums;

public enum EstadoRevisionAcceso
{
    EnCurso = 1,
    Cerrada = 2,
    Cancelada = 3,
}

// Todos = revisión trimestral de todas las cuentas activas; Administradores = revisión
// mensual de las cuentas con permisos de administración (las privilegiadas).
public enum AlcanceRevisionAcceso
{
    Todos = 1,
    Administradores = 2,
}

public enum DecisionAcceso
{
    Pendiente = 0,
    Mantener = 1,
    Quitar = 2,
    CambiarRol = 3,
}
