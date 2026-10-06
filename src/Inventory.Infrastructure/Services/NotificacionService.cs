using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Security;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Services;

// La campanita. TODAS las notificaciones se guardan por persona, pero se GENERAN en un solo lugar a partir del
// estado real del sistema (no hay ganchos repartidos en cada servicio de negocio): cada sincronización calcula
// «qué debería estar notificado ahora», lo compara con lo guardado y crea / actualiza / resuelve la diferencia.
// La Clave identifica cada notificación para cada persona, así nunca se duplica y se cierra sola cuando el
// estado cambia (una solicitud aprobada deja de estar «por aprobar» para sus aprobadores).
public class NotificacionService : INotificacionService
{
    // Las que dependen de solicitudes, avisos y préstamos se refrescan seguido (que «te aprobaron» llegue pronto);
    // las de existencias y control son más caras de calcular y cambian despacio.
    private static readonly TimeSpan IntervaloRapido = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IntervaloLento = TimeSpan.FromMinutes(10);

    private const int DiasResultadoReciente = 7;      // solo se notifica un resultado ocurrido en la última semana
    private const int DiasVisibleResultado = 30;      // un resultado se muestra 30 días
    private const int DiasAvisoRetorno = 2;           // préstamo «por vencer»: faltan 2 días o menos
    private const int DiasAvisoVencimiento = 30;      // lote «por vencer»: mismo umbral que VencimientoTag
    private const int DiasConteoAbierto = 7;
    private const int DiasCuentaSinIngreso = 14;
    private const int MaximoItems = 50;

    private readonly InventoryDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IDevolucionService _devoluciones;
    private readonly IRevisionAccesoService _accesos;
    private readonly ILogger<NotificacionService>? _logger;

    public NotificacionService(InventoryDbContext db, IMemoryCache cache, IDevolucionService devoluciones,
        IRevisionAccesoService accesos, ILogger<NotificacionService>? logger = null)
    {
        _db = db;
        _cache = cache;
        _devoluciones = devoluciones;
        _accesos = accesos;
        _logger = logger;
    }

    // Lo que «debería estar notificado» para una persona. Persistente = no se resuelve sola cuando deja de cumplirse
    // (un resultado queda como historial hasta que vence).
    private sealed record Deseada(
        int UsuarioId, string Clave, CategoriaNotificacion Categoria, SeveridadNotificacion Severidad,
        string Titulo, string Mensaje, string? Url, int? Valor = null);

    private sealed class Contexto
    {
        public required int PaisId { get; init; }
        public Dictionary<string, List<int>> PorPermiso { get; } = [];
    }

    // ------------------------------------------------------------------ lectura

    public async Task<NotificacionesDto> ListarAsync(int usuarioId, int paisId, CancellationToken ct)
    {
        // Una falla al calcular las notificaciones nunca debe romper la pantalla: se muestran las que ya hay.
        try { await SincronizarConFrenoAsync(paisId, forzar: false, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "No se pudieron sincronizar las notificaciones del país {PaisId}.", paisId);
            DescartarCambiosPendientes();
        }

        var desde = DateTime.UtcNow.AddDays(-DiasVisibleResultado);
        var vigentes = _db.Notificaciones.AsNoTracking()
            .Where(n => n.UsuarioId == usuarioId && n.PaisId == paisId && !n.Resuelta
                && (n.Categoria != CategoriaNotificacion.Resultado || n.FechaCreacion >= desde));

        var noLeidas = await vigentes.CountAsync(n => n.FechaLeida == null, ct);
        var items = await vigentes
            .OrderByDescending(n => n.FechaCreacion)
            .Take(MaximoItems)
            .Select(n => new NotificacionDto(n.Id, n.Categoria, n.Severidad, n.Titulo, n.Mensaje, n.Url, n.FechaCreacion, n.FechaLeida != null))
            .ToListAsync(ct);

        return new NotificacionesDto(noLeidas, items);
    }

