namespace Inventory.Infrastructure.Controles;

// Controles de trazabilidad configurables sin tocar código (sección "Controles" de appsettings).
// Los valores por defecto son los ESTRICTOS: pensados para que "sacar material de favor y
// cargarlo después" deje de ser posible dentro del sistema y, cuando se intente, quede a la vista.
public class ControlesOptions
{
    public const string SectionName = "Controles";

    // Toda entrada y salida de stock se registra contra una línea de solicitud aprobada. Las
    // correcciones se hacen con un ajuste (que exige motivo y queda auditado).
    public bool ExigirSolicitudEnMovimientos { get; set; } = true;

    // Quien pide el material no lo aprueba, ni lo rechaza, ni registra su entrega: son tres personas
    // distintas. Con un equipo muy chico se puede desactivar, sabiendo que se pierde el control.
    public bool SeparacionDeFunciones { get; set; } = true;

    // Toda devolución de un préstamo hecho contra una solicitud se registra contra un AVISO de quien pidió el
    // material (ver DevolucionService): el solicitante avisa, el operario recibe. Los préstamos viejos sin
    // solicitud no tienen a quién avisar y se siguen devolviendo directo.
    public bool ExigirAvisoEnDevoluciones { get; set; } = true;

    // Revisión periódica de accesos (ver RevisionAccesoService): cada cuántos días vence la
    // revisión de todas las cuentas (trimestral) y la de las cuentas administradoras (mensual),
    // y desde cuántos días sin ingresar una cuenta se marca "sin actividad reciente".
    public int RevisionTodosCadaDias { get; set; } = 90;
    public int RevisionAdministradoresCadaDias { get; set; } = 30;
    public int DiasSinActividad { get; set; } = 90;

    // Sin configuración (por ejemplo en las pruebas unitarias, que arman el servicio a mano) no se
    // aplica ningún control: cada prueba activa solo el que quiere verificar.
    public static ControlesOptions Permisivo { get; } = new() { ExigirSolicitudEnMovimientos = false, SeparacionDeFunciones = false, ExigirAvisoEnDevoluciones = false };
}
