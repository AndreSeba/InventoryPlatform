using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.UnitTests;

// Datos iniciales del módulo Conteo Externo (los mismos que trae por defecto la app de escritorio de Fagal).
public class ConteoExternoSemillaTests : IDisposable
{
    private const int Bolivia = 1;
    private const int Peru = 2;

    private readonly SqliteConnection _connection;
    private readonly InventoryDbContext _db;
    private readonly IServiceProvider _services;

    public ConteoExternoSemillaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton(_db);
        _services = servicios.BuildServiceProvider();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Siembra_UnidadesAlmacenesYCatalogoDeBolivia()
    {
        await ConteoExternoSemilla.SembrarAsync(_services);

        var unidades = await _db.ConteoExternoUnidades.Where(u => u.PaisId == Bolivia).ToListAsync();
        Assert.Equal(new[] { "CAJ", "KG", "PACK" }, unidades.Select(u => u.Codigo).OrderBy(c => c).ToArray());
        Assert.Equal(2, unidades.Single(u => u.Codigo == "CAJ").DecimalesPermitidos);
        Assert.Equal(3, unidades.Single(u => u.Codigo == "KG").DecimalesPermitidos);

        var almacenes = await _db.ConteoExternoAlmacenes.Where(a => a.PaisId == Bolivia).ToListAsync();
        Assert.Equal(8, almacenes.Count);
        Assert.Equal("Alm. Prod. Terminado - Lotes", almacenes.Single(a => a.Codigo == "103004").Nombre);

        var productos = await _db.ConteoExternoProductos.Where(p => p.PaisId == Bolivia).ToListAsync();
        Assert.Equal(414, productos.Count);                                          // 421 filas, 414 códigos únicos
        Assert.Equal(productos.Count, productos.Select(p => p.CodigoNormalizado).Distinct().Count());
        Assert.All(productos, p => Assert.Equal(OrigenConteoExternoProducto.BaseDatos, p.Origen));
        Assert.All(productos, p => Assert.Null(p.UnidadCodigo));                     // la unidad se fija con el primer lote
        Assert.All(productos, p => Assert.Null(p.CostoReferencia));
    }

    [Fact]
    public async Task Siembra_NoTocaOtrosPaises()
    {
        await ConteoExternoSemilla.SembrarAsync(_services);

        Assert.False(await _db.ConteoExternoUnidades.AnyAsync(u => u.PaisId == Peru));
        Assert.False(await _db.ConteoExternoAlmacenes.AnyAsync(a => a.PaisId == Peru));
        Assert.False(await _db.ConteoExternoProductos.AnyAsync(p => p.PaisId == Peru));
    }

    [Fact]
    public async Task Siembra_EsIdempotente_YNoPisaLoQueElUsuarioYaTiene()
    {
        // El usuario ya cargó una unidad y un almacén propios: esos bloques no se siembran.
        _db.ConteoExternoUnidades.Add(new ConteoExternoUnidad { Codigo = "UNI", DecimalesPermitidos = 0, PaisId = Bolivia, Activo = true });
        _db.ConteoExternoAlmacenes.Add(new ConteoExternoAlmacen { Codigo = "999", Nombre = "Mío", PaisId = Bolivia, Activo = true });
        await _db.SaveChangesAsync();

        await ConteoExternoSemilla.SembrarAsync(_services);
        await ConteoExternoSemilla.SembrarAsync(_services);

        Assert.Equal("UNI", (await _db.ConteoExternoUnidades.SingleAsync()).Codigo);
        Assert.Equal("999", (await _db.ConteoExternoAlmacenes.SingleAsync()).Codigo);
        Assert.Equal(414, await _db.ConteoExternoProductos.CountAsync());             // el catálogo sí, una sola vez
    }
}
