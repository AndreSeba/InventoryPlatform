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

// Firma manuscrita por usuario: se guarda con versiones, y solo se estampa en el formulario de una
// solicitud cuando esa persona realmente hizo la acción.
public class FirmaTests : IDisposable
{
    private const int PaisId = 1;

    private readonly SqliteConnection _connection;
    private readonly InventoryDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly FirmaService _firmas;
    private readonly UsuarioActuante _solicitante;
    private readonly UsuarioActuante _aprobador;
    private readonly int _productoId;

    public FirmaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _auditoria = new AuditoriaService(_db);
        _firmas = new FirmaService(_db, _auditoria);

        _solicitante = NuevoUsuario("solicitante@inventario.local", "Solicitante");
        _aprobador = NuevoUsuario("aprobador@inventario.local", "Aprobador");

        var categoria = new Categoria { CodigoCategoria = "FIR", PaisId = PaisId, Activo = true };
        var producto = new Producto { ClaveProducto = "FIR-1-UNI", CodigoProducto = "FIR-1", Nombre = "Banner", Categoria = categoria, PaisId = PaisId, UnidadMedida = "UNI", CostoUnitario = 1m, Activo = true };
        _db.AddRange(categoria, producto);
        _db.SaveChanges();
        _productoId = producto.Id;
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

    // PNG mínimo: solo el encabezado que el servicio valida (firma + IHDR con ancho y alto) y relleno.
    private static string Png(int ancho = 400, int alto = 150, int relleno = 40)
    {
        var d = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
        d.AddRange([(byte)(ancho >> 24), (byte)(ancho >> 16), (byte)(ancho >> 8), (byte)ancho, (byte)(alto >> 24), (byte)(alto >> 16), (byte)(alto >> 8), (byte)alto]);
        d.AddRange(Enumerable.Repeat((byte)7, relleno));
        return Convert.ToBase64String(d.ToArray());
    }

    private SolicitudService Solicitudes() =>
        new(_db, new LoggingSolicitudNotificationService(NullLogger<LoggingSolicitudNotificationService>.Instance),
            NullLogger<SolicitudService>.Instance, _auditoria,
            Options.Create(new ControlesOptions { ExigirSolicitudEnMovimientos = false, SeparacionDeFunciones = true }));

    private async Task<int> NuevaSolicitudAsync()
    {
        var area = new Area { CodigoArea = "F" + Guid.NewGuid().ToString("N")[..5], NombreArea = "Marketing", PaisId = PaisId, Activo = true };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync();
        var dto = await Solicitudes().CrearAsync(new CrearSolicitudDto(area.Id, TipoSolicitud.Salida, [new CrearSolicitudDetalleDto(_productoId, 5)]), PaisId, _solicitante, default);
        return dto.Id;
    }

    private async Task AprobarAsync(int solicitudId)
    {
        var linea = await _db.SolicitudDetalles.Where(d => d.SolicitudId == solicitudId).Select(d => d.Id).SingleAsync();
        await Solicitudes().AprobarAsync(solicitudId, new AprobarSolicitudDto([new AprobarSolicitudDetalleDto(linea, 5)]), PaisId, _aprobador, default);
    }

    [Fact]
    public async Task Guardar_ExigeUnPngValido()
    {
        await Assert.ThrowsAsync<ValidacionException>(() => _firmas.GuardarAsync(new GuardarFirmaDto(Convert.ToBase64String(new byte[100])), PaisId, _solicitante, default));
        await Assert.ThrowsAsync<ValidacionException>(() => _firmas.GuardarAsync(new GuardarFirmaDto(Png(ancho: 5000)), PaisId, _solicitante, default));
        await Assert.ThrowsAsync<ValidacionException>(() => _firmas.GuardarAsync(new GuardarFirmaDto(Png(relleno: 400 * 1024)), PaisId, _solicitante, default));
        await Assert.ThrowsAsync<ValidacionException>(() => _firmas.GuardarAsync(new GuardarFirmaDto(""), PaisId, _solicitante, default));
        Assert.False((await _firmas.ObtenerPropiaAsync(_solicitante.Id, default)).Tiene);

        var ok = await _firmas.GuardarAsync(new GuardarFirmaDto("data:image/png;base64," + Png()), PaisId, _solicitante, default);
        Assert.True(ok.Tiene);
        Assert.True((await _firmas.ObtenerPropiaAsync(_solicitante.Id, default)).Tiene);
        Assert.False((await _firmas.ObtenerPropiaAsync(_aprobador.Id, default)).Tiene);   // cada quien la suya
    }

