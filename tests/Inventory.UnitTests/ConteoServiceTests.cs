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

public class ConteoServiceTests : IDisposable
{
    private const int PaisId = 1;
    private const int OtroPaisId = 2; // Perú, semilla de PaisConfiguration

    private readonly SqliteConnection _connection;
    private readonly InventoryDbContext _db;
    private readonly ConteoService _service;
    private readonly MovimientoService _movimientos;
    private readonly UsuarioActuante _usuario;

    public ConteoServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var auditoria = new AuditoriaService(_db);
        _service = new ConteoService(_db, auditoria);
        _movimientos = new MovimientoService(_db, auditoria);

        var usuario = new Usuario
        {
            Email = "conteo@inventario.local", NombreCompleto = "Contador", PasswordHash = "x",
            RolId = 1, PaisId = PaisId, Activo = true,
        };
        _db.Usuarios.Add(usuario);
        _db.SaveChanges();
        _usuario = new UsuarioActuante(usuario.Id, usuario.NombreCompleto);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static readonly byte[] PngValido = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

    // Crea un producto con stock en una ubicación y devuelve sus ids.
    private async Task<(int ProductoId, int UbicacionId)> ProductoConStockAsync(string codigo, int stock, int paisId = PaisId)
    {
        var categoria = new Categoria { CodigoCategoria = "C" + codigo, PaisId = paisId, Activo = true };
        var ubicacion = new Ubicacion
        {
            AlmacenId = 1, TipoUbicacion = TipoUbicacion.Rack, Nro = "01", Lado = "A", Nivel = "01",
            CodigoUbicacion = $"A-01-01-{codigo}", Activo = true,
        };
        var producto = new Producto
        {
            ClaveProducto = $"{codigo}-UNI", CodigoProducto = codigo, Nombre = $"Producto {codigo}", Categoria = categoria,
            PaisId = paisId, UnidadMedida = "UNI", CostoUnitario = 1m, Activo = true,
        };
        _db.AddRange(categoria, ubicacion, producto);
        await _db.SaveChangesAsync();

        // El almacén sembrado es de Bolivia: solo ahí se puede mover stock en estos tests.
        if (paisId == PaisId)
            await _movimientos.RegistrarAjusteAsync(
                new RegistrarAjusteDto(producto.Id, ubicacion.Id, stock, true, "Stock inicial de prueba"), paisId, _usuario, default);
        return (producto.Id, ubicacion.Id);
    }

    private async Task<ConteoDetalleDto> CrearConteoAsync(params int[] productoIds)
        => await _service.CrearAsync(new CrearConteoDto("Conteo de prueba", null, productoIds.ToList()), PaisId, _usuario, default);

    private Task<ConteoDetalleDto> ContarTodoAsync(ConteoDetalleDto conteo, Func<ConteoLineaDto, int> cantidad)
        => _service.GuardarCantidadesAsync(conteo.Resumen.Id,
            new GuardarCantidadesDto(conteo.Lineas.Select(l => new CantidadLineaDto(l.Id, cantidad(l))).ToList()),
            PaisId, _usuario, default);

    [Fact]
    public async Task Crear_ArmaLineasConFotoDeLaExistenciaYCodigoUnico()
    {
        var (p1, _) = await ProductoConStockAsync("P1", 40);
        var (p2, _) = await ProductoConStockAsync("P2", 15);

        var a = await CrearConteoAsync(p1, p2);
        var b = await CrearConteoAsync(p1);

        Assert.StartsWith("CONT-", a.Resumen.Codigo);
        Assert.NotEqual(a.Resumen.Codigo, b.Resumen.Codigo);
        Assert.Equal(EstadoConteo.EnCurso, a.Resumen.Estado);
        Assert.Equal(2, a.Lineas.Count);
        Assert.Contains(a.Lineas, l => l.ProductoId == p1 && l.ExistenciaSistema == 40 && l.CantidadContada is null && l.Diferencia is null);
    }

    [Fact]
    public async Task Crear_SinProductosOConProductoAjeno_Falla()
    {
        await Assert.ThrowsAsync<SeleccionDeProductosVaciaException>(() =>
            _service.CrearAsync(new CrearConteoDto(null, null, []), PaisId, _usuario, default));

        var (peru, _) = await ProductoConStockAsync("PE1", 5, OtroPaisId);
        await Assert.ThrowsAsync<ConteoInvalidoException>(() => CrearConteoAsync(peru));
    }

