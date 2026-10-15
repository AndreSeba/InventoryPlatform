using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ConteoExternoAlmacenConfiguration : IEntityTypeConfiguration<ConteoExternoAlmacen>
{
    public void Configure(EntityTypeBuilder<ConteoExternoAlmacen> builder)
    {
        builder.ToTable("ConteoExternoAlmacen");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(a => a.Nombre).HasMaxLength(150).IsRequired();

        builder.HasIndex(a => new { a.PaisId, a.Codigo }).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(a => a.Pais)
            .WithMany()
            .HasForeignKey(a => a.PaisId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
