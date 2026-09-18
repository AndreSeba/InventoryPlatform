using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("Categoria");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CodigoCategoria).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Descripcion).HasMaxLength(255);

        // Único POR País, ya no global — Bolivia y Perú pueden legítimamente repetir un
        // código de categoría.
        builder.HasIndex(c => new { c.PaisId, c.CodigoCategoria }).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(c => c.Pais)
            .WithMany()
            .HasForeignKey(c => c.PaisId)
            .OnDelete(DeleteBehavior.Restrict);

        // Nunca se borra un usuario con historial (categorías apuntándolo como encargado
        // incluidas) — Restrict, mismo criterio que el resto del proyecto.
        builder.HasOne(c => c.Encargado)
            .WithMany()
            .HasForeignKey(c => c.EncargadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