    [Fact]
    public async Task Reemplazar_DejaUnaSolaActiva_YConservaLaHistoria()
    {
        await _firmas.GuardarAsync(new GuardarFirmaDto(Png(relleno: 10)), PaisId, _solicitante, default);
        await _firmas.GuardarAsync(new GuardarFirmaDto(Png(relleno: 20)), PaisId, _solicitante, default);

        Assert.Equal(2, await _db.Firmas.CountAsync(f => f.UsuarioId == _solicitante.Id));
        Assert.Equal(1, await _db.Firmas.CountAsync(f => f.UsuarioId == _solicitante.Id && f.Activa));

        await _firmas.EliminarAsync(PaisId, _solicitante, default);
        Assert.False((await _firmas.ObtenerPropiaAsync(_solicitante.Id, default)).Tiene);
        Assert.Equal(2, await _db.Firmas.CountAsync(f => f.UsuarioId == _solicitante.Id));   // nunca se borra la fila
        Assert.Contains(await _db.Auditorias.Select(a => a.Accion).ToListAsync(), a => a == "Reemplazar");
    }

    [Fact]
    public async Task Solicitud_FijaLaVersionConLaQueSeFirmo_YSoloEstampaAQuienActuo()
    {
        await _firmas.GuardarAsync(new GuardarFirmaDto(Png(relleno: 10)), PaisId, _solicitante, default);
        await _firmas.GuardarAsync(new GuardarFirmaDto(Png(relleno: 10)), PaisId, _aprobador, default);
        var id = await NuevaSolicitudAsync();

        // Pendiente: firma del solicitante, ninguna de quien autoriza (todavía no hizo nada).
        var pendiente = await _firmas.ObtenerDeSolicitudAsync(id, PaisId, default);
        Assert.NotNull(pendiente.SolicitantePngBase64);
        Assert.Null(pendiente.AutorizaPngBase64);

        await AprobarAsync(id);
        var aprobada = await _firmas.ObtenerDeSolicitudAsync(id, PaisId, default);
        Assert.NotNull(aprobada.AutorizaPngBase64);
        Assert.Equal(await _db.Roles.Where(r => r.Id == 1).Select(r => r.Nombre).SingleAsync(), aprobada.AutorizaCargo);

        // El solicitante cambia su firma: el formulario de la solicitud ya creada conserva la que usó.
        var antes = aprobada.SolicitantePngBase64;
        await _firmas.GuardarAsync(new GuardarFirmaDto(Png(relleno: 99)), PaisId, _solicitante, default);
        Assert.Equal(antes, (await _firmas.ObtenerDeSolicitudAsync(id, PaisId, default)).SolicitantePngBase64);
    }

    [Fact]
    public async Task SolicitudRechazada_NoEstampaLaFirmaDeQuienAutoriza()
    {
        await _firmas.GuardarAsync(new GuardarFirmaDto(Png()), PaisId, _aprobador, default);
        var id = await NuevaSolicitudAsync();
        await Solicitudes().RechazarAsync(id, new RechazarSolicitudDto("Sin presupuesto"), PaisId, _aprobador, default);

        var firmas = await _firmas.ObtenerDeSolicitudAsync(id, PaisId, default);
        Assert.Null(firmas.AutorizaPngBase64);
    }

    [Fact]
    public async Task SinFirmaAlCrear_UsaLaActivaDeHoy_YSinNingunaDevuelveNull()
    {
        var id = await NuevaSolicitudAsync();
        Assert.Null((await _firmas.ObtenerDeSolicitudAsync(id, PaisId, default)).SolicitantePngBase64);

        await _firmas.GuardarAsync(new GuardarFirmaDto(Png()), PaisId, _solicitante, default);
        Assert.NotNull((await _firmas.ObtenerDeSolicitudAsync(id, PaisId, default)).SolicitantePngBase64);
    }

    [Fact]
    public async Task OtroPais_NoVeLasFirmasDeLaSolicitud()
    {
        var id = await NuevaSolicitudAsync();
        await Assert.ThrowsAsync<SolicitudNoEncontradaException>(() => _firmas.ObtenerDeSolicitudAsync(id, 999, default));
    }
}
