using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Controles;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Inventory.UnitTests;

// Devoluciones con aviso: el solicitante avisa que devuelve, el operario registra la entrada contra el aviso,
// y el solicitante ve lo que tiene prestado a su nombre.
public class DevolucionTests : IDisposable
{
    private const int PaisId = 1;

    private readonly SqliteConnection _connection;
    private readonly InventoryDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly UsuarioActuante _solicitante;
    private readonly UsuarioActuante _otro;
    private readonly UsuarioActuante _operador;
    private readonly int _productoId;
    private readonly int _ubicacionId;

    public DevolucionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _auditoria = new AuditoriaService(_db);

        _solicitante = NuevoUsuario("solicitante@inventario.local", "Solicitante");
        _otro = NuevoUsuario("otro@inventario.local", "Otra Persona");
        _operador = NuevoUsuario("operador@inventario.local", "Operador");

        var categoria = new Categoria { CodigoCategoria = "DEV", PaisId = PaisId, Activo = true };
        var ubicacion = new Ubicacion { AlmacenId = 1, TipoUbicacion = TipoUbicacion.Rack, Nro = "01", Lado = "A", Nivel = "01", CodigoUbicacion = "A-01-01-DEV", Activo = true };
        var producto = new Producto { ClaveProducto = "DEV-1-UNI", CodigoProducto = "DEV-1", Nombre = "Banner de evento", Categoria = categoria, PaisId = PaisId, UnidadMedida = "UNI", CostoUnitario = 1m, Activo = true };
        _db.AddRange(categoria, ubicacion, producto);
        _db.SaveChanges();
        _productoId = producto.Id;
        _ubicacionId = ubicacion.Id;

        new MovimientoService(_db, _auditoria).RegistrarAjusteAsync(
            new RegistrarAjusteDto(_productoId, _ubicacionId, 50, true, "Stock inicial de prueba"), PaisId, _operador, default).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private UsuarioActuante NuevoUsuario(string email, string nombre)
    {
        var u = new Usuario { Email = email, NombreCompleto = nombre, PasswordHash = "x", RolId = 1, PaisId = PaisId, Activo = true };
        _db.Usuarios.Add(u);
        _db.SaveChanges();
        return new UsuarioActuante(u.Id, nombre);
    }

    private MovimientoService Movimientos(bool exigirAviso = true, bool separacion = true) =>
        new(_db, _auditoria, Options.Create(new ControlesOptions
        {
            ExigirSolicitudEnMovimientos = false, SeparacionDeFunciones = separacion, ExigirAvisoEnDevoluciones = exigirAviso,
        }));

    private DevolucionService Devoluciones() => new(_db, _auditoria);

