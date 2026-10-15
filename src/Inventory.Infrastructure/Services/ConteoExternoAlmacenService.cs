using Inventory.Application;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

// Módulo de conteo independiente — nombres de almacén (Fase 6). Solo para mostrar: el
// Codigo que ya usan ConteoExternoExistencia/ConteoExternoLinea sigue siendo la
// identidad real, este catálogo nunca lo reemplaza (ver ConteoExternoAlmacen.cs).
public class ConteoExternoAlmacenService : IConteoExternoAlmacenService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ConteoExternoAlmacenService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static object Snapshot(ConteoExternoAlmacen a) => new { a.Codigo, a.Nombre, a.Activo };

    public async Task<IReadOnlyList<ConteoExternoAlmacenDto>> ListarAsync(int paisId, bool incluirInactivos, CancellationToken ct)
    {
        var query = _db.ConteoExternoAlmacenes.AsNoTracking().Include(a => a.Pais).Where(a => a.PaisId == paisId);
        if (!incluirInactivos) query = query.Where(a => a.Activo);

        return await query.OrderBy(a => a.Codigo)
            .Select(a => new ConteoExternoAlmacenDto(a.Id, a.Codigo, a.Nombre, a.PaisId, a.Pais!.Nombre, a.Activo))
            .ToListAsync(ct);
    }

    public async Task<ConteoExternoAlmacenDto> CrearAsync(CrearConteoExternoAlmacenDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = Validacion.Texto(dto.Codigo, 30, "El código de almacén").ToUpperInvariant();
        var nombre = Validacion.Texto(dto.Nombre, 150, "El nombre de almacén");

        var yaExiste = await _db.ConteoExternoAlmacenes.AnyAsync(a => a.PaisId == paisId && a.Codigo == codigo && a.Activo, ct);
        if (yaExiste)
            throw new CodigoConteoExternoAlmacenDuplicadoException(codigo);

        var almacen = new ConteoExternoAlmacen { Codigo = codigo, Nombre = nombre, PaisId = paisId, Activo = true };
        _db.ConteoExternoAlmacenes.Add(almacen);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoAlmacen), almacen.Codigo, "Crear", null, _auditoria.Capturar(Snapshot(almacen)), paisId, usuario, null, ct);

        await _db.Entry(almacen).Reference(a => a.Pais).LoadAsync(ct);
        return new ConteoExternoAlmacenDto(almacen.Id, almacen.Codigo, almacen.Nombre, almacen.PaisId, almacen.Pais!.Nombre, almacen.Activo);
    }

    // Solo Nombre y Activo: el Código es inmutable, es la clave que ya usan las
    // existencias/líneas de sesión — igual que en Unidad.
    public async Task<ConteoExternoAlmacenDto> ActualizarAsync(int id, ActualizarConteoExternoAlmacenDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var almacen = await _db.ConteoExternoAlmacenes.Include(a => a.Pais)
            .FirstOrDefaultAsync(a => a.Id == id && a.PaisId == paisId, ct)
            ?? throw new ConteoExternoAlmacenNoEncontradoException(id);

        var anterior = _auditoria.Capturar(Snapshot(almacen));

        almacen.Nombre = Validacion.Texto(dto.Nombre, 150, "El nombre de almacén");
        almacen.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoAlmacen), almacen.Codigo, "Actualizar", anterior, _auditoria.Capturar(Snapshot(almacen)), paisId, usuario, null, ct);

        return new ConteoExternoAlmacenDto(almacen.Id, almacen.Codigo, almacen.Nombre, almacen.PaisId, almacen.Pais!.Nombre, almacen.Activo);
    }
}
