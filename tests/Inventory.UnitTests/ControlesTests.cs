using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Controles;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Inventory.UnitTests;

// Controles de trazabilidad: toda entrada/salida contra una solicitud, y quien pide no aprueba
// ni entrega. Cada prueba arma los servicios con los controles que quiere verificar.
public class ControlesTests : IDisposable
{
    private const int PaisId = 1;

    private readonly SqliteConnection _connection;
    private readonly InventoryDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly UsuarioActuante _solicitante;
    private readonly UsuarioActuante _operador;
    private readonly int _productoId;
    private readonly int _ubicacionId;

    public ControlesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _auditoria = new AuditoriaService(_db);

        _solicitante = NuevoUsuario("solicitante@inventario.local", "Solicitante");
        _operador = NuevoUsuario("operador@inventario.local", "Operador");

        var categoria = new Categoria { CodigoCategoria = "CTL", PaisId = PaisId, Activo = true };
        var ubicacion = new Ubicacion { AlmacenId = 1, TipoUbicacion = TipoUbicacion.Rack, Nro = "01", Lado = "A", Nivel = "01", CodigoUbicacion = "A-01-01-CTL", Activo = true };
        var producto = new Producto { ClaveProducto = "CTL-1-UNI", CodigoProducto = "CTL-1", Nombre = "Producto de control", Categoria = categoria, PaisId = PaisId, UnidadMedida = "UNI", CostoUnitario = 1m, Activo = true };
        _db.AddRange(categoria, ubicacion, producto);
        _db.SaveChanges();
        _productoId = producto.Id;
        _ubicacionId = ubicacion.Id;

        // Stock inicial con un servicio SIN controles (el ajuste es la vía legítima para corregir).
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

    private MovimientoService Movimientos(bool exigirSolicitud = false, bool separacion = false) =>
        new(_db, _auditoria, Options.Create(new ControlesOptions { ExigirSolicitudEnMovimientos = exigirSolicitud, SeparacionDeFunciones = separacion }));

    private SolicitudService Solicitudes(bool separacion) =>
        new(_db, new LoggingSolicitudNotificationService(NullLogger<LoggingSolicitudNotificationService>.Instance),
            NullLogger<SolicitudService>.Instance, _auditoria,
            Options.Create(new ControlesOptions { ExigirSolicitudEnMovimientos = false, SeparacionDeFunciones = separacion }));

    // Una solicitud de salida del `_solicitante`, ya aprobada, con una línea de 5 unidades.
    private async Task<int> SolicitudAprobadaAsync()
    {
        var area = new Area { CodigoArea = "CTL" + Guid.NewGuid().ToString("N")[..4], NombreArea = "Área de control", PaisId = PaisId, Activo = true };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync();

        var solicitud = new Solicitud
        {
            AreaId = area.Id, Tipo = TipoSolicitud.Salida, Estado = EstadoSolicitud.Aprobada,
            SolicitadoPorId = _solicitante.Id, SolicitadoPorNombre = _solicitante.Nombre,
            NumeroSolicitud = "TMP-" + Guid.NewGuid().ToString("N"),
            Detalles = [new SolicitudDetalle { ProductoId = _productoId, CantidadSolicitada = 5, CantidadAprobada = 5 }],
        };
        _db.Solicitudes.Add(solicitud);
        await _db.SaveChangesAsync();
        return solicitud.Detalles.Single().Id;
    }

    private static RegistrarSalidaDto Salida(int productoId, int ubicacionId, int? detalleId) =>
        new(productoId, ubicacionId, 2, null, false, null, null, detalleId);

    [Fact]
    public async Task Salida_SinSolicitud_ConElControlActivo_SeRechaza()
    {
        var ex = await Assert.ThrowsAsync<ValidacionException>(() =>
            Movimientos(exigirSolicitud: true).RegistrarSalidaAsync(Salida(_productoId, _ubicacionId, null), PaisId, _operador, default));
        Assert.Contains("solicitud aprobada", ex.Message);
    }

