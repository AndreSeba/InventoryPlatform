using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Services;

public class CategoriaService : ICategoriaService
{
    private readonly InventoryDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public CategoriaService(InventoryDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    private static object Snapshot(Categoria c) => new { c.CodigoCategoria, c.Descripcion, c.EncargadoId, c.Activo };

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct)
    {
        IQueryable<Categoria> query = _db.Categorias.AsNoTracking().Include(c => c.Encargado).Include(c => c.Pais)
            .Where(c => c.PaisId == paisId);
        if (!incluirInactivas) query = query.Where(c => c.Activo);

        return await query.OrderBy(c => c.CodigoCategoria)
            .Select(c => new CategoriaDto(c.Id, c.CodigoCategoria, c.Descripcion, c.Activo, c.EncargadoId, c.Encargado!.NombreCompleto, c.PaisId, c.Pais!.Nombre))
            .ToListAsync(ct);
    }

    public async Task<CategoriaDto> CrearAsync(CrearCategoriaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var codigo = dto.CodigoCategoria.Trim().ToUpperInvariant();

        // Único POR PAÍS, no global — dos países pueden repetir un código de categoría.
        var yaExiste = await _db.Categorias.AnyAsync(c => c.PaisId == paisId && c.CodigoCategoria == codigo && c.Activo, ct);
        if (yaExiste)
            throw new CodigoCategoriaDuplicadoException(codigo);

        var encargadoNombre = await ResolverEncargadoAsync(dto.EncargadoId, paisId, ct);

        var categoria = new Categoria { CodigoCategoria = codigo, Descripcion = dto.Descripcion, EncargadoId = dto.EncargadoId, PaisId = paisId, Activo = true };
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Categoria), categoria.CodigoCategoria, "Crear", null, _auditoria.Capturar(Snapshot(categoria)), paisId, usuario, null, ct);

        await _db.Entry(categoria).Reference(c => c.Pais).LoadAsync(ct);
        return new CategoriaDto(categoria.Id, categoria.CodigoCategoria, categoria.Descripcion, categoria.Activo, categoria.EncargadoId, encargadoNombre, categoria.PaisId, categoria.Pais!.Nombre);
    }

    public async Task<CategoriaDto> ActualizarAsync(int id, ActualizarCategoriaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct)
    {
        var categoria = await _db.Categorias.Include(c => c.Pais).FirstOrDefaultAsync(c => c.Id == id && c.PaisId == paisId, ct)
            ?? throw new CategoriaNoEncontradaException(id);

        var anterior = _auditoria.Capturar(Snapshot(categoria));

        var codigo = dto.CodigoCategoria.Trim().ToUpperInvariant();

        var yaExiste = await _db.Categorias.AnyAsync(c => c.PaisId == paisId && c.CodigoCategoria == codigo && c.Activo && c.Id != id, ct);
        if (yaExiste)
            throw new CodigoCategoriaDuplicadoException(codigo);

        var encargadoNombre = await ResolverEncargadoAsync(dto.EncargadoId, paisId, ct);

        categoria.CodigoCategoria = codigo;
        categoria.Descripcion = dto.Descripcion;
        categoria.Activo = dto.Activo;
        categoria.EncargadoId = dto.EncargadoId;

        await _db.SaveChangesAsync(ct);

        await _auditoria.RegistrarAsync(nameof(Categoria), categoria.CodigoCategoria, "Actualizar", anterior, _auditoria.Capturar(Snapshot(categoria)), paisId, usuario, null, ct);

        return new CategoriaDto(categoria.Id, categoria.CodigoCategoria, categoria.Descripcion, categoria.Activo, categoria.EncargadoId, encargadoNombre, categoria.PaisId, categoria.Pais!.Nombre);
    }

    private async Task<string?> ResolverEncargadoAsync(int? encargadoId, int paisId, CancellationToken ct)
    {
        if (encargadoId is null) return null;

        // También validado server-side (no solo confiar en que el dropdown del frontend
        // ya filtra) — mismo criterio que ProductoService con categoría/unidad.
        var usuario = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == encargadoId && u.PaisId == paisId && u.Activo, ct)
            ?? throw new UsuarioNoEncontradoException(encargadoId.Value);

        return usuario.NombreCompleto;
    }
}
