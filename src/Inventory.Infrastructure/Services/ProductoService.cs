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

    public async Task<IReadOnlyList<ProductoDto>> ListarAsync(int? categoriaId, bool incluirInactivos, CancellationToken ct)
    {
        var query = _db.Productos.AsNoTracking().Include(p => p.Categoria).AsQueryable();

        if (!incluirInactivos)
            query = query.Where(p => p.Activo);

        if (categoriaId is not null)
            query = query.Where(p => p.CategoriaId == categoriaId);

        var productos = await query.OrderBy(p => p.Nombre).ToListAsync(ct);
        return await MapearConExistenciaAsync(productos, ct);
    }

    public async Task<ProductoDto> ObtenerPorIdAsync(int id, CancellationToken ct)
    {
        var producto = await _db.Productos.AsNoTracking().Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new ProductoNoEncontradoException(id);

        var existencia = await CalcularExistenciaAsync(id, ct);
        return AProductoDto(producto, existencia);
    }

    public async Task<ProductoDto> CrearAsync(CrearProductoDto dto, string usuarioId, CancellationToken ct)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == dto.CategoriaId && c.Activo, ct)
            ?? throw new CategoriaNoEncontradaException(dto.CategoriaId);

        var codigo = dto.CodigoProducto.Trim().ToUpperInvariant();
        var unidad = dto.UnidadMedida.Trim().ToUpperInvariant();
        var clave = $"{codigo}-{unidad}"; // regla de la guía v4: Codigo + '-' + Unidad

        var yaExiste = await _db.Productos.AnyAsync(p => p.ClaveProducto == clave && p.Activo, ct);
        if (yaExiste)
            throw new CodigoProductoDuplicadoException(clave);

        var producto = new Producto
        {
            ClaveProducto = clave,
            CodigoProducto = codigo,
            Nombre = dto.Nombre,
            CategoriaId = dto.CategoriaId,
            UnidadMedida = unidad,
            CostoUnitario = dto.CostoUnitario,
            StockMinimo = dto.StockMinimo,
            Detalle = dto.Detalle,
            ImagenUrl = dto.ImagenUrl,
            Activo = true,
        };

        _db.Productos.Add(producto);

        _db.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = nameof(Producto),
            EntidadId = clave,
            Accion = "Crear",
            ValorNuevo = JsonSerializer.Serialize(dto),
        });

        await _db.SaveChangesAsync(ct);
        return AProductoDto(producto, categoria, 0);
    }

    public async Task<ProductoDto> ActualizarAsync(int id, ActualizarProductoDto dto, string usuarioId, CancellationToken ct)
    {
        var producto = await _db.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new ProductoNoEncontradoException(id);

        var categoria = producto.CategoriaId == dto.CategoriaId
            ? producto.Categoria!
            : await _db.Categorias.FirstOrDefaultAsync(c => c.Id == dto.CategoriaId && c.Activo, ct)
                ?? throw new CategoriaNoEncontradaException(dto.CategoriaId);

        var valorAnterior = JsonSerializer.Serialize(new
        {
            producto.Nombre, producto.CategoriaId, producto.UnidadMedida,
            producto.CostoUnitario, producto.StockMinimo, producto.Detalle, producto.ImagenUrl,
        });

        producto.Nombre = dto.Nombre;
        producto.CategoriaId = dto.CategoriaId;
        producto.UnidadMedida = dto.UnidadMedida.Trim().ToUpperInvariant();
        producto.CostoUnitario = dto.CostoUnitario;
        producto.StockMinimo = dto.StockMinimo;
        producto.Detalle = dto.Detalle;
        producto.ImagenUrl = dto.ImagenUrl;

        _db.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = nameof(Producto),
            EntidadId = producto.ClaveProducto,
            Accion = "Actualizar",
            ValorAnterior = valorAnterior,
            ValorNuevo = JsonSerializer.Serialize(dto),
        });

        await _db.SaveChangesAsync(ct);

        var existencia = await CalcularExistenciaAsync(id, ct);
        return AProductoDto(producto, categoria, existencia);
    }

    public async Task DesactivarAsync(int id, string usuarioId, CancellationToken ct)
    {
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new ProductoNoEncontradoException(id);

        producto.Activo = false;

        _db.Auditorias.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = nameof(Producto),
            EntidadId = producto.ClaveProducto,
            Accion = "Desactivar",
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task<decimal> CalcularExistenciaAsync(int productoId, CancellationToken ct)
    {
        return await _db.Movimientos
            .Where(m => m.ProductoId == productoId)
            .SumAsync(m => (decimal?)m.CantidadEfectiva, ct) ?? 0m;
    }

    private async Task<List<ProductoDto>> MapearConExistenciaAsync(List<Producto> productos, CancellationToken ct)
    {
        if (productos.Count == 0)
            return [];

        var ids = productos.Select(p => p.Id).ToList();

        // Existencia de TODOS los productos listados en una sola consulta agrupada,
        // evita N+1 en el listado.
        var existenciaPorProducto = await _db.Movimientos
            .Where(m => ids.Contains(m.ProductoId))
            .GroupBy(m => m.ProductoId)
            .Select(g => new { ProductoId = g.Key, Existencia = g.Sum(m => m.CantidadEfectiva) })
            .ToDictionaryAsync(x => x.ProductoId, x => x.Existencia, ct);

        return productos.Select(p =>
            AProductoDto(p, existenciaPorProducto.TryGetValue(p.Id, out var e) ? e : 0m)
        ).ToList();
    }

    private static ProductoDto AProductoDto(Producto p, decimal existencia) => AProductoDto(p, p.Categoria!, existencia);

    private static ProductoDto AProductoDto(Producto p, Categoria categoria, decimal existencia) => new(
        p.Id, p.ClaveProducto, p.CodigoProducto, p.Nombre, p.CategoriaId, categoria.CodigoCategoria,
        p.UnidadMedida, p.CostoUnitario, p.StockMinimo, p.Detalle, p.ImagenUrl, p.Activo, existencia
    );
}