    [Fact]
    public async Task Entrada_SinSolicitud_ConElControlActivo_SeRechaza()
    {
        await Assert.ThrowsAsync<ValidacionException>(() =>
            Movimientos(exigirSolicitud: true).RegistrarEntradaAsync(new RegistrarEntradaDto(_productoId, _ubicacionId, 3, null), PaisId, _operador, default));
    }

    [Fact]
    public async Task Salida_SinSolicitud_ConElControlApagado_SigueFuncionando()
    {
        var r = await Movimientos(exigirSolicitud: false).RegistrarSalidaAsync(Salida(_productoId, _ubicacionId, null), PaisId, _operador, default);
        Assert.Equal(48, r.ExistenciaResultante);
    }

    [Fact]
    public async Task Ajuste_NoNecesitaSolicitud_ElControlNoLoAfecta()
    {
        var r = await Movimientos(exigirSolicitud: true).RegistrarAjusteAsync(
            new RegistrarAjusteDto(_productoId, _ubicacionId, 4, false, "Corrección por conteo"), PaisId, _operador, default);
        Assert.Equal(46, r.ExistenciaResultante);
    }

    [Fact]
    public async Task Entrega_PorQuienPidio_ConSeparacionDeFunciones_SeRechaza()
    {
        var detalleId = await SolicitudAprobadaAsync();
        await Assert.ThrowsAsync<SeparacionDeFuncionesException>(() =>
            Movimientos(exigirSolicitud: true, separacion: true).RegistrarSalidaAsync(Salida(_productoId, _ubicacionId, detalleId), PaisId, _solicitante, default));

        // y nada cambió: el stock sigue igual
        var ok = await Movimientos().RegistrarAjusteAsync(new RegistrarAjusteDto(_productoId, _ubicacionId, 1, true, "verificación"), PaisId, _operador, default);
        Assert.Equal(51, ok.ExistenciaResultante);
    }

    [Fact]
    public async Task Entrega_PorOtraPersona_ConSeparacionDeFunciones_Funciona()
    {
        var detalleId = await SolicitudAprobadaAsync();
        var r = await Movimientos(exigirSolicitud: true, separacion: true)
            .RegistrarSalidaAsync(Salida(_productoId, _ubicacionId, detalleId), PaisId, _operador, default);
        Assert.Equal(48, r.ExistenciaResultante);
    }

    [Fact]
    public async Task Aprobar_LaPropiaSolicitud_ConSeparacionDeFunciones_SeRechaza_YOtraPersonaPuede()
    {
        var area = new Area { CodigoArea = "APR", NombreArea = "Área de aprobación", PaisId = PaisId, Activo = true };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync();
        var s = new Solicitud
        {
            AreaId = area.Id, Tipo = TipoSolicitud.Salida, Estado = EstadoSolicitud.Pendiente,
            SolicitadoPorId = _solicitante.Id, SolicitadoPorNombre = _solicitante.Nombre,
            NumeroSolicitud = "TMP-" + Guid.NewGuid().ToString("N"),
            Detalles = [new SolicitudDetalle { ProductoId = _productoId, CantidadSolicitada = 3 }],
        };
        _db.Solicitudes.Add(s);
        await _db.SaveChangesAsync();
        var linea = s.Detalles.Single().Id;
        var dto = new AprobarSolicitudDto([new AprobarSolicitudDetalleDto(linea, 3)]);

        await Assert.ThrowsAsync<SeparacionDeFuncionesException>(() => Solicitudes(separacion: true).AprobarAsync(s.Id, dto, PaisId, _solicitante, default));
        await Assert.ThrowsAsync<SeparacionDeFuncionesException>(() => Solicitudes(separacion: true).RechazarAsync(s.Id, new RechazarSolicitudDto("no"), PaisId, _solicitante, default));

        var aprobada = await Solicitudes(separacion: true).AprobarAsync(s.Id, dto, PaisId, _operador, default);
        Assert.Equal(EstadoSolicitud.Aprobada, aprobada.Estado);
    }
}
