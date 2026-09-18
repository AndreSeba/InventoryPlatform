using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class UbicacionService : IUbicacionService
{
    private readonly InventoryDbContext _db;

    public UbicacionService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<UbicacionDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct)
    {
        var query = _db.Ubicaciones.AsNoTracking().Include(u => u.Almacen).ThenInclude(a => a!.Pais)
            .Where(u => u.Almacen!.PaisId == paisId);
        if (!incluirInactivas) query = query.Where(u => u.Activo);

        return await query.OrderBy(u => u.CodigoUbicacion).Select(u => AUbicacionDto(u)).ToListAsync(ct);
    }

    public async Task<UbicacionDto> CrearAsync(CrearUbicacionDto dto, int paisId, CancellationToken ct)
    {
        var almacen = await _db.Almacenes.Include(a => a.Pais)
            .FirstOrDefaultAsync(a => a.Id == dto.AlmacenId && a.PaisId == paisId && a.Activo, ct)
            ?? throw new AlmacenNoEncontradoException(dto.AlmacenId);

        // CK_Ubicacion_Nivel (guía v4, 5.9): un RACK exige Nivel, un MUEBLE no lo lleva.
        // Se valida acá también (no solo en el CHECK de base) para devolver un mensaje
        // claro en vez de un 500 por violación de constraint.
        if (dto.TipoUbicacion == TipoUbicacion.Rack && string.IsNullOrWhiteSpace(dto.Nivel))
            throw new UbicacionInvalidaException("Una ubicación de tipo RACK requiere Nivel.");
        if (dto.TipoUbicacion == TipoUbicacion.Mueble && !string.IsNullOrWhiteSpace(dto.Nivel))
            throw new UbicacionInvalidaException("Una ubicación de tipo MUEBLE no lleva Nivel.");

        var nro = dto.Nro.Trim();
        var lado = dto.Lado.Trim().ToUpperInvariant();
        var nivel = dto.Nivel?.Trim();

        // Equivalente al flujo "INV Generar codigo ubicacion" de la guía v4.
        var codigo = dto.TipoUbicacion == TipoUbicacion.Rack
            ? $"{lado}-{nro}-{nivel}"
            : $"M-{nro}-{lado}";

        // Único POR ALMACÉN, no global — dos almacenes distintos pueden repetir un código.
        var yaExiste = await _db.Ubicaciones.AnyAsync(u => u.AlmacenId == almacen.Id && u.CodigoUbicacion == codigo && u.Activo, ct);
        if (yaExiste)
            throw new UbicacionDuplicadaException(codigo);

        var ubicacion = new Ubicacion
        {
            AlmacenId = almacen.Id,
            TipoUbicacion = dto.TipoUbicacion,
            Nro = nro,
            Lado = lado,
            Nivel = nivel,
            CodigoUbicacion = codigo,
            Activo = true,
        };

        _db.Ubicaciones.Add(ubicacion);
        await _db.SaveChangesAsync(ct);

        return new UbicacionDto(
            ubicacion.Id, ubicacion.TipoUbicacion, ubicacion.Nro, ubicacion.Lado, ubicacion.Nivel,
            ubicacion.CodigoUbicacion, ubicacion.Activo, almacen.Id, almacen.Nombre, almacen.Pais?.Nombre ?? string.Empty
        );
    }

    private static UbicacionDto AUbicacionDto(Ubicacion u) => new(
        u.Id, u.TipoUbicacion, u.Nro, u.Lado, u.Nivel, u.CodigoUbicacion, u.Activo,
        u.AlmacenId, u.Almacen?.Nombre ?? string.Empty, u.Almacen?.Pais?.Nombre ?? string.Empty
    );
}
