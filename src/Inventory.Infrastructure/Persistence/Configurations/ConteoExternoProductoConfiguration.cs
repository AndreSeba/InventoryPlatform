using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ConteoExternoProductoConfiguration : IEntityTypeConfiguration<ConteoExternoProducto>
{
    public void Configure(EntityTypeBuilder<ConteoExternoProducto> builder)
    {
        builder.ToTable("ConteoExternoProducto");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).HasMaxLength(30).IsRequired();
        builder.Property(p => p.CodigoNormalizado).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Familia).HasMaxLength(100).IsRequired();
        builder.Property(p => p.UnidadCodigo).HasMaxLength(10);
        builder.Property(p => p.CostoReferencia).HasColumnType("decimal(18,4)");

        builder.HasIndex(p => new { p.PaisId, p.CodigoNormalizado }).IsUnique().HasFilter("[Activo] = 1");
        builder.HasIndex(p => new { p.PaisId, p.Familia });

        builder.HasOne(p => p.Pais)
            .WithMany()
            .HasForeignKey(p => p.PaisId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ConteoExternoProducto_CostoNoNegativo",
            "[CostoReferencia] IS NULL OR [CostoReferencia] >= 0"
        ));
    }
}
