namespace Inventory.Domain.Security;

// Definición de los 4 roles con los que arranca CADA país (Rol pasó a ser por país —
// ver RolConfiguration/RolPermisoConfiguration para el seed de Bolivia/Perú vía
// migración, y PaisService.CrearAsync para un país agregado después en caliente).
// Administrador no tiene lista acá — siempre es el catálogo completo de Permisos.Catalogo
// en el momento en que se siembra, para que un país nuevo arranque con TODOS los permisos
// que existan a esa fecha, no una copia vieja.
public static class RolesPorDefecto
{
    public const string Administrador = "Administrador";
    public const string Operador = "Operador";
    public const string Consulta = "Consulta";
    public const string Solicitante = "Solicitante";

    // InicioVer se agrega explícito — antes el link de Inicio era incondicional para
    // cualquier logueado, ahora depende de este permiso.
    public static readonly string[] PermisosOperador =
    [
        Permisos.InicioVer,
        Permisos.ProductosVer,
        Permisos.MovimientosVer, Permisos.MovimientosEntrada, Permisos.MovimientosSalida,
        Permisos.MovimientosAjuste, Permisos.MovimientosDevolucion,
        Permisos.SolicitudesVer, Permisos.SolicitudesCrear, Permisos.SolicitudesEntregar,
        Permisos.ConteosVer, Permisos.ConteosRegistrar,
        Permisos.CategoriasVer, Permisos.AreasVer, Permisos.UbicacionesVer,
        Permisos.UnidadesVer, Permisos.AlmacenesVer,
    ];

    public static readonly string[] PermisosConsulta =
    [
        Permisos.InicioVer,
        Permisos.ProductosVer, Permisos.MovimientosVer, Permisos.SolicitudesVer,
        Permisos.ConteosVer, Permisos.CategoriasVer, Permisos.AreasVer, Permisos.UbicacionesVer,
        Permisos.UnidadesVer, Permisos.AlmacenesVer,
    ];

    // Productos/Áreas/Ubicaciones/Categorias/Unidades en modo Ver son necesarios para
    // poder ARMAR una solicitud (elegir producto/área, y el alta rápida de producto
    // desde el carrito) — no es que puedan gestionar esos catálogos, solo leerlos.
    public static readonly string[] PermisosSolicitante =
    [
        Permisos.SolicitudesCrear,
        Permisos.ProductosVer, Permisos.AreasVer, Permisos.UbicacionesVer,
        Permisos.CategoriasVer, Permisos.UnidadesVer,
    ];
}
