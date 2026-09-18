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

    public CategoriaService(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct)
    {
        IQueryable<Categoria> query = _db.Categorias.AsNoTracking().Include(c => c.Encargado).Include(c => c.Pais)
            .Where(c => c.PaisId == paisId);
        if (!incluirInactivas) query = query.Where(c => c.Activo);

        return await query.OrderBy(c => c.CodigoCategoria)
            .Select(c => new CategoriaDto(c.Id, c.CodigoCategoria, c.Descripcion, c.Activo, c.EncargadoId, c.Encargado!.NombreCompleto, c.PaisId, c.Pais!.Nombre))
            .ToListAsync(ct);
    }

    public async Task<CategoriaDto> CrearAsync(CrearCategoriaDto dto, int paisId, CancellationToken ct)
    {
        var codigo = dto.CodigoCategoria.Trim().ToUpperInvariant();

        // Único POR PAÍS, no global — dos países pueden repetir un código de categoría.
        var yaExiste = await _db.Categorias.AnyAsync(c => c.PaisId == paisId && c.CodigoCategoria == codigo && c.Activo, ct);
        if (yaExiste)
            throw new CodigoCategoriaDuplicadoException(codigo);

        var encargadoNombre = await ResolverEncargadoAsync(dto.EncargadoId, ct);

        var categoria = new Categoria { CodigoCategoria = codigo, Descripcion = dto.Descripcion, EncargadoId = dto.EncargadoId, PaisId = paisId, Activo = true };
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(categoria).Reference(c => c.Pais).LoadAsync(ct);
        return new CategoriaDto(categoria.Id, categoria.CodigoCategoria, categoria.Descripcion, categoria.Activo, categoria.EncargadoId, encargadoNombre, categoria.PaisId, categoria.Pais!.Nombre);
    }

    public async Task<CategoriaDto> ActualizarAsync(int id, ActualizarCategoriaDto dto, int paisId, CancellationToken ct)
    {
        var categoria = await _db.Categorias.Include(c => c.Pais).FirstOrDefaultAsync(c => c.Id == id && c.PaisId == paisId, ct)
            ?? throw new CategoriaNoEncontradaException(id);

        var codigo = dto.CodigoCategoria.Trim().ToUpperInvariant();

        var yaExiste = await _db.Categorias.AnyAsync(c => c.PaisId == paisId && c.CodigoCategoria == codigo && c.Activo && c.Id != id, ct);
        if (yaExiste)
            throw new CodigoCategoriaDuplicadoException(codigo);

        var encargadoNombre = await ResolverEncargadoAsync(dto.EncargadoId, ct);

        categoria.CodigoCategoria = codigo;
        categoria.Descripcion = dto.Descripcion;
        categoria.Activo = dto.Activo;
        categoria.EncargadoId = dto.EncargadoId;

        await _db.SaveChangesAsync(ct);
        return new CategoriaDto(categoria.Id, categoria.CodigoCategoria, categoria.Descripcion, categoria.Activo, categoria.EncargadoId, encargadoNombre, categoria.PaisId, categoria.Pais!.Nombre);
    }

    private async Task<string?> ResolverEncargadoAsync(int? encargadoId, CancellationToken ct)
    {
        if (encargadoId is null) return null;

        var usuario = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == encargadoId && u.Activo, ct)
            ?? throw new UsuarioNoEncontradoException(encargadoId.Value);

        return usuario.NombreCompleto;
    }
}
