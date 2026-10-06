using ClosedXML.Excel;
using Inventory.Application;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Security;
using Inventory.Infrastructure.Controles;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.Services;

// Revisión periódica de accesos: quien tiene el permiso `accesos.revisar` abre una campaña (foto de las
// cuentas activas del país), decide por cada cuenta (mantener / quitar / cambiar de rol) y la cierra.
// Al cerrar se APLICAN las decisiones, todo o nada, y la campaña queda inmutable como evidencia.
public class RevisionAccesoService : IRevisionAccesoService
{
    // "Privilegiada" = el rol puede administrar usuarios o roles, o revisar accesos.
    private static readonly string[] PermisosPrivilegiados =
        [Permisos.UsuariosGestionar, Permisos.RolesGestionar, Permisos.AccesosRevisar];

    private const string ContentTypeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IMemoryCache _cache;
    private readonly ControlesOptions _controles;

    public RevisionAccesoService(InventoryDbContext db, IAuditoriaService auditoria, IMemoryCache cache, IOptions<ControlesOptions>? controles = null)
    {
        _db = db;
        _auditoria = auditoria;
        _cache = cache;
        _controles = controles?.Value ?? ControlesOptions.Permisivo;
    }

    // Mismos campos que UsuarioService.Snapshot, para que el diff de Auditoría sea comparable.
    private static object SnapshotUsuario(Usuario u) => new { u.Email, u.NombreCompleto, u.RolId, u.Activo };

    // ------------------------------------------------------------------ lectura

    public async Task<IReadOnlyList<RevisionAccesoResumenDto>> ListarAsync(int paisId, CancellationToken ct) =>
        await _db.RevisionesAcceso.AsNoTracking()
            .Where(r => r.PaisId == paisId)
            .OrderByDescending(r => r.FechaInicio)
            .Select(r => new RevisionAccesoResumenDto(
                r.Id, r.Codigo, r.Alcance, r.Estado, r.FechaInicio, r.IniciadaPorNombre, r.FechaCierre, r.CerradaPorNombre,
                r.Lineas.Count,
                r.Lineas.Count(l => l.Decision != DecisionAcceso.Pendiente),
                r.Lineas.Count(l => l.Decision == DecisionAcceso.Mantener),
                r.Lineas.Count(l => l.Decision == DecisionAcceso.Quitar),
                r.Lineas.Count(l => l.Decision == DecisionAcceso.CambiarRol)))
            .ToListAsync(ct);

    public async Task<RevisionAccesoDetalleDto> ObtenerAsync(int id, int paisId, int usuarioId, CancellationToken ct)
    {
        var r = await _db.RevisionesAcceso.AsNoTracking()
            .Include(x => x.Lineas)
            .FirstOrDefaultAsync(x => x.Id == id && x.PaisId == paisId, ct)
            ?? throw new RevisionAccesoNoEncontradaException(id);

        var limite = r.FechaInicio.AddDays(-_controles.DiasSinActividad);
        var lineas = r.Lineas.OrderBy(l => l.UsuarioNombre).ThenBy(l => l.Id).Select(l => new RevisionAccesoLineaDto(
            l.Id, l.UsuarioId, l.UsuarioNombre, l.UsuarioEmail, l.RolNombre, l.EsPrivilegiado,
            l.CuentaCreadaEn, l.UltimoLoginEn, l.UltimoLoginEn is null, l.UltimoLoginEn is { } u && u < limite,
            l.Decision, l.RolNuevoId, l.RolNuevoNombre, l.Comentario,
            l.RevisadoPorNombre, l.FechaRevision, l.Aplicada, l.UsuarioId == usuarioId)).ToList();

        var resumen = new RevisionAccesoResumenDto(
            r.Id, r.Codigo, r.Alcance, r.Estado, r.FechaInicio, r.IniciadaPorNombre, r.FechaCierre, r.CerradaPorNombre,
            lineas.Count,
            lineas.Count(l => l.Decision != DecisionAcceso.Pendiente),
            lineas.Count(l => l.Decision == DecisionAcceso.Mantener),
            lineas.Count(l => l.Decision == DecisionAcceso.Quitar),
            lineas.Count(l => l.Decision == DecisionAcceso.CambiarRol));

        var roles = await _db.Roles.AsNoTracking()
            .Where(x => x.PaisId == paisId && x.Activo)
            .OrderBy(x => x.Nombre)
            .Select(x => new RolOpcionDto(x.Id, x.Nombre))
            .ToListAsync(ct);

        return new RevisionAccesoDetalleDto(resumen, r.Notas, r.MotivoCancelacion, _controles.DiasSinActividad, lineas, roles);
    }

