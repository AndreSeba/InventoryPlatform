namespace Inventory.Application.Exceptions;

// Excepción base para errores de negocio esperables (sección 12 de la propuesta):
// el handler global las traduce a una respuesta HTTP clara con .Status propio,
// nunca expone err.Message crudo de excepciones no controladas.
public abstract class DominioException : Exception
{
    public abstract int Status { get; }

    protected DominioException(string message) : base(message) { }
}

public class ProductoNoEncontradoException : DominioException
{
    public override int Status => 404;

    public ProductoNoEncontradoException(int id)
        : base($"No existe un producto activo con id {id}.") { }
}

public class CodigoProductoDuplicadoException : DominioException
{
    public override int Status => 409;

    public CodigoProductoDuplicadoException(string codigo)
        : base($"Ya existe un producto activo con el código '{codigo}'.") { }
}

public class StockInsuficienteException : DominioException
{
    public override int Status => 409;

    public StockInsuficienteException(string codigoProducto, decimal existencia, decimal cantidadSolicitada)
        : base($"Stock insuficiente para '{codigoProducto}': existencia {existencia}, se solicitó una salida de {cantidadSolicitada}.") { }
}

public class CategoriaNoEncontradaException : DominioException
{
    public override int Status => 404;

    public CategoriaNoEncontradaException(int id)
        : base($"No existe una categoría activa con id {id}.") { }
}

public class CodigoCategoriaDuplicadoException : DominioException
{
    public override int Status => 409;

    public CodigoCategoriaDuplicadoException(string codigo)
        : base($"Ya existe una categoría activa con el código '{codigo}'.") { }
}

public class SeleccionDeProductosVaciaException : DominioException
{
    public override int Status => 400;

    public SeleccionDeProductosVaciaException()
        : base("Seleccioná al menos un producto para la hoja de conteo. La hoja no se genera con todo el catálogo: un conteo físico se hace sobre un conjunto acotado de productos.") { }
}

public class UnidadNoEncontradaException : DominioException
{
    public override int Status => 404;

    public UnidadNoEncontradaException(int id)
        : base($"No existe una unidad de medida activa con id {id}.") { }

    public UnidadNoEncontradaException(string codigo)
        : base($"La unidad de medida '{codigo}' no existe o está inactiva. Dela de alta en Unidades antes de usarla.") { }
}

public class CodigoUnidadDuplicadoException : DominioException
{
    public override int Status => 409;

    public CodigoUnidadDuplicadoException(string codigo)
        : base($"Ya existe una unidad de medida activa con el código '{codigo}'.") { }
}

public class CodigoAreaDuplicadoException : DominioException
{
    public override int Status => 409;

    public CodigoAreaDuplicadoException(string codigo)
        : base($"Ya existe un área activa con el código '{codigo}'.") { }
}

public class AreaNoEncontradaException : DominioException
{
    public override int Status => 404;

    public AreaNoEncontradaException(int id) : base($"No existe un área con id {id}.") { }
}

public class UbicacionInvalidaException : DominioException
{
    public override int Status => 400;

    public UbicacionInvalidaException(string mensaje) : base(mensaje) { }
}

public class UbicacionNoEncontradaException : DominioException
{
    public override int Status => 404;

    public UbicacionNoEncontradaException(int id)
        : base($"No existe una ubicación activa con id {id}.") { }
}

public class UbicacionDuplicadaException : DominioException
{
    public override int Status => 409;

    public UbicacionDuplicadaException(string codigo)
        : base($"Ya existe una ubicación activa con el código '{codigo}'.") { }
}

public class MovimientoOrigenInvalidoException : DominioException
{
    public override int Status => 409;

    public MovimientoOrigenInvalidoException(string mensaje) : base(mensaje) { }
}

public class SolicitudNoEncontradaException : DominioException
{
    public override int Status => 404;

    public SolicitudNoEncontradaException(int id)
        : base($"No existe una solicitud con id {id}.") { }
}

public class SeparacionDeFuncionesException : DominioException
{
    public override int Status => 403;

    public SeparacionDeFuncionesException(string mensaje) : base(mensaje) { }
}

public class SolicitudEstadoInvalidoException : DominioException
{
    public override int Status => 409;

