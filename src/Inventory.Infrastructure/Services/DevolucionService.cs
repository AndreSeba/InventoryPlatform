using Inventory.Application;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

// Devoluciones de préstamos con trazabilidad en las dos puntas: quien pidió el material AVISA que lo va a
// devolver (aviso) y el operario REGISTRA la entrada contra ese aviso (ver MovimientoService.RegistrarDevolucionAsync).
// Además el solicitante puede ver en cualquier momento qué material tiene prestado a su nombre.
public class DevolucionService : IDevolucionService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public DevolucionService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static object Snapshot(AvisoDevolucion a) => new { a.Codigo, a.Estado, a.Cantidad, a.MovimientoOrigenId, a.Notas };

    // ------------------------------------------------------------------ lectura

    public async Task<IReadOnlyList<PrestamoDto>> ListarPrestamosAsync(int paisId, int? solicitadoPorId, CancellationToken ct)
    {
        var query = _db.Movimientos.AsNoTracking()
            .Where(m => m.TipoMovimiento == TipoMovimiento.Salida && m.Retorna && m.Producto!.PaisId == paisId);
        if (solicitadoPorId is not null)
            query = query.Where(m => m.SolicitudDetalle!.Solicitud!.SolicitadoPorId == solicitadoPorId);

        var salidas = await query.Select(m => new
        {
            m.Id, m.NumeroMovimiento, m.ProductoId,
            ProductoCodigo = m.Producto!.CodigoProducto, ProductoNombre = m.Producto.Nombre,
            m.Cantidad, m.UbicacionExterna, m.FechaRetornoEsperada, m.FechaMovimiento,
            SolicitudId = (int?)m.SolicitudDetalle!.SolicitudId,
            NumeroSolicitud = m.SolicitudDetalle!.Solicitud!.NumeroSolicitud,
            SolicitadoPorId = (int?)m.SolicitudDetalle!.Solicitud!.SolicitadoPorId,
            SolicitadoPor = m.SolicitudDetalle!.Solicitud!.SolicitadoPorNombre,
        }).ToListAsync(ct);

        if (salidas.Count == 0) return [];

        var ids = salidas.Select(s => s.Id).ToList();
        var devuelto = await _db.Movimientos.AsNoTracking()
            .Where(m => m.MovimientoOrigenId != null && ids.Contains(m.MovimientoOrigenId!.Value))
            .GroupBy(m => m.MovimientoOrigenId!.Value)
            .Select(g => new { Origen = g.Key, Total = g.Sum(m => m.Cantidad) })
            .ToDictionaryAsync(x => x.Origen, x => x.Total, ct);
        var enAviso = await _db.AvisosDevolucion.AsNoTracking()
            .Where(a => a.Estado == EstadoAvisoDevolucion.Pendiente && ids.Contains(a.MovimientoOrigenId))
            .GroupBy(a => a.MovimientoOrigenId)
            .Select(g => new { Origen = g.Key, Total = g.Sum(a => a.Cantidad) })
            .ToDictionaryAsync(x => x.Origen, x => x.Total, ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        return salidas
            .Select(s =>
            {
                var dev = devuelto.GetValueOrDefault(s.Id);
                var pendiente = s.Cantidad - dev;
                var mora = s.FechaRetornoEsperada is { } f && f < hoy ? hoy.DayNumber - f.DayNumber : 0;
                return new PrestamoDto(
                    s.Id, s.NumeroMovimiento, s.ProductoId, s.ProductoCodigo, s.ProductoNombre,
                    s.Cantidad, dev, Math.Min(enAviso.GetValueOrDefault(s.Id), Math.Max(pendiente, 0)), pendiente,
                    s.UbicacionExterna, s.FechaRetornoEsperada, mora, s.FechaMovimiento,
                    s.SolicitudId, s.NumeroSolicitud, s.SolicitadoPorId, s.SolicitadoPor);
            })
            .Where(p => p.Pendiente > 0)
            .OrderBy(p => p.FechaRetornoEsperada is null).ThenBy(p => p.FechaRetornoEsperada).ThenBy(p => p.MovimientoId)
            .ToList();
    }

    public async Task<IReadOnlyList<AvisoDevolucionDto>> ListarAvisosAsync(int paisId, int? avisadoPorId, EstadoAvisoDevolucion? estado, CancellationToken ct)
    {
        var query = _db.AvisosDevolucion.AsNoTracking().Where(a => a.PaisId == paisId);
        if (avisadoPorId is not null) query = query.Where(a => a.AvisadoPorId == avisadoPorId);
        if (estado is not null) query = query.Where(a => a.Estado == estado);

        return await Proyectar(query.OrderByDescending(a => a.FechaAviso)).ToListAsync(ct);
    }

    private static IQueryable<AvisoDevolucionDto> Proyectar(IQueryable<AvisoDevolucion> query) =>
        query.Select(a => new AvisoDevolucionDto(
            a.Id, a.Codigo, a.Estado,
            a.MovimientoOrigenId, a.MovimientoOrigen!.NumeroMovimiento, a.MovimientoOrigen.Producto!.Nombre,
            a.MovimientoOrigen.SolicitudDetalle!.Solicitud!.NumeroSolicitud,
            a.Cantidad, a.Notas, a.AvisadoPorId, a.AvisadoPorNombre, a.FechaAviso,
            a.FechaResolucion, a.ResueltoPorNombre, a.CantidadRecibida,
            a.MovimientoDevolucion!.NumeroMovimiento, a.MotivoCancelacion));

    // ------------------------------------------------------------------ avisar

    public async Task<AvisoDevolucionDto> AvisarAsync(CrearAvisoDevolucionDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (dto.Cantidad <= 0 || dto.Cantidad > 1_000_000_000)
            throw new ValidacionException("La cantidad a devolver debe ser un número mayor a cero.");
        var notas = Validacion.TextoOpcional(dto.Notas, 500, "Las notas");

        AvisoDevolucion aviso;
        await using (var tx = await _db.Database.BeginTransactionAsync(ct))
        {
            var origen = await _db.Movimientos
                .Include(m => m.SolicitudDetalle).ThenInclude(sd => sd!.Solicitud)
                .FirstOrDefaultAsync(m => m.Id == dto.MovimientoOrigenId && m.Producto!.PaisId == paisId, ct)
                ?? throw new MovimientoOrigenInvalidoException($"No existe el préstamo {dto.MovimientoOrigenId}.");

            if (origen.TipoMovimiento != TipoMovimiento.Salida || !origen.Retorna)
                throw new MovimientoOrigenInvalidoException("El movimiento indicado no es un préstamo (una salida con retorno).");

            var solicitante = origen.SolicitudDetalle?.Solicitud?.SolicitadoPorId
                ?? throw new AvisoDevolucionNoPermitidoException("Este préstamo no salió contra una solicitud, así que no tiene a quién avisar: la devolución la registra directamente el operario.");
            if (solicitante != usuario.Id)
                throw new AvisoDevolucionNoPermitidoException("Solo quien pidió el material puede avisar su devolución.");

            // Serializa con cualquier otro movimiento del producto y con otros avisos del mismo préstamo: sin
            // esto, dos avisos simultáneos leen el mismo "pendiente" y entre los dos prometen más de lo prestado.
            var filas = await _db.Productos
                .Where(p => p.Id == origen.ProductoId && p.PaisId == paisId)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.TieneImagen, p => p.TieneImagen), ct);
            if (filas == 0) throw new ProductoNoEncontradoException(origen.ProductoId);

            var devuelto = await _db.Movimientos.Where(m => m.MovimientoOrigenId == origen.Id).SumAsync(m => (int?)m.Cantidad, ct) ?? 0;
            var enAviso = await _db.AvisosDevolucion
                .Where(a => a.MovimientoOrigenId == origen.Id && a.Estado == EstadoAvisoDevolucion.Pendiente)
                .SumAsync(a => (int?)a.Cantidad, ct) ?? 0;
            var disponible = origen.Cantidad - devuelto - enAviso;

            if (dto.Cantidad > disponible)
                throw new ValidacionException(disponible <= 0
                    ? "Ya avisaste la devolución de todo lo que queda de este préstamo."
                    : $"Solo quedan {disponible} unidad(es) por avisar de este préstamo (prestadas {origen.Cantidad}, devueltas {devuelto}, ya avisadas {enAviso}).");

            aviso = new AvisoDevolucion
            {
                // Código provisional ÚNICO (no una constante compartida): dos altas simultáneas
                // no pueden chocar contra el índice único mientras esperan su Id definitivo.
                Codigo = "TMP-" + Guid.NewGuid().ToString("N"),
                PaisId = paisId,
                MovimientoOrigenId = origen.Id,
                Cantidad = dto.Cantidad,
                Notas = notas,
                Estado = EstadoAvisoDevolucion.Pendiente,
                AvisadoPorId = usuario.Id,
                AvisadoPorNombre = usuario.Nombre,
                FechaAviso = DateTime.UtcNow,
            };
            _db.AvisosDevolucion.Add(aviso);
            await _db.SaveChangesAsync(ct);

            aviso.Codigo = $"DEV-{DateTime.UtcNow:yyyy}-{aviso.Id:D6}";
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await _auditoria.RegistrarAsync(nameof(AvisoDevolucion), aviso.Codigo, "Avisar", null, _auditoria.Capturar(Snapshot(aviso)), paisId, usuario, null, ct);
        return await ObtenerAsync(aviso.Id, paisId, ct);
    }

    // ------------------------------------------------------------------ cancelar

    public async Task<AvisoDevolucionDto> CancelarAvisoAsync(int id, CancelarAvisoDevolucionDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var motivo = Validacion.Texto(dto.Motivo, 500, "El motivo");

        var existente = await _db.AvisosDevolucion.AsNoTracking()
            .Where(a => a.Id == id && a.PaisId == paisId)
            .Select(a => new { a.Codigo, a.AvisadoPorId, a.Estado })
            .FirstOrDefaultAsync(ct)
            ?? throw new AvisoDevolucionNoEncontradoException(id);
        if (existente.AvisadoPorId != usuario.Id)
            throw new AvisoDevolucionNoPermitidoException("Solo quien avisó la devolución puede cancelarla.");

        // Transición atómica: si el operario lo recibe justo ahora, una de las dos operaciones gana y la otra falla.
        var ahora = DateTime.UtcNow;
        var filas = await _db.AvisosDevolucion
            .Where(a => a.Id == id && a.PaisId == paisId && a.Estado == EstadoAvisoDevolucion.Pendiente)
            .ExecuteUpdateAsync(u => u
                .SetProperty(a => a.Estado, EstadoAvisoDevolucion.Cancelado)
                .SetProperty(a => a.FechaResolucion, ahora)
                .SetProperty(a => a.ResueltoPorId, usuario.Id)
                .SetProperty(a => a.ResueltoPorNombre, usuario.Nombre)
                .SetProperty(a => a.MotivoCancelacion, motivo), ct);
        if (filas == 0)
            throw new AvisoDevolucionEstadoInvalidoException($"El aviso {existente.Codigo} ya no está pendiente: no se puede cancelar.");

        await _auditoria.RegistrarAsync(nameof(AvisoDevolucion), existente.Codigo, "Cancelar",
            _auditoria.Capturar(new { Estado = EstadoAvisoDevolucion.Pendiente }),
            _auditoria.Capturar(new { Estado = EstadoAvisoDevolucion.Cancelado }), paisId, usuario, motivo, ct);

        return await ObtenerAsync(id, paisId, ct);
    }

    private async Task<AvisoDevolucionDto> ObtenerAsync(int id, int paisId, CancellationToken ct) =>
        await Proyectar(_db.AvisosDevolucion.AsNoTracking().Where(a => a.Id == id && a.PaisId == paisId)).FirstOrDefaultAsync(ct)
        ?? throw new AvisoDevolucionNoEncontradoException(id);
}
