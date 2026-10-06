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
    private readonly IAuditoriaService _auditoria;
    private readonly Controles.ControlesOptions _controles;

    public SolicitudService(InventoryDbContext db, ISolicitudNotificationService notificationService, ILogger<SolicitudService> logger, IAuditoriaService auditoria,
        Microsoft.Extensions.Options.IOptions<Controles.ControlesOptions>? controles = null)
    {
        _db = db;
        _notificationService = notificationService;
        _logger = logger;
        _auditoria = auditoria;
        _controles = controles?.Value ?? Controles.ControlesOptions.Permisivo;
    }

    // Separación de funciones: quien pide no aprueba ni rechaza su propia solicitud.
    private void ExigirOtraPersona(Solicitud solicitud, UsuarioActuante usuario, string accion)
    {
        if (_controles.SeparacionDeFunciones && solicitud.SolicitadoPorId == usuario.Id)
            throw new SeparacionDeFuncionesException($"No podés {accion} tu propia solicitud: debe hacerlo otra persona.");
    }

    private static object SnapshotEstado(Solicitud s) => new
    {
        s.Estado, s.MotivoRechazo,
        Detalles = s.Detalles.Select(d => new { d.Id, d.CantidadAprobada, d.CantidadEntregada }).ToList(),
    };

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
        if (dto.Tipo is not (TipoSolicitud.Entrada or TipoSolicitud.Salida))
            throw new ValidacionException("El tipo de solicitud debe ser Entrada o Salida.");

        if (dto.Detalles is not { Count: > 0 })
            throw new SolicitudEstadoInvalidoException("Una solicitud necesita al menos una línea.");
        if (dto.Detalles.Count > 200)
            throw new ValidacionException("Una solicitud admite hasta 200 líneas.");

        foreach (var d in dto.Detalles)
        {
            if (d.CantidadSolicitada is < 1 or > 1_000_000_000)
                throw new ValidacionException("La cantidad solicitada debe ser un entero entre 1 y 1.000.000.000.");

            if (d.Retorna)
            {
                // Mismo criterio que una Salida con préstamo en Movimientos: sin destino y
                // fecha de retorno no hay nada que reclamar después.
                if (string.IsNullOrWhiteSpace(d.UbicacionExterna))
                    throw new ValidacionException("Indicá a dónde va el material en cada línea que retorna.");
                if (d.UbicacionExterna.Trim().Length > 255)
                    throw new ValidacionException("La ubicación externa no puede superar los 255 caracteres.");
                if (d.FechaRetornoEsperada is null)
                    throw new ValidacionException("Indicá la fecha de retorno esperada en cada línea que retorna.");
                if (d.FechaRetornoEsperada.Value < DateOnly.FromDateTime(DateTime.Today))
                    throw new ValidacionException("La fecha de retorno esperada no puede estar en el pasado.");
            }
        }

        if (dto.Detalles.Select(d => d.ProductoId).Distinct().Count() != dto.Detalles.Count)
            throw new SolicitudEstadoInvalidoException("Un mismo producto no puede repetirse en la misma solicitud.");

        // Retorna (préstamo) solo tiene sentido en una Solicitud de Salida.
        if (dto.Tipo != TipoSolicitud.Salida && dto.Detalles.Any(d => d.Retorna))
            throw new SolicitudEstadoInvalidoException("El retorno de material solo aplica a solicitudes de Salida.");

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
            // Provisorio ÚNICO (no "PENDIENTE" fijo): con una constante compartida, dos altas
            // simultáneas chocaban contra el índice único y una de cada dos daba 500.
            NumeroSolicitud = "TMP-" + Guid.NewGuid().ToString("N"),
        };
        solicitud.Detalles = dto.Detalles.Select(d => new SolicitudDetalle
        {
            ProductoId = d.ProductoId,
            CantidadSolicitada = d.CantidadSolicitada,
            Retorna = d.Retorna,
            UbicacionExterna = d.Retorna ? d.UbicacionExterna!.Trim() : null,
            FechaRetornoEsperada = d.Retorna ? d.FechaRetornoEsperada : null,
        }).ToList();

        _db.Solicitudes.Add(solicitud);
        await _db.SaveChangesAsync(ct);

        // Mismo patrón de numeración en dos pasos que Movimiento (necesita el Id autogenerado).
        solicitud.NumeroSolicitud = $"SOL-{DateTime.UtcNow:yyyy}-{solicitud.Id:D6}";
        await _db.SaveChangesAsync(ct);

        await _db.Entry(solicitud).Reference(s => s.Area).LoadAsync(ct);
        foreach (var d in solicitud.Detalles)
            await _db.Entry(d).Reference(x => x.Producto).LoadAsync(ct);

        var snapshot = new { solicitud.NumeroSolicitud, solicitud.AreaId, solicitud.Tipo, solicitud.Estado,
            Detalles = solicitud.Detalles.Select(d => new { d.ProductoId, d.CantidadSolicitada, d.Retorna }).ToList() };
        await _auditoria.RegistrarAsync(nameof(Solicitud), solicitud.NumeroSolicitud, "Crear", null, _auditoria.Capturar(snapshot), paisId, usuario, null, ct);

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

        ExigirOtraPersona(solicitud, usuario, "aprobar");

        var anterior = _auditoria.Capturar(SnapshotEstado(solicitud));

        // Hay que decidir TODAS las líneas: aprobar "sin líneas" dejaba la solicitud Aprobada
        // sin cantidades, y de ahí pasaba a Entregada sin haber entregado nada.
        var lineasDto = dto.Detalles ?? [];
        if (lineasDto.Select(l => l.SolicitudDetalleId).Distinct().Count() != lineasDto.Count)
            throw new ValidacionException("Una misma línea no puede venir dos veces.");
        if (lineasDto.Count != solicitud.Detalles.Count)
            throw new ValidacionException("Indicá la cantidad aprobada de todas las líneas de la solicitud (0 para no aprobar una).");

        var aprobadas = new Dictionary<SolicitudDetalle, int>();
        foreach (var linea in lineasDto)
        {
            var detalle = solicitud.Detalles.FirstOrDefault(d => d.Id == linea.SolicitudDetalleId)
                ?? throw new SolicitudEstadoInvalidoException($"La línea {linea.SolicitudDetalleId} no pertenece a esta solicitud.");

            if (linea.CantidadAprobada < 0 || linea.CantidadAprobada > detalle.CantidadSolicitada)
                throw new SolicitudEstadoInvalidoException(
                    $"La cantidad aprobada para '{detalle.Producto?.Nombre}' no puede superar la solicitada ({detalle.CantidadSolicitada}).");

            aprobadas[detalle] = linea.CantidadAprobada;
        }
        if (aprobadas.Values.All(c => c == 0))
            throw new ValidacionException("No se aprobó ninguna cantidad. Si no corresponde nada, usá «Rechazar».");

        // Pendiente -> Aprobada en un solo UPDATE condicional: si dos personas aprueban a la vez,
        // solo una lo logra y la otra recibe el error de estado (antes se aprobaba dos veces).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var ahora = DateTime.UtcNow;
        var filas = await _db.Solicitudes
            .Where(s => s.Id == id && s.Estado == EstadoSolicitud.Pendiente)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Estado, EstadoSolicitud.Aprobada)
                .SetProperty(s => s.AprobadoPorId, usuario.Id)
                .SetProperty(s => s.AprobadoPorNombre, usuario.Nombre)
                .SetProperty(s => s.FechaResolucion, ahora), ct);
        if (filas == 0)
            throw new SolicitudEstadoInvalidoException("La solicitud ya fue resuelta por otra persona.");

        foreach (var (detalle, cantidad) in aprobadas)
            detalle.CantidadAprobada = cantidad;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        solicitud.Estado = EstadoSolicitud.Aprobada;
        solicitud.AprobadoPorId = usuario.Id;
        solicitud.AprobadoPorNombre = usuario.Nombre;
        solicitud.FechaResolucion = ahora;

        await _auditoria.RegistrarAsync(nameof(Solicitud), solicitud.NumeroSolicitud, "Aprobar", anterior, _auditoria.Capturar(SnapshotEstado(solicitud)), paisId, usuario, null, ct);

        return ASolicitudDto(solicitud);
    }

    public async Task<SolicitudDto> RechazarAsync(int id, RechazarSolicitudDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.MotivoRechazo))
            throw new SolicitudEstadoInvalidoException("Una solicitud rechazada requiere motivo de rechazo.");

        var motivo = dto.MotivoRechazo.Trim();
        if (motivo.Length > 2000)
            throw new ValidacionException("El motivo de rechazo no puede superar los 2000 caracteres.");

        var solicitud = await _db.Solicitudes.Include(s => s.Area).Include(s => s.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(s => s.Id == id && s.Area!.PaisId == paisId, ct)
            ?? throw new SolicitudNoEncontradaException(id);

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
            throw new SolicitudEstadoInvalidoException($"Solo se puede rechazar una solicitud en estado Pendiente (actual: {solicitud.Estado}).");

        ExigirOtraPersona(solicitud, usuario, "rechazar");

        var anterior = _auditoria.Capturar(SnapshotEstado(solicitud));

        var ahora = DateTime.UtcNow;
        var filas = await _db.Solicitudes
            .Where(s => s.Id == id && s.Estado == EstadoSolicitud.Pendiente)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Estado, EstadoSolicitud.Rechazada)
                .SetProperty(s => s.MotivoRechazo, motivo)
                .SetProperty(s => s.AprobadoPorId, usuario.Id)
                .SetProperty(s => s.AprobadoPorNombre, usuario.Nombre)
                .SetProperty(s => s.FechaResolucion, ahora), ct);
        if (filas == 0)
            throw new SolicitudEstadoInvalidoException("La solicitud ya fue resuelta por otra persona.");

        solicitud.Estado = EstadoSolicitud.Rechazada;
        solicitud.MotivoRechazo = motivo;
        solicitud.AprobadoPorId = usuario.Id;
        solicitud.AprobadoPorNombre = usuario.Nombre;
        solicitud.FechaResolucion = ahora;
        await _auditoria.RegistrarAsync(nameof(Solicitud), solicitud.NumeroSolicitud, "Rechazar", anterior, _auditoria.Capturar(SnapshotEstado(solicitud)), paisId, usuario, motivo, ct);

        return ASolicitudDto(solicitud);
    }

    private static SolicitudDto ASolicitudDto(Solicitud s) => new(
        s.Id, s.NumeroSolicitud, s.AreaId, s.Area?.NombreArea ?? string.Empty, s.Tipo, s.Estado,
        s.FechaSolicitud, s.SolicitadoPorId, s.SolicitadoPorNombre,
        s.AprobadoPorId, s.AprobadoPorNombre, s.FechaResolucion, s.MotivoRechazo,
        s.Detalles.Select(d => new SolicitudDetalleDto(
            d.Id, d.ProductoId, d.Producto?.Nombre ?? string.Empty, d.Producto?.CodigoProducto ?? string.Empty,
            d.Producto?.UnidadMedida ?? string.Empty, d.Producto?.CostoUnitario,
            d.CantidadSolicitada, d.CantidadAprobada, d.CantidadEntregada,
            d.Retorna, d.UbicacionExterna, d.FechaRetornoEsperada,
            d.Producto is { TieneImagen: true } ? $"/api/productos/{d.ProductoId}/imagen" : null
        )).ToList()
    );
}