    private static DateOnly Futuro => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);

    // Un préstamo pedido por `_solicitante` y entregado por `_operador`: devuelve el id de la Salida.
    private async Task<int> NuevoPrestamoAsync(int cantidad)
    {
        var area = new Area { CodigoArea = "DEV" + Guid.NewGuid().ToString("N")[..4], NombreArea = "Área de prueba", PaisId = PaisId, Activo = true };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync();

        var solicitud = new Solicitud
        {
            AreaId = area.Id, Tipo = TipoSolicitud.Salida, Estado = EstadoSolicitud.Aprobada,
            SolicitadoPorId = _solicitante.Id, SolicitadoPorNombre = _solicitante.Nombre,
            NumeroSolicitud = "TMP-" + Guid.NewGuid().ToString("N"),
            Detalles = [new SolicitudDetalle { ProductoId = _productoId, CantidadSolicitada = cantidad, CantidadAprobada = cantidad, Retorna = true, UbicacionExterna = "Evento", FechaRetornoEsperada = Futuro }],
        };
        _db.Solicitudes.Add(solicitud);
        await _db.SaveChangesAsync();

        var salida = await Movimientos().RegistrarSalidaAsync(
            new RegistrarSalidaDto(_productoId, _ubicacionId, cantidad, null, true, "Evento", Futuro, solicitud.Detalles.Single().Id), PaisId, _operador, default);
        return salida.MovimientoId;
    }

    private Task<AvisoDevolucionDto> Avisar(int origenId, int cantidad, UsuarioActuante? quien = null) =>
        Devoluciones().AvisarAsync(new CrearAvisoDevolucionDto(origenId, cantidad, null), PaisId, quien ?? _solicitante, default);

    private Task<MovimientoResultadoDto> Recibir(int origenId, int cantidad, int? avisoId, UsuarioActuante? quien = null, MovimientoService? servicio = null) =>
        (servicio ?? Movimientos()).RegistrarDevolucionAsync(
            new RegistrarDevolucionDto(origenId, _ubicacionId, cantidad, "Devolución de préstamo", avisoId), PaisId, quien ?? _operador, default);

    [Fact]
    public async Task Prestamos_MuestranAQuienLosPidio_YSoloSusPrestamosEnMisPrestamos()
    {
        var origen = await NuevoPrestamoAsync(5);

        var todos = await Devoluciones().ListarPrestamosAsync(PaisId, null, default);
        var p = Assert.Single(todos);
        Assert.Equal(origen, p.MovimientoId);
        Assert.Equal(_solicitante.Id, p.SolicitadoPorId);
        Assert.Equal("Solicitante", p.SolicitadoPor);
        Assert.Equal(5, p.Pendiente);
        Assert.Equal(0, p.EnAviso);
        Assert.NotNull(p.NumeroSolicitud);

        Assert.Single(await Devoluciones().ListarPrestamosAsync(PaisId, _solicitante.Id, default));
        Assert.Empty(await Devoluciones().ListarPrestamosAsync(PaisId, _otro.Id, default));
    }

    [Fact]
    public async Task Avisar_SoloPuedeQuienPidioElMaterial()
    {
        var origen = await NuevoPrestamoAsync(5);

        await Assert.ThrowsAsync<AvisoDevolucionNoPermitidoException>(() => Avisar(origen, 2, _otro));
        await Assert.ThrowsAsync<AvisoDevolucionNoPermitidoException>(() => Avisar(origen, 2, _operador));

        var aviso = await Avisar(origen, 2);
        Assert.Equal(EstadoAvisoDevolucion.Pendiente, aviso.Estado);
        Assert.StartsWith("DEV-", aviso.Codigo);
        Assert.Equal("Solicitante", aviso.AvisadoPor);

        var prestamo = Assert.Single(await Devoluciones().ListarPrestamosAsync(PaisId, _solicitante.Id, default));
        Assert.Equal(2, prestamo.EnAviso);
        Assert.Equal(5, prestamo.Pendiente); // sigue prestado hasta que el operario lo recibe
    }

    [Fact]
    public async Task Avisar_NoPuedePrometerMasDeLoQueQueda_NiEntreVariosAvisos()
    {
        var origen = await NuevoPrestamoAsync(5);

        await Assert.ThrowsAsync<ValidacionException>(() => Avisar(origen, 6));
        await Assert.ThrowsAsync<ValidacionException>(() => Avisar(origen, 0));

        await Avisar(origen, 3);
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => Avisar(origen, 3));
        Assert.Contains("Solo quedan 2", ex.Message);
        await Avisar(origen, 2);
        await Assert.ThrowsAsync<ValidacionException>(() => Avisar(origen, 1));
    }

    [Fact]
    public async Task Devolucion_SinAviso_ConElControlActivo_SeRechaza_YNoMueveStock()
    {
        var origen = await NuevoPrestamoAsync(5);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => Recibir(origen, 5, null));
        Assert.Contains("aviso", ex.Message);

        var prestamo = Assert.Single(await Devoluciones().ListarPrestamosAsync(PaisId, null, default));
        Assert.Equal(5, prestamo.Pendiente);
    }

    [Fact]
    public async Task Devolucion_SinAviso_ConElControlApagado_SigueFuncionando()
    {
        var origen = await NuevoPrestamoAsync(5);
        await Recibir(origen, 5, null, servicio: Movimientos(exigirAviso: false));
        Assert.Empty(await Devoluciones().ListarPrestamosAsync(PaisId, null, default));
    }

    [Fact]
    public async Task Recibir_ContraElAviso_CreaLaEntrada_CierraElAviso_YDescuentaElPrestamo()
    {
        var origen = await NuevoPrestamoAsync(5);
        var aviso = await Avisar(origen, 5);

        var r = await Recibir(origen, 5, aviso.Id);
        Assert.Equal(50, r.ExistenciaResultante);

        var avisos = await Devoluciones().ListarAvisosAsync(PaisId, _solicitante.Id, null, default);
        var cerrado = Assert.Single(avisos);
        Assert.Equal(EstadoAvisoDevolucion.Recibido, cerrado.Estado);
        Assert.Equal(5, cerrado.CantidadRecibida);
        Assert.Equal("Operador", cerrado.ResueltoPor);
        Assert.Equal(r.NumeroMovimiento, cerrado.NumeroMovimientoDevolucion);

        Assert.Empty(await Devoluciones().ListarPrestamosAsync(PaisId, _solicitante.Id, default));

        // el mismo aviso no se recibe dos veces
        await Assert.ThrowsAsync<AvisoDevolucionEstadoInvalidoException>(() => Recibir(origen, 1, aviso.Id));
    }

    [Fact]
    public async Task Recibir_UnaParte_CierraElAviso_YElResto_SigueComoPrestamoPendiente()
    {
        var origen = await NuevoPrestamoAsync(5);
        var aviso = await Avisar(origen, 5);

        await Recibir(origen, 3, aviso.Id);

        var prestamo = Assert.Single(await Devoluciones().ListarPrestamosAsync(PaisId, _solicitante.Id, default));
        Assert.Equal(2, prestamo.Pendiente);
        Assert.Equal(0, prestamo.EnAviso);
        await Avisar(origen, 2); // y se puede avisar lo que falta
    }

    [Fact]
    public async Task Recibir_MasDeLoAvisado_OContraElAvisoDeOtroPrestamo_SeRechaza()
    {
        var origen = await NuevoPrestamoAsync(5);
        var otroPrestamo = await NuevoPrestamoAsync(4);
        var aviso = await Avisar(origen, 2);

        await Assert.ThrowsAsync<ValidacionException>(() => Recibir(origen, 3, aviso.Id));
        await Assert.ThrowsAsync<MovimientoOrigenInvalidoException>(() => Recibir(otroPrestamo, 2, aviso.Id));
    }

    [Fact]
    public async Task Recibir_PorQuienAviso_ConSeparacionDeFunciones_SeRechaza_YSinSeparacionSePermite()
    {
        var origen = await NuevoPrestamoAsync(5);
        var aviso = await Avisar(origen, 5);

        await Assert.ThrowsAsync<SeparacionDeFuncionesException>(() => Recibir(origen, 5, aviso.Id, _solicitante));

        await Recibir(origen, 5, aviso.Id, _solicitante, Movimientos(separacion: false));
        Assert.Empty(await Devoluciones().ListarPrestamosAsync(PaisId, null, default));
    }

    [Fact]
    public async Task Cancelar_SoloQuienAviso_MientrasEstePendiente()
    {
        var origen = await NuevoPrestamoAsync(5);
        var aviso = await Avisar(origen, 5);

        await Assert.ThrowsAsync<AvisoDevolucionNoPermitidoException>(() =>
            Devoluciones().CancelarAvisoAsync(aviso.Id, new CancelarAvisoDevolucionDto("no corresponde"), PaisId, _operador, default));
        await Assert.ThrowsAsync<ValidacionException>(() =>
            Devoluciones().CancelarAvisoAsync(aviso.Id, new CancelarAvisoDevolucionDto(" "), PaisId, _solicitante, default));

        var cancelado = await Devoluciones().CancelarAvisoAsync(aviso.Id, new CancelarAvisoDevolucionDto("Me equivoqué de cantidad"), PaisId, _solicitante, default);
        Assert.Equal(EstadoAvisoDevolucion.Cancelado, cancelado.Estado);

        // un aviso cancelado ya no se recibe, y libera la cantidad para avisar de nuevo
        await Assert.ThrowsAsync<AvisoDevolucionEstadoInvalidoException>(() => Recibir(origen, 5, aviso.Id));
        await Avisar(origen, 5);
    }

    [Fact]
    public async Task Cancelar_UnAvisoYaRecibido_SeRechaza()
    {
        var origen = await NuevoPrestamoAsync(5);
        var aviso = await Avisar(origen, 5);
        await Recibir(origen, 5, aviso.Id);

        await Assert.ThrowsAsync<AvisoDevolucionEstadoInvalidoException>(() =>
            Devoluciones().CancelarAvisoAsync(aviso.Id, new CancelarAvisoDevolucionDto("tarde"), PaisId, _solicitante, default));
    }

    [Fact]
    public async Task PrestamoSinSolicitud_NoTieneAQuienAvisar_YElOperarioLoDevuelveDirecto()
    {
        var salida = await Movimientos().RegistrarSalidaAsync(
            new RegistrarSalidaDto(_productoId, _ubicacionId, 2, null, true, "Taller externo", Futuro, null), PaisId, _operador, default);

        var prestamo = Assert.Single(await Devoluciones().ListarPrestamosAsync(PaisId, null, default));
        Assert.Null(prestamo.SolicitadoPorId);

        await Assert.ThrowsAsync<AvisoDevolucionNoPermitidoException>(() => Avisar(salida.MovimientoId, 2));

        await Recibir(salida.MovimientoId, 2, null); // con el control activo igual pasa: no hay a quién avisar
        Assert.Empty(await Devoluciones().ListarPrestamosAsync(PaisId, null, default));
    }
}