    public SolicitudEstadoInvalidoException(string mensaje) : base(mensaje) { }
}

public class ConteoNoEncontradoException : DominioException
{
    public override int Status => 404;

    public ConteoNoEncontradoException(int id)
        : base($"No existe un conteo con id {id}.") { }
}

public class ConteoEstadoInvalidoException : DominioException
{
    public override int Status => 409;

    public ConteoEstadoInvalidoException(string mensaje) : base(mensaje) { }
}

public class ConteoInvalidoException : DominioException
{
    public override int Status => 400;

    public ConteoInvalidoException(string mensaje) : base(mensaje) { }
}

public class NotificacionNoEncontradaException : DominioException
{
    public override int Status => 404;

    public NotificacionNoEncontradaException(long id) : base($"No existe una notificación con id {id}.") { }
}

public class AvisoDevolucionNoEncontradoException : DominioException
{
    public override int Status => 404;

    public AvisoDevolucionNoEncontradoException(int id)
        : base($"No existe un aviso de devolución con id {id}.") { }
}

public class AvisoDevolucionEstadoInvalidoException : DominioException
{
    public override int Status => 409;

    public AvisoDevolucionEstadoInvalidoException(string mensaje) : base(mensaje) { }
}

public class AvisoDevolucionNoPermitidoException : DominioException
{
    public override int Status => 403;

    public AvisoDevolucionNoPermitidoException(string mensaje) : base(mensaje) { }
}

public class RevisionAccesoNoEncontradaException : DominioException
{
    public override int Status => 404;

    public RevisionAccesoNoEncontradaException(int id)
        : base($"No existe una revisión de accesos con id {id}.") { }
}

public class RevisionAccesoEstadoInvalidoException : DominioException
{
    public override int Status => 409;

    public RevisionAccesoEstadoInvalidoException(string mensaje) : base(mensaje) { }
}

public class CredencialesInvalidasException : DominioException
{
    public override int Status => 401;

    public CredencialesInvalidasException() : base("Email o contraseña incorrectos.") { }
}

public class LoginBloqueadoException : DominioException
{
    public override int Status => 429;

    public LoginBloqueadoException(int minutos)
        : base($"Demasiados intentos fallidos. Esperá {minutos} minuto(s) e intentá de nuevo.") { }
}

public class UsuarioInactivoException : DominioException
{
    public override int Status => 403;

    public UsuarioInactivoException() : base("Este usuario está desactivado. Contactá a un administrador.") { }
}

public class EmailDuplicadoException : DominioException
{
    public override int Status => 409;

    public EmailDuplicadoException(string email) : base($"Ya existe un usuario con el email '{email}'.") { }
}

public class UsuarioNoEncontradoException : DominioException
{
    public override int Status => 404;

    public UsuarioNoEncontradoException(int id) : base($"No existe un usuario con id {id}.") { }
}

public class RolNoEncontradoException : DominioException
{
    public override int Status => 404;

    public RolNoEncontradoException(int id) : base($"No existe un rol activo con id {id}.") { }
}

public class NombreRolDuplicadoException : DominioException
{
    public override int Status => 409;

    public NombreRolDuplicadoException(string nombre) : base($"Ya existe un rol activo con el nombre '{nombre}'.") { }
}

public class PermisoInvalidoException : DominioException
{
    public override int Status => 400;

    public PermisoInvalidoException(string codigo) : base($"'{codigo}' no es un código de permiso válido.") { }
}

public class ArchivoInvalidoException : DominioException
{
    public override int Status => 400;

    public ArchivoInvalidoException(string mensaje) : base(mensaje) { }
}

public class PaisNoEncontradoException : DominioException
{
    public override int Status => 404;

    public PaisNoEncontradoException(int id) : base($"No existe un país activo con id {id}.") { }
}

public class CodigoPaisDuplicadoException : DominioException
{
    public override int Status => 409;

    public CodigoPaisDuplicadoException(string codigoIso)
        : base($"Ya existe un país activo con el código ISO '{codigoIso}'.") { }
}

public class AlmacenNoEncontradoException : DominioException
{
    public override int Status => 404;

    public AlmacenNoEncontradoException(int id) : base($"No existe un almacén activo con id {id}.") { }
}

public class CodigoAlmacenDuplicadoException : DominioException
{
    public override int Status => 409;