    public async Task<EstadoRevisionesAccesoDto> ObtenerEstadoAsync(int paisId, CancellationToken ct)
    {
        var campanas = await _db.RevisionesAcceso.AsNoTracking()
            .Where(r => r.PaisId == paisId && (r.Estado == EstadoRevisionAcceso.Cerrada || r.Estado == EstadoRevisionAcceso.EnCurso))
            .Select(r => new { r.Id, r.Alcance, r.Estado, r.FechaCierre })
            .ToListAsync(ct);

        DateTime? Ultima(Func<AlcanceRevisionAcceso, bool> alcances) => campanas
            .Where(c => c.Estado == EstadoRevisionAcceso.Cerrada && alcances(c.Alcance))
            .Max(c => c.FechaCierre);
        int? EnCurso(AlcanceRevisionAcceso a) => campanas
            .Where(c => c.Estado == EstadoRevisionAcceso.EnCurso && c.Alcance == a)
            .Select(c => (int?)c.Id).FirstOrDefault();

        var ahora = DateTime.UtcNow;
        var ultimaTodos = Ultima(a => a == AlcanceRevisionAcceso.Todos);
        // La revisión de TODAS las cuentas también cubre a las administradoras.
        var ultimaAdmins = Ultima(_ => true);

        int? Dias(DateTime? f) => f is null ? null : (int)(ahora - f.Value).TotalDays;
        var diasTodos = Dias(ultimaTodos);
        var diasAdmins = Dias(ultimaAdmins);

        return new EstadoRevisionesAccesoDto(
            ultimaTodos, diasTodos, _controles.RevisionTodosCadaDias,
            diasTodos is null || diasTodos > _controles.RevisionTodosCadaDias, EnCurso(AlcanceRevisionAcceso.Todos),
            ultimaAdmins, diasAdmins, _controles.RevisionAdministradoresCadaDias,
            diasAdmins is null || diasAdmins > _controles.RevisionAdministradoresCadaDias, EnCurso(AlcanceRevisionAcceso.Administradores));
    }

    // ------------------------------------------------------------------ abrir

