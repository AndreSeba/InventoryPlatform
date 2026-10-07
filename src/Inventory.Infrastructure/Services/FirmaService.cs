using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Inventory.Infrastructure.Services;

public class FirmaService : IFirmaService
{
    // Un PNG de firma recortado pesa decenas de KB; 300 KB es un tope holgado. Las dimensiones se leen del encabezado.
    private const int MaxBytes = 300 * 1024;
    private const int AnchoMin = 60, AnchoMax = 2000, AltoMin = 30, AltoMax = 800;

    // Frena adivinar la contraseña de otra persona desde una sesión que quedó abierta (mismo criterio que el login).
    private const int MaxIntentos = 5;
    private static readonly TimeSpan Ventana = TimeSpan.FromMinutes(10);
    private sealed record Fallos(int Cantidad, DateTime Vence);

    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IMemoryCache? _cache;

    public FirmaService(InventoryDbContext db, IAuditoriaService auditoria, IMemoryCache? cache = null)
    {
        _db = db;
        _auditoria = auditoria;
        _cache = cache;
    }

    public async Task<FirmaPropiaDto> ObtenerPropiaAsync(int usuarioId, CancellationToken ct)
    {
        var firma = await _db.Firmas.AsNoTracking().FirstOrDefaultAsync(f => f.UsuarioId == usuarioId && f.Activa, ct);
        return firma is null ? new FirmaPropiaDto(false, null, null) : new FirmaPropiaDto(true, Convert.ToBase64String(firma.Datos), firma.FechaRegistro);
    }

    public async Task<FirmaPropiaDto> GuardarAsync(GuardarFirmaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var datos = DecodificarPng(dto.PngBase64);
        await ExigirContrasenaAsync(usuario.Id, dto.Password, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var anterior = await _db.Firmas.FirstOrDefaultAsync(f => f.UsuarioId == usuario.Id && f.Activa, ct);
        if (anterior is not null)
        {
            anterior.Activa = false;
            await _db.SaveChangesAsync(ct);   // libera el índice único filtrado antes de insertar la nueva
        }
        var nueva = new FirmaUsuario { UsuarioId = usuario.Id, Datos = datos, FechaRegistro = DateTime.UtcNow, Activa = true };
        _db.Firmas.Add(nueva);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _auditoria.RegistrarAsync(nameof(FirmaUsuario), usuario.Id.ToString(), anterior is null ? "Registrar" : "Reemplazar",
            _auditoria.Capturar(new { Tiene = anterior is not null }), _auditoria.Capturar(new { Tiene = true, Version = nueva.Id }),
            paisId, usuario, null, ct);

        return new FirmaPropiaDto(true, Convert.ToBase64String(nueva.Datos), nueva.FechaRegistro);
    }

    public async Task EliminarAsync(int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        // No se borra la fila: las solicitudes ya firmadas apuntan a esa versión.
        var filas = await _db.Firmas.Where(f => f.UsuarioId == usuario.Id && f.Activa).ExecuteUpdateAsync(u => u.SetProperty(f => f.Activa, false), ct);
        if (filas == 0) return;
        await _auditoria.RegistrarAsync(nameof(FirmaUsuario), usuario.Id.ToString(), "Eliminar",
            _auditoria.Capturar(new { Tiene = true }), _auditoria.Capturar(new { Tiene = false }), paisId, usuario, null, ct);
    }

    public async Task<FirmasSolicitudDto> ObtenerDeSolicitudAsync(int solicitudId, int paisId, CancellationToken ct)
    {
        var s = await _db.Solicitudes.AsNoTracking()
            .Where(x => x.Id == solicitudId && x.Area!.PaisId == paisId)
            .Select(x => new { x.SolicitadoPorId, x.AprobadoPorId, x.Estado, x.FirmaSolicitanteId, x.FirmaAprobadorId })
            .FirstOrDefaultAsync(ct)
            ?? throw new SolicitudNoEncontradaException(solicitudId);

        // La firma de quien autoriza solo se estampa si la solicitud realmente se aprobó (no pendiente, no rechazada).
        var aprobada = s.Estado is EstadoSolicitud.Aprobada or EstadoSolicitud.EntregadaParcial or EstadoSolicitud.Entregada;

        var (pngSol, cargoSol) = await ResolverAsync(s.FirmaSolicitanteId, s.SolicitadoPorId, ct);
        var (pngAut, cargoAut) = aprobada && s.AprobadoPorId is int aprobador
            ? await ResolverAsync(s.FirmaAprobadorId, aprobador, ct)
            : (null, null);
        return new FirmasSolicitudDto(pngSol, cargoSol, pngAut, cargoAut);
    }

    // La versión fijada al firmar; si esa persona todavía no tenía firma en ese momento, su firma activa de hoy.
    private async Task<(string? Png, string? Cargo)> ResolverAsync(int? versionId, int usuarioId, CancellationToken ct)
    {
        var datos = versionId is int v
            ? await _db.Firmas.AsNoTracking().Where(f => f.Id == v && f.UsuarioId == usuarioId).Select(f => f.Datos).FirstOrDefaultAsync(ct)
            : null;
        datos ??= await _db.Firmas.AsNoTracking().Where(f => f.UsuarioId == usuarioId && f.Activa).Select(f => f.Datos).FirstOrDefaultAsync(ct);
        var cargo = await _db.Usuarios.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.Rol!.Nombre).FirstOrDefaultAsync(ct);
        return (datos is null ? null : Convert.ToBase64String(datos), cargo);
    }

