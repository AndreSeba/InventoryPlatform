using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ConteoExternoExistenciaConfiguration : IEntityTypeConfiguration<ConteoExternoExistencia>
{
    public void Configure(EntityTypeBuilder<ConteoExternoExistencia> builder)
    {
        builder.ToTable("ConteoExternoExistencia");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AlmacenCodigo).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Lote).HasMaxLength(50);
        builder.Property(e => e.CantidadSistema).HasColumnType("decimal(18,4)");

        builder.HasIndex(e => new { e.PaisId, e.ConteoExternoProductoId, e.AlmacenCodigo });

        builder.HasOne(e => e.ConteoExternoProducto)
            .WithMany()
            .HasForeignKey(e => e.ConteoExternoProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Pais)
            .WithMany()
            .HasForeignKey(e => e.PaisId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ConteoExternoExistencia_Cantidad",
            "[CantidadSistema] >= 0"
        ));
    }
}
