using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ConteoExternoRegistroConfiguration : IEntityTypeConfiguration<ConteoExternoRegistro>
{
    public void Configure(EntityTypeBuilder<ConteoExternoRegistro> builder)
    {
        builder.ToTable("ConteoExternoRegistro");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Ubicacion).HasMaxLength(ConteoExternoRegistro.LargoMaxUbicacion).IsRequired();
        builder.Property(r => r.Cantidad).HasColumnType("decimal(18,4)");
        builder.Property(r => r.ContadoPorNombre).HasMaxLength(150).IsRequired();

        builder.HasIndex(r => new { r.ConteoExternoLineaId, r.NumeroConteo, r.Ubicacion }).IsUnique();

        builder.HasOne(r => r.ConteoExternoLinea)
            .WithMany(l => l.Registros)
            .HasForeignKey(r => r.ConteoExternoLineaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ContadoPor)
            .WithMany()
            .HasForeignKey(r => r.ContadoPorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ConteoExternoRegistro_Numero",
            $"[NumeroConteo] >= 1 AND [NumeroConteo] <= {ConteoExternoRegistro.MaxConteos}"
        ));
    }
}
