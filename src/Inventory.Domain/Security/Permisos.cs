namespace Inventory.Domain.Security;

// Códigos de permiso — viajan como claim "permiso" en el JWT y se usan directo como
// nombre de policy en los Controllers ([Authorize(Policy = Permisos.ProductosCrear)]).
// Agregar uno acá + al seed de InventoryDbContext (PermisoConfiguration/seed) es lo único
// que hace falta para dar de alta un permiso nuevo — no hace falta registrar la policy
// a mano en Program.cs, la resuelve PermissionPolicyProvider dinámicamente.
public static class Permisos
{
    public const string ProductosVer = "productos.ver";
    public const string ProductosCrear = "productos.crear";
    public const string ProductosEditar = "productos.editar";
    public const string ProductosDesactivar = "productos.desactivar";

    public const string MovimientosVer = "movimientos.ver";
    public const string MovimientosEntrada = "movimientos.entrada";
    public const string MovimientosSalida = "movimientos.salida";
    public const string MovimientosAjuste = "movimientos.ajuste";
    public const string MovimientosDevolucion = "movimientos.devolucion";

    public const string SolicitudesVer = "solicitudes.ver";
    public const string SolicitudesCrear = "solicitudes.crear";
    public const string SolicitudesAprobar = "solicitudes.aprobar";
    public const string SolicitudesRechazar = "solicitudes.rechazar";
    public const string SolicitudesEntregar = "solicitudes.entregar";

    public const string ConteosVer = "conteos.ver";
    public const string ConteosRegistrar = "conteos.registrar";

    public const string CategoriasVer = "categorias.ver";
    public const string CategoriasCrear = "categorias.crear";

    public const string AreasVer = "areas.ver";
    public const string AreasCrear = "areas.crear";

    public const string UbicacionesVer = "ubicaciones.ver";
    public const string UbicacionesCrear = "ubicaciones.crear";

    public const string UsuariosGestionar = "usuarios.gestionar";
    public const string RolesGestionar = "roles.gestionar";

    public static readonly IReadOnlyList<(string Codigo, string Modulo, string Descripcion)> Catalogo =
    [
        (ProductosVer, "Productos", "Ver el catálogo de productos y su existencia"),
        (ProductosCrear, "Productos", "Crear productos nuevos"),
        (ProductosEditar, "Productos", "Editar productos existentes"),
        (ProductosDesactivar, "Productos", "Desactivar productos"),

        (MovimientosVer, "Movimientos", "Ver el historial de movimientos"),
        (MovimientosEntrada, "Movimientos", "Registrar entradas de stock"),
        (MovimientosSalida, "Movimientos", "Registrar salidas de stock"),
        (MovimientosAjuste, "Movimientos", "Registrar ajustes positivos/negativos"),
        (MovimientosDevolucion, "Movimientos", "Registrar devoluciones de préstamo"),

        (SolicitudesVer, "Solicitudes", "Ver solicitudes de material"),
        (SolicitudesCrear, "Solicitudes", "Crear solicitudes de material"),
        (SolicitudesAprobar, "Solicitudes", "Aprobar solicitudes pendientes"),
        (SolicitudesRechazar, "Solicitudes", "Rechazar solicitudes pendientes"),
        (SolicitudesEntregar, "Solicitudes", "Registrar la entrega de una solicitud aprobada"),

        (ConteosVer, "Conteo físico", "Ver conteos por sesión"),
        (ConteosRegistrar, "Conteo físico", "Registrar conteos"),

        (CategoriasVer, "Categorías", "Ver categorías"),
        (CategoriasCrear, "Categorías", "Crear categorías"),

        (AreasVer, "Áreas", "Ver áreas"),
        (AreasCrear, "Áreas", "Crear áreas"),

        (UbicacionesVer, "Ubicaciones", "Ver ubicaciones"),
        (UbicacionesCrear, "Ubicaciones", "Crear ubicaciones"),

        (UsuariosGestionar, "Administración", "Crear y editar usuarios"),
        (RolesGestionar, "Administración", "Crear roles y asignarles permisos"),
    ];
}
