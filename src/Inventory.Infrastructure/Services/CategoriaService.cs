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

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(bool incluirInactivas, CancellationToken ct)
    {
        var query = _db.Categorias.AsNoTracking();
        if (!incluirInactivas) query = query.Where(c => c.Activo);

        return await query.OrderBy(c => c.CodigoCategoria)
            .Select(c => new CategoriaDto(c.Id, c.CodigoCategoria, c.Descripcion, c.Activo))
            .ToListAsync(ct);
    }

    public async Task<CategoriaDto> CrearAsync(CrearCategoriaDto dto, CancellationToken ct)
    {
        var codigo = dto.CodigoCategoria.Trim().ToUpperInvariant();

        var yaExiste = await _db.Categorias.AnyAsync(c => c.CodigoCategoria == codigo && c.Activo, ct);
        if (yaExiste)
            throw new CodigoCategoriaDuplicadoException(codigo);

        var categoria = new Categoria { CodigoCategoria = codigo, Descripcion = dto.Descripcion, Activo = true };
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync(ct);

        return new CategoriaDto(categoria.Id, categoria.CodigoCategoria, categoria.Descripcion, categoria.Activo);
    }

    public async Task<CategoriaDto> ActualizarAsync(int id, ActualizarCategoriaDto dto, CancellationToken ct)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new CategoriaNoEncontradaException(id);

        var codigo = dto.CodigoCategoria.Trim().ToUpperInvariant();

        var yaExiste = await _db.Categorias.AnyAsync(c => c.CodigoCategoria == codigo && c.Activo && c.Id != id, ct);
        if (yaExiste)
            throw new CodigoCategoriaDuplicadoException(codigo);

        categoria.CodigoCategoria = codigo;
        categoria.Descripcion = dto.Descripcion;
        categoria.Activo = dto.Activo;

        await _db.SaveChangesAsync(ct);
        return new CategoriaDto(categoria.Id, categoria.CodigoCategoria, categoria.Descripcion, categoria.Activo);
    }
}
