using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class ResumenService : IResumenService
{
    private const int CantidadRecientes = 5;

    private readonly InventoryDbContext _db;
    private readonly IMovimientoService _movimientos;

    public ResumenService(InventoryDbContext db, IMovimientoService movimientos)
    {
        _db = db;
        _movimientos = movimientos;
    }

    public async Task<ResumenInicioDto> ObtenerResumenInicioAsync(int paisId, CancellationToken ct)
    {
        var activos = _db.Productos.AsNoTracking().Where(p => p.PaisId == paisId && p.Activo);

        var itemsTotales = await activos.CountAsync(ct);

        // Mismo criterio que usaba el frontend: existencia (SUM de CantidadEfectiva, nunca
        // cacheada — ver D2) menor o igual al stock mínimo. Un producto sin movimientos
        // tiene existencia 0, así que con StockMinimo 0 también cuenta, igual que antes.
        var bajoMinimo = await activos.CountAsync(p =>
            (_db.Movimientos.Where(m => m.ProductoId == p.Id).Sum(m => (int?)m.CantidadEfectiva) ?? 0) <= p.StockMinimo, ct);

        // Las fechas se guardan en UTC; el mes se corta en UTC, el desfase de horas en el
        // borde de mes es irrelevante para un contador del tablero.
        var ahora = DateTime.UtcNow;
        var inicioMes = new DateTime(ahora.Year, ahora.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var solicitudesDelMes = await _db.Solicitudes.AsNoTracking()
            .CountAsync(s => s.Area!.PaisId == paisId && s.FechaSolicitud >= inicioMes, ct);

        var prestamos = await _movimientos.ListarPrestamosPendientesAsync(paisId, ct);
        var recientes = await _movimientos.ListarRecientesAsync(paisId, CantidadRecientes, ct);

        return new ResumenInicioDto(bajoMinimo, prestamos.Count, itemsTotales, solicitudesDelMes, recientes);
    }
}
