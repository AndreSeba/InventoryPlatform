using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class AlmacenService : IAlmacenService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public AlmacenService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static object Snapshot(Almacen a) => new
    {
        a.CodigoAlmacen, a.Nombre, a.TipoAlmacen, a.ProveedorNombre, a.ProveedorContacto, a.ProveedorDireccion, a.Activo,
    };

    public async Task<IReadOnlyList<AlmacenDto>> ListarAsync(int paisId, bool incluirInactivos, CancellationToken ct)
    {
        var query = _db.Almacenes.AsNoTracking().Include(a => a.Pais).Where(a => a.PaisId == paisId);
        if (!incluirInactivos) query = query.Where(a => a.Activo);

        return await query.OrderBy(a => a.Nombre).Select(a => AAlmacenDto(a)).ToListAsync(ct);
    }

    public async Task<AlmacenDto> CrearAsync(CrearAlmacenDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        ValidarProveedor(dto.TipoAlmacen, dto.ProveedorNombre);

        var codigo = dto.CodigoAlmacen.Trim().ToUpperInvariant();

        var yaExiste = await _db.Almacenes.AnyAsync(a => a.CodigoAlmacen == codigo && a.Activo, ct);
        if (yaExiste)
            throw new CodigoAlmacenDuplicadoException(codigo);

        var almacen = new Almacen
        {
            CodigoAlmacen = codigo,
            Nombre = dto.Nombre,
            PaisId = paisId,
            TipoAlmacen = dto.TipoAlmacen,
            ProveedorNombre = dto.TipoAlmacen == TipoAlmacen.Externo ? dto.ProveedorNombre : null,
            ProveedorContacto = dto.TipoAlmacen == TipoAlmacen.Externo ? dto.ProveedorContacto : null,
            ProveedorDireccion = dto.TipoAlmacen == TipoAlmacen.Externo ? dto.ProveedorDireccion : null,
            Activo = true,
        };

        _db.Almacenes.Add(almacen);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Almacen), almacen.CodigoAlmacen, "Crear", null, _auditoria.Capturar(Snapshot(almacen)), paisId, usuario, null, ct);

        await _db.Entry(almacen).Reference(a => a.Pais).LoadAsync(ct);
        return AAlmacenDto(almacen);
    }

    public async Task<AlmacenDto> ActualizarAsync(int id, ActualizarAlmacenDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var almacen = await _db.Almacenes.Include(a => a.Pais)
            .FirstOrDefaultAsync(a => a.Id == id && a.PaisId == paisId, ct)
            ?? throw new AlmacenNoEncontradoException(id);

        var anterior = _auditoria.Capturar(Snapshot(almacen));

        ValidarProveedor(dto.TipoAlmacen, dto.ProveedorNombre);

        var codigo = dto.CodigoAlmacen.Trim().ToUpperInvariant();

        var yaExiste = await _db.Almacenes.AnyAsync(a => a.CodigoAlmacen == codigo && a.Activo && a.Id != id, ct);
        if (yaExiste)
            throw new CodigoAlmacenDuplicadoException(codigo);

        almacen.CodigoAlmacen = codigo;
        almacen.Nombre = dto.Nombre;
        almacen.TipoAlmacen = dto.TipoAlmacen;
        almacen.ProveedorNombre = dto.TipoAlmacen == TipoAlmacen.Externo ? dto.ProveedorNombre : null;
        almacen.ProveedorContacto = dto.TipoAlmacen == TipoAlmacen.Externo ? dto.ProveedorContacto : null;
        almacen.ProveedorDireccion = dto.TipoAlmacen == TipoAlmacen.Externo ? dto.ProveedorDireccion : null;
        almacen.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Almacen), almacen.CodigoAlmacen, "Actualizar", anterior, _auditoria.Capturar(Snapshot(almacen)), paisId, usuario, null, ct);

        return AAlmacenDto(almacen);
    }

    // Mismo criterio que UbicacionService valida CK_Ubicacion_Nivel en C#: un mensaje
    // claro en vez de un 500 crudo por violación del CHECK CK_Almacen_Proveedor.
    private static void ValidarProveedor(TipoAlmacen tipo, string? proveedorNombre)
    {
        if (tipo == TipoAlmacen.Propio && !string.IsNullOrWhiteSpace(proveedorNombre))
            throw new AlmacenInvalidoException("Un almacén Propio no lleva datos de proveedor.");
        if (tipo == TipoAlmacen.Externo && string.IsNullOrWhiteSpace(proveedorNombre))
            throw new AlmacenInvalidoException("Un almacén Externo requiere el nombre del proveedor.");
    }

    private static AlmacenDto AAlmacenDto(Almacen a) => new(
        a.Id, a.CodigoAlmacen, a.Nombre, a.PaisId, a.Pais?.Nombre ?? string.Empty, a.Pais?.CodigoIso ?? string.Empty,
        a.TipoAlmacen, a.ProveedorNombre, a.ProveedorContacto, a.ProveedorDireccion, a.Activo
    );
}
