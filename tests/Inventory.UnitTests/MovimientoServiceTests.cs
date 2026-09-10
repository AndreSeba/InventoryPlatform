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

// SQLite en memoria (no el proveedor InMemory de EF) porque el service usa
// una transacción explícita (BeginTransactionAsync) para la regla crítica de
// stock — InMemory no soporta transacciones reales.
public class MovimientoServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly InventoryDbContext _db;
    private readonly MovimientoService _service;

    public MovimientoServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new InventoryDbContext(options);
        _db.Database.EnsureCreated();
        _service = new MovimientoService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<(int ProductoId, int UbicacionId)> CrearProductoYUbicacionAsync(string codigo = "TEST_1")
    {
        var categoria = new Categoria { CodigoCategoria = "PRUEBA", Activo = true };
        _db.Categorias.Add(categoria);

        var ubicacion = new Ubicacion
        {
            TipoUbicacion = TipoUbicacion.Rack,
            Nro = "01",
            Lado = "A",
            Nivel = "01",
            CodigoUbicacion = $"A-01-01-{codigo}",
            Activo = true,
        };
        _db.Ubicaciones.Add(ubicacion);

        var producto = new Producto
        {
            ClaveProducto = $"{codigo}-UNI",
            CodigoProducto = codigo,
            Nombre = "Producto de prueba",
            Categoria = categoria,
            UnidadMedida = "UNI",
            CostoUnitario = 10m,
            Activo = true,
        };
        _db.Productos.Add(producto);

        await _db.SaveChangesAsync();
        return (producto.Id, ubicacion.Id);
    }

    [Fact]
    public async Task RegistrarSalida_ConStockSuficiente_Confirma()
    {
        var (productoId, ubicacionId) = await CrearProductoYUbicacionAsync();
        await _service.RegistrarEntradaAsync(new RegistrarEntradaDto(productoId, ubicacionId, 100, null), "usuario-1", default);

        var resultado = await _service.RegistrarSalidaAsync(
            new RegistrarSalidaDto(productoId, ubicacionId, 30, "Entrega marketing", false, null, null, null), "usuario-1", default);

        Assert.Equal("CONFIRMADO", resultado.Estado);
        Assert.Equal(70m, resultado.ExistenciaResultante);
    }

    [Fact]
    public async Task RegistrarSalida_SuperiorAlStockDisponible_RechazaConStockInsuficienteException()
    {
        var (productoId, ubicacionId) = await CrearProductoYUbicacionAsync();
        await _service.RegistrarEntradaAsync(new RegistrarEntradaDto(productoId, ubicacionId, 10, null), "usuario-1", default);

        await Assert.ThrowsAsync<StockInsuficienteException>(() =>
            _service.RegistrarSalidaAsync(new RegistrarSalidaDto(productoId, ubicacionId, 11, null, false, null, null, null), "usuario-1", default));

        // La regla exige que el rechazo no deje datos a medias: el movimiento
        // rechazado no debe haberse persistido.
        var movimientos = await _db.Movimientos.Where(m => m.ProductoId == productoId).ToListAsync();
        Assert.Single(movimientos); // solo la entrada de 10, la salida rechazada no quedó
    }

    [Fact]
    public async Task RegistrarSalida_SinNingunaEntradaPrevia_Rechaza()
    {
        var (productoId, ubicacionId) = await CrearProductoYUbicacionAsync();

        await Assert.ThrowsAsync<StockInsuficienteException>(() =>
            _service.RegistrarSalidaAsync(new RegistrarSalidaDto(productoId, ubicacionId, 1, null, false, null, null, null), "usuario-1", default));
    }

    [Fact]
    public async Task RegistrarMovimiento_ProductoInexistente_LanzaProductoNoEncontrado()
    {
        var (_, ubicacionId) = await CrearProductoYUbicacionAsync();

        await Assert.ThrowsAsync<ProductoNoEncontradoException>(() =>
            _service.RegistrarEntradaAsync(new RegistrarEntradaDto(999, ubicacionId, 1, null), "usuario-1", default));
    }

    [Fact]
    public async Task RegistrarMovimiento_CantidadCeroONegativa_LanzaArgumentOutOfRange()
    {
        var (productoId, ubicacionId) = await CrearProductoYUbicacionAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.RegistrarSalidaAsync(new RegistrarSalidaDto(productoId, ubicacionId, 0, null, false, null, null, null), "usuario-1", default));
    }

    [Fact]
    public async Task RegistrarAjusteNegativo_SuperiorAlStock_Rechaza()
    {
        var (productoId, ubicacionId) = await CrearProductoYUbicacionAsync();
        await _service.RegistrarEntradaAsync(new RegistrarEntradaDto(productoId, ubicacionId, 5, null), "usuario-1", default);

        await Assert.ThrowsAsync<StockInsuficienteException>(() =>
            _service.RegistrarAjusteAsync(new RegistrarAjusteDto(productoId, ubicacionId, 6, false, "Ajuste por rotura"), "usuario-1", default));
    }

    [Fact]
    public async Task RegistrarDevolucion_SobreSalidaConRetorna_Confirma()
    {
        var (productoId, ubicacionId) = await CrearProductoYUbicacionAsync();
        await _service.RegistrarEntradaAsync(new RegistrarEntradaDto(productoId, ubicacionId, 20, null), "usuario-1", default);

        var salida = await _service.RegistrarSalidaAsync(
            new RegistrarSalidaDto(productoId, ubicacionId, 10, "Préstamo evento", true, "Evento Trade Marketing", null, null),
            "usuario-1", default);

        var devolucion = await _service.RegistrarDevolucionAsync(
            new RegistrarDevolucionDto(salida.MovimientoId, ubicacionId, 10, "Devolución"), "usuario-1", default);

        Assert.Equal("CONFIRMADO", devolucion.Estado);
        Assert.Equal(20m, devolucion.ExistenciaResultante); // 20 entrada - 10 salida + 10 devuelto
    }

    [Fact]
    public async Task RegistrarDevolucion_SobreSalidaSinRetorna_Rechaza()
    {
        var (productoId, ubicacionId) = await CrearProductoYUbicacionAsync();
        await _service.RegistrarEntradaAsync(new RegistrarEntradaDto(productoId, ubicacionId, 20, null), "usuario-1", default);

        var salida = await _service.RegistrarSalidaAsync(
            new RegistrarSalidaDto(productoId, ubicacionId, 10, "Salida normal", false, null, null, null), "usuario-1", default);

        await Assert.ThrowsAsync<MovimientoOrigenInvalidoException>(() =>
            _service.RegistrarDevolucionAsync(new RegistrarDevolucionDto(salida.MovimientoId, ubicacionId, 10, null), "usuario-1", default));
    }
}
