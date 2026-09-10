using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class SolicitudService : ISolicitudService
{
    private readonly InventoryDbContext _db;

    public SolicitudService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<SolicitudDto>> ListarAsync(string? estado, CancellationToken ct)
    {
        var query = _db.Solicitudes.AsNoTracking()
            .Include(s => s.Area)
            .Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado) && Enum.TryParse<EstadoSolicitud>(estado, true, out var estadoEnum))
            query = query.Where(s => s.Estado == estadoEnum);

        var solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync(ct);
        return solicitudes.Select(ASolicitudDto).ToList();
    }

    public async Task<SolicitudDto> ObtenerPorIdAsync(int id, CancellationToken ct)
    {
        var solicitud = await _db.Solicitudes.AsNoTracking()
            .Include(s => s.Area)
            .Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new SolicitudNoEncontradaException(id);

        return ASolicitudDto(solicitud);
    }

    public async Task<SolicitudDto> CrearAsync(CrearSolicitudDto dto, string usuarioId, CancellationToken ct)
    {
        if (dto.Detalles.Count == 0)
            throw new SolicitudEstadoInvalidoException("Una solicitud necesita al menos una línea.");

        if (dto.Detalles.Select(d => d.ProductoId).Distinct().Count() != dto.Detalles.Count)
            throw new SolicitudEstadoInvalidoException("Un mismo producto no puede repetirse en la misma solicitud.");

        var area = await _db.Areas.FirstOrDefaultAsync(a => a.Id == dto.AreaId && a.Activo, ct)
            ?? throw new SolicitudEstadoInvalidoException($"No existe un área activa con id {dto.AreaId}.");

        var productoIds = dto.Detalles.Select(d => d.ProductoId).ToList();
        var productosValidos = await _db.Productos.Where(p => productoIds.Contains(p.Id) && p.Activo).Select(p => p.Id).ToListAsync(ct);
        var faltante = productoIds.Except(productosValidos).FirstOrDefault();
        if (faltante != 0)
            throw new ProductoNoEncontradoException(faltante);

        var solicitud = new Solicitud
        {
            AreaId = area.Id,
            Estado = EstadoSolicitud.Pendiente,
            FechaSolicitud = DateTime.UtcNow,
            SolicitadoPor = usuarioId,
            NumeroSolicitud = "PENDIENTE",
        };
        solicitud.Detalles = dto.Detalles.Select(d => new SolicitudDetalle
        {
            ProductoId = d.ProductoId,
            CantidadSolicitada = d.CantidadSolicitada,
        }).ToList();

        _db.Solicitudes.Add(solicitud);
        await _db.SaveChangesAsync(ct);

        // Mismo patrón de numeración en dos pasos que Movimiento (necesita el Id autogenerado).
        solicitud.NumeroSolicitud = $"SOL-{DateTime.UtcNow:yyyy}-{solicitud.Id:D6}";
        await _db.SaveChangesAsync(ct);

        await _db.Entry(solicitud).Reference(s => s.Area).LoadAsync(ct);
        foreach (var d in solicitud.Detalles)
            await _db.Entry(d).Reference(x => x.Producto).LoadAsync(ct);

        return ASolicitudDto(solicitud);
    }

    public async Task<SolicitudDto> AprobarAsync(int id, AprobarSolicitudDto dto, string usuarioId, CancellationToken ct)
    {
        var solicitud = await _db.Solicitudes.Include(s => s.Area).Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new SolicitudNoEncontradaException(id);

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
            throw new SolicitudEstadoInvalidoException($"Solo se puede aprobar una solicitud en estado Pendiente (actual: {solicitud.Estado}).");

        foreach (var linea in dto.Detalles)
        {
            var detalle = solicitud.Detalles.FirstOrDefault(d => d.Id == linea.SolicitudDetalleId)
                ?? throw new SolicitudEstadoInvalidoException($"La línea {linea.SolicitudDetalleId} no pertenece a esta solicitud.");

            if (linea.CantidadAprobada < 0 || linea.CantidadAprobada > detalle.CantidadSolicitada)
                throw new SolicitudEstadoInvalidoException(
                    $"La cantidad aprobada para '{detalle.Producto?.Nombre}' no puede superar la solicitada ({detalle.CantidadSolicitada}).");

            detalle.CantidadAprobada = linea.CantidadAprobada;
        }

        solicitud.Estado = EstadoSolicitud.Aprobada;
        solicitud.AprobadoPor = usuarioId;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ASolicitudDto(solicitud);
    }

    public async Task<SolicitudDto> RechazarAsync(int id, RechazarSolicitudDto dto, string usuarioId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.MotivoRechazo))
            throw new SolicitudEstadoInvalidoException("Una solicitud rechazada requiere motivo de rechazo.");

        var solicitud = await _db.Solicitudes.Include(s => s.Area).Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new SolicitudNoEncontradaException(id);

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
            throw new SolicitudEstadoInvalidoException($"Solo se puede rechazar una solicitud en estado Pendiente (actual: {solicitud.Estado}).");

        solicitud.Estado = EstadoSolicitud.Rechazada;
        solicitud.MotivoRechazo = dto.MotivoRechazo;
        solicitud.AprobadoPor = usuarioId;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ASolicitudDto(solicitud);
    }

    private static SolicitudDto ASolicitudDto(Solicitud s) => new(
        s.Id, s.NumeroSolicitud, s.AreaId, s.Area?.NombreArea ?? string.Empty, s.Estado,
        s.FechaSolicitud, s.SolicitadoPor, s.AprobadoPor, s.FechaResolucion, s.MotivoRechazo,
        s.Detalles.Select(d => new SolicitudDetalleDto(
            d.Id, d.ProductoId, d.Producto?.Nombre ?? string.Empty, d.CantidadSolicitada, d.CantidadAprobada, d.CantidadEntregada
        )).ToList()
    );
}