    [Fact]
    public async Task Crear_ProductoSinStock_Falla()
    {
        var categoria = new Categoria { CodigoCategoria = "VACIA", PaisId = PaisId, Activo = true };
        var producto = new Producto
        {
            ClaveProducto = "SINSTOCK-UNI", CodigoProducto = "SINSTOCK", Nombre = "Sin stock", Categoria = categoria,
            PaisId = PaisId, UnidadMedida = "UNI", CostoUnitario = 1m, Activo = true,
        };
        _db.AddRange(categoria, producto);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConteoInvalidoException>(() => CrearConteoAsync(producto.Id));
    }

    [Fact]
    public async Task Crear_TextosDemasiadoLargos_DanErrorDeNegocioNoDeBase()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        await Assert.ThrowsAsync<ConteoInvalidoException>(() =>
            _service.CrearAsync(new CrearConteoDto(new string('x', 151), null, [p]), PaisId, _usuario, default));
    }

    [Fact]
    public async Task GuardarCantidades_CalculaDiferenciaContraLaFoto()
    {
        var (p, _) = await ProductoConStockAsync("P1", 40);
        var conteo = await CrearConteoAsync(p);

        var guardado = await ContarTodoAsync(conteo, _ => 37);

        Assert.Equal(-3, guardado.Lineas.Single().Diferencia);
        Assert.Equal(1, guardado.Resumen.LineasContadas);
        Assert.Equal(1, guardado.Resumen.LineasConDiferencia);
    }

    [Fact]
    public async Task GuardarCantidades_Negativa_Excedida_OLineaAjena_Fallan()
    {
        var (p, _) = await ProductoConStockAsync("P1", 40);
        var (q, _) = await ProductoConStockAsync("P2", 40);
        var conteo = await CrearConteoAsync(p);
        var otro = await CrearConteoAsync(q);
        var lineaId = conteo.Lineas.Single().Id;

        await Assert.ThrowsAsync<ConteoInvalidoException>(() => _service.GuardarCantidadesAsync(conteo.Resumen.Id,
            new GuardarCantidadesDto([new CantidadLineaDto(lineaId, -1)]), PaisId, _usuario, default));
        await Assert.ThrowsAsync<ConteoInvalidoException>(() => _service.GuardarCantidadesAsync(conteo.Resumen.Id,
            new GuardarCantidadesDto([new CantidadLineaDto(lineaId, int.MaxValue)]), PaisId, _usuario, default));
        // La línea del OTRO conteo no se puede tocar desde este.
        await Assert.ThrowsAsync<ConteoInvalidoException>(() => _service.GuardarCantidadesAsync(conteo.Resumen.Id,
            new GuardarCantidadesDto([new CantidadLineaDto(otro.Lineas.Single().Id, 5)]), PaisId, _usuario, default));
    }

    [Fact]
    public async Task Cerrar_SinEvidencia_OConLineasSinContar_Falla()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        var conteo = await CrearConteoAsync(p);

        var sinContar = await Assert.ThrowsAsync<ConteoEstadoInvalidoException>(() => _service.CerrarAsync(conteo.Resumen.Id, PaisId, _usuario, default));
        Assert.Contains("por contar", sinContar.Message);

        await ContarTodoAsync(conteo, _ => 10);
        var sinEvidencia = await Assert.ThrowsAsync<ConteoEstadoInvalidoException>(() => _service.CerrarAsync(conteo.Resumen.Id, PaisId, _usuario, default));
        Assert.Contains("evidencia", sinEvidencia.Message);
    }

    [Fact]
    public async Task Cerrar_ConTodoContadoYEvidencia_Cierra_YYaNoSeModifica()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        var conteo = await CrearConteoAsync(p);
        await ContarTodoAsync(conteo, _ => 10);
        await _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, "hoja.png", PngValido, PaisId, _usuario, default);

        var cerrado = await _service.CerrarAsync(conteo.Resumen.Id, PaisId, _usuario, default);

        Assert.Equal(EstadoConteo.Cerrado, cerrado.Resumen.Estado);
        Assert.NotNull(cerrado.Resumen.FechaCierre);
        Assert.Equal("Contador", cerrado.Resumen.CerradoPor);

        await Assert.ThrowsAsync<ConteoEstadoInvalidoException>(() => ContarTodoAsync(conteo, _ => 99));
        await Assert.ThrowsAsync<ConteoEstadoInvalidoException>(() =>
            _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, "otra.png", PngValido, PaisId, _usuario, default));
        await Assert.ThrowsAsync<ConteoEstadoInvalidoException>(() =>
            _service.EliminarEvidenciaAsync(conteo.Resumen.Id, cerrado.Evidencias.Single().Id, PaisId, _usuario, default));
        // Un segundo cierre tampoco pasa.
        await Assert.ThrowsAsync<ConteoEstadoInvalidoException>(() => _service.CerrarAsync(conteo.Resumen.Id, PaisId, _usuario, default));
    }

    [Fact]
    public async Task Evidencia_SeValidaPorContenidoNoPorExtension()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        var conteo = await CrearConteoAsync(p);

        await Assert.ThrowsAsync<ArchivoInvalidoException>(() =>
            _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, "falsa.png", "esto no es una imagen"u8.ToArray(), PaisId, _usuario, default));
        await Assert.ThrowsAsync<ArchivoInvalidoException>(() =>
            _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, "vacia.png", [], PaisId, _usuario, default));
        // Un zip con extensión .png no entra; uno con .xlsx sí pasa el chequeo de firma.
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0x00];
        await Assert.ThrowsAsync<ArchivoInvalidoException>(() =>
            _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, "zip.png", zip, PaisId, _usuario, default));

        var ok = await _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, "..\\..\\ruta\\hoja.PNG", PngValido, PaisId, _usuario, default);
        Assert.Equal("image/png", ok.ContentType);
        Assert.Equal("hoja.PNG", ok.NombreArchivo); // sin directorios
    }

    [Fact]
    public async Task Evidencia_TopeDeArchivosPorConteo()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        var conteo = await CrearConteoAsync(p);

        for (var i = 0; i < 10; i++)
            await _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, $"h{i}.png", PngValido, PaisId, _usuario, default);

        await Assert.ThrowsAsync<ConteoInvalidoException>(() =>
            _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, "h11.png", PngValido, PaisId, _usuario, default));
    }

    [Fact]
    public async Task Cancelar_ExigeMotivo_YQuedaCancelado()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        var conteo = await CrearConteoAsync(p);

        await Assert.ThrowsAsync<ConteoInvalidoException>(() =>
            _service.CancelarAsync(conteo.Resumen.Id, new CancelarConteoDto("  "), PaisId, _usuario, default));

        var cancelado = await _service.CancelarAsync(conteo.Resumen.Id, new CancelarConteoDto("Se contó el rack equivocado"), PaisId, _usuario, default);
        Assert.Equal(EstadoConteo.Cancelado, cancelado.Resumen.Estado);
        Assert.Equal("Se contó el rack equivocado", cancelado.MotivoCancelacion);

        await Assert.ThrowsAsync<ConteoEstadoInvalidoException>(() => _service.CerrarAsync(conteo.Resumen.Id, PaisId, _usuario, default));
    }

    [Fact]
    public async Task Reconteo_SoloDeConteoCerradoConDiferencias_ConLasLineasQueDifieren()
    {
        var (p1, _) = await ProductoConStockAsync("P1", 10);
        var (p2, _) = await ProductoConStockAsync("P2", 20);
        var conteo = await CrearConteoAsync(p1, p2);

        // En curso: no se puede.
        await Assert.ThrowsAsync<ConteoEstadoInvalidoException>(() => _service.CrearReconteoAsync(conteo.Resumen.Id, PaisId, _usuario, default));

        await ContarTodoAsync(conteo, l => l.ProductoId == p1 ? 10 : 18); // P2 difiere
        await _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, "h.png", PngValido, PaisId, _usuario, default);
        await _service.CerrarAsync(conteo.Resumen.Id, PaisId, _usuario, default);

        var reconteo = await _service.CrearReconteoAsync(conteo.Resumen.Id, PaisId, _usuario, default);

        Assert.Equal(conteo.Resumen.Id, reconteo.Resumen.ConteoOrigenId);
        Assert.Equal(conteo.Resumen.Codigo, reconteo.Resumen.ConteoOrigenCodigo);
        Assert.Equal(EstadoConteo.EnCurso, reconteo.Resumen.Estado);
        Assert.Equal(p2, reconteo.Lineas.Single().ProductoId);

        // Un segundo reconteo mientras el primero está abierto: rechazado.
        await Assert.ThrowsAsync<ConteoEstadoInvalidoException>(() => _service.CrearReconteoAsync(conteo.Resumen.Id, PaisId, _usuario, default));
    }

    [Fact]
    public async Task Reconteo_SinDiferencias_Falla()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        var conteo = await CrearConteoAsync(p);
        await ContarTodoAsync(conteo, _ => 10);
        await _service.AdjuntarEvidenciaAsync(conteo.Resumen.Id, "h.png", PngValido, PaisId, _usuario, default);
        await _service.CerrarAsync(conteo.Resumen.Id, PaisId, _usuario, default);

        await Assert.ThrowsAsync<ConteoInvalidoException>(() => _service.CrearReconteoAsync(conteo.Resumen.Id, PaisId, _usuario, default));
    }

    [Fact]
    public async Task OtroPais_NoVeNiTocaNada()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        var conteo = await CrearConteoAsync(p);
        var id = conteo.Resumen.Id;

        Assert.Empty(await _service.ListarAsync(null, OtroPaisId, default));
        await Assert.ThrowsAsync<ConteoNoEncontradoException>(() => _service.ObtenerAsync(id, OtroPaisId, default));
        await Assert.ThrowsAsync<ConteoNoEncontradoException>(() => _service.GenerarHojaAsync(id, OtroPaisId, default));
        await Assert.ThrowsAsync<ConteoNoEncontradoException>(() =>
            _service.GuardarCantidadesAsync(id, new GuardarCantidadesDto([new CantidadLineaDto(conteo.Lineas.Single().Id, 1)]), OtroPaisId, _usuario, default));
        await Assert.ThrowsAsync<ConteoNoEncontradoException>(() => _service.CerrarAsync(id, OtroPaisId, _usuario, default));
        await Assert.ThrowsAsync<ConteoNoEncontradoException>(() =>
            _service.CancelarAsync(id, new CancelarConteoDto("no es mío"), OtroPaisId, _usuario, default));
        await Assert.ThrowsAsync<ConteoNoEncontradoException>(() =>
            _service.AdjuntarEvidenciaAsync(id, "h.png", PngValido, OtroPaisId, _usuario, default));
    }

    [Fact]
    public async Task Excel_GenerarYReimportar_CargaLasCantidades_YRechazaHojaDeOtroConteo()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        var conteo = await CrearConteoAsync(p);
        var otro = await CrearConteoAsync(p);

        var (hoja, nombre) = await _service.GenerarHojaAsync(conteo.Resumen.Id, PaisId, default);
        Assert.Equal($"{conteo.Resumen.Codigo}.xlsx", nombre);

        // Se llena la columna "Cantidad Contada" (9) de la primera fila de datos (6).
        byte[] llena;
        using (var libro = new ClosedXML.Excel.XLWorkbook(new MemoryStream(hoja)))
        {
            libro.Worksheet(1).Cell(6, 9).Value = 7;
            using var ms = new MemoryStream();
            libro.SaveAs(ms);
            llena = ms.ToArray();
        }

        // Hoja de OTRO conteo: se rechaza.
        await Assert.ThrowsAsync<ArchivoInvalidoException>(() =>
            _service.ImportarHojaAsync(otro.Resumen.Id, llena, "h.xlsx", false, PaisId, _usuario, default));

        var resultado = await _service.ImportarHojaAsync(conteo.Resumen.Id, llena, "h.xlsx", true, PaisId, _usuario, default);
        Assert.Equal(1, resultado.LineasActualizadas);
        Assert.True(resultado.EvidenciaAdjuntada);

        var detalle = await _service.ObtenerAsync(conteo.Resumen.Id, PaisId, default);
        Assert.Equal(7, detalle.Lineas.Single().CantidadContada);
        Assert.Equal(-3, detalle.Lineas.Single().Diferencia);
        Assert.Contains(detalle.Evidencias, e => e.NombreArchivo == "h.xlsx");
    }

    [Fact]
    public async Task Excel_CantidadConDecimalesOTexto_SeRechaza()
    {
        var (p, _) = await ProductoConStockAsync("P1", 10);
        var conteo = await CrearConteoAsync(p);
        var (hoja, _) = await _service.GenerarHojaAsync(conteo.Resumen.Id, PaisId, default);

        foreach (var valor in new object[] { 3.5, "mucho", -2 })
        {
            byte[] llena;
            using (var libro = new ClosedXML.Excel.XLWorkbook(new MemoryStream(hoja)))
            {
                libro.Worksheet(1).Cell(6, 9).Value = ClosedXML.Excel.XLCellValue.FromObject(valor);
                using var ms = new MemoryStream();
                libro.SaveAs(ms);
                llena = ms.ToArray();
            }
            await Assert.ThrowsAsync<ArchivoInvalidoException>(() =>
                _service.ImportarHojaAsync(conteo.Resumen.Id, llena, "h.xlsx", false, PaisId, _usuario, default));
        }
    }
}
