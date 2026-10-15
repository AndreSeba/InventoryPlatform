using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ConteoExternoLineaConfiguration : IEntityTypeConfiguration<ConteoExternoLinea>
{
    public void Configure(EntityTypeBuilder<ConteoExternoLinea> builder)
    {
        builder.ToTable("ConteoExternoLinea");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ProductoCodigo).HasMaxLength(30).IsRequired();
        builder.Property(l => l.ProductoNombre).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Familia).HasMaxLength(100).IsRequired();
        builder.Property(l => l.AlmacenCodigo).HasMaxLength(30).IsRequired();
        builder.Property(l => l.Lote).HasMaxLength(50);
        builder.Property(l => l.UnidadCodigo).HasMaxLength(10).IsRequired();
        builder.Property(l => l.ExistenciaSistema).HasColumnType("decimal(18,4)");
        builder.Property(l => l.CostoUnitario).HasColumnType("decimal(18,4)");
        builder.Property(l => l.Observacion).HasMaxLength(200);

        // Una existencia no puede entrar dos veces a la misma sesión.
        builder.HasIndex(l => new { l.ConteoExternoSesionId, l.ConteoExternoExistenciaId }).IsUnique();

        builder.HasOne(l => l.ConteoExternoSesion)
            .WithMany(s => s.Lineas)
            .HasForeignKey(l => l.ConteoExternoSesionId)
            .OnDelete(DeleteBehavior.Cascade); // la línea no tiene sentido sin su sesión

        builder.HasOne(l => l.ConteoExternoExistencia)
            .WithMany()
            .HasForeignKey(l => l.ConteoExternoExistenciaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
