using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ProductoImagenConfiguration : IEntityTypeConfiguration<ProductoImagen>
{
    public void Configure(EntityTypeBuilder<ProductoImagen> builder)
    {
        builder.ToTable("ProductoImagen");
        builder.HasKey(i => i.ProductoId);

        // Sin HasColumnType a propósito: EF ya mapea byte[] al binario correcto por
        // proveedor (varbinary(max) en SQL Server, BLOB en SQLite) — forzarlo rompe los
        // tests, que corren contra SQLite en memoria.
        builder.Property(i => i.Datos).IsRequired();
        builder.Property(i => i.ContentType).HasMaxLength(100).IsRequired();

        builder.HasOne(i => i.Producto)
            .WithOne(p => p.Imagen)
            .HasForeignKey<ProductoImagen>(i => i.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
