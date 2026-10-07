using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Controles;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Inventory.UnitTests;

// La campanita: las notificaciones se generan del estado real del sistema, por persona, y se cierran solas.
// Roles sembrados: 1 Administrador (todos los permisos), 2 Operador, 3 Consulta, 4 Solicitante.
public class NotificacionTests : IDisposable
{
    private const int PaisId = 1;

    private readonly SqliteConnection _connection;
    private readonly InventoryDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly NotificacionService _notificaciones;

    private readonly UsuarioActuante _admin;
    private readonly UsuarioActuante _operador;
    private readonly UsuarioActuante _consulta;
    private readonly UsuarioActuante _solicitante;
    private readonly int _productoId;
    private readonly int _ubicacionId;

    public NotificacionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _auditoria = new AuditoriaService(_db);

        _admin = NuevoUsuario("admin@t.local", "Ada Admin", 1);
        _operador = NuevoUsuario("op@t.local", "Oscar Operador", 2);
        _consulta = NuevoUsuario("con@t.local", "Cora Consulta", 3);
        _solicitante = NuevoUsuario("sol@t.local", "Sofía Solicitante", 4);

        var categoria = new Categoria { CodigoCategoria = "NOT", PaisId = PaisId, Activo = true };
        var ubicacion = new Ubicacion { AlmacenId = 1, TipoUbicacion = TipoUbicacion.Rack, Nro = "01", Lado = "A", Nivel = "01", CodigoUbicacion = "A-01-01-NOT", Activo = true };
        var producto = new Producto { ClaveProducto = "NOT-1-UNI", CodigoProducto = "NOT-1", Nombre = "Banner", Categoria = categoria, PaisId = PaisId, UnidadMedida = "UNI", CostoUnitario = 1m, StockMinimo = 0, Activo = true };
        _db.AddRange(categoria, ubicacion, producto);
        _db.SaveChanges();
        _productoId = producto.Id;
        _ubicacionId = ubicacion.Id;
        Movimientos().RegistrarAjusteAsync(new RegistrarAjusteDto(_productoId, _ubicacionId, 50, true, "Stock inicial"), PaisId, _operador, default).GetAwaiter().GetResult();

