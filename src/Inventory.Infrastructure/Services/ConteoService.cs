using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class ConteoService : IConteoService
{
    private readonly InventoryDbContext _db;

    public ConteoService(InventoryDbContext db) => _db = db;

    public async Task<ConteoDto> RegistrarAsync(RegistrarConteoDto dto, string usuarioId, CancellationToken ct)
    {
        if (dto.NumeroConteo < 1)
            throw new ArgumentOutOfRangeException(nameof(dto.NumeroConteo), "El número de conteo debe ser 1 o mayor.");
        if (dto.CantidadContada < 0)
            throw new ArgumentOutOfRangeException(nameof(dto.CantidadContada), "La cantidad contada no puede ser negativa.");

        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == dto.ProductoId && p.Activo, ct)
            ?? throw new ProductoNoEncontradoException(dto.ProductoId);

        var ubicacion = await _db.Ubicaciones.FirstOrDefaultAsync(u => u.Id == dto.UbicacionId && u.Activo, ct)
            ?? throw new UbicacionNoEncontradaException(dto.UbicacionId);

        // UQ_Conteo_Sesion (guía v3/v4): permite reconteos, no duplicar el mismo número.
        var yaExiste = await _db.Conteos.AnyAsync(c =>
            c.SesionConteo == dto.SesionConteo && c.ProductoId == dto.ProductoId &&
            c.UbicacionId == dto.UbicacionId && c.NumeroConteo == dto.NumeroConteo, ct);
        if (yaExiste)
            throw new ConteoDuplicadoException(dto.SesionConteo, dto.NumeroConteo);

        var conteo = new Conteo
        {
            SesionConteo = dto.SesionConteo,
            ProductoId = producto.Id,
            UbicacionId = ubicacion.Id,
            NumeroConteo = dto.NumeroConteo,
            CantidadContada = dto.CantidadContada,
            ContadoPor = usuarioId,
            FechaConteo = DateTime.UtcNow,
        };

        _db.Conteos.Add(conteo);
        await _db.SaveChangesAsync(ct);

        var existenciaSistema = await _db.Movimientos
            .Where(m => m.ProductoId == producto.Id && m.UbicacionId == ubicacion.Id)
            .SumAsync(m => (decimal?)m.CantidadEfectiva, ct) ?? 0m;

        return new ConteoDto(
            conteo.Id, conteo.SesionConteo, conteo.ProductoId, producto.Nombre, conteo.UbicacionId,
            ubicacion.CodigoUbicacion, conteo.NumeroConteo, conteo.CantidadContada, conteo.ContadoPor,
            conteo.FechaConteo, existenciaSistema, conteo.CantidadContada - existenciaSistema
        );
    }

    public async Task<IReadOnlyList<ConteoDto>> ListarPorSesionAsync(string sesionConteo, CancellationToken ct)
    {
        var conteos = await _db.Conteos.AsNoTracking()
            .Include(c => c.Producto).Include(c => c.Ubicacion)
            .Where(c => c.SesionConteo == sesionConteo)
            .OrderBy(c => c.Producto!.Nombre).ThenBy(c => c.NumeroConteo)
            .ToListAsync(ct);

        if (conteos.Count == 0) return [];

        var claves = conteos.Select(c => (c.ProductoId, c.UbicacionId)).Distinct().ToList();
        var existencias = new Dictionary<(int, int), decimal>();
        foreach (var (productoId, ubicacionId) in claves)
        {
            existencias[(productoId, ubicacionId)] = await _db.Movimientos
                .Where(m => m.ProductoId == productoId && m.UbicacionId == ubicacionId)
                .SumAsync(m => (decimal?)m.CantidadEfectiva, ct) ?? 0m;
        }

        return conteos.Select(c =>
        {
            var existenciaSistema = existencias[(c.ProductoId, c.UbicacionId)];
            return new ConteoDto(
                c.Id, c.SesionConteo, c.ProductoId, c.Producto!.Nombre, c.UbicacionId, c.Ubicacion!.CodigoUbicacion,
                c.NumeroConteo, c.CantidadContada, c.ContadoPor, c.FechaConteo, existenciaSistema, c.CantidadContada - existenciaSistema
            );
        }).ToList();
    }
}