    public async Task<RevisionAccesoDetalleDto> CrearAsync(CrearRevisionAccesoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (!Enum.IsDefined(dto.Alcance))
            throw new ValidacionException("El alcance de la revisión no es válido.");
        var notas = Validacion.TextoOpcional(dto.Notas, 500, "Las notas");

        if (await _db.RevisionesAcceso.AnyAsync(r => r.PaisId == paisId && r.Alcance == dto.Alcance && r.Estado == EstadoRevisionAcceso.EnCurso, ct))
            throw new RevisionAccesoEstadoInvalidoException("Ya hay una revisión de este tipo en curso: cerrala o cancelala antes de abrir otra.");

        var cuentas = await _db.Usuarios.AsNoTracking()
            .Where(u => u.PaisId == paisId && u.Activo)
            .Select(u => new
            {
                u.Id, u.NombreCompleto, u.Email, u.RolId, RolNombre = u.Rol!.Nombre, u.CreadoEn, u.UltimoLoginEn,
                Privilegiada = u.Rol!.RolPermisos.Any(rp => PermisosPrivilegiados.Contains(rp.Permiso!.Codigo)),
            })
            .ToListAsync(ct);

        if (dto.Alcance == AlcanceRevisionAcceso.Administradores)
            cuentas = cuentas.Where(c => c.Privilegiada).ToList();
        if (cuentas.Count == 0)
            throw new ValidacionException("No hay cuentas activas para revisar con ese alcance.");

        var revision = new RevisionAcceso
        {
            // Código provisional ÚNICO (no una constante compartida): dos altas simultáneas
            // no pueden chocar contra el índice único mientras esperan su Id definitivo.
            Codigo = "TMP-" + Guid.NewGuid().ToString("N"),
            PaisId = paisId,
            Alcance = dto.Alcance,
            Estado = EstadoRevisionAcceso.EnCurso,
            Notas = notas,
            IniciadaPorId = usuario.Id,
            IniciadaPorNombre = usuario.Nombre,
            FechaInicio = DateTime.UtcNow,
            Lineas = cuentas.Select(c => new RevisionAccesoLinea
            {
                UsuarioId = c.Id,
                UsuarioNombre = c.NombreCompleto,
                UsuarioEmail = c.Email,
                RolId = c.RolId,
                RolNombre = c.RolNombre,
                EsPrivilegiado = c.Privilegiada,
                CuentaCreadaEn = c.CreadoEn,
                UltimoLoginEn = c.UltimoLoginEn,
            }).ToList(),
        };

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            _db.RevisionesAcceso.Add(revision);
            await _db.SaveChangesAsync(ct);
            revision.Codigo = $"REV-{DateTime.UtcNow:yyyy}-{revision.Id:D6}";
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // El índice único filtrado (una campaña en curso por país y alcance) frenó una carrera.
            throw new RevisionAccesoEstadoInvalidoException("Ya hay una revisión de este tipo en curso: cerrala o cancelala antes de abrir otra.");
        }

        await _auditoria.RegistrarAsync(nameof(RevisionAcceso), revision.Codigo, "Iniciar", null,
            _auditoria.Capturar(new { revision.Codigo, revision.Alcance, revision.Estado, Cuentas = cuentas.Count }),
            paisId, usuario, null, ct);

