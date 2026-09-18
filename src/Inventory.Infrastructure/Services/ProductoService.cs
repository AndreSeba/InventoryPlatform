using System.Text.Json;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class ProductoService : IProductoService
{
    private readonly InventoryDbContext _db;

    public ProductoService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProductoDto>> ListarAsync(int paisId, int? categoriaId, bool incluirInactivos, CancellationToken ct)
    {
        var query = _db.Productos.AsNoTracking().Include(p => p.Categoria).Include(p => p.Pais)
            .Where(p => p.PaisId == paisId);

        if (!incluirInactivos)
            query = query.Where(p => p.Activo);

        if (categoriaId is not null)
            query = query.Where(p => p.CategoriaId == categoriaId);

        var productos = await query.OrderBy(p => p.Nombre).ToListAsync(ct);
        return await MapearConExistenciaAsync(productos, ct);
    }

    public async Task<ProductoDto> ObtenerPorIdAsync(int id, int paisId, CancellationToken ct)
    {
        var producto = await _db.Productos.AsNoTracking().Include(p => p.Categoria).Include(p => p.Pais)
            .FirstOrDefaultAsync(p => p.Id == id && p.PaisId == paisId, ct)
            ?? throw new ProductoNoEncontradoException(id);

        var existencia = await CalcularExistenciaAsync(id, ct);
        return AProductoDto(producto, existencia);
    }

    public async Task<SiguienteCodigoDto> ObtenerSiguienteCodigoAsync(int categoriaId, int paisId, CancellationToken ct)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == categoriaId && c.PaisId == paisId && c.Activo, ct)
            ?? throw new CategoriaNoEncontradaException(categoriaId);

        var codigo = await GenerarCodigoAsync(categoria, paisId, ct);
        return new SiguienteCodigoDto(codigo);
    }

    public async Task<ProductoDto> CrearAsync(CrearProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == dto.CategoriaId && c.PaisId == paisId && c.Activo, ct)
            ?? throw new CategoriaNoEncontradaException(dto.CategoriaId);

        var pais = await _db.Paises.FirstOrDefaultAsync(p => p.Id == paisId && p.Activo, ct)
            ?? throw new PaisNoEncontradoException(paisId);

        var codigo = await GenerarCodigoAsync(categoria, paisId, ct);
        var unidad = await ResolverUnidadAsync(dto.UnidadMedida, paisId, ct);
        var clave = $"{codigo}-{unidad}"; // regla de la guía v4: Codigo + '-' + Unidad

        // Único por (País, ClaveProducto) — dos países pueden llegar al mismo código.
        var yaExiste = await _db.Productos.AnyAsync(p => p.PaisId == paisId && p.ClaveProducto == clave && p.Activo, ct);
        if (yaExiste)
            throw new CodigoProductoDuplicadoException(clave);

        ValidarImagen(dto.ImagenData, dto.ImagenContentType);

        var producto = new Producto
        {
            ClaveProducto = clave,
            CodigoProducto = codigo,
            Nombre = dto.Nombre,
            CategoriaId = dto.CategoriaId,
            PaisId = paisId,
            UnidadMedida = unidad,
            CostoUnitario = dto.CostoUnitario,
            StockMinimo = dto.StockMinimo,
            Detalle = dto.Detalle,
            ImagenData = dto.ImagenData,
            ImagenContentType = dto.ImagenData is not null ? dto.ImagenContentType : null,
            Activo = true,
        };

        _db.Productos.Add(producto);

        _db.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuario.Id,
            UsuarioNombre = usuario.Nombre,
            Entidad = nameof(Producto),
            EntidadId = clave,
            Accion = "Crear",
            ValorNuevo = JsonSerializer.Serialize(dto),
        });

        await _db.SaveChangesAsync(ct);
        return AProductoDto(producto, categoria, pais, 0);
    }

    // Las unidades válidas salen del catálogo Unidad (editable desde /unidades), ya no
    // de un CHECK fijo en la base. Escopeado por país — la unidad de un país no sirve
    // para validar un producto de otro. Devuelve el código normalizado en mayúsculas.
    private async Task<string> ResolverUnidadAsync(string unidadMedida, int paisId, CancellationToken ct)
    {
        var codigo = (unidadMedida ?? string.Empty).Trim().ToUpperInvariant();

        var existe = await _db.Unidades.AnyAsync(u => u.CodigoUnidad == codigo && u.PaisId == paisId && u.Activo, ct);
        if (!existe)
            throw new UnidadNoEncontradaException(codigo);

        return codigo;
    }

    public async Task<ProductoDto> ActualizarAsync(int id, ActualizarProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var producto = await _db.Productos.Include(p => p.Categoria).Include(p => p.Pais)
            .FirstOrDefaultAsync(p => p.Id == id && p.PaisId == paisId, ct)
            ?? throw new ProductoNoEncontradoException(id);

        var categoria = producto.CategoriaId == dto.CategoriaId
            ? producto.Categoria!
            : await _db.Categorias.FirstOrDefaultAsync(c => c.Id == dto.CategoriaId && c.PaisId == paisId && c.Activo, ct)
                ?? throw new CategoriaNoEncontradaException(dto.CategoriaId);

        var valorAnterior = JsonSerializer.Serialize(new
        {
            producto.Nombre,
            producto.CategoriaId,
            producto.UnidadMedida,
            producto.CostoUnitario,
            producto.StockMinimo,
            producto.Detalle,
            TeniaImagen = producto.ImagenData is not null,
        });

        var unidadNueva = await ResolverUnidadAsync(dto.UnidadMedida, paisId, ct);
        if (unidadNueva != producto.UnidadMedida)
        {
            // ClaveProducto es "{CodigoProducto}-{Unidad}" (regla de la guía v4). Antes
            // se cambiaba la unidad sin regenerar la clave, así que quedaba mintiendo
            // (clave ...-UNI con UnidadMedida CAJA). Se regenera y se revalida que la
            // nueva no choque con otro producto activo del mismo país.
            var claveNueva = $"{producto.CodigoProducto}-{unidadNueva}";
            var claveEnUso = await _db.Productos
                .AnyAsync(p => p.PaisId == paisId && p.ClaveProducto == claveNueva && p.Activo && p.Id != producto.Id, ct);
            if (claveEnUso)
                throw new CodigoProductoDuplicadoException(claveNueva);

            producto.ClaveProducto = claveNueva;
            producto.UnidadMedida = unidadNueva;
        }

        producto.Nombre = dto.Nombre;
        producto.CategoriaId = dto.CategoriaId;
        producto.CostoUnitario = dto.CostoUnitario;
        producto.StockMinimo = dto.StockMinimo;
        producto.Detalle = dto.Detalle;

        // null = "no tocar la imagen actual" — evita reenviar los bytes ya guardados
        // solo porque se editó otro campo del producto (ver comentario en el DTO).
        if (dto.ImagenData is not null)
        {
            ValidarImagen(dto.ImagenData, dto.ImagenContentType);
            producto.ImagenData = dto.ImagenData;
            producto.ImagenContentType = dto.ImagenContentType;
        }

        _db.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuario.Id,
            UsuarioNombre = usuario.Nombre,
            Entidad = nameof(Producto),
            EntidadId = producto.ClaveProducto,
            Accion = "Actualizar",
            ValorAnterior = valorAnterior,
            ValorNuevo = JsonSerializer.Serialize(dto),
        });

        await _db.SaveChangesAsync(ct);

        var existencia = await CalcularExistenciaAsync(id, ct);
        return AProductoDto(producto, categoria, producto.Pais!, existencia);
    }

    public async Task DesactivarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == id && p.PaisId == paisId, ct)
            ?? throw new ProductoNoEncontradoException(id);

        producto.Activo = false;

        _db.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuario.Id,
            UsuarioNombre = usuario.Nombre,
            Entidad = nameof(Producto),
            EntidadId = producto.ClaveProducto,
            Accion = "Desactivar",
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Datos, string ContentType)> ObtenerImagenAsync(int id, CancellationToken ct)
    {
        var producto = await _db.Productos.AsNoTracking()
            .Select(p => new { p.Id, p.ImagenData, p.ImagenContentType })
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (producto?.ImagenData is null)
            throw new ProductoNoEncontradoException(id);

        return (producto.ImagenData, producto.ImagenContentType ?? "application/octet-stream");
    }

    // Usado por Movimientos (Salida/Ajuste negativo) y por Conteo físico para no dejar
    // elegir/generar una fila en una ubicación donde este producto no tiene nada guardado.
    // Mismo criterio de agregación que MovimientoService.CalcularExistenciaEnUbicacionAsync,
    // acá agrupado por TODAS las ubicaciones del producto de una sola pasada.
    public async Task<IReadOnlyList<UbicacionConExistenciaDto>> ListarUbicacionesConStockAsync(int productoId, CancellationToken ct)
    {
        return await _db.Movimientos.AsNoTracking()
            .Where(m => m.ProductoId == productoId)
            .GroupBy(m => new
            {
                m.UbicacionId, m.Ubicacion!.CodigoUbicacion,
                AlmacenId = m.Ubicacion!.Almacen!.Id, AlmacenNombre = m.Ubicacion!.Almacen!.Nombre,
            })
            .Where(g => g.Sum(m => m.CantidadEfectiva) > 0)
            .OrderBy(g => g.Key.AlmacenNombre).ThenBy(g => g.Key.CodigoUbicacion)
            .Select(g => new UbicacionConExistenciaDto(
                g.Key.UbicacionId, g.Key.CodigoUbicacion, g.Key.AlmacenId, g.Key.AlmacenNombre, g.Sum(m => m.CantidadEfectiva)))
            .ToListAsync(ct);
    }

    // CERE-01: primeras 4 letras del código de categoría + correlativo de 2 dígitos.
    // Cuenta TODOS los productos de la categoría Y país (activos e inactivos) para que
    // el correlativo nunca retroceda ni se repita si alguno se desactiva — Bolivia y
    // Perú arrancan cada uno su propio conteo, por eso el filtro incluye PaisId.
    private async Task<string> GenerarCodigoAsync(Categoria categoria, int paisId, CancellationToken ct)
    {
        var prefijo = categoria.CodigoCategoria.Length >= 4
            ? categoria.CodigoCategoria[..4].ToUpperInvariant()
            : categoria.CodigoCategoria.ToUpperInvariant();

        var cantidadEnCategoria = await _db.Productos.CountAsync(p => p.CategoriaId == categoria.Id && p.PaisId == paisId, ct);
        return $"{prefijo}-{(cantidadEnCategoria + 1):D2}";
    }

    private async Task<int> CalcularExistenciaAsync(int productoId, CancellationToken ct)
    {
        return await _db.Movimientos
            .Where(m => m.ProductoId == productoId)
            .SumAsync(m => (int?)m.CantidadEfectiva, ct) ?? 0;
    }

    private async Task<List<ProductoDto>> MapearConExistenciaAsync(List<Producto> productos, CancellationToken ct)
    {
        if (productos.Count == 0)
            return [];

        var ids = productos.Select(p => p.Id).ToList();

        var existenciaPorProducto = await _db.Movimientos
            .Where(m => ids.Contains(m.ProductoId))
            .GroupBy(m => m.ProductoId)
            .Select(g => new { ProductoId = g.Key, Existencia = g.Sum(m => m.CantidadEfectiva) })
            .ToDictionaryAsync(x => x.ProductoId, x => x.Existencia, ct);

        return productos.Select(p =>
            AProductoDto(p, existenciaPorProducto.TryGetValue(p.Id, out var e) ? e : 0)
        ).ToList();
    }

    private static readonly HashSet<string> TiposImagenPermitidos =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    private const long TamanoMaximoImagenBytes = 5 * 1024 * 1024; // 5 MB

    // Antes vivía en AlmacenamientoImagenesService (disco) — misma regla, ahora corre acá
    // porque la imagen ya no pasa por un endpoint de upload aparte, viaja en el mismo
    // Crear/Actualizar.
    private static void ValidarImagen(byte[]? datos, string? contentType)
    {
        if (datos is null) return;

        if (datos.Length == 0)
            throw new ArchivoInvalidoException("El archivo está vacío.");
        if (datos.Length > TamanoMaximoImagenBytes)
            throw new ArchivoInvalidoException("La imagen no puede superar los 5 MB.");
        if (contentType is null || !TiposImagenPermitidos.Contains(contentType))
            throw new ArchivoInvalidoException("Formato no permitido. Usá JPG, PNG o WEBP.");
    }

    private static ProductoDto AProductoDto(Producto p, int existencia) => AProductoDto(p, p.Categoria!, p.Pais!, existencia);

    private static ProductoDto AProductoDto(Producto p, Categoria categoria, Pais pais, int existencia) => new(
        p.Id, p.ClaveProducto, p.CodigoProducto, p.Nombre, p.CategoriaId, categoria.CodigoCategoria,
        p.PaisId, pais.Nombre,
        p.UnidadMedida, p.CostoUnitario, p.StockMinimo, p.Detalle,
        p.ImagenData is not null ? $"/api/productos/{p.Id}/imagen" : null,
        p.Activo, existencia
    );
}
