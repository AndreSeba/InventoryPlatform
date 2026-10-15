using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Inventory.UnitTests;

// Módulo Conteo Externo: producto sobrante, cantidad final (transcripción ágil), cierre que quita los lotes
// contados de Existencias y buscador/sugerencias del catálogo. Reglas de negocio: ver CLAUDE.md.
public class ConteoExternoSobranteTests : IDisposable
{
    private const int PaisId = 1;

    private readonly SqliteConnection _connection;
    private readonly InventoryDbContext _db;
    private readonly ConteoExternoSesionService _sesiones;
    private readonly ConteoExternoProductoService _productos;
    private readonly UsuarioActuante _usuario;

    public ConteoExternoSobranteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var auditoria = new AuditoriaService(_db);
        _sesiones = new ConteoExternoSesionService(_db, auditoria);
        _productos = new ConteoExternoProductoService(_db, auditoria);

        var usuario = new Usuario
        {
            Email = "conteo.externo@inventario.local", NombreCompleto = "Supervisor", PasswordHash = "x",
            RolId = 1, PaisId = PaisId, Activo = true,
        };
        _db.Usuarios.Add(usuario);
        _db.ConteoExternoUnidades.AddRange(
            new ConteoExternoUnidad { Codigo = "CAJ", DecimalesPermitidos = 2, PaisId = PaisId, Activo = true },
            new ConteoExternoUnidad { Codigo = "KG", DecimalesPermitidos = 3, PaisId = PaisId, Activo = true });
        _db.SaveChanges();
        _usuario = new UsuarioActuante(usuario.Id, usuario.NombreCompleto);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // Producto del catálogo con una existencia en el sistema (almacén 103004).
    private async Task<(ConteoExternoProducto Producto, ConteoExternoExistencia Existencia)> ProductoConLoteAsync(
        string codigo, string lote, decimal cantidad, decimal costo = 10m, string unidad = "CAJ")
    {
        var producto = new ConteoExternoProducto
        {
            Codigo = codigo, CodigoNormalizado = CodigoConteoExterno.Normalizar(codigo), Nombre = $"Producto {codigo}",
            Familia = "RELLENAS", UnidadCodigo = unidad, CostoReferencia = costo, PaisId = PaisId, Activo = true,
        };
        var existencia = new ConteoExternoExistencia
        {
            ConteoExternoProducto = producto, AlmacenCodigo = "103004", Lote = lote, CantidadSistema = cantidad, PaisId = PaisId, Activo = true,
        };
        _db.AddRange(producto, existencia);
        await _db.SaveChangesAsync();
        return (producto, existencia);
    }

    private async Task<int> CrearSesionAsync()
        => (await _sesiones.CrearAsync(new CrearConteoExternoSesionDto("Sesión de prueba", null, null), PaisId, _usuario, default)).Id;

    private static AgregarSobranteConteoExternoDto Sobrante(
        string codigo, string lote = "LS1", string? nombre = "Producto nuevo", string? familia = "rellenas",
        string? unidad = "CAJ", decimal? costo = 7m, string? ubicacion = "UB1", decimal? contada = 3m)
        => new(codigo, nombre, familia, "103004", lote, unidad, costo, ubicacion, contada);

    private async Task<ConteoExternoLineaDto> LineaAsync(int sesionId, string codigo, string lote)
        => (await _sesiones.ObtenerAsync(sesionId, PaisId, default)).Lineas.Single(l => l.ProductoCodigo == codigo && l.Lote == lote);

    // ---------------------------------------------------------------- sobrantes

    [Fact]
    public async Task Sobrante_ProductoNuevo_CreaCatalogoExistenciaEnCeroYLineaConLoContado()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();

        var linea = await _sesiones.AgregarSobranteAsync(sesionId, Sobrante("NUEVO-1"), PaisId, _usuario, default);

        var producto = await _db.ConteoExternoProductos.SingleAsync(p => p.Codigo == "NUEVO-1");
        Assert.Equal("RELLENAS", producto.Familia);
        Assert.Equal("CAJ", producto.UnidadCodigo);
        Assert.Equal(7m, producto.CostoReferencia);
        Assert.Equal(OrigenConteoExternoProducto.Manual, producto.Origen);

        var existencia = await _db.ConteoExternoExistencias.SingleAsync(e => e.ConteoExternoProductoId == producto.Id);
        Assert.Equal(0m, existencia.CantidadSistema);
        Assert.Equal("LS1", existencia.Lote);