        return await ObtenerAsync(revision.Id, paisId, usuario.Id, ct);
    }

    // ------------------------------------------------------------------ decidir

    public async Task<RevisionAccesoDetalleDto> DecidirAsync(int id, int lineaId, DecidirAccesoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        if (!Enum.IsDefined(dto.Decision))
            throw new ValidacionException("La decisión no es válida.");
        var comentario = Validacion.TextoOpcional(dto.Comentario, 500, "El comentario");
        if (dto.Decision is DecisionAcceso.Quitar or DecisionAcceso.CambiarRol && (comentario is null || comentario.Length < 3))
            throw new ValidacionException("Indicá el motivo (comentario) para quitar el acceso o cambiar el rol.");
        if (dto.Decision == DecisionAcceso.CambiarRol && dto.RolNuevoId is null)
            throw new ValidacionException("Elegí el rol nuevo de la cuenta.");
        if (dto.Decision != DecisionAcceso.CambiarRol && dto.RolNuevoId is not null)
            throw new ValidacionException("Solo se indica un rol nuevo cuando la decisión es cambiar de rol.");

        string codigo = string.Empty;
        object anterior = null!, nuevo = null!;

        await EnCursoAsync(id, paisId, async () =>
        {
            var linea = await _db.RevisionAccesoLineas.Include(l => l.RevisionAcceso)
                .FirstOrDefaultAsync(l => l.Id == lineaId && l.RevisionAccesoId == id, ct)
                ?? throw new ValidacionException("La cuenta no pertenece a esta revisión.");
            codigo = linea.RevisionAcceso!.Codigo;

            // Separación de funciones: nadie revisa su propio acceso.
            if (_controles.SeparacionDeFunciones && linea.UsuarioId == usuario.Id)
                throw new SeparacionDeFuncionesException("No podés revisar tu propio acceso: debe hacerlo otra persona con permiso de revisión.");
            if (linea.UsuarioId == usuario.Id && dto.Decision == DecisionAcceso.Quitar)
                throw new ValidacionException("No podés quitar tu propio acceso.");

            Rol? rolNuevo = null;
            if (dto.Decision == DecisionAcceso.CambiarRol)
            {
                rolNuevo = await _db.Roles.FirstOrDefaultAsync(r => r.Id == dto.RolNuevoId && r.PaisId == paisId && r.Activo, ct)
                    ?? throw new RolNoEncontradoException(dto.RolNuevoId ?? 0);
                if (rolNuevo.Id == linea.RolId)
                    throw new ValidacionException("El rol nuevo es el mismo que ya tiene la cuenta.");
            }

            anterior = new { linea.UsuarioEmail, linea.Decision, RolNuevo = linea.RolNuevoNombre, linea.Comentario };

            var pendiente = dto.Decision == DecisionAcceso.Pendiente;
            linea.Decision = dto.Decision;
            linea.RolNuevoId = rolNuevo?.Id;
            linea.RolNuevoNombre = rolNuevo?.Nombre;
            linea.Comentario = pendiente ? null : comentario;
            linea.RevisadoPorId = pendiente ? null : usuario.Id;
            linea.RevisadoPorNombre = pendiente ? null : usuario.Nombre;
            linea.FechaRevision = pendiente ? null : DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            nuevo = new { linea.UsuarioEmail, linea.Decision, RolNuevo = linea.RolNuevoNombre, linea.Comentario };
            return 0;
        }, ct);

        await _auditoria.RegistrarAsync(nameof(RevisionAcceso), codigo, "Decidir",
            _auditoria.Capturar(anterior), _auditoria.Capturar(nuevo), paisId, usuario, null, ct);

        return await ObtenerAsync(id, paisId, usuario.Id, ct);
    }

    // ------------------------------------------------------------------ cerrar

    public async Task<RevisionAccesoDetalleDto> CerrarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var cambiadas = new List<int>();
        string codigo;

        await using (var tx = await _db.Database.BeginTransactionAsync(ct))
        {
            // Una sola sentencia atómica: "en curso" y "todo decidido" se evalúan en el mismo UPDATE que
            // cambia el estado, así dos cierres simultáneos o un cierre contra una decisión no se pisan.
            var ahora = DateTime.UtcNow;
            var filas = await _db.RevisionesAcceso
                .Where(r => r.Id == id && r.PaisId == paisId && r.Estado == EstadoRevisionAcceso.EnCurso
                    && !r.Lineas.Any(l => l.Decision == DecisionAcceso.Pendiente))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(r => r.Estado, EstadoRevisionAcceso.Cerrada)
                    .SetProperty(r => r.FechaCierre, ahora)
                    .SetProperty(r => r.CerradaPorId, usuario.Id)
                    .SetProperty(r => r.CerradaPorNombre, usuario.Nombre), ct);

            if (filas == 0)
                await LanzarPorNoCerrableAsync(id, paisId, ct);

            codigo = await _db.RevisionesAcceso.AsNoTracking().Where(r => r.Id == id).Select(r => r.Codigo).FirstAsync(ct);

            // Aplicar las decisiones. Cualquier fallo revierte TODO (también el cambio de estado de arriba).
            var lineas = await _db.RevisionAccesoLineas
                .Where(l => l.RevisionAccesoId == id && (l.Decision == DecisionAcceso.Quitar || l.Decision == DecisionAcceso.CambiarRol))
                .ToListAsync(ct);
            var ids = lineas.Select(l => l.UsuarioId).ToList();
            var cuentas = await _db.Usuarios.Where(u => ids.Contains(u.Id) && u.PaisId == paisId).ToDictionaryAsync(u => u.Id, ct);

            foreach (var linea in lineas)
            {
                if (!cuentas.TryGetValue(linea.UsuarioId, out var cuenta))
                    throw new ValidacionException($"La cuenta {linea.UsuarioEmail} ya no existe en este país.");

                var antes = _auditoria.Capturar(SnapshotUsuario(cuenta));

                if (linea.Decision == DecisionAcceso.Quitar)
                {
                    if (cuenta.Id == usuario.Id)
                        throw new ValidacionException("No podés quitar tu propio acceso.");
                    cuenta.Activo = false;
                }
                else
                {
                    var rol = await _db.Roles.FirstOrDefaultAsync(r => r.Id == linea.RolNuevoId && r.PaisId == paisId && r.Activo, ct)
                        ?? throw new ValidacionException($"El rol elegido para {linea.UsuarioEmail} ya no está disponible. Volvé a decidir esa cuenta.");
                    cuenta.RolId = rol.Id;
                }

                linea.Aplicada = true;
                await _db.SaveChangesAsync(ct);
                cambiadas.Add(cuenta.Id);

                await _auditoria.RegistrarAsync(nameof(Usuario), cuenta.Id.ToString(), "Actualizar", antes,
                    _auditoria.Capturar(SnapshotUsuario(cuenta)), paisId, usuario,
                    $"Revisión de accesos {codigo}: {(linea.Decision == DecisionAcceso.Quitar ? "quitar acceso" : "cambiar de rol")} — {linea.Comentario}", ct);
            }

            // Las demás decisiones (mantener) también quedan como "aplicadas": no había nada que hacer.
            await _db.RevisionAccesoLineas
                .Where(l => l.RevisionAccesoId == id && l.Decision == DecisionAcceso.Mantener)
                .ExecuteUpdateAsync(u => u.SetProperty(l => l.Aplicada, true), ct);

            // Red de seguridad: la revisión nunca puede dejar al país sin quién administre usuarios.
            if (cambiadas.Count > 0 && !await HayAdministradorActivoAsync(paisId, ct))
                throw new ValidacionException("Aplicar estas decisiones dejaría al país sin ninguna cuenta activa que pueda administrar usuarios. Mantené al menos una.");

            await tx.CommitAsync(ct);
        }

        // Que las bajas y los cambios de rol valgan YA, no cuando venza la entrada de la caché.
        foreach (var uid in cambiadas)
            SesionUsuarioCache.Invalidar(_cache, uid);

        var detalle = await ObtenerAsync(id, paisId, usuario.Id, ct);
        await _auditoria.RegistrarAsync(nameof(RevisionAcceso), codigo, "Cerrar",
            _auditoria.Capturar(new { Estado = EstadoRevisionAcceso.EnCurso }),
            _auditoria.Capturar(new { detalle.Resumen.Estado, detalle.Resumen.Mantener, detalle.Resumen.Quitar, detalle.Resumen.CambiarRol }),
            paisId, usuario, null, ct);
        return detalle;
    }

    private Task<bool> HayAdministradorActivoAsync(int paisId, CancellationToken ct) =>
        _db.Usuarios.AnyAsync(u => u.PaisId == paisId && u.Activo
            && u.Rol!.Activo && u.Rol.RolPermisos.Any(rp => rp.Permiso!.Codigo == Permisos.UsuariosGestionar), ct);

    private async Task LanzarPorNoCerrableAsync(int id, int paisId, CancellationToken ct)
    {
        var r = await _db.RevisionesAcceso.AsNoTracking()
            .Where(x => x.Id == id && x.PaisId == paisId)
            .Select(x => new { x.Codigo, x.Estado, Pendientes = x.Lineas.Count(l => l.Decision == DecisionAcceso.Pendiente) })
            .FirstOrDefaultAsync(ct)
            ?? throw new RevisionAccesoNoEncontradaException(id);

        if (r.Estado != EstadoRevisionAcceso.EnCurso)
            throw new RevisionAccesoEstadoInvalidoException($"La revisión {r.Codigo} ya está {TextoEstado(r.Estado)}.");
        throw new RevisionAccesoEstadoInvalidoException($"Faltan {r.Pendientes} cuenta(s) por decidir. Resolvé todas (mantener, quitar o cambiar de rol) antes de cerrar.");
    }

    // ------------------------------------------------------------------ cancelar

    public async Task<RevisionAccesoDetalleDto> CancelarAsync(int id, CancelarRevisionAccesoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var motivo = Validacion.Texto(dto.Motivo, 500, "El motivo");
        if (motivo.Length < 3)
            throw new ValidacionException("Indicá el motivo de la cancelación.");

        var ahora = DateTime.UtcNow;
        var filas = await _db.RevisionesAcceso
            .Where(r => r.Id == id && r.PaisId == paisId && r.Estado == EstadoRevisionAcceso.EnCurso)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Estado, EstadoRevisionAcceso.Cancelada)
                .SetProperty(r => r.FechaCierre, ahora)
                .SetProperty(r => r.CerradaPorId, usuario.Id)
                .SetProperty(r => r.CerradaPorNombre, usuario.Nombre)
                .SetProperty(r => r.MotivoCancelacion, motivo), ct);

        if (filas == 0)
            await LanzarPorEstadoAsync(id, paisId, ct);

        var codigo = await _db.RevisionesAcceso.AsNoTracking().Where(r => r.Id == id).Select(r => r.Codigo).FirstAsync(ct);
        await _auditoria.RegistrarAsync(nameof(RevisionAcceso), codigo, "Cancelar",
            _auditoria.Capturar(new { Estado = EstadoRevisionAcceso.EnCurso }),
            _auditoria.Capturar(new { Estado = EstadoRevisionAcceso.Cancelada }), paisId, usuario, motivo, ct);

        return await ObtenerAsync(id, paisId, usuario.Id, ct);
    }

    // ------------------------------------------------------------------ acta

    public async Task<(byte[] Contenido, string NombreArchivo)> GenerarActaAsync(int id, int paisId, CancellationToken ct)
    {
        var r = await _db.RevisionesAcceso.AsNoTracking()
            .Include(x => x.Pais).Include(x => x.Lineas)
            .FirstOrDefaultAsync(x => x.Id == id && x.PaisId == paisId, ct)
            ?? throw new RevisionAccesoNoEncontradaException(id);

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Acta");

        var titulo = hoja.Cell(1, 1);
        titulo.Value = "Acta de revisión de accesos";
        titulo.Style.Font.Bold = true;
        titulo.Style.Font.FontSize = 16;

        void Dato(int fila, string etiqueta, string? valor)
        {
            hoja.Cell(fila, 1).Value = etiqueta;
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            hoja.Cell(fila, 2).Value = valor ?? "—";
        }
        static string Fecha(DateTime? f) => f is null ? "—" : f.Value.ToString("yyyy-MM-dd HH:mm") + " UTC";

        Dato(2, "Código", r.Codigo);
        Dato(3, "País", r.Pais?.Nombre);
        Dato(4, "Alcance", r.Alcance == AlcanceRevisionAcceso.Todos ? "Todas las cuentas activas" : "Cuentas administradoras");
        Dato(5, "Estado", TextoEstado(r.Estado, mayuscula: true));
        Dato(6, "Iniciada por", $"{r.IniciadaPorNombre} — {Fecha(r.FechaInicio)}");
        Dato(7, "Resuelta por", r.CerradaPorNombre is null ? "—" : $"{r.CerradaPorNombre} — {Fecha(r.FechaCierre)}");
        Dato(8, "Notas", r.Notas);
        if (r.MotivoCancelacion is not null) Dato(9, "Motivo de cancelación", r.MotivoCancelacion);

        const int cabecera = 11;
        string[] columnas = ["Nombre", "Email", "Rol al revisar", "Privilegiada", "Último ingreso", "Decisión", "Rol nuevo", "Comentario", "Revisado por", "Fecha de revisión", "Aplicada"];
        for (var c = 0; c < columnas.Length; c++)
        {
            var celda = hoja.Cell(cabecera, c + 1);
            celda.Value = columnas[c];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2A37");
            celda.Style.Font.FontColor = XLColor.White;
        }

        var fila = cabecera + 1;
        foreach (var l in r.Lineas.OrderBy(x => x.UsuarioNombre))
        {
            hoja.Cell(fila, 1).Value = l.UsuarioNombre;
            hoja.Cell(fila, 2).Value = l.UsuarioEmail;
            hoja.Cell(fila, 3).Value = l.RolNombre;
            hoja.Cell(fila, 4).Value = l.EsPrivilegiado ? "Sí" : "No";
            hoja.Cell(fila, 5).Value = l.UltimoLoginEn is null ? "Nunca" : Fecha(l.UltimoLoginEn);
            hoja.Cell(fila, 6).Value = l.Decision switch
            {
                DecisionAcceso.Mantener => "Mantener",
                DecisionAcceso.Quitar => "Quitar acceso",
                DecisionAcceso.CambiarRol => "Cambiar de rol",
                _ => "Pendiente",
            };
            hoja.Cell(fila, 7).Value = l.RolNuevoNombre ?? "—";
            hoja.Cell(fila, 8).Value = l.Comentario ?? "";
            hoja.Cell(fila, 9).Value = l.RevisadoPorNombre ?? "—";
            hoja.Cell(fila, 10).Value = Fecha(l.FechaRevision);
            hoja.Cell(fila, 11).Value = l.Aplicada ? "Sí" : "No";
            fila++;
        }

        // Bloque de firmas: el acta se imprime y la firma quien revisó.
        fila += 2;
        hoja.Cell(fila, 1).Value = "Revisor: ______________________________";
        hoja.Cell(fila, 4).Value = "Fecha: ______________";
        hoja.Cell(fila + 2, 1).Value = "Aprobación (TI / Seguridad): ______________________________";
        hoja.Cell(fila + 2, 4).Value = "Fecha: ______________";

        hoja.Columns().AdjustToContents();
        hoja.Column(8).Width = 45;
        hoja.SheetView.FreezeRows(cabecera);

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return (ms.ToArray(), $"{r.Codigo}-acta-revision-accesos.xlsx");
    }

    // ------------------------------------------------------------------ utilidades

    // Toda modificación de una campaña pasa por acá: transacción + un UPDATE no-op que toma el candado
    // de la fila solo si sigue EnCurso. Sin eso se podría guardar una decisión DESPUÉS del cierre.
    private async Task<T> EnCursoAsync<T>(int id, int paisId, Func<Task<T>> accion, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var bloqueadas = await _db.RevisionesAcceso
            .Where(r => r.Id == id && r.PaisId == paisId && r.Estado == EstadoRevisionAcceso.EnCurso)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Estado, EstadoRevisionAcceso.EnCurso), ct);
        if (bloqueadas == 0)
            await LanzarPorEstadoAsync(id, paisId, ct);

        var resultado = await accion();
        await tx.CommitAsync(ct);
        return resultado;
    }

    // Distingue "no existe" (404) de "ya no está en curso" (409). Siempre lanza.
    private async Task LanzarPorEstadoAsync(int id, int paisId, CancellationToken ct)
    {
        var r = await _db.RevisionesAcceso.AsNoTracking()
            .Where(x => x.Id == id && x.PaisId == paisId)
            .Select(x => new { x.Codigo, x.Estado })
            .FirstOrDefaultAsync(ct)
            ?? throw new RevisionAccesoNoEncontradaException(id);
        throw new RevisionAccesoEstadoInvalidoException($"La revisión {r.Codigo} ya está {TextoEstado(r.Estado)}: no se puede modificar.");
    }

    private static string TextoEstado(EstadoRevisionAcceso estado, bool mayuscula = false)
    {
        var texto = estado switch
        {
            EstadoRevisionAcceso.Cerrada => "cerrada",
            EstadoRevisionAcceso.Cancelada => "cancelada",
            _ => "en curso",
        };
        return mayuscula ? char.ToUpperInvariant(texto[0]) + texto[1..] : texto;
    }
}
