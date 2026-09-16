using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class MovimientoService : IMovimientoService
{
    private readonly InventoryDbContext _db;

    public MovimientoService(InventoryDbContext db) => _db = db;

    public Task<MovimientoResultadoDto> RegistrarEntradaAsync(RegistrarEntradaDto dto, UsuarioActuante usuario, CancellationToken ct) =>
        EjecutarAsync(async (producto, ubicacion, tx) =>
        {
            var movimiento = NuevoMovimiento(producto.Id, TipoMovimiento.Entrada, dto.Cantidad, dto.Cantidad, ubicacion.Id, usuario, dto.Motivo);
            await GuardarConNumeroAsync(movimiento, "MOV", ct);
            return movimiento;
        }, dto.ProductoId, dto.UbicacionId, ct);

    public Task<MovimientoResultadoDto> RegistrarSalidaAsync(RegistrarSalidaDto dto, UsuarioActuante usuario, CancellationToken ct) =>
        EjecutarAsync(async (producto, ubicacion, tx) =>
        {
            if (dto.Cantidad <= 0)
                throw new ArgumentOutOfRangeException(nameof(dto.Cantidad), "La cantidad debe ser mayor a cero.");

            var existenciaUbicacion = await CalcularExistenciaEnUbicacionAsync(producto.Id, ubicacion.Id, ct);
            if (dto.Cantidad > existenciaUbicacion)
                throw new StockInsuficienteException($"{producto.CodigoProducto} en {ubicacion.CodigoUbicacion}", existenciaUbicacion, dto.Cantidad);

            SolicitudDetalle? detalle = null;
            if (dto.SolicitudDetalleId is not null)
            {
                detalle = await _db.SolicitudDetalles.Include(d => d.Solicitud)
                    .FirstOrDefaultAsync(d => d.Id == dto.SolicitudDetalleId, ct)
                    ?? throw new SolicitudEstadoInvalidoException($"No existe la línea de solicitud {dto.SolicitudDetalleId}.");

                if (detalle.CantidadAprobada is null)
                    throw new SolicitudEstadoInvalidoException("La línea de solicitud todavía no fue aprobada.");

                var pendiente = detalle.CantidadAprobada.Value - detalle.CantidadEntregada;
                if (dto.Cantidad > pendiente)
                    throw new SolicitudEstadoInvalidoException($"La salida ({dto.Cantidad}) supera lo pendiente de entregar en la solicitud ({pendiente}).");
            }

            var movimiento = NuevoMovimiento(producto.Id, TipoMovimiento.Salida, dto.Cantidad, -dto.Cantidad, ubicacion.Id, usuario, dto.Motivo);
            movimiento.Retorna = dto.Retorna;
            movimiento.UbicacionExterna = dto.Retorna ? dto.UbicacionExterna : null;
            movimiento.FechaRetornoEsperada = dto.Retorna ? dto.FechaRetornoEsperada : null;
            movimiento.SolicitudDetalleId = detalle?.Id;

            await GuardarConNumeroAsync(movimiento, "MOV", ct);

            if (detalle is not null)
                await AcumularEntregaAsync(detalle, dto.Cantidad, ct);

            return movimiento;
        }, dto.ProductoId, dto.UbicacionId, ct);

    public Task<MovimientoResultadoDto> RegistrarAjusteAsync(RegistrarAjusteDto dto, UsuarioActuante usuario, CancellationToken ct) =>
        EjecutarAsync(async (producto, ubicacion, tx) =>
        {
            if (dto.Cantidad <= 0)
                throw new ArgumentOutOfRangeException(nameof(dto.Cantidad), "La cantidad debe ser mayor a cero.");

            var tipo = dto.EsPositivo ? TipoMovimiento.AjustePositivo : TipoMovimiento.AjusteNegativo;
            var efectiva = dto.EsPositivo ? dto.Cantidad : -dto.Cantidad;

            if (!dto.EsPositivo)
            {
                var existenciaUbicacion = await CalcularExistenciaEnUbicacionAsync(producto.Id, ubicacion.Id, ct);
                if (dto.Cantidad > existenciaUbicacion)
                    throw new StockInsuficienteException($"{producto.CodigoProducto} en {ubicacion.CodigoUbicacion}", existenciaUbicacion, dto.Cantidad);
            }

            var movimiento = NuevoMovimiento(producto.Id, tipo, dto.Cantidad, efectiva, ubicacion.Id, usuario, dto.Motivo);
            await GuardarConNumeroAsync(movimiento, "MOV", ct);
            return movimiento;
        }, dto.ProductoId, dto.UbicacionId, ct);

    public async Task<MovimientoResultadoDto> RegistrarDevolucionAsync(RegistrarDevolucionDto dto, UsuarioActuante usuario, CancellationToken ct)
    {
        if (dto.Cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(dto.Cantidad), "La cantidad debe ser mayor a cero.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var origen = await _db.Movimientos.Include(m => m.Producto)
            .FirstOrDefaultAsync(m => m.Id == dto.MovimientoOrigenId, ct)
            ?? throw new MovimientoOrigenInvalidoException($"No existe el movimiento origen {dto.MovimientoOrigenId}.");

        // CK_Movimiento_OrigenSoloEntrada + la regla de negocio de la guía v4: una
        // devolución solo puede apuntar a una Salida marcada como préstamo (Retorna=1).
        if (origen.TipoMovimiento != TipoMovimiento.Salida || !origen.Retorna)
            throw new MovimientoOrigenInvalidoException("El movimiento origen debe ser una Salida con Retorna = true.");

        var yaDevuelto = await _db.Movimientos
            .Where(m => m.MovimientoOrigenId == origen.Id)
            .SumAsync(m => (decimal?)m.Cantidad, ct) ?? 0m;

        if (yaDevuelto + dto.Cantidad > origen.Cantidad)
            throw new MovimientoOrigenInvalidoException(
                $"La devolución ({dto.Cantidad}) sumada a lo ya devuelto ({yaDevuelto}) supera la cantidad de la salida original ({origen.Cantidad}).");

        var ubicacion = await _db.Ubicaciones.FirstOrDefaultAsync(u => u.Id == dto.UbicacionId && u.Activo, ct)
            ?? throw new UbicacionNoEncontradaException(dto.UbicacionId);

        var movimiento = NuevoMovimiento(origen.ProductoId, TipoMovimiento.Entrada, dto.Cantidad, dto.Cantidad, ubicacion.Id, usuario, dto.Motivo);
        movimiento.MovimientoOrigenId = origen.Id;

        await GuardarConNumeroAsync(movimiento, "MOV", ct);
        await tx.CommitAsync(ct);

        var existenciaResultante = await CalcularExistenciaTotalAsync(origen.ProductoId, ct);
        return new MovimientoResultadoDto(movimiento.Id, movimiento.NumeroMovimiento, "CONFIRMADO", movimiento.FechaMovimiento, existenciaResultante);
    }

    public async Task<IReadOnlyList<MovimientoDto>> ListarPorProductoAsync(int productoId, CancellationToken ct)
    {
        return await _db.Movimientos.AsNoTracking().Include(m => m.Producto).Include(m => m.Ubicacion)
            .Where(m => m.ProductoId == productoId)
            .OrderByDescending(m => m.FechaMovimiento)
            .Select(m => AMovimientoDto(m))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MovimientoDto>> ListarAsync(DateTime? desde, DateTime? hasta, CancellationToken ct)
    {
        var query = _db.Movimientos.AsNoTracking().Include(m => m.Producto).Include(m => m.Ubicacion).AsQueryable();

        if (desde is not null) query = query.Where(m => m.FechaMovimiento >= desde.Value);
        if (hasta is not null) query = query.Where(m => m.FechaMovimiento <= hasta.Value);

        return await query.OrderByDescending(m => m.FechaMovimiento).Select(m => AMovimientoDto(m)).ToListAsync(ct);
    }

    // Equivalente a vw_PrestamosPendientes de la guía v4: salidas con Retorna=1 que
    // todavía no tienen ninguna devolución que las cierre por completo.
    public async Task<IReadOnlyList<MovimientoDto>> ListarPrestamosPendientesAsync(CancellationToken ct)
    {
        var salidas = await _db.Movimientos.AsNoTracking().Include(m => m.Producto).Include(m => m.Ubicacion)
            .Where(m => m.TipoMovimiento == TipoMovimiento.Salida && m.Retorna)
            .ToListAsync(ct);

        if (salidas.Count == 0) return [];

        var ids = salidas.Select(s => s.Id).ToList();
        var devueltoPorOrigen = await _db.Movimientos
            .Where(m => m.MovimientoOrigenId != null && ids.Contains(m.MovimientoOrigenId!.Value))
            .GroupBy(m => m.MovimientoOrigenId!.Value)
            .Select(g => new { OrigenId = g.Key, Total = g.Sum(m => m.Cantidad) })
            .ToDictionaryAsync(x => x.OrigenId, x => x.Total, ct);

        return salidas
            .Where(s => !devueltoPorOrigen.TryGetValue(s.Id, out var devuelto) || devuelto < s.Cantidad)
            .OrderBy(s => s.FechaRetornoEsperada)
            .Select(m => AMovimientoDto(m))
            .ToList();
    }

    // ---- helpers ----

    private async Task<MovimientoResultadoDto> EjecutarAsync(
        Func<Producto, Ubicacion, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction, Task<Movimiento>> accion,
        int productoId, int ubicacionId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == productoId && p.Activo, ct)
            ?? throw new ProductoNoEncontradoException(productoId);

        var ubicacion = await _db.Ubicaciones.FirstOrDefaultAsync(u => u.Id == ubicacionId && u.Activo, ct)
            ?? throw new UbicacionNoEncontradaException(ubicacionId);

        var movimiento = await accion(producto, ubicacion, tx);
        await tx.CommitAsync(ct);

        var existenciaResultante = await CalcularExistenciaTotalAsync(productoId, ct);
        return new MovimientoResultadoDto(movimiento.Id, movimiento.NumeroMovimiento, "CONFIRMADO", movimiento.FechaMovimiento, existenciaResultante);
    }

    private static Movimiento NuevoMovimiento(int productoId, TipoMovimiento tipo, decimal cantidad, decimal cantidadEfectiva, int ubicacionId, UsuarioActuante usuario, string? motivo) => new()
    {
        ProductoId = productoId,
        TipoMovimiento = tipo,
        Cantidad = cantidad,
        CantidadEfectiva = cantidadEfectiva,
        UbicacionId = ubicacionId,
        RegistradoPorId = usuario.Id,
        RegistradoPorNombre = usuario.Nombre,
        Motivo = motivo,
        FechaMovimiento = DateTime.UtcNow,
        NumeroMovimiento = "PENDIENTE", // se reemplaza en GuardarConNumeroAsync una vez que hay Id
    };

    // El número definitivo necesita el Id autogenerado — se guarda en dos pasos,
    // mismo patrón que "INV Numerar solicitud" de la guía v4 (ahí el ID lo daba
    // SharePoint; acá lo da la IDENTITY de SQL Server).
    private async Task GuardarConNumeroAsync(Movimiento movimiento, string prefijo, CancellationToken ct)
    {
        _db.Movimientos.Add(movimiento);
        await _db.SaveChangesAsync(ct);

        movimiento.NumeroMovimiento = $"{prefijo}-{DateTime.UtcNow:yyyy}-{movimiento.Id:D6}";
        await _db.SaveChangesAsync(ct);
    }

    private async Task AcumularEntregaAsync(SolicitudDetalle detalle, decimal cantidadEntregada, CancellationToken ct)
    {
        detalle.CantidadEntregada += cantidadEntregada;

        var solicitud = detalle.Solicitud ?? await _db.Solicitudes.Include(s => s.Detalles).FirstAsync(s => s.Id == detalle.SolicitudId, ct);
        var detalles = solicitud.Detalles.Count > 0 ? solicitud.Detalles : await _db.SolicitudDetalles.Where(d => d.SolicitudId == solicitud.Id).ToListAsync(ct);

        var completa = detalles.All(d => d.CantidadAprobada is not null && d.CantidadEntregada >= d.CantidadAprobada);
        solicitud.Estado = completa ? EstadoSolicitud.Entregada : EstadoSolicitud.EntregadaParcial;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<decimal> CalcularExistenciaTotalAsync(int productoId, CancellationToken ct) =>
        await _db.Movimientos.Where(m => m.ProductoId == productoId).SumAsync(m => (decimal?)m.CantidadEfectiva, ct) ?? 0m;

    private async Task<decimal> CalcularExistenciaEnUbicacionAsync(int productoId, int ubicacionId, CancellationToken ct) =>
        await _db.Movimientos.Where(m => m.ProductoId == productoId && m.UbicacionId == ubicacionId)
            .SumAsync(m => (decimal?)m.CantidadEfectiva, ct) ?? 0m;

    private static MovimientoDto AMovimientoDto(Movimiento m) => new(
        m.Id, m.NumeroMovimiento, m.ProductoId, m.Producto?.Nombre ?? string.Empty, m.TipoMovimiento, m.Cantidad,
        m.UbicacionId, m.Ubicacion?.CodigoUbicacion ?? string.Empty, m.Retorna, m.UbicacionExterna, m.FechaRetornoEsperada,
        m.MovimientoOrigenId, m.SolicitudDetalleId, m.RegistradoPorId, m.RegistradoPorNombre, m.Motivo, m.FechaMovimiento
    );
}
