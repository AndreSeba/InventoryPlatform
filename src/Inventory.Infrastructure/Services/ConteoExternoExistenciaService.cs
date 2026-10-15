using Inventory.Application;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class ConteoExternoExistenciaService : IConteoExternoExistenciaService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ConteoExternoExistenciaService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static object Snapshot(ConteoExternoExistencia e) => new
    {
        e.ConteoExternoProductoId, e.AlmacenCodigo, e.Lote, e.CantidadSistema, e.Activo
    };

    public async Task<IReadOnlyList<ConteoExternoExistenciaDto>> ListarAsync(int paisId, int? conteoExternoProductoId, string? almacenCodigo, bool incluirInactivas, CancellationToken ct)
    {
        var query = _db.ConteoExternoExistencias.AsNoTracking().Include(e => e.ConteoExternoProducto).Where(e => e.PaisId == paisId);
        if (!incluirInactivas) query = query.Where(e => e.Activo);
        if (conteoExternoProductoId is not null) query = query.Where(e => e.ConteoExternoProductoId == conteoExternoProductoId);
        if (!string.IsNullOrWhiteSpace(almacenCodigo)) query = query.Where(e => e.AlmacenCodigo == almacenCodigo);

        return await query.OrderBy(e => e.ConteoExternoProducto!.Codigo).ThenBy(e => e.AlmacenCodigo)
            .Select(e => new ConteoExternoExistenciaDto(
                e.Id, e.ConteoExternoProductoId, e.ConteoExternoProducto!.Codigo, e.ConteoExternoProducto.Nombre, e.ConteoExternoProducto.UnidadCodigo,
                e.AlmacenCodigo,
                _db.ConteoExternoAlmacenes.Where(a => a.PaisId == paisId && a.Activo && a.Codigo == e.AlmacenCodigo).Select(a => a.Nombre).FirstOrDefault() ?? e.AlmacenCodigo,
                e.Lote, e.CantidadSistema, e.ConteoExternoProducto.CostoReferencia, e.PaisId, e.Activo))
            .ToListAsync(ct);
    }

    // Solo para mostrar (Fase 6) — si no hay nombre registrado, se devuelve el código tal cual.
    private async Task<string> ResolverNombreAlmacenAsync(string codigo, int paisId, CancellationToken ct)
        => await _db.ConteoExternoAlmacenes.AsNoTracking()
            .Where(a => a.PaisId == paisId && a.Activo && a.Codigo == codigo)
            .Select(a => a.Nombre)
            .FirstOrDefaultAsync(ct) ?? codigo;

    public async Task<ConteoExternoExistenciaDto> CrearAsync(CrearConteoExternoExistenciaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var producto = await _db.ConteoExternoProductos.FirstOrDefaultAsync(p => p.Id == dto.ConteoExternoProductoId && p.PaisId == paisId && p.Activo, ct)
            ?? throw new ConteoExternoProductoNoEncontradoException(dto.ConteoExternoProductoId);

        var almacenCodigo = Validacion.Texto(dto.AlmacenCodigo, 30, "El código de almacén");
        var lote = Validacion.TextoOpcional(dto.Lote, 50, "El lote");

        var codigoUnidad = Validacion.Texto(dto.UnidadCodigo, 10, "La unidad").ToUpperInvariant();
        var unidad = await _db.ConteoExternoUnidades.FirstOrDefaultAsync(u => u.PaisId == paisId && u.Codigo == codigoUnidad && u.Activo, ct)
            ?? throw new ConteoExternoUnidadNoEncontradaException(codigoUnidad);

        // La unidad se fija con el primer lote de existencia y después no se puede cambiar.
        if (producto.UnidadCodigo is null)
            producto.UnidadCodigo = codigoUnidad;
        else if (producto.UnidadCodigo != codigoUnidad)
            throw new UnidadConteoExternoProductoYaFijadaException(producto.Codigo, producto.UnidadCodigo);

        var cantidad = CantidadConteoExterno.Validar(dto.Cantidad, unidad.DecimalesPermitidos, "La cantidad");

        var existencia = new ConteoExternoExistencia
        {
            ConteoExternoProductoId = producto.Id,
            AlmacenCodigo = almacenCodigo,
            Lote = lote,
            CantidadSistema = cantidad,
            PaisId = paisId,
            Activo = true,
        };
        _db.ConteoExternoExistencias.Add(existencia);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoExistencia), existencia.Id.ToString(), "Crear", null, _auditoria.Capturar(Snapshot(existencia)), paisId, usuario, null, ct);

        var nombreAlmacen = await ResolverNombreAlmacenAsync(existencia.AlmacenCodigo, paisId, ct);
        return new ConteoExternoExistenciaDto(existencia.Id, producto.Id, producto.Codigo, producto.Nombre, producto.UnidadCodigo, existencia.AlmacenCodigo, nombreAlmacen, existencia.Lote, existencia.CantidadSistema, producto.CostoReferencia, paisId, existencia.Activo);
    }

    public async Task<ConteoExternoExistenciaDto> ActualizarAsync(int id, ActualizarConteoExternoExistenciaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var existencia = await _db.ConteoExternoExistencias.Include(e => e.ConteoExternoProducto)
            .FirstOrDefaultAsync(e => e.Id == id && e.PaisId == paisId, ct)
            ?? throw new ConteoExternoExistenciaNoEncontradaException(id);

        var producto = existencia.ConteoExternoProducto!;
        var anterior = _auditoria.Capturar(Snapshot(existencia));

        // El producto ya tiene su unidad fijada para poder haber llegado a tener esta
        // existencia — CrearAsync ya la garantizó.
        var unidad = await _db.ConteoExternoUnidades.FirstOrDefaultAsync(u => u.PaisId == paisId && u.Codigo == producto.UnidadCodigo, ct)
            ?? throw new ConteoExternoUnidadNoEncontradaException(producto.UnidadCodigo!);

        var cantidad = CantidadConteoExterno.Validar(dto.Cantidad, unidad.DecimalesPermitidos, "La cantidad");

        existencia.CantidadSistema = cantidad;
        existencia.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(ConteoExternoExistencia), existencia.Id.ToString(), "Actualizar", anterior, _auditoria.Capturar(Snapshot(existencia)), paisId, usuario, null, ct);

        var nombreAlmacen = await ResolverNombreAlmacenAsync(existencia.AlmacenCodigo, paisId, ct);
        return new ConteoExternoExistenciaDto(existencia.Id, producto.Id, producto.Codigo, producto.Nombre, producto.UnidadCodigo, existencia.AlmacenCodigo, nombreAlmacen, existencia.Lote, existencia.CantidadSistema, producto.CostoReferencia, paisId, existencia.Activo);
    }
}