    // Id de la firma activa de una persona, para fijarlo en la solicitud al crearla / aprobarla (null si no tiene).
    public static Task<int?> VersionActivaAsync(InventoryDbContext db, int usuarioId, CancellationToken ct) =>
        db.Firmas.AsNoTracking().Where(f => f.UsuarioId == usuarioId && f.Activa).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct);

    private static byte[] DecodificarPng(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new ValidacionException("Dibujá tu firma antes de guardar.");
        // Acepta tanto el base64 pelado como un data URL.
        var coma = base64.IndexOf(',');
        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && coma > 0) base64 = base64[(coma + 1)..];

        byte[] d;
        try { d = Convert.FromBase64String(base64); }
        catch (FormatException) { throw new ValidacionException("La firma no es una imagen válida."); }

        if (d.Length > MaxBytes)
            throw new ValidacionException("La firma es demasiado pesada. Dibujala más simple.");
        // Firma PNG (8 bytes) + chunk IHDR: ancho y alto en los bytes 16-23.
        if (d.Length < 33 || d[0] != 0x89 || d[1] != 0x50 || d[2] != 0x4E || d[3] != 0x47 || d[4] != 0x0D || d[5] != 0x0A || d[6] != 0x1A || d[7] != 0x0A
            || d[12] != (byte)'I' || d[13] != (byte)'H' || d[14] != (byte)'D' || d[15] != (byte)'R')
            throw new ValidacionException("La firma tiene que ser una imagen PNG.");
        var ancho = (d[16] << 24) | (d[17] << 16) | (d[18] << 8) | d[19];
        var alto = (d[20] << 24) | (d[21] << 16) | (d[22] << 8) | d[23];
        if (ancho is < AnchoMin or > AnchoMax || alto is < AltoMin or > AltoMax)
            throw new ValidacionException("El tamaño de la firma no es válido.");
        return d;
    }

    private async Task ExigirContrasenaAsync(int usuarioId, string? password, CancellationToken ct)
    {
        var clave = $"firma-fallos:{usuarioId}";
        Fallos? previo = null;
        if (_cache is not null && _cache.TryGetValue(clave, out Fallos? f) && f is not null && f.Vence > DateTime.UtcNow)
        {
            previo = f;
            if (previo.Cantidad >= MaxIntentos)
                throw new ValidacionException($"Demasiados intentos con la contraseña. Probá de nuevo en {(int)Math.Ceiling((previo.Vence - DateTime.UtcNow).TotalMinutes)} minuto(s).");
        }

        var hash = await _db.Usuarios.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.PasswordHash).FirstOrDefaultAsync(ct);
        // 400 y no 401: un 401 en el frontend se lee como «la sesión venció» y manda a iniciar sesión de nuevo.
        if (hash is null || string.IsNullOrEmpty(password) || !BCrypt.Net.BCrypt.Verify(password, hash))
        {
            if (_cache is not null)
            {
                var vence = previo?.Vence ?? DateTime.UtcNow.Add(Ventana);
                _cache.Set(clave, new Fallos((previo?.Cantidad ?? 0) + 1, vence), new DateTimeOffset(vence, TimeSpan.Zero));
            }
            throw new ValidacionException("La contraseña no es correcta.");
        }
        _cache?.Remove(clave);
    }
}
