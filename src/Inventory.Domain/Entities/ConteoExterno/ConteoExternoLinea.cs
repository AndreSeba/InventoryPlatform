namespace Inventory.Domain.Entities;

// Línea de sesión — foto fija de una existencia (producto, almacén, lote) al momento de
// crear la sesión. Congelar los datos acá significa que si alguien corrige la
// existencia original DESPUÉS, esta sesión sigue mostrando contra qué se contó
// realmente.
public class ConteoExternoLinea
{
    public int Id { get; set; }

    public int ConteoExternoSesionId { get; set; }
    public ConteoExternoSesion? ConteoExternoSesion { get; set; }

    // Referencia a la existencia de origen, solo para trazabilidad — todos los demás
    // campos de abajo son la FOTO FIJA, no se leen desde acá en tiempo real.
    public int ConteoExternoExistenciaId { get; set; }
    public ConteoExternoExistencia? ConteoExternoExistencia { get; set; }

    public int ConteoExternoProductoId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public string Familia { get; set; } = string.Empty;
    public string AlmacenCodigo { get; set; } = string.Empty;
    public string? Lote { get; set; }
    public string UnidadCodigo { get; set; } = string.Empty;
    public decimal ExistenciaSistema { get; set; }
    public decimal CostoUnitario { get; set; }

    // Editable mientras la sesión está EnCurso. Null = sin observación.
    public string? Observacion { get; set; }

    public ICollection<ConteoExternoRegistro> Registros { get; set; } = [];
}
