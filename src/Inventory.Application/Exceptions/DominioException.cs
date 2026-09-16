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

public class SolicitudEstadoInvalidoException : DominioException
{
    public override int Status => 409;

    public SolicitudEstadoInvalidoException(string mensaje) : base(mensaje) { }
}

public class ConteoDuplicadoException : DominioException
{
    public override int Status => 409;

    public ConteoDuplicadoException(string sesion, int numeroConteo)
        : base($"Ya existe el conteo N.º {numeroConteo} de este producto/ubicación en la sesión '{sesion}'.") { }
}

public class CredencialesInvalidasException : DominioException
{
    public override int Status => 401;

    public CredencialesInvalidasException() : base("Email o contraseña incorrectos.") { }
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
