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

// Revisión periódica de accesos: abrir la campaña, decidir por cuenta, cerrar aplicando las decisiones.
// Roles sembrados por el modelo: 1 = Administrador (privilegiado), 2 = Operador, 3 = Consulta.
public class RevisionAccesoTests : IDisposable
{
    private const int PaisId = 1;
    private const int RolAdministrador = 1;
    private const int RolOperador = 2;
    private const int RolConsulta = 3;

    private readonly SqliteConnection _connection;
    private readonly InventoryDbContext _db;
    private readonly AuditoriaService _auditoria;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private readonly UsuarioActuante _revisor;   // administrador que revisa
    private readonly UsuarioActuante _otroAdmin; // administrador revisado
    private readonly int _operadorId;
    private readonly int _consultaId;

    public RevisionAccesoTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _auditoria = new AuditoriaService(_db);

        _revisor = NuevoCuenta("revisor@inventario.local", "Revisor", RolAdministrador, out _);
        _otroAdmin = NuevoCuenta("admin2@inventario.local", "Segundo Admin", RolAdministrador, out _);
        NuevoCuenta("operador@inventario.local", "Operador", RolOperador, out _operadorId);
        NuevoCuenta("consulta@inventario.local", "Consulta", RolConsulta, out _consultaId);
        NuevoCuenta("baja@inventario.local", "Cuenta ya inactiva", RolConsulta, out _, activo: false);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        _cache.Dispose();
    }

    private UsuarioActuante NuevoCuenta(string email, string nombre, int rolId, out int id, bool activo = true)
    {
        var u = new Usuario { Email = email, NombreCompleto = nombre, PasswordHash = "x", RolId = rolId, PaisId = PaisId, Activo = activo };
        _db.Usuarios.Add(u);
        _db.SaveChanges();
        id = u.Id;
        return new UsuarioActuante(u.Id, nombre);
    }

    private RevisionAccesoService Servicio(bool separacion = true) =>
        new(_db, _auditoria, _cache, Options.Create(new ControlesOptions { ExigirSolicitudEnMovimientos = false, SeparacionDeFunciones = separacion }));

    private Task<RevisionAccesoDetalleDto> Abrir(RevisionAccesoService s, AlcanceRevisionAcceso alcance = AlcanceRevisionAcceso.Todos) =>
        s.CrearAsync(new CrearRevisionAccesoDto(alcance, null), PaisId, _revisor, default);

    private static int LineaDe(RevisionAccesoDetalleDto d, string email) => d.Lineas.Single(l => l.UsuarioEmail == email).Id;

    private static DecidirAccesoDto Mantener() => new(DecisionAcceso.Mantener, null, null);

    // Resuelve todas las cuentas excepto las indicadas (que el test decide a mano) manteniéndolas.
    private async Task<RevisionAccesoDetalleDto> MantenerTodasAsync(RevisionAccesoService s, RevisionAccesoDetalleDto d, params string[] salvo)
    {
        foreach (var l in d.Lineas.Where(l => !salvo.Contains(l.UsuarioEmail) && l.Decision == DecisionAcceso.Pendiente))
            d = await s.DecidirAsync(d.Resumen.Id, l.Id, Mantener(), PaisId, l.UsuarioId == _revisor.Id ? _otroAdmin : _revisor, default);
        return d;
    }

    [Fact]
    public async Task Abrir_Todos_TomaFotoSoloDeLasCuentasActivas()
    {
        var d = await Abrir(Servicio());

        Assert.Equal(EstadoRevisionAcceso.EnCurso, d.Resumen.Estado);
        Assert.StartsWith("REV-", d.Resumen.Codigo);
        Assert.Equal(4, d.Lineas.Count); // la cuenta inactiva no entra
        Assert.DoesNotContain(d.Lineas, l => l.UsuarioEmail == "baja@inventario.local");
        Assert.All(d.Lineas, l => Assert.Equal(DecisionAcceso.Pendiente, l.Decision));
        Assert.All(d.Lineas, l => Assert.True(l.NuncaIngreso));
    }

    [Fact]
    public async Task Abrir_Administradores_SoloIncluyeLasCuentasPrivilegiadas()
    {
        var d = await Abrir(Servicio(), AlcanceRevisionAcceso.Administradores);

        Assert.Equal(2, d.Lineas.Count);
        Assert.All(d.Lineas, l => Assert.True(l.EsPrivilegiado));
    }

    [Fact]
    public async Task Abrir_UnaSegundaDelMismoAlcance_ConLaPrimeraEnCurso_SeRechaza()
    {
        var s = Servicio();
        await Abrir(s);
        await Assert.ThrowsAsync<RevisionAccesoEstadoInvalidoException>(() => Abrir(s));
        await Abrir(s, AlcanceRevisionAcceso.Administradores); // otro alcance sí puede convivir
    }

    [Fact]
    public async Task Decidir_LaPropiaCuenta_ConSeparacionDeFunciones_SeRechaza_YOtraPersonaPuede()
    {
        var s = Servicio(separacion: true);
        var d = await Abrir(s);
        var propia = LineaDe(d, "revisor@inventario.local");

        await Assert.ThrowsAsync<SeparacionDeFuncionesException>(() => s.DecidirAsync(d.Resumen.Id, propia, Mantener(), PaisId, _revisor, default));

        var resuelta = await s.DecidirAsync(d.Resumen.Id, propia, Mantener(), PaisId, _otroAdmin, default);
        Assert.Equal(DecisionAcceso.Mantener, resuelta.Lineas.Single(l => l.Id == propia).Decision);
    }

    [Fact]
    public async Task Decidir_LaPropiaCuenta_SinSeparacionDeFunciones_SePermiteMantener_PeroNuncaQuitar()
    {
        var s = Servicio(separacion: false);
        var d = await Abrir(s);
        var propia = LineaDe(d, "revisor@inventario.local");

        await s.DecidirAsync(d.Resumen.Id, propia, Mantener(), PaisId, _revisor, default);
        await Assert.ThrowsAsync<ValidacionException>(() =>
            s.DecidirAsync(d.Resumen.Id, propia, new DecidirAccesoDto(DecisionAcceso.Quitar, null, "me voy"), PaisId, _revisor, default));
    }

    [Fact]
    public async Task Decidir_QuitarOCambiarRol_ExigeComentario_YCambiarRolExigeElRolNuevo()
    {
        var s = Servicio();
        var d = await Abrir(s);
        var linea = LineaDe(d, "operador@inventario.local");

        await Assert.ThrowsAsync<ValidacionException>(() =>
            s.DecidirAsync(d.Resumen.Id, linea, new DecidirAccesoDto(DecisionAcceso.Quitar, null, null), PaisId, _revisor, default));
        await Assert.ThrowsAsync<ValidacionException>(() =>
            s.DecidirAsync(d.Resumen.Id, linea, new DecidirAccesoDto(DecisionAcceso.CambiarRol, null, "baja de privilegios"), PaisId, _revisor, default));
        // el rol nuevo no puede ser el que ya tiene
        await Assert.ThrowsAsync<ValidacionException>(() =>
            s.DecidirAsync(d.Resumen.Id, linea, new DecidirAccesoDto(DecisionAcceso.CambiarRol, RolOperador, "mismo rol"), PaisId, _revisor, default));
    }

    [Fact]
    public async Task Cerrar_ConCuentasSinDecidir_SeRechaza_YNoCambiaNada()
    {
        var s = Servicio();
        var d = await Abrir(s);
        d = await s.DecidirAsync(d.Resumen.Id, LineaDe(d, "operador@inventario.local"),
            new DecidirAccesoDto(DecisionAcceso.Quitar, null, "ya no trabaja acá"), PaisId, _revisor, default);

        var ex = await Assert.ThrowsAsync<RevisionAccesoEstadoInvalidoException>(() => s.CerrarAsync(d.Resumen.Id, PaisId, _revisor, default));
        Assert.Contains("Faltan 3", ex.Message);
        Assert.True((await _db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == _operadorId)).Activo);
    }

    [Fact]
    public async Task Cerrar_AplicaLasDecisiones_YLaCampanaQuedaInmutable()
    {
        var s = Servicio();
        var d = await Abrir(s);
        var id = d.Resumen.Id;

        await s.DecidirAsync(id, LineaDe(d, "operador@inventario.local"),
            new DecidirAccesoDto(DecisionAcceso.Quitar, null, "pasó a otra área"), PaisId, _revisor, default);
        await s.DecidirAsync(id, LineaDe(d, "consulta@inventario.local"),
            new DecidirAccesoDto(DecisionAcceso.CambiarRol, RolOperador, "ahora opera el almacén"), PaisId, _revisor, default);
        d = await MantenerTodasAsync(s, await s.ObtenerAsync(id, PaisId, _revisor.Id, default),
            "operador@inventario.local", "consulta@inventario.local");

        var cerrada = await s.CerrarAsync(id, PaisId, _revisor, default);

        Assert.Equal(EstadoRevisionAcceso.Cerrada, cerrada.Resumen.Estado);
        Assert.Equal(1, cerrada.Resumen.Quitar);
        Assert.Equal(1, cerrada.Resumen.CambiarRol);
        Assert.All(cerrada.Lineas, l => Assert.True(l.Aplicada));

        _db.ChangeTracker.Clear();
        Assert.False((await _db.Usuarios.SingleAsync(u => u.Id == _operadorId)).Activo);
        Assert.Equal(RolOperador, (await _db.Usuarios.SingleAsync(u => u.Id == _consultaId)).RolId);

        // los cambios quedaron auditados sobre la entidad Usuario, con la campaña como motivo
        var auditoriaUsuario = await _db.Auditorias.AsNoTracking()
            .Where(a => a.Entidad == nameof(Usuario) && a.EntidadId == _operadorId.ToString()).ToListAsync();
        Assert.Contains(auditoriaUsuario, a => a.Motivo!.Contains(cerrada.Resumen.Codigo));

        // inmutable: ya no se decide ni se cierra de nuevo
        await Assert.ThrowsAsync<RevisionAccesoEstadoInvalidoException>(() =>
            s.DecidirAsync(id, LineaDe(d, "operador@inventario.local"), Mantener(), PaisId, _revisor, default));
        await Assert.ThrowsAsync<RevisionAccesoEstadoInvalidoException>(() => s.CerrarAsync(id, PaisId, _revisor, default));
    }

    [Fact]
    public async Task Cerrar_QueDejariaAlPaisSinAdministrador_SeRevierteTodo()
    {
        // Sin separación de funciones el revisor puede cambiarse a sí mismo: ambos admins quedarían sin el permiso.
        var s = Servicio(separacion: false);
        var d = await Abrir(s, AlcanceRevisionAcceso.Administradores);
        var id = d.Resumen.Id;

        await s.DecidirAsync(id, LineaDe(d, "revisor@inventario.local"),
            new DecidirAccesoDto(DecisionAcceso.CambiarRol, RolOperador, "pruebo dejarme sin permisos"), PaisId, _revisor, default);
        await s.DecidirAsync(id, LineaDe(d, "admin2@inventario.local"),
            new DecidirAccesoDto(DecisionAcceso.CambiarRol, RolConsulta, "pruebo dejarlo sin permisos"), PaisId, _revisor, default);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => s.CerrarAsync(id, PaisId, _revisor, default));
        Assert.Contains("administrar usuarios", ex.Message);

        _db.ChangeTracker.Clear();
        Assert.Equal(EstadoRevisionAcceso.EnCurso, (await _db.RevisionesAcceso.SingleAsync(r => r.Id == id)).Estado);
        Assert.Equal(RolAdministrador, (await _db.Usuarios.SingleAsync(u => u.Id == _revisor.Id)).RolId);
        Assert.Equal(RolAdministrador, (await _db.Usuarios.SingleAsync(u => u.Id == _otroAdmin.Id)).RolId);
    }

    [Fact]
    public async Task Cancelar_ExigeMotivo_YNoAplicaNada()
    {
        var s = Servicio();
        var d = await Abrir(s);
        await s.DecidirAsync(d.Resumen.Id, LineaDe(d, "operador@inventario.local"),
            new DecidirAccesoDto(DecisionAcceso.Quitar, null, "baja"), PaisId, _revisor, default);

        await Assert.ThrowsAsync<ValidacionException>(() => s.CancelarAsync(d.Resumen.Id, new CancelarRevisionAccesoDto(" "), PaisId, _revisor, default));

        var cancelada = await s.CancelarAsync(d.Resumen.Id, new CancelarRevisionAccesoDto("se abrió por error"), PaisId, _revisor, default);
        Assert.Equal(EstadoRevisionAcceso.Cancelada, cancelada.Resumen.Estado);
        Assert.True((await _db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == _operadorId)).Activo);

        // y se puede abrir otra del mismo alcance
        await Abrir(s);
    }

    [Fact]
    public async Task Estado_SinRevisiones_EstaVencida_YLaRevisionDeTodosTambienCubreAAdministradores()
    {
        var s = Servicio();
        var vacio = await s.ObtenerEstadoAsync(PaisId, default);
        Assert.True(vacio.TodosVencida);
        Assert.True(vacio.AdministradoresVencida);

        var d = await Abrir(s);
        Assert.Equal(d.Resumen.Id, (await s.ObtenerEstadoAsync(PaisId, default)).CampanaTodosEnCursoId);

        d = await MantenerTodasAsync(s, d);
        await s.CerrarAsync(d.Resumen.Id, PaisId, _revisor, default);

        var estado = await s.ObtenerEstadoAsync(PaisId, default);
        Assert.False(estado.TodosVencida);
        Assert.False(estado.AdministradoresVencida);
        Assert.Null(estado.CampanaTodosEnCursoId);
    }

    [Fact]
    public async Task Acta_SeGeneraConElCodigoDeLaCampana()
    {
        var s = Servicio();
        var d = await Abrir(s);

        var (contenido, nombre) = await s.GenerarActaAsync(d.Resumen.Id, PaisId, default);

        Assert.True(contenido.Length > 1000);
        Assert.Equal($"{d.Resumen.Codigo}-acta-revision-accesos.xlsx", nombre);
        await Assert.ThrowsAsync<RevisionAccesoNoEncontradaException>(() => s.GenerarActaAsync(9999, PaisId, default));
    }
}
