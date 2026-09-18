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

    public const string CategoriasEditar = "categorias.editar";
    public const string AreasEditar = "areas.editar";

    public const string InicioVer = "inicio.ver";

    public const string UnidadesVer = "unidades.ver";
    public const string UnidadesCrear = "unidades.crear";
    public const string UnidadesEditar = "unidades.editar";

    public const string AlmacenesVer = "almacenes.ver";
    public const string AlmacenesCrear = "almacenes.crear";
    public const string AlmacenesEditar = "almacenes.editar";

    public const string PaisesVer = "paises.ver";
    public const string PaisesCrear = "paises.crear";
    public const string PaisesEditar = "paises.editar";

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

        // Agregados 2026-09-14 al final a propósito (no intercalados con Ver/Crear de
        // arriba) — el seed de Permiso/RolPermiso usa la POSICIÓN en este array como Id
        // fijo (ver PermisoConfiguration), insertar en el medio desplazaría los Id de
        // todo lo que viene después y rompería el mapeo contra una base ya sembrada.
        (CategoriasEditar, "Categorías", "Editar categorías existentes"),
        (AreasEditar, "Áreas", "Editar áreas existentes"),

        // Agregado 2026-09-15, también al final por la misma razón que el bloque de arriba.
        (InicioVer, "Inicio", "Ver el panel de inicio con indicadores generales"),

        // Agregados 2026-09-16, también al final por el mismo motivo de arriba.
        (UnidadesVer, "Unidades", "Ver las unidades de medida"),
        (UnidadesCrear, "Unidades", "Crear unidades de medida"),
        (UnidadesEditar, "Unidades", "Editar unidades de medida existentes"),

        // Agregados 2026-09-18 (módulo País + Almacén), también al final por el mismo
        // motivo de arriba.
        (AlmacenesVer, "Almacenes", "Ver almacenes"),
        (AlmacenesCrear, "Almacenes", "Crear almacenes"),
        (AlmacenesEditar, "Almacenes", "Editar almacenes existentes"),

        (PaisesVer, "Países", "Ver países"),
        (PaisesCrear, "Países", "Crear países"),
        (PaisesEditar, "Países", "Editar países existentes"),
    ];
}
