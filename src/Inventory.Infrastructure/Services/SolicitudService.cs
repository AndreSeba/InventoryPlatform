using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Services;

public class SolicitudService : ISolicitudService
{
    private readonly InventoryDbContext _db;
    private readonly ISolicitudNotificationService _notificationService;
    private readonly ILogger<SolicitudService> _logger;

    public SolicitudService(InventoryDbContext db, ISolicitudNotificationService notificationService, ILogger<SolicitudService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SolicitudDto>> ListarAsync(int paisId, string? estado, CancellationToken ct)
    {
        var query = _db.Solicitudes.AsNoTracking()
            .Include(s => s.Area)
            .Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .Where(s => s.Area!.PaisId == paisId);

        if (!string.IsNullOrWhiteSpace(estado) && Enum.TryParse<EstadoSolicitud>(estado, true, out var estadoEnum))
            query = query.Where(s => s.Estado == estadoEnum);

        var solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync(ct);
        return solicitudes.Select(ASolicitudDto).ToList();
    }

    // Mismo filtro por estado y país que ListarAsync, más SolicitadoPorId == usuarioId —
    // usada por el rol Solicitante, que no tiene permiso para ver las de todos (ver
    // Permisos.InicioVer y RolPermisoConfiguration.PermisosSolicitante).
    public async Task<IReadOnlyList<SolicitudDto>> ListarMiasAsync(int usuarioId, int paisId, string? estado, CancellationToken ct)
    {
        var query = _db.Solicitudes.AsNoTracking()
            .Include(s => s.Area)
            .Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .Where(s => s.SolicitadoPorId == usuarioId && s.Area!.PaisId == paisId);

        if (!string.IsNullOrWhiteSpace(estado) && Enum.TryParse<EstadoSolicitud>(estado, true, out var estadoEnum))
            query = query.Where(s => s.Estado == estadoEnum);

        var solicitudes = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync(ct);
        return solicitudes.Select(ASolicitudDto).ToList();
    }

    public async Task<SolicitudDto> ObtenerPorIdAsync(int id, int paisId, CancellationToken ct)
    {
        var solicitud = await _db.Solicitudes.AsNoTracking()
            .Include(s => s.Area)
            .Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(s => s.Id == id && s.Area!.PaisId == paisId, ct)
            ?? throw new SolicitudNoEncontradaException(id);

        return ASolicitudDto(solicitud);
    }

    public async Task<SolicitudDto> CrearAsync(CrearSolicitudDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (dto.Detalles.Count == 0)
            throw new SolicitudEstadoInvalidoException("Una solicitud necesita al menos una línea.");

        if (dto.Detalles.Select(d => d.ProductoId).Distinct().Count() != dto.Detalles.Count)
            throw new SolicitudEstadoInvalidoException("Un mismo producto no puede repetirse en la misma solicitud.");

        // Área y productos tienen que ser del mismo país que la sesión — nunca se arma
        // una solicitud mezclando el área de un país con productos de otro.
        var area = await _db.Areas.FirstOrDefaultAsync(a => a.Id == dto.AreaId && a.PaisId == paisId && a.Activo, ct)
            ?? throw new SolicitudEstadoInvalidoException($"No existe un área activa con id {dto.AreaId}.");

        var productoIds = dto.Detalles.Select(d => d.ProductoId).ToList();
        var productosValidos = await _db.Productos.Where(p => productoIds.Contains(p.Id) && p.PaisId == paisId && p.Activo).Select(p => p.Id).ToListAsync(ct);
        var faltante = productoIds.Except(productosValidos).FirstOrDefault();
        if (faltante != 0)
            throw new ProductoNoEncontradoException(faltante);

        var solicitud = new Solicitud
        {
            AreaId = area.Id,
            Tipo = dto.Tipo,
            Estado = EstadoSolicitud.Pendiente,
            FechaSolicitud = DateTime.UtcNow,
            SolicitadoPorId = usuario.Id,
            SolicitadoPorNombre = usuario.Nombre,
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

        var solicitudDto = ASolicitudDto(solicitud);
        await AvisarEncargadosAsync(solicitud, solicitudDto, ct);

        return solicitudDto;
    }

    // Agrupa las líneas por la categoría de cada producto, se queda solo con las
    // categorías que tienen encargado asignado, funde en un solo grupo dos categorías con
    // el mismo encargado (una sola notificación con todas sus líneas), y avisa. Una falla
    // acá nunca tira abajo la creación — la solicitud ya quedó guardada.
    private async Task AvisarEncargadosAsync(Solicitud solicitud, SolicitudDto solicitudDto, CancellationToken ct)
    {
        try
        {
            var productoIds = solicitud.Detalles.Select(d => d.ProductoId).ToList();
            var productos = await _db.Productos.AsNoTracking()
                .Include(p => p.Categoria).ThenInclude(c => c!.Encargado)
                .Where(p => productoIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            var grupos = solicitudDto.Detalles
                .Select(d => (Detalle: d, Producto: productos.GetValueOrDefault(d.ProductoId)))
                .Where(x => x.Producto?.Categoria?.Encargado is not null)
                .GroupBy(x => x.Producto!.Categoria!.EncargadoId!.Value)
                .Select(g => new EncargadoNotificacionDto(
                    g.Key,
                    g.First().Producto!.Categoria!.Encargado!.NombreCompleto,
                    g.First().Producto!.Categoria!.Encargado!.Email,
                    g.Select(x => x.Detalle).ToList()
                ))
                .ToList();

            if (grupos.Count > 0)
                await _notificationService.NotificarNuevaSolicitudAsync(solicitudDto, grupos, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo avisar a los encargados de la solicitud {NumeroSolicitud} — la solicitud igual quedó creada.", solicitud.NumeroSolicitud);
        }
    }

    public async Task<SolicitudDto> AprobarAsync(int id, AprobarSolicitudDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var solicitud = await _db.Solicitudes.Include(s => s.Area).Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(s => s.Id == id && s.Area!.PaisId == paisId, ct)
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
        solicitud.AprobadoPorId = usuario.Id;
        solicitud.AprobadoPorNombre = usuario.Nombre;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ASolicitudDto(solicitud);
    }

    public async Task<SolicitudDto> RechazarAsync(int id, RechazarSolicitudDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.MotivoRechazo))
            throw new SolicitudEstadoInvalidoException("Una solicitud rechazada requiere motivo de rechazo.");

        var solicitud = await _db.Solicitudes.Include(s => s.Area).Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(s => s.Id == id && s.Area!.PaisId == paisId, ct)
            ?? throw new SolicitudNoEncontradaException(id);

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
            throw new SolicitudEstadoInvalidoException($"Solo se puede rechazar una solicitud en estado Pendiente (actual: {solicitud.Estado}).");

        solicitud.Estado = EstadoSolicitud.Rechazada;
        solicitud.MotivoRechazo = dto.MotivoRechazo;
        solicitud.AprobadoPorId = usuario.Id;
        solicitud.AprobadoPorNombre = usuario.Nombre;
        solicitud.FechaResolucion = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ASolicitudDto(solicitud);
    }

    private static SolicitudDto ASolicitudDto(Solicitud s) => new(
        s.Id, s.NumeroSolicitud, s.AreaId, s.Area?.NombreArea ?? string.Empty, s.Tipo, s.Estado,
        s.FechaSolicitud, s.SolicitadoPorId, s.SolicitadoPorNombre,
        s.AprobadoPorId, s.AprobadoPorNombre, s.FechaResolucion, s.MotivoRechazo,
        s.Detalles.Select(d => new SolicitudDetalleDto(
            d.Id, d.ProductoId, d.Producto?.Nombre ?? string.Empty, d.Producto?.CodigoProducto ?? string.Empty,
            d.Producto?.UnidadMedida ?? string.Empty, d.Producto?.CostoUnitario,
            d.CantidadSolicitada, d.CantidadAprobada, d.CantidadEntregada
        )).ToList()
    );
}