        Assert.Equal(0m, linea.ExistenciaSistema);
        Assert.Equal(3m, linea.TotalContado);
        Assert.Equal(3m, linea.Diferencia);          // sobrante: contado − 0
        Assert.Equal(21m, linea.DiferenciaValor);    // 3 × 7
        var registro = Assert.Single(linea.Registros);
        Assert.Equal(1, registro.NumeroConteo);
        Assert.Equal("UB1", registro.Ubicacion);
    }

    [Fact]
    public async Task Sobrante_ProductoExistente_UsaSuUnidadYCostoYQuedaSinContar()
    {
        var (producto, _) = await ProductoConLoteAsync("P1", "L1", 10, costo: 12m);
        var sesionId = await CrearSesionAsync();

        // Código con ceros a la izquierda: es el mismo producto (no se duplica el catálogo).
        var linea = await _sesiones.AgregarSobranteAsync(sesionId,
            Sobrante("0P1", lote: "OTRO", nombre: null, familia: null, unidad: null, costo: null, ubicacion: null, contada: null),
            PaisId, _usuario, default);

        Assert.Equal(1, await _db.ConteoExternoProductos.CountAsync());
        Assert.Equal(producto.Id, linea.ConteoExternoProductoId);
        Assert.Equal(12m, linea.CostoUnitario);
        Assert.Equal("CAJ", linea.UnidadCodigo);
        Assert.Null(linea.TotalContado);
        Assert.Empty(linea.Registros);
    }

    [Fact]
    public async Task Sobrante_ProductoNuevoSinCosto_SeRechazaYNoQuedaNadaAMedias()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();

        await Assert.ThrowsAsync<ValidacionException>(() =>
            _sesiones.AgregarSobranteAsync(sesionId, Sobrante("NUEVO-2", costo: null), PaisId, _usuario, default));

        Assert.False(await _db.ConteoExternoProductos.AnyAsync(p => p.Codigo == "NUEVO-2"));
        Assert.Equal(1, await _db.ConteoExternoExistencias.CountAsync());
        Assert.Equal(1, await _db.ConteoExternoLineas.CountAsync());
    }

    [Fact]
    public async Task Sobrante_CantidadConDecimalesDeMas_SeRechazaYNoQuedaNadaAMedias()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();

        await Assert.ThrowsAsync<ValidacionException>(() =>
            _sesiones.AgregarSobranteAsync(sesionId, Sobrante("NUEVO-3", contada: 1.555m), PaisId, _usuario, default));

        Assert.False(await _db.ConteoExternoProductos.AnyAsync(p => p.Codigo == "NUEVO-3"));
        Assert.Equal(1, await _db.ConteoExternoLineas.CountAsync());
    }

    [Fact]
    public async Task Sobrante_LoteQueYaExisteEnElSistemaOEnLaSesion_Conflicto()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();

        // L1 ya está en el sistema y en la sesión: no es un sobrante.
        await Assert.ThrowsAsync<ConteoExternoSobranteYaExisteException>(() =>
            _sesiones.AgregarSobranteAsync(sesionId, Sobrante("P1", lote: "L1"), PaisId, _usuario, default));

        // Un sobrante legítimo se agrega una sola vez.
        await _sesiones.AgregarSobranteAsync(sesionId, Sobrante("P1", lote: "NUEVO"), PaisId, _usuario, default);
        await Assert.ThrowsAsync<ConteoExternoSobranteYaExisteException>(() =>
            _sesiones.AgregarSobranteAsync(sesionId, Sobrante("P1", lote: "NUEVO"), PaisId, _usuario, default));
    }

    [Fact]
    public async Task Sobrante_EnSesionCerrada_SeRechaza()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();
        var linea = await LineaAsync(sesionId, "P1", "L1");
        await _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(10m), PaisId, _usuario, default);
        await _sesiones.CerrarAsync(sesionId, PaisId, _usuario, default);

        await Assert.ThrowsAsync<ConteoExternoSesionCerradaException>(() =>
            _sesiones.AgregarSobranteAsync(sesionId, Sobrante("NUEVO-4"), PaisId, _usuario, default));
    }

    // ---------------------------------------------------------------- cantidad final (transcripción ágil)

    [Fact]
    public async Task CantidadFinal_LineaSinDatos_GuardaConteo1EnSinUbicacion()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();
        var linea = await LineaAsync(sesionId, "P1", "L1");

        await _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(8m), PaisId, _usuario, default);

        var actual = await LineaAsync(sesionId, "P1", "L1");
        Assert.Equal(8m, actual.TotalContado);
        Assert.Equal(-2m, actual.Diferencia);
        var registro = Assert.Single(actual.Registros);
        Assert.Equal((1, "S/U", 8m), (registro.NumeroConteo, registro.Ubicacion, registro.Cantidad));
    }

    [Fact]
    public async Task CantidadFinal_ConUnaUbicacionExistente_LaReemplazaConservandoSuNombre()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();
        var linea = await LineaAsync(sesionId, "P1", "L1");
        await _sesiones.RegistrarConteoAsync(linea.Id, 1, new RegistrarConteoExternoDto(new() { ["A1"] = 4m }), PaisId, _usuario, default);

        await _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(9m), PaisId, _usuario, default);

        var registro = Assert.Single((await LineaAsync(sesionId, "P1", "L1")).Registros);
        Assert.Equal(("A1", 9m), (registro.Ubicacion, registro.Cantidad));
    }

    [Fact]
    public async Task CantidadFinal_ConDetalle_SeRechazaParaNoPisarInformacion()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();
        var linea = await LineaAsync(sesionId, "P1", "L1");
        await _sesiones.RegistrarConteoAsync(linea.Id, 1, new RegistrarConteoExternoDto(new() { ["A1"] = 4m, ["A2"] = 6m }), PaisId, _usuario, default);

        await Assert.ThrowsAsync<ValidacionException>(() =>
            _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(9m), PaisId, _usuario, default));

        Assert.Equal(10m, (await LineaAsync(sesionId, "P1", "L1")).TotalContado);   // intacto
    }

    [Fact]
    public async Task CantidadFinal_VaciaNoBorra_YCeroEsUnConteoReal()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();
        var linea = await LineaAsync(sesionId, "P1", "L1");

        await _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(null), PaisId, _usuario, default);
        Assert.Null((await LineaAsync(sesionId, "P1", "L1")).TotalContado);        // vacío = sin contar

        await _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(0m), PaisId, _usuario, default);
        var cero = await LineaAsync(sesionId, "P1", "L1");
        Assert.Equal(0m, cero.TotalContado);                                       // 0 = se contó y no había nada
        Assert.Equal(-10m, cero.Diferencia);

        await _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(null), PaisId, _usuario, default);
        Assert.Equal(0m, (await LineaAsync(sesionId, "P1", "L1")).TotalContado);   // vacío no borra lo ya contado
    }

    [Fact]
    public async Task CantidadFinal_ConDecimalesDeMasParaLaUnidad_SeRechaza()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();
        var linea = await LineaAsync(sesionId, "P1", "L1");

        await Assert.ThrowsAsync<ValidacionException>(() =>
            _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(1.234m), PaisId, _usuario, default));
    }

    // ---------------------------------------------------------------- cierre

    [Fact]
    public async Task Cerrar_QuitaDeExistenciasLosLotesContados_YLaSesionConservaSuFoto()
    {
        var (_, contado) = await ProductoConLoteAsync("P1", "L1", 10);
        var (_, segundoLote) = await ProductoConLoteAsync("P2", "L9", 5);
        var sesionId = (await _sesiones.CrearAsync(new CrearConteoExternoSesionDto("Parcial", null, null), PaisId, _usuario, default)).Id;

        // Una existencia que aparece DESPUÉS de crear la sesión no es de la sesión y no debe tocarse.
        var posterior = new ConteoExternoExistencia
        {
            ConteoExternoProductoId = segundoLote.ConteoExternoProductoId, AlmacenCodigo = "103004", Lote = "L10",
            CantidadSistema = 1m, PaisId = PaisId, Activo = true,
        };
        _db.ConteoExternoExistencias.Add(posterior);
        await _db.SaveChangesAsync();

        foreach (var l in (await _sesiones.ObtenerAsync(sesionId, PaisId, default)).Lineas)
            await _sesiones.RegistrarCantidadFinalAsync(l.Id, new ConteoExternoCantidadFinalDto(l.ExistenciaSistema), PaisId, _usuario, default);

        await _sesiones.CerrarAsync(sesionId, PaisId, _usuario, default);

        _db.ChangeTracker.Clear();
        Assert.False((await _db.ConteoExternoExistencias.FindAsync(contado.Id))!.Activo);
        Assert.False((await _db.ConteoExternoExistencias.FindAsync(segundoLote.Id))!.Activo);
        Assert.True((await _db.ConteoExternoExistencias.FindAsync(posterior.Id))!.Activo);

        var detalle = await _sesiones.ObtenerAsync(sesionId, PaisId, default);
        Assert.Equal(EstadoConteoExternoSesion.Cerrada, detalle.Estado);
        Assert.Equal(2, detalle.Lineas.Count);                                    // la foto no cambia
        Assert.All(detalle.Lineas, l => Assert.Equal(0m, l.Diferencia));
    }

    [Fact]
    public async Task Cerrar_ConLineasSinContar_SeRechaza_YNoQuitaNada()
    {
        var (_, existencia) = await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();

        await Assert.ThrowsAsync<ValidacionException>(() => _sesiones.CerrarAsync(sesionId, PaisId, _usuario, default));

        _db.ChangeTracker.Clear();
        Assert.True((await _db.ConteoExternoExistencias.FindAsync(existencia.Id))!.Activo);
    }

    [Fact]
    public async Task EnSesionCerrada_NoSeAceptaNingunConteoNiObservacion()
    {
        await ProductoConLoteAsync("P1", "L1", 10);
        var sesionId = await CrearSesionAsync();
        var linea = await LineaAsync(sesionId, "P1", "L1");
        await _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(10m), PaisId, _usuario, default);
        await _sesiones.CerrarAsync(sesionId, PaisId, _usuario, default);

        await Assert.ThrowsAsync<ConteoExternoSesionCerradaException>(() =>
            _sesiones.RegistrarCantidadFinalAsync(linea.Id, new ConteoExternoCantidadFinalDto(1m), PaisId, _usuario, default));
        await Assert.ThrowsAsync<ConteoExternoSesionCerradaException>(() =>
            _sesiones.RegistrarConteoAsync(linea.Id, 2, new RegistrarConteoExternoDto(new() { ["A"] = 1m }), PaisId, _usuario, default));
        await Assert.ThrowsAsync<ConteoExternoSesionCerradaException>(() =>
            _sesiones.ActualizarObservacionAsync(linea.Id, new ActualizarObservacionConteoExternoDto("Planta"), PaisId, _usuario, default));
    }

    // ---------------------------------------------------------------- buscador y sugerencias

    [Fact]
    public async Task Buscar_PorCodigoONombre_ElCodigoExactoVaPrimero()
    {
        await ProductoConLoteAsync("10101105", "L1", 1);
        await ProductoConLoteAsync("20101105", "L1", 1);

        var porCodigo = await _productos.BuscarAsync(PaisId, "010101105", 6, default);   // con cero inicial
        Assert.Equal("10101105", porCodigo[0].Codigo);

        var porNombre = await _productos.BuscarAsync(PaisId, "producto 20101105", 6, default);
        Assert.Equal("20101105", Assert.Single(porNombre).Codigo);

        Assert.Empty(await _productos.BuscarAsync(PaisId, "   ", 6, default));
    }

    [Fact]
    public async Task Sugerencias_TraenUnidadCostoAlmacenHabitualYLotes()
    {
        var (producto, _) = await ProductoConLoteAsync("P1", "L2", 5, costo: 12m);
        _db.ConteoExternoExistencias.AddRange(
            new ConteoExternoExistencia { ConteoExternoProductoId = producto.Id, AlmacenCodigo = "103004", Lote = "L1", CantidadSistema = 1, PaisId = PaisId, Activo = true },
            new ConteoExternoExistencia { ConteoExternoProductoId = producto.Id, AlmacenCodigo = "103001", Lote = "L3", CantidadSistema = 1, PaisId = PaisId, Activo = true });
        await _db.SaveChangesAsync();

        var s = await _productos.ObtenerSugerenciasAsync(producto.Id, PaisId, default);

        Assert.Equal("CAJ", s.UnidadCodigo);
        Assert.Equal(12m, s.CostoReferencia);
        Assert.Equal("103004", s.AlmacenHabitual);                         // 2 lotes en 103004 vs 1 en 103001
        Assert.Equal(new[] { "L1", "L2", "L3" }, s.Lotes.ToArray());
    }
}
