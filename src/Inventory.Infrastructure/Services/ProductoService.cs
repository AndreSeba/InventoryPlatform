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
    private readonly IAuditoriaService _auditoria;

    public ProductoService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // Misma forma de objeto para "antes" y "después" — así el frontend puede diffear
    // campo por campo. Nunca los bytes de la imagen: infla la auditoría con base64 sin
    // aportar nada legible, alcanza con saber si tenía una.
    private static object Snapshot(Producto p) => new
    {
        p.ClaveProducto, p.CodigoProducto, p.Nombre, p.CategoriaId, p.UnidadMedida,
        p.CostoUnitario, p.StockMinimo, p.Detalle, p.TieneImagen, p.Activo,
    };

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

    // Ver IProductoService — paralelo a ListarAsync, solo para Productos/Index (catálogo
    // grande tras las cargas masivas). Filtra/cuenta/pagina en SQL y recién ahí calcula
    // existencia/próximo vencimiento, solo para las filas de ESA página (MapearConExistenciaAsync
    // ya soporta listas chicas — antes se la llamaba con el catálogo entero).
    public async Task<PaginaDto<ProductoDto>> ListarPaginadoAsync(int paisId, int? categoriaId, bool incluirInactivos, string? busqueda, int pagina, int tamanoPagina, CancellationToken ct)
    {
        // Topes: una página enorme desbordaba el OFFSET de SQL y daba 500.
        pagina = Math.Clamp(pagina, 1, 1_000_000);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);

        var query = _db.Productos.AsNoTracking().Include(p => p.Categoria).Include(p => p.Pais)
            .Where(p => p.PaisId == paisId);

        if (!incluirInactivos)
            query = query.Where(p => p.Activo);

        if (categoriaId is not null)
            query = query.Where(p => p.CategoriaId == categoriaId);

        var q = busqueda?.Trim();
        if (q is { Length: > 100 }) q = q[..100]; // un LIKE de miles de caracteres da 500 en SQL
        if (!string.IsNullOrEmpty(q))
            query = query.Where(p => p.Nombre.Contains(q) || p.CodigoProducto.Contains(q));

        query = query.OrderBy(p => p.Nombre);

        var total = await query.CountAsync(ct);
        var productosDePagina = await query.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToListAsync(ct);
        var items = await MapearConExistenciaAsync(productosDePagina, ct);

        return new PaginaDto<ProductoDto>(items, total);
    }

    public async Task<ProductoDto> ObtenerPorIdAsync(int id, int paisId, CancellationToken ct)
    {
        var producto = await _db.Productos.AsNoTracking().Include(p => p.Categoria).Include(p => p.Pais)
            .FirstOrDefaultAsync(p => p.Id == id && p.PaisId == paisId, ct)
            ?? throw new ProductoNoEncontradoException(id);

        var existencia = await CalcularExistenciaAsync(id, ct);
        var proximoVencimiento = await CalcularProximoVencimientoAsync(id, ct);
        return AProductoDto(producto, existencia, proximoVencimiento);
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

        var nombre = ValidarCampos(dto.Nombre, dto.CostoUnitario, dto.StockMinimo, dto.Detalle);
        var unidad = await ResolverUnidadAsync(dto.UnidadMedida, paisId, ct);
        ValidarImagen(dto.ImagenData, dto.ImagenContentType);

        // El correlativo se calcula leyendo lo que hay: dos altas simultáneas pueden elegir el
        // mismo. Se serializan con un candado de aplicación por país (vale hasta el commit); el
        // índice único (País, ClaveProducto) queda como respaldo y dispara un reintento.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (_db.Database.IsSqlServer())
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC sp_getapplock @Resource = {"producto-codigo:" + paisId}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000", ct);

        Producto producto;
        string clave;
        var intento = 0;
        while (true)
        {
            var codigo = await GenerarCodigoAsync(categoria, paisId, ct);
            clave = $"{codigo}-{unidad}"; // regla de la guía v4: Codigo + '-' + Unidad

            // Único por (País, ClaveProducto) — dos países pueden llegar al mismo código.
            if (await _db.Productos.AnyAsync(p => p.PaisId == paisId && p.ClaveProducto == clave && p.Activo, ct))
                throw new CodigoProductoDuplicadoException(clave);

            producto = new Producto
            {
                ClaveProducto = clave,
                CodigoProducto = codigo,
                Nombre = nombre,
                CategoriaId = dto.CategoriaId,
                PaisId = paisId,
                UnidadMedida = unidad,
                CostoUnitario = dto.CostoUnitario,
                StockMinimo = dto.StockMinimo,
                Detalle = dto.Detalle?.Trim(),
                TieneImagen = dto.ImagenData is not null,
                Imagen = dto.ImagenData is not null
                    ? new ProductoImagen { Datos = dto.ImagenData, ContentType = dto.ImagenContentType! }
                    : null,
                Activo = true,
            };

            _db.Productos.Add(producto);
            try
            {
                await _db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (++intento < 8)
            {
                _db.ChangeTracker.Clear(); // descarta el producto fallido y reintenta con el siguiente número
                categoria = await _db.Categorias.AsNoTracking().FirstAsync(c => c.Id == dto.CategoriaId, ct);
                pais = await _db.Paises.AsNoTracking().FirstAsync(p => p.Id == paisId, ct);
            }
        }

        await tx.CommitAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Producto), clave, "Crear", null, _auditoria.Capturar(Snapshot(producto)), paisId, usuario, null, ct);

        return AProductoDto(producto, categoria, pais, 0, null); // recién creado, sin entradas todavía
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

        var nombreNuevo = ValidarCampos(dto.Nombre, dto.CostoUnitario, dto.StockMinimo, dto.Detalle);
        var valorAnterior = _auditoria.Capturar(Snapshot(producto));

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

        producto.Nombre = nombreNuevo;
        producto.CategoriaId = dto.CategoriaId;
        producto.CostoUnitario = dto.CostoUnitario;
        producto.StockMinimo = dto.StockMinimo;
        producto.Detalle = dto.Detalle?.Trim();

        // null = "no tocar la imagen actual" — evita reenviar los bytes ya guardados
        // solo porque se editó otro campo del producto (ver comentario en el DTO).
        if (dto.ImagenData is not null)
        {
            ValidarImagen(dto.ImagenData, dto.ImagenContentType);

            var imagen = await _db.ProductoImagenes.FirstOrDefaultAsync(i => i.ProductoId == id, ct);
            if (imagen is null)
                _db.ProductoImagenes.Add(new ProductoImagen { ProductoId = id, Datos = dto.ImagenData, ContentType = dto.ImagenContentType! });
            else
            {
                imagen.Datos = dto.ImagenData;
                imagen.ContentType = dto.ImagenContentType!;
            }
            producto.TieneImagen = true;
        }

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Producto), producto.ClaveProducto, "Actualizar", valorAnterior, _auditoria.Capturar(Snapshot(producto)), paisId, usuario, null, ct);

        var existencia = await CalcularExistenciaAsync(id, ct);
        var proximoVencimiento = await CalcularProximoVencimientoAsync(id, ct);
        return AProductoDto(producto, categoria, producto.Pais!, existencia, proximoVencimiento);
    }

    public async Task DesactivarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == id && p.PaisId == paisId, ct)
            ?? throw new ProductoNoEncontradoException(id);

        var anterior = _auditoria.Capturar(Snapshot(producto));
        producto.Activo = false;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Producto), producto.ClaveProducto, "Desactivar", anterior, _auditoria.Capturar(Snapshot(producto)), paisId, usuario, null, ct);
    }

    public async Task<(byte[] Datos, string ContentType)> ObtenerImagenAsync(int id, CancellationToken ct)
    {
        var imagen = await _db.ProductoImagenes.AsNoTracking()
            .Where(i => i.ProductoId == id)
            .Select(i => new { i.Datos, i.ContentType })
            .FirstOrDefaultAsync(ct);

        if (imagen is null)
            throw new ProductoNoEncontradoException(id);

        return (imagen.Datos, imagen.ContentType);
    }

    // Usado por Movimientos (Salida/Ajuste negativo) y por Conteo físico para no dejar
    // elegir/generar una fila en una ubicación donde este producto no tiene nada guardado.
    // Mismo criterio de agregación que MovimientoService.CalcularExistenciaEnUbicacionAsync,
    // acá agrupado por TODAS las ubicaciones del producto de una sola pasada.
    public async Task<IReadOnlyList<UbicacionConExistenciaDto>> ListarUbicacionesConStockAsync(int productoId, int paisId, CancellationToken ct)
    {
        // Filtrado por el país del producto: sin esto, un usuario de Perú veía el stock y
        // las ubicaciones de un producto de Bolivia con solo conocer su id.
        return await _db.Movimientos.AsNoTracking()
            .Where(m => m.ProductoId == productoId && m.Producto!.PaisId == paisId)
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
    //
    // El correlativo es el MÁXIMO ya usado con ese prefijo + 1, no la cantidad de productos de
    // la categoría: dos categorías que comparten las 4 primeras letras (ej. «MKT-MABEL» y
    // «MKT-CAFE») generaban el mismo código y la segunda ya no podía crear productos. El
    // prefijo usa solo letras y números, así «MKT-MABEL» -> «MKTM» y «MKT-CAFE» -> «MKTC».
    private async Task<string> GenerarCodigoAsync(Categoria categoria, int paisId, CancellationToken ct)
    {
        var alfanumerico = new string(categoria.CodigoCategoria.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var prefijo = alfanumerico.Length >= 4 ? alfanumerico[..4] : alfanumerico;
        if (prefijo.Length == 0) prefijo = "PROD";

        var existentes = await _db.Productos.AsNoTracking()
            .Where(p => p.PaisId == paisId && p.CodigoProducto.StartsWith(prefijo + "-"))
            .Select(p => p.CodigoProducto)
            .ToListAsync(ct);

        var maximo = 0;
        foreach (var c in existentes)
            if (int.TryParse(c[(prefijo.Length + 1)..], out var n) && n > maximo)
                maximo = n;

        return $"{prefijo}-{(maximo + 1):D2}";
    }

    // Nombre obligatorio y topes que coinciden con las columnas: antes un nombre vacío entraba
    // y uno largo o un costo negativo terminaban en un 500 de la base.
    private static string ValidarCampos(string? nombre, decimal? costo, int stockMinimo, string? detalle)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        if (limpio.Length == 0)
            throw new ValidacionException("El nombre del producto es obligatorio.");
        if (limpio.Length > 255)
            throw new ValidacionException("El nombre del producto no puede superar los 255 caracteres.");
        if (detalle is not null && detalle.Trim().Length > 2000)
            throw new ValidacionException("El detalle no puede superar los 2000 caracteres.");
        if (costo is < 0 or > 1_000_000_000m)
            throw new ValidacionException("El costo unitario debe estar entre 0 y 1.000.000.000.");
        if (stockMinimo is < 0 or > 1_000_000_000)
            throw new ValidacionException("El stock mínimo debe estar entre 0 y 1.000.000.000.");
        return limpio;
    }

    private async Task<int> CalcularExistenciaAsync(int productoId, CancellationToken ct)
    {
        return await _db.Movimientos
            .Where(m => m.ProductoId == productoId)
            .SumAsync(m => (int?)m.CantidadEfectiva, ct) ?? 0;
    }

    // MÍNIMO FechaVencimiento entre las Entradas de este producto que la tienen cargada —
    // null si ninguna. Ver comentario en ProductoDto.ProximoVencimiento.
    private async Task<DateOnly?> CalcularProximoVencimientoAsync(int productoId, CancellationToken ct)
    {
        return await _db.Movimientos
            .Where(m => m.ProductoId == productoId && m.TipoMovimiento == TipoMovimiento.Entrada && m.FechaVencimiento != null)
            .Select(m => m.FechaVencimiento)
            .MinAsync(ct);
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

        var proximoVencimientoPorProducto = await _db.Movimientos
            .Where(m => ids.Contains(m.ProductoId) && m.TipoMovimiento == TipoMovimiento.Entrada && m.FechaVencimiento != null)
            .GroupBy(m => m.ProductoId)
            .Select(g => new { ProductoId = g.Key, Proximo = g.Min(m => m.FechaVencimiento) })
            .ToDictionaryAsync(x => x.ProductoId, x => x.Proximo, ct);

        return productos.Select(p =>
            AProductoDto(p,
                existenciaPorProducto.TryGetValue(p.Id, out var e) ? e : 0,
                proximoVencimientoPorProducto.TryGetValue(p.Id, out var pv) ? pv : null)
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

        // El Content-Type lo manda el cliente: se confirma mirando los primeros bytes.
        var real = datos switch
        {
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
            [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
            _ => null,
        };
        if (real is null || !string.Equals(real, contentType, StringComparison.OrdinalIgnoreCase))
            throw new ArchivoInvalidoException("El archivo no es una imagen JPG, PNG o WEBP válida.");
    }

    private static ProductoDto AProductoDto(Producto p, int existencia, DateOnly? proximoVencimiento) =>
        AProductoDto(p, p.Categoria!, p.Pais!, existencia, proximoVencimiento);

    private static ProductoDto AProductoDto(Producto p, Categoria categoria, Pais pais, int existencia, DateOnly? proximoVencimiento) => new(
        p.Id, p.ClaveProducto, p.CodigoProducto, p.Nombre, p.CategoriaId, categoria.CodigoCategoria,
        p.PaisId, pais.Nombre,
        p.UnidadMedida, p.CostoUnitario, p.StockMinimo, p.Detalle,
        p.TieneImagen ? $"/api/productos/{p.Id}/imagen" : null,
        p.Activo, existencia, proximoVencimiento
    );
}