    public async Task MarcarLeidaAsync(long id, int usuarioId, CancellationToken ct)
    {
        if (!await _db.Notificaciones.AnyAsync(n => n.Id == id && n.UsuarioId == usuarioId, ct))
            throw new NotificacionNoEncontradaException(id);

        var ahora = DateTime.UtcNow;
        await _db.Notificaciones
            .Where(n => n.Id == id && n.UsuarioId == usuarioId && n.FechaLeida == null)
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.FechaLeida, ahora), ct);
    }

    public Task<int> MarcarTodasLeidasAsync(int usuarioId, int paisId, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        return _db.Notificaciones
            .Where(n => n.UsuarioId == usuarioId && n.PaisId == paisId && !n.Resuelta && n.FechaLeida == null)
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.FechaLeida, ahora), ct);
    }

    // ------------------------------------------------------------------ sincronización

    public Task SincronizarAsync(int paisId, CancellationToken ct) => SincronizarConFrenoAsync(paisId, forzar: true, ct);

    private async Task SincronizarConFrenoAsync(int paisId, bool forzar, CancellationToken ct)
    {
        // El freno se toma ANTES de calcular: dos consultas casi simultáneas no calculan dos veces. Si igual lo
        // hicieran, el índice único (persona + clave activa) impide duplicar.
        var rapida = forzar || ReservarTurno($"notif-rapida:{paisId}", IntervaloRapido);
        var lenta = forzar || ReservarTurno($"notif-lenta:{paisId}", IntervaloLento);

        if (rapida) await SincronizarRapidasAsync(paisId, ct);
        if (lenta) await SincronizarLentasAsync(paisId, ct);
    }

    private bool ReservarTurno(string clave, TimeSpan intervalo)
    {
        if (_cache.TryGetValue(clave, out _)) return false;
        _cache.Set(clave, true, intervalo);
        return true;
    }

    private async Task SincronizarRapidasAsync(int paisId, CancellationToken ct)
    {
        var ctx = new Contexto { PaisId = paisId };
        var estados = new List<Deseada>();    // se resuelven solas cuando dejan de cumplirse
        var resultados = new List<Deseada>(); // quedan como historial

        await SolicitudesAsync(ctx, estados, resultados, ct);
        await AvisosAsync(ctx, estados, resultados, ct);
        await PrestamosAsync(ctx, estados, ct);

        foreach (var prefijo in new[] { "sol-pend:", "sol-entregar:", "aviso-recibir:", "pres-" })
            await AplicarAsync(paisId, prefijo, estados, resolverFaltantes: true, ct);
        foreach (var prefijo in new[] { "sol-res:", "aviso-res:" })
            await AplicarAsync(paisId, prefijo, resultados, resolverFaltantes: false, ct);
    }

    private async Task SincronizarLentasAsync(int paisId, CancellationToken ct)
    {
        var ctx = new Contexto { PaisId = paisId };
        var estados = new List<Deseada>();

        await InventarioAsync(ctx, estados, ct);
        await ControlAsync(ctx, estados, ct);

        foreach (var prefijo in new[] { "inv-", "ctl-" })
            await AplicarAsync(paisId, prefijo, estados, resolverFaltantes: true, ct);

        await LimpiarAsync(paisId, ct);
    }

    // ------------------------------------------------------------------ solicitudes

    private async Task SolicitudesAsync(Contexto ctx, List<Deseada> estados, List<Deseada> resultados, CancellationToken ct)
    {
        var solicitudes = _db.Solicitudes.AsNoTracking().Where(s => s.Area!.PaisId == ctx.PaisId);

        // 1) Por aprobar -> quienes aprueban (menos quien la pidió).
        var pendientes = await solicitudes.Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .Select(s => new { s.Id, s.NumeroSolicitud, s.SolicitadoPorId, s.SolicitadoPorNombre, Area = s.Area!.NombreArea })
            .ToListAsync(ct);
        if (pendientes.Count > 0)
        {
            var aprobadores = await ConPermisoAsync(ctx, Permisos.SolicitudesAprobar, ct);
            foreach (var s in pendientes)
                foreach (var uid in aprobadores.Where(u => u != s.SolicitadoPorId))
                    estados.Add(new Deseada(uid, $"sol-pend:{s.Id}", CategoriaNotificacion.Pendiente, SeveridadNotificacion.Aviso,
                        "Solicitud por aprobar", $"{s.NumeroSolicitud} de {s.SolicitadoPorNombre} ({s.Area})", $"/solicitudes/{s.Id}"));
        }

        // 2) Aprobada o entregada en parte -> quienes entregan (menos quien la pidió).
        var porEntregar = await solicitudes
            .Where(s => s.Estado == EstadoSolicitud.Aprobada || s.Estado == EstadoSolicitud.EntregadaParcial)
            .Select(s => new { s.Id, s.NumeroSolicitud, s.Estado, s.Tipo, s.SolicitadoPorId, s.SolicitadoPorNombre })
            .ToListAsync(ct);
        if (porEntregar.Count > 0)
        {
            var operarios = await ConPermisoAsync(ctx, Permisos.SolicitudesEntregar, ct);
            foreach (var s in porEntregar)
            {
                var verbo = s.Tipo == TipoSolicitud.Salida ? "entregar" : "recibir";
                var parcial = s.Estado == EstadoSolicitud.EntregadaParcial;
                foreach (var uid in operarios.Where(u => u != s.SolicitadoPorId))
                    estados.Add(new Deseada(uid, $"sol-entregar:{s.Id}", CategoriaNotificacion.Pendiente, SeveridadNotificacion.Aviso,
                        parcial ? $"Falta {verbo} parte de una solicitud" : $"Solicitud por {verbo}",
                        $"{s.NumeroSolicitud} de {s.SolicitadoPorNombre}", $"/solicitudes/{s.Id}"));
            }
        }

        // 3) Resultados para quien la pidió: aprobada, rechazada, entregada (en parte o completa), de la última semana.
        var desde = DateTime.UtcNow.AddDays(-DiasResultadoReciente);
        var conEntregaReciente = await _db.Movimientos.AsNoTracking()
            .Where(m => m.SolicitudDetalleId != null && m.FechaMovimiento >= desde && m.SolicitudDetalle!.Solicitud!.Area!.PaisId == ctx.PaisId)
            .Select(m => m.SolicitudDetalle!.SolicitudId)
            .Distinct()
            .ToListAsync(ct);

        var recientes = await solicitudes
            .Where(s => ((s.Estado == EstadoSolicitud.Aprobada || s.Estado == EstadoSolicitud.Rechazada) && s.FechaResolucion >= desde)
                || ((s.Estado == EstadoSolicitud.EntregadaParcial || s.Estado == EstadoSolicitud.Entregada) && conEntregaReciente.Contains(s.Id)))
            .Select(s => new { s.Id, s.NumeroSolicitud, s.Estado, s.SolicitadoPorId, s.AprobadoPorNombre, s.MotivoRechazo })
            .ToListAsync(ct);

        foreach (var s in recientes)
        {
            var (severidad, titulo, mensaje) = s.Estado switch
            {
                EstadoSolicitud.Aprobada => (SeveridadNotificacion.Info, "Tu solicitud fue aprobada", $"{s.NumeroSolicitud} · aprobada por {s.AprobadoPorNombre}"),
                EstadoSolicitud.Rechazada => (SeveridadNotificacion.Alerta, "Tu solicitud fue rechazada", $"{s.NumeroSolicitud}: {s.MotivoRechazo}"),
                EstadoSolicitud.EntregadaParcial => (SeveridadNotificacion.Info, "Tu solicitud se entregó en parte", $"{s.NumeroSolicitud} · falta entregar el resto"),
                _ => (SeveridadNotificacion.Info, "Tu solicitud fue entregada", $"{s.NumeroSolicitud} · entrega completa"),
            };
            resultados.Add(new Deseada(s.SolicitadoPorId, $"sol-res:{s.Id}:{(int)s.Estado}", CategoriaNotificacion.Resultado, severidad,
                titulo, Recortar(mensaje, 500), $"/solicitudes/{s.Id}"));
        }
    }

    // ------------------------------------------------------------------ avisos de devolución

    private async Task AvisosAsync(Contexto ctx, List<Deseada> estados, List<Deseada> resultados, CancellationToken ct)
    {
        var avisos = _db.AvisosDevolucion.AsNoTracking().Where(a => a.PaisId == ctx.PaisId);

        var pendientes = await avisos.Where(a => a.Estado == EstadoAvisoDevolucion.Pendiente)
            .Select(a => new { a.Id, a.Codigo, a.Cantidad, a.AvisadoPorId, a.AvisadoPorNombre, Producto = a.MovimientoOrigen!.Producto!.Nombre })
            .ToListAsync(ct);
        if (pendientes.Count > 0)
        {
            var operarios = await ConPermisoAsync(ctx, Permisos.MovimientosDevolucion, ct);
            foreach (var a in pendientes)
                foreach (var uid in operarios.Where(u => u != a.AvisadoPorId))
                    estados.Add(new Deseada(uid, $"aviso-recibir:{a.Id}", CategoriaNotificacion.Pendiente, SeveridadNotificacion.Aviso,
                        "Devolución por recibir", Recortar($"{a.AvisadoPorNombre} devuelve {a.Cantidad} × {a.Producto} ({a.Codigo})", 500), "/movimientos"));
        }

        var desde = DateTime.UtcNow.AddDays(-DiasResultadoReciente);
        var recibidos = await avisos.Where(a => a.Estado == EstadoAvisoDevolucion.Recibido && a.FechaResolucion >= desde)
            .Select(a => new { a.Id, a.AvisadoPorId, a.CantidadRecibida, a.ResueltoPorNombre, Producto = a.MovimientoOrigen!.Producto!.Nombre })
            .ToListAsync(ct);
        foreach (var a in recibidos)
            resultados.Add(new Deseada(a.AvisadoPorId, $"aviso-res:{a.Id}", CategoriaNotificacion.Resultado, SeveridadNotificacion.Info,
                "Tu devolución fue recibida", Recortar($"{a.Producto}: {a.CantidadRecibida} unidad(es) recibidas por {a.ResueltoPorNombre}", 500), "/prestamos/mios"));
    }

    // ------------------------------------------------------------------ préstamos

    private async Task PrestamosAsync(Contexto ctx, List<Deseada> estados, CancellationToken ct)
    {
        var prestamos = await _devoluciones.ListarPrestamosAsync(ctx.PaisId, null, ct);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var p in prestamos.Where(p => p.SolicitadoPorId is not null))
        {
            var uid = p.SolicitadoPorId!.Value;
            if (p.DiasMora > 0)
            {
                estados.Add(new Deseada(uid, $"pres-mora:{p.MovimientoId}", CategoriaNotificacion.Inventario, SeveridadNotificacion.Alerta,
                    "Préstamo vencido",
                    Recortar($"{p.ProductoNombre} ({p.Pendiente} pendientes) debía volver el {p.FechaRetornoEsperada:dd/MM/yyyy}: {p.DiasMora} día(s) de mora", 500),
                    "/prestamos/mios"));
            }
            else if (p.FechaRetornoEsperada is { } f && f.DayNumber - hoy.DayNumber <= DiasAvisoRetorno)
            {
                var dias = f.DayNumber - hoy.DayNumber;
                var cuando = dias <= 0 ? "hoy" : dias == 1 ? "mañana" : $"el {f:dd/MM/yyyy}";
                estados.Add(new Deseada(uid, $"pres-vence:{p.MovimientoId}", CategoriaNotificacion.Inventario, SeveridadNotificacion.Aviso,
                    "Préstamo por vencer", Recortar($"{p.ProductoNombre} ({p.Pendiente} pendientes) debe volver {cuando}", 500), "/prestamos/mios"));
            }
        }

        var enMora = prestamos.Count(p => p.DiasMora > 0);
        if (enMora > 0)
        {
            foreach (var uid in await ConPermisoAsync(ctx, Permisos.MovimientosDevolucion, ct))
                estados.Add(new Deseada(uid, "pres-mora-total", CategoriaNotificacion.Inventario, SeveridadNotificacion.Alerta,
                    "Préstamos vencidos", $"{enMora} préstamo(s) pasaron su fecha de retorno sin devolverse", "/movimientos", enMora));
        }
    }

    // ------------------------------------------------------------------ inventario

    private async Task InventarioAsync(Contexto ctx, List<Deseada> estados, CancellationToken ct)
    {
        var productos = await _db.Productos.AsNoTracking()
            .Where(p => p.PaisId == ctx.PaisId && p.Activo)
            .Select(p => new
            {
                p.StockMinimo,
                Existencia = _db.Movimientos.Where(m => m.ProductoId == p.Id).Sum(m => (int?)m.CantidadEfectiva) ?? 0,
            })
            .ToListAsync(ct);
        var sinStock = productos.Count(p => p.Existencia <= 0);
        var bajoMinimo = productos.Count(p => p.Existencia > 0 && p.Existencia <= p.StockMinimo);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var limite = hoy.AddDays(DiasAvisoVencimiento);
        var proximos = await _db.Movimientos.AsNoTracking()
            .Where(m => m.TipoMovimiento == TipoMovimiento.Entrada && m.FechaVencimiento != null
                && m.Producto!.PaisId == ctx.PaisId && m.Producto.Activo)
            .GroupBy(m => m.ProductoId)
            .Select(g => g.Min(m => m.FechaVencimiento))
            .ToListAsync(ct);
        var vencidos = proximos.Count(f => f < hoy);
        var porVencer = proximos.Count(f => f >= hoy && f <= limite);

        if (sinStock + bajoMinimo + vencidos + porVencer == 0) return;
        foreach (var uid in await ConPermisoAsync(ctx, Permisos.MovimientosVer, ct))
        {
            if (sinStock > 0)
                estados.Add(new Deseada(uid, "inv-sin-stock", CategoriaNotificacion.Inventario, SeveridadNotificacion.Alerta,
                    "Productos sin stock", $"{sinStock} producto(s) sin existencia", "/productos", sinStock));
            if (bajoMinimo > 0)
                estados.Add(new Deseada(uid, "inv-bajo-minimo", CategoriaNotificacion.Inventario, SeveridadNotificacion.Aviso,
                    "Productos bajo el mínimo", $"{bajoMinimo} producto(s) con existencia igual o menor a su stock mínimo", "/productos", bajoMinimo));
            if (vencidos > 0)
                estados.Add(new Deseada(uid, "inv-vencidos", CategoriaNotificacion.Inventario, SeveridadNotificacion.Alerta,
                    "Lotes vencidos", $"{vencidos} producto(s) tienen algún lote vencido: revisá cuál", "/productos", vencidos));
            if (porVencer > 0)
                estados.Add(new Deseada(uid, "inv-por-vencer", CategoriaNotificacion.Inventario, SeveridadNotificacion.Aviso,
                    "Lotes por vencer", $"{porVencer} producto(s) tienen un lote que vence en {DiasAvisoVencimiento} días o menos", "/productos", porVencer));
        }
    }

    // ------------------------------------------------------------------ control y seguridad

    private async Task ControlAsync(Contexto ctx, List<Deseada> estados, CancellationToken ct)
    {
        // Revisión periódica de accesos vencida -> quienes revisan.
        var accesos = await _accesos.ObtenerEstadoAsync(ctx.PaisId, ct);
        if (accesos.AdministradoresVencida || accesos.TodosVencida)
        {
            foreach (var uid in await ConPermisoAsync(ctx, Permisos.AccesosRevisar, ct))
            {
                if (accesos.AdministradoresVencida)
                    estados.Add(new Deseada(uid, "ctl-accesos-admin", CategoriaNotificacion.Control, SeveridadNotificacion.Alerta,
                        "Revisión de accesos de administradores vencida",
                        accesos.UltimaAdministradores is null
                            ? "Nunca se revisaron (corresponde cada mes)."
                            : $"La última fue hace {accesos.DiasDesdeAdministradores} días (corresponde cada {accesos.FrecuenciaAdministradoresDias}).",
                        "/accesos"));
                if (accesos.TodosVencida)
                    estados.Add(new Deseada(uid, "ctl-accesos-todos", CategoriaNotificacion.Control, SeveridadNotificacion.Aviso,
                        "Revisión de accesos de todas las cuentas vencida",
                        accesos.UltimaTodos is null
                            ? "Nunca se revisaron (corresponde cada trimestre)."
                            : $"La última fue hace {accesos.DiasDesdeTodos} días (corresponde cada {accesos.FrecuenciaTodosDias}).",
                        "/accesos"));
            }
        }

        // Conteos abiertos hace demasiado -> quienes cuentan.
        var limiteConteo = DateTime.UtcNow.AddDays(-DiasConteoAbierto);
        var conteosViejos = await _db.SesionesConteo.AsNoTracking()
            .CountAsync(c => c.PaisId == ctx.PaisId && c.Estado == EstadoConteo.EnCurso && c.FechaCreacion < limiteConteo, ct);
        if (conteosViejos > 0)
        {
            foreach (var uid in await ConPermisoAsync(ctx, Permisos.ConteosRegistrar, ct))
                estados.Add(new Deseada(uid, "ctl-conteos", CategoriaNotificacion.Control, SeveridadNotificacion.Aviso,
                    "Conteos abiertos hace días", $"{conteosViejos} conteo(s) llevan más de {DiasConteoAbierto} días sin cerrarse", "/conteos", conteosViejos));
        }

        // Cuentas activas que nunca ingresaron -> quienes administran usuarios.
        var limiteCuenta = DateTime.UtcNow.AddDays(-DiasCuentaSinIngreso);
        var sinIngreso = await _db.Usuarios.AsNoTracking()
            .CountAsync(u => u.PaisId == ctx.PaisId && u.Activo && u.UltimoLoginEn == null && u.CreadoEn < limiteCuenta, ct);
        if (sinIngreso > 0)
        {
            foreach (var uid in await ConPermisoAsync(ctx, Permisos.UsuariosGestionar, ct))
                estados.Add(new Deseada(uid, "ctl-cuentas", CategoriaNotificacion.Control, SeveridadNotificacion.Aviso,
                    "Cuentas que nunca ingresaron", $"{sinIngreso} cuenta(s) activas no ingresaron nunca (creadas hace más de {DiasCuentaSinIngreso} días)", "/usuarios", sinIngreso));
        }
    }

    // ------------------------------------------------------------------ aplicar la diferencia

    // Compara lo que debería haber (de UNA familia de claves, identificada por su prefijo) con lo guardado:
    // crea lo que falta, actualiza lo que cambió y, si corresponde, resuelve lo que ya no se cumple.
    private async Task AplicarAsync(int paisId, string prefijo, List<Deseada> deseadas, bool resolverFaltantes, CancellationToken ct)
    {
        var de = deseadas.Where(d => d.Clave.StartsWith(prefijo, StringComparison.Ordinal)).ToList();

        // Lo marcado como leído se escribe con UPDATE directo (sin pasar por el rastreo de EF): si una lectura
        // anterior del mismo contexto sigue rastreada, EF devolvería sus valores viejos en vez de los de la base.
        DescartarCambiosPendientes();

        var existentes = await _db.Notificaciones
            .Where(n => n.PaisId == paisId && !n.Resuelta && n.Clave.StartsWith(prefijo))
            .ToListAsync(ct);
        if (existentes.Count == 0 && de.Count == 0) return;

        var porClave = existentes.ToDictionary(n => (n.UsuarioId, n.Clave));
        var vistas = new HashSet<(int, string)>();
        var ahora = DateTime.UtcNow;

        foreach (var d in de)
        {
            if (!vistas.Add((d.UsuarioId, d.Clave))) continue;

            if (porClave.TryGetValue((d.UsuarioId, d.Clave), out var n))
            {
                // Si el número de una notificación agregada SUBE, vuelve a quedar sin leer y sube en la lista.
                if (d.Valor is not null && d.Valor > (n.Valor ?? 0))
                {
                    n.FechaLeida = null;
                    n.FechaCreacion = ahora;
                }
                n.Severidad = d.Severidad;
                n.Titulo = d.Titulo;
                n.Mensaje = d.Mensaje;
                n.Url = d.Url;
                n.Valor = d.Valor;
            }
            else
            {
                _db.Notificaciones.Add(new Notificacion
                {
                    PaisId = paisId, UsuarioId = d.UsuarioId, Clave = d.Clave, Categoria = d.Categoria, Severidad = d.Severidad,
                    Titulo = d.Titulo, Mensaje = d.Mensaje, Url = d.Url, Valor = d.Valor, FechaCreacion = ahora,
                });
            }
        }

        if (resolverFaltantes)
        {
            foreach (var n in existentes.Where(n => !vistas.Contains((n.UsuarioId, n.Clave))))
            {
                n.Resuelta = true;
                n.FechaResolucion = ahora;
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Otra consulta creó la misma notificación un instante antes (índice único): no es un error, la
            // próxima sincronización lo deja consistente.
            _logger?.LogDebug(ex, "Choque al sincronizar notificaciones «{Prefijo}»; se reintenta en el próximo ciclo.", prefijo);
            DescartarCambiosPendientes();
        }
    }

    // Borra lo viejo: resueltas de hace más de un mes y resultados que ya no se muestran.
    private async Task LimpiarAsync(int paisId, CancellationToken ct)
    {
        var haceUnMes = DateTime.UtcNow.AddDays(-DiasVisibleResultado);
        await _db.Notificaciones
            .Where(n => n.PaisId == paisId && ((n.Resuelta && n.FechaResolucion < haceUnMes)
                || (n.Categoria == CategoriaNotificacion.Resultado && n.FechaCreacion < haceUnMes)))
            .ExecuteDeleteAsync(ct);
    }

    // ------------------------------------------------------------------ utilidades

    // Personas ACTIVAS del país, con rol activo, que tienen ese permiso. Se calcula una vez por sincronización.
    private async Task<List<int>> ConPermisoAsync(Contexto ctx, string codigo, CancellationToken ct)
    {
        if (ctx.PorPermiso.TryGetValue(codigo, out var ya)) return ya;

        var ids = await _db.Usuarios.AsNoTracking()
            .Where(u => u.PaisId == ctx.PaisId && u.Activo && u.Rol!.Activo
                && u.Rol.RolPermisos.Any(rp => rp.Permiso!.Codigo == codigo))
            .Select(u => u.Id)
            .ToListAsync(ct);
        ctx.PorPermiso[codigo] = ids;
        return ids;
    }

    private void DescartarCambiosPendientes()
    {
        foreach (var e in _db.ChangeTracker.Entries<Notificacion>().ToList())
            e.State = EntityState.Detached;
    }

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..(maximo - 1)] + "…";
}