        var accesos = new RevisionAccesoService(_db, _auditoria, _cache, Options.Create(new ControlesOptions()));
        _notificaciones = new NotificacionService(_db, _cache, new DevolucionService(_db, _auditoria), accesos);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        _cache.Dispose();
    }

    private UsuarioActuante NuevoUsuario(string email, string nombre, int rolId, DateTime? creadoEn = null)
    {
        var u = new Usuario { Email = email, NombreCompleto = nombre, PasswordHash = "x", RolId = rolId, PaisId = PaisId, Activo = true, CreadoEn = creadoEn ?? DateTime.UtcNow };
        _db.Usuarios.Add(u);
        _db.SaveChanges();
        return new UsuarioActuante(u.Id, nombre);
    }

    private MovimientoService Movimientos() =>
        new(_db, _auditoria, Options.Create(new ControlesOptions { ExigirSolicitudEnMovimientos = false, SeparacionDeFunciones = true, ExigirAvisoEnDevoluciones = true }));

    private async Task<IReadOnlyList<NotificacionDto>> De(UsuarioActuante u)
    {
        await _notificaciones.SincronizarAsync(PaisId, default);
        return (await _notificaciones.ListarAsync(u.Id, PaisId, default)).Items;
    }

    private async Task<Solicitud> NuevaSolicitudAsync(EstadoSolicitud estado, bool conPrestamo = false)
    {
        var area = new Area { CodigoArea = "N" + Guid.NewGuid().ToString("N")[..5], NombreArea = "Marketing", PaisId = PaisId, Activo = true };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync();

        var solicitud = new Solicitud
        {
            AreaId = area.Id, Tipo = TipoSolicitud.Salida, Estado = estado,
            SolicitadoPorId = _solicitante.Id, SolicitadoPorNombre = _solicitante.Nombre,
            NumeroSolicitud = "SOL-T-" + Guid.NewGuid().ToString("N")[..6],
            Detalles = [new SolicitudDetalle
            {
                ProductoId = _productoId, CantidadSolicitada = 5, CantidadAprobada = estado == EstadoSolicitud.Pendiente ? null : 5,
                Retorna = conPrestamo, UbicacionExterna = conPrestamo ? "Evento" : null,
                FechaRetornoEsperada = conPrestamo ? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5) : null,
            }],
        };
        if (estado is EstadoSolicitud.Aprobada or EstadoSolicitud.Rechazada)
        {
            solicitud.AprobadoPorId = _admin.Id;
            solicitud.AprobadoPorNombre = _admin.Nombre;
            solicitud.FechaResolucion = DateTime.UtcNow;
            if (estado == EstadoSolicitud.Rechazada) solicitud.MotivoRechazo = "Sin presupuesto";
        }
        _db.Solicitudes.Add(solicitud);
        await _db.SaveChangesAsync();
        return solicitud;
    }

    // Préstamo entregado por el operario a lo que pidió `_solicitante`: devuelve el id de la Salida.
    private async Task<int> NuevoPrestamoAsync(int cantidad)
    {
        var solicitud = await NuevaSolicitudAsync(EstadoSolicitud.Aprobada, conPrestamo: true);
        var detalle = solicitud.Detalles.Single();
        detalle.CantidadSolicitada = cantidad; detalle.CantidadAprobada = cantidad;
        await _db.SaveChangesAsync();
        var r = await Movimientos().RegistrarSalidaAsync(
            new RegistrarSalidaDto(_productoId, _ubicacionId, cantidad, null, true, "Evento", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), detalle.Id), PaisId, _operador, default);
        return r.MovimientoId;
    }

    [Fact]
    public async Task SolicitudPendiente_AvisaALosQueAprueban_YSeCierraSolaAlResolverse()
    {
        var s = await NuevaSolicitudAsync(EstadoSolicitud.Pendiente);

        var admin = await De(_admin);
        var aviso = Assert.Single(admin, n => n.Categoria == CategoriaNotificacion.Pendiente && n.Titulo == "Solicitud por aprobar");
        Assert.Contains(s.NumeroSolicitud, aviso.Mensaje);
        Assert.Equal($"/solicitudes/{s.Id}", aviso.Url);
        Assert.DoesNotContain(await De(_solicitante), n => n.Titulo == "Solicitud por aprobar"); // quien pide no aprueba
        Assert.DoesNotContain(await De(_operador), n => n.Titulo == "Solicitud por aprobar");    // el operario no tiene solicitudes.aprobar

        s.Estado = EstadoSolicitud.Aprobada;
        s.AprobadoPorId = _admin.Id; s.AprobadoPorNombre = _admin.Nombre; s.FechaResolucion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Assert.DoesNotContain(await De(_admin), n => n.Titulo == "Solicitud por aprobar");
        Assert.Contains(await De(_solicitante), n => n.Categoria == CategoriaNotificacion.Resultado && n.Titulo == "Tu solicitud fue aprobada");
        Assert.Contains(await De(_operador), n => n.Titulo == "Solicitud por entregar");
    }

    [Fact]
    public async Task SolicitudRechazada_AvisaAlSolicitanteConElMotivo()
    {
        await NuevaSolicitudAsync(EstadoSolicitud.Rechazada);

        var n = Assert.Single(await De(_solicitante), x => x.Titulo == "Tu solicitud fue rechazada");
        Assert.Equal(SeveridadNotificacion.Alerta, n.Severidad);
        Assert.Contains("Sin presupuesto", n.Mensaje);
        Assert.DoesNotContain(await De(_operador), x => x.Titulo.StartsWith("Tu solicitud"));
    }

    [Fact]
    public async Task AvisoDeDevolucion_AvisaAlOperario_YAlRecibirseNotificaAlSolicitante()
    {
        var origen = await NuevoPrestamoAsync(5);
        var devoluciones = new DevolucionService(_db, _auditoria);
        var aviso = await devoluciones.AvisarAsync(new CrearAvisoDevolucionDto(origen, 5, null), PaisId, _solicitante, default);

        Assert.Contains(await De(_operador), n => n.Titulo == "Devolución por recibir" && n.Mensaje.Contains(aviso.Codigo));
        Assert.DoesNotContain(await De(_solicitante), n => n.Titulo == "Devolución por recibir");

        await Movimientos().RegistrarDevolucionAsync(new RegistrarDevolucionDto(origen, _ubicacionId, 5, "Devolución", aviso.Id), PaisId, _operador, default);

        Assert.DoesNotContain(await De(_operador), n => n.Titulo == "Devolución por recibir");
        var recibida = Assert.Single(await De(_solicitante), n => n.Titulo == "Tu devolución fue recibida");
        Assert.Contains("Oscar Operador", recibida.Mensaje);
        Assert.Equal("/prestamos/mios", recibida.Url);
    }

    [Fact]
    public async Task Leidas_SeMarcanPorPersona_YNadieMarcaLasDeOtro()
    {
        await NuevaSolicitudAsync(EstadoSolicitud.Rechazada);
        await NuevaSolicitudAsync(EstadoSolicitud.Aprobada);
        await De(_solicitante);

        var antes = await _notificaciones.ListarAsync(_solicitante.Id, PaisId, default);
        Assert.Equal(2, antes.NoLeidas);

        await _notificaciones.MarcarLeidaAsync(antes.Items[0].Id, _solicitante.Id, default);
        var despues = await _notificaciones.ListarAsync(_solicitante.Id, PaisId, default);
        Assert.Equal(1, despues.NoLeidas);
        Assert.Single(despues.Items, n => n.Leida);

        // la notificación es de otra persona: no existe para el operario
        await Assert.ThrowsAsync<NotificacionNoEncontradaException>(() => _notificaciones.MarcarLeidaAsync(antes.Items[0].Id, _operador.Id, default));

        await _notificaciones.MarcarTodasLeidasAsync(_solicitante.Id, PaisId, default);
        Assert.Equal(0, (await _notificaciones.ListarAsync(_solicitante.Id, PaisId, default)).NoLeidas);
    }

    [Fact]
    public async Task Sincronizar_VariasVeces_NoDuplicaNada()
    {
        await NuevaSolicitudAsync(EstadoSolicitud.Pendiente);

        var primera = await De(_admin);
        var segunda = await De(_admin);
        var tercera = await De(_admin);

        Assert.Equal(primera.Count, segunda.Count);
        Assert.Equal(primera.Count, tercera.Count);
        Assert.Single(tercera, n => n.Titulo == "Solicitud por aprobar");
    }

    [Fact]
    public async Task ProductosSinStock_AvisaAQuienVeMovimientos_YSeCierraAlReponer()
    {
        // el producto de la prueba tiene 50; uno nuevo sin movimientos queda sin stock
        var otro = new Producto { ClaveProducto = "NOT-2-UNI", CodigoProducto = "NOT-2", Nombre = "Sin stock", CategoriaId = (await _db.Categorias.FirstAsync()).Id, PaisId = PaisId, UnidadMedida = "UNI", CostoUnitario = 1m, Activo = true };
        _db.Productos.Add(otro);
        await _db.SaveChangesAsync();

        var n = Assert.Single(await De(_operador), x => x.Titulo == "Productos sin stock");
        Assert.Contains("1 producto", n.Mensaje);
        Assert.Contains(await De(_consulta), x => x.Titulo == "Productos sin stock");
        Assert.DoesNotContain(await De(_solicitante), x => x.Titulo == "Productos sin stock"); // no ve movimientos

        await Movimientos().RegistrarAjusteAsync(new RegistrarAjusteDto(otro.Id, _ubicacionId, 10, true, "Reposición"), PaisId, _operador, default);
        Assert.DoesNotContain(await De(_operador), x => x.Titulo == "Productos sin stock");
    }

    [Fact]
    public async Task UnaAlertaAgregadaLeida_VuelveASinLeer_CuandoElNumeroSube()
    {
        var categoria = (await _db.Categorias.FirstAsync()).Id;
        _db.Productos.Add(new Producto { ClaveProducto = "NOT-3-UNI", CodigoProducto = "NOT-3", Nombre = "Uno", CategoriaId = categoria, PaisId = PaisId, UnidadMedida = "UNI", CostoUnitario = 1m, Activo = true });
        await _db.SaveChangesAsync();

        var primera = Assert.Single(await De(_operador), x => x.Titulo == "Productos sin stock");
        await _notificaciones.MarcarLeidaAsync(primera.Id, _operador.Id, default);
        Assert.True(Assert.Single(await De(_operador), x => x.Titulo == "Productos sin stock").Leida);

        _db.Productos.Add(new Producto { ClaveProducto = "NOT-4-UNI", CodigoProducto = "NOT-4", Nombre = "Dos", CategoriaId = categoria, PaisId = PaisId, UnidadMedida = "UNI", CostoUnitario = 1m, Activo = true });
        await _db.SaveChangesAsync();

        var segunda = Assert.Single(await De(_operador), x => x.Titulo == "Productos sin stock");
        Assert.False(segunda.Leida);
        Assert.Contains("2 producto", segunda.Mensaje);
    }

    [Fact]
    public async Task Prestamo_VencidoYPorVencer_AvisanAlSolicitante_YElTotalAlOperario()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var vencido = await NuevoPrestamoAsync(2);
        var porVencer = await NuevoPrestamoAsync(2);
        await _db.Movimientos.Where(m => m.Id == vencido).ExecuteUpdateAsync(u => u.SetProperty(m => m.FechaRetornoEsperada, hoy.AddDays(-3)));
        await _db.Movimientos.Where(m => m.Id == porVencer).ExecuteUpdateAsync(u => u.SetProperty(m => m.FechaRetornoEsperada, hoy.AddDays(1)));

        var suyas = await De(_solicitante);
        var mora = Assert.Single(suyas, n => n.Titulo == "Préstamo vencido");
        Assert.Equal(SeveridadNotificacion.Alerta, mora.Severidad);
        Assert.Contains("3 día(s) de mora", mora.Mensaje);
        Assert.Single(suyas, n => n.Titulo == "Préstamo por vencer" && n.Mensaje.Contains("mañana"));

        var total = Assert.Single(await De(_operador), n => n.Titulo == "Préstamos vencidos");
        Assert.Contains("1 préstamo", total.Mensaje);
        Assert.DoesNotContain(await De(_solicitante), n => n.Titulo == "Préstamos vencidos");

        // al devolverse el vencido, deja de avisar
        var devoluciones = new DevolucionService(_db, _auditoria);
        var aviso = await devoluciones.AvisarAsync(new CrearAvisoDevolucionDto(vencido, 2, null), PaisId, _solicitante, default);
        await Movimientos().RegistrarDevolucionAsync(new RegistrarDevolucionDto(vencido, _ubicacionId, 2, "Devolución", aviso.Id), PaisId, _operador, default);
        Assert.DoesNotContain(await De(_solicitante), n => n.Titulo == "Préstamo vencido");
        Assert.DoesNotContain(await De(_operador), n => n.Titulo == "Préstamos vencidos");
    }

    [Fact]
    public async Task Control_AvisaDeRevisionesVencidas_YDeCuentasQueNuncaIngresaron()
    {
        NuevoUsuario("vieja@t.local", "Cuenta Vieja", 3, creadoEn: DateTime.UtcNow.AddDays(-30));

        var admin = await De(_admin);
        Assert.Contains(admin, n => n.Categoria == CategoriaNotificacion.Control && n.Titulo.StartsWith("Revisión de accesos de administradores"));
        Assert.Contains(admin, n => n.Titulo.StartsWith("Revisión de accesos de todas"));
        var cuentas = Assert.Single(admin, n => n.Titulo == "Cuentas que nunca ingresaron");
        Assert.Contains("1 cuenta", cuentas.Mensaje);

        // las cuentas nuevas (creadas hoy) no cuentan, y nadie sin el permiso ve estas alertas
        Assert.DoesNotContain(await De(_consulta), n => n.Categoria == CategoriaNotificacion.Control);
    }

    [Fact]
    public async Task RevisionDeAccesosApagada_NoGeneraAvisosDeEsaRevision()
    {
        var accesos = new RevisionAccesoService(_db, _auditoria, _cache, Options.Create(new ControlesOptions()));
        var apagado = new NotificacionService(_db, _cache, new DevolucionService(_db, _auditoria), accesos, null,
            Options.Create(new FuncionesOptions { RevisionAccesos = false }));

        // con el módulo encendido (por defecto en las pruebas) el administrador sí recibe los avisos...
        await _notificaciones.SincronizarAsync(PaisId, default);
        Assert.Contains((await _notificaciones.ListarAsync(_admin.Id, PaisId, default)).Items, n => n.Titulo.StartsWith("Revisión de accesos"));

        // ...y apagado se resuelven solos y no se vuelven a generar
        await apagado.SincronizarAsync(PaisId, default);
        Assert.DoesNotContain((await apagado.ListarAsync(_admin.Id, PaisId, default)).Items, n => n.Titulo.StartsWith("Revisión de accesos"));
    }

    private sealed class CorreoFalso : Inventory.Application.Interfaces.ICorreoSaliente
    {
        public List<Inventory.Application.Interfaces.MensajeCorreo> Enviados { get; } = [];
        public void Encolar(Inventory.Application.Interfaces.MensajeCorreo m) => Enviados.Add(m);
    }

    [Fact]
    public async Task Correo_SoloAvisaDeNoticiasPersonalesNuevas_UnaSolaVez()
    {
        var correo = new CorreoFalso();
        var accesos = new RevisionAccesoService(_db, _auditoria, _cache, Options.Create(new ControlesOptions()));
        var servicio = new NotificacionService(_db, _cache, new DevolucionService(_db, _auditoria), accesos, null, null, correo);

        var s = await NuevaSolicitudAsync(EstadoSolicitud.Pendiente);
        await servicio.SincronizarAsync(PaisId, default);

        // «Solicitud por aprobar»: le llega a quien aprueba (el admin), no a quien la pidió ni al operario
        var porAprobar = Assert.Single(correo.Enviados, m => m.Titulo == "Solicitud por aprobar");
        Assert.Equal("admin@t.local", porAprobar.ParaEmail);
        Assert.Contains(s.NumeroSolicitud, porAprobar.Mensaje);
        Assert.StartsWith("[Inventario]", porAprobar.Asunto);
        Assert.Equal($"/solicitudes/{s.Id}", porAprobar.Url);

        // las alertas agregadas (sin stock, control de accesos...) NO salen por correo
        Assert.DoesNotContain(correo.Enviados, m => m.Titulo.StartsWith("Revisión de accesos") || m.Titulo.StartsWith("Productos"));

        // volver a sincronizar sin novedades no repite nada
        var cantidad = correo.Enviados.Count;
        await servicio.SincronizarAsync(PaisId, default);
        Assert.Equal(cantidad, correo.Enviados.Count);

        // al aprobarse: el solicitante recibe el resultado y el operario el «por entregar»
        s.Estado = EstadoSolicitud.Aprobada; s.AprobadoPorId = _admin.Id; s.AprobadoPorNombre = _admin.Nombre; s.FechaResolucion = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await servicio.SincronizarAsync(PaisId, default);
        Assert.Contains(correo.Enviados, m => m.Titulo == "Tu solicitud fue aprobada" && m.ParaEmail == "sol@t.local");
        Assert.Contains(correo.Enviados, m => m.Titulo == "Solicitud por entregar" && m.ParaEmail == "op@t.local");
    }

    [Fact]
    public async Task ConteoAbiertoHaceDias_AvisaAQuienCuenta()
    {
        _db.SesionesConteo.Add(new SesionConteo
        {
            Codigo = "CONT-T-1", PaisId = PaisId, Estado = EstadoConteo.EnCurso, CreadoPorId = _operador.Id, CreadoPorNombre = _operador.Nombre,
            FechaCreacion = DateTime.UtcNow.AddDays(-10),
        });
        await _db.SaveChangesAsync();

        Assert.Contains(await De(_operador), n => n.Titulo == "Conteos abiertos hace días");
        Assert.DoesNotContain(await De(_solicitante), n => n.Titulo == "Conteos abiertos hace días");
    }
}