    public CodigoAlmacenDuplicadoException(string codigo)
        : base($"Ya existe un almacén activo con el código '{codigo}'.") { }
}

public class AlmacenInvalidoException : DominioException
{
    public override int Status => 400;

    public AlmacenInvalidoException(string mensaje) : base(mensaje) { }
}

// Dato de entrada inválido (campo vacío, fuera de rango, demasiado largo…): 400 con un
// mensaje pensado para mostrarse. Preferirla a ArgumentException en los servicios.
public class ValidacionException : DominioException
{
    public override int Status => 400;

    public ValidacionException(string mensaje) : base(mensaje) { }
}

// ---- Conteo Externo (módulo independiente, ver CLAUDE.md) ----

public class ConteoExternoUnidadNoEncontradaException : DominioException
{
    public override int Status => 404;

    public ConteoExternoUnidadNoEncontradaException(int id)
        : base($"No existe una unidad (conteo externo) activa con id {id}.") { }

    public ConteoExternoUnidadNoEncontradaException(string codigo)
        : base($"La unidad (conteo externo) '{codigo}' no existe o está inactiva. Dala de alta antes de usarla.") { }
}

public class CodigoConteoExternoUnidadDuplicadoException : DominioException
{
    public override int Status => 409;

    public CodigoConteoExternoUnidadDuplicadoException(string codigo)
        : base($"Ya existe una unidad (conteo externo) activa con el código '{codigo}'.") { }
}

public class ConteoExternoProductoNoEncontradoException : DominioException
{
    public override int Status => 404;

    public ConteoExternoProductoNoEncontradoException(int id)
        : base($"No existe un producto (conteo externo) activo con id {id}.") { }
}

public class CodigoConteoExternoProductoDuplicadoException : DominioException
{
    public override int Status => 409;

    public CodigoConteoExternoProductoDuplicadoException(string codigo)
        : base($"Ya existe un producto (conteo externo) activo con el código '{codigo}' (comparado sin ceros a la izquierda).") { }
}

public class ConteoExternoExistenciaNoEncontradaException : DominioException
{
    public override int Status => 404;

    public ConteoExternoExistenciaNoEncontradaException(int id)
        : base($"No existe una existencia (conteo externo) con id {id}.") { }
}

public class UnidadConteoExternoProductoYaFijadaException : DominioException
{
    public override int Status => 409;

    public UnidadConteoExternoProductoYaFijadaException(string codigoProducto, string unidadFijada)
        : base($"El producto (conteo externo) '{codigoProducto}' ya tiene fijada la unidad '{unidadFijada}' (se fija con el primer lote de existencia y no se puede cambiar).") { }
}

public class ConteoExternoSesionNoEncontradaException : DominioException
{
    public override int Status => 404;

    public ConteoExternoSesionNoEncontradaException(int id)
        : base($"No existe una sesión de conteo (conteo externo) con id {id}.") { }
}

public class ConteoExternoLineaNoEncontradaException : DominioException
{
    public override int Status => 404;

    public ConteoExternoLineaNoEncontradaException(int id)
        : base($"No existe una línea de conteo (conteo externo) con id {id}.") { }
}

public class ConteoExternoSesionCerradaException : DominioException
{
    public override int Status => 409;

    public ConteoExternoSesionCerradaException()
        : base("Esta sesión de conteo (conteo externo) ya está cerrada y no se puede modificar.") { }
}

public class ConteoExternoSesionSinExistenciasException : DominioException
{
    public override int Status => 400;

    public ConteoExternoSesionSinExistenciasException()
        : base("No hay existencias (conteo externo) que coincidan con esos filtros — cargá existencias antes de crear la sesión.") { }
}

public class ConteoExternoAlmacenNoEncontradoException : DominioException
{
    public override int Status => 404;

    public ConteoExternoAlmacenNoEncontradoException(int id)
        : base($"No existe un almacén (conteo externo) activo con id {id}.") { }
}

public class CodigoConteoExternoAlmacenDuplicadoException : DominioException
{
    public override int Status => 409;

    public CodigoConteoExternoAlmacenDuplicadoException(string codigo)
        : base($"Ya existe un almacén (conteo externo) activo con el código '{codigo}'.") { }
}
