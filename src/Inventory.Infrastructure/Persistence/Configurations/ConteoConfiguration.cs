using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ConteoConfiguration : IEntityTypeConfiguration<Conteo>
{
    public void Configure(EntityTypeBuilder<Conteo> builder)
    {
        builder.ToTable("Conteo");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.SesionConteo).HasMaxLength(50).IsRequired();
        builder.Property(c => c.CantidadContada).HasColumnType("decimal(18,3)");
        builder.Property(c => c.ContadoPor).HasMaxLength(150).IsRequired();

        builder.HasOne(c => c.Producto).WithMany().HasForeignKey(c => c.ProductoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Ubicacion).WithMany().HasForeignKey(c => c.UbicacionId).OnDelete(DeleteBehavior.Restrict);

        // UQ_Conteo_Sesion (guía v3/v4): permite reconteos (NumeroConteo 1, 2, 3...)
        // pero no cargar dos veces el mismo producto/ubicación/número en la misma sesión.
        builder.HasIndex(c => new { c.SesionConteo, c.ProductoId, c.UbicacionId, c.NumeroConteo }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("CK_Conteo_Cantidad", "[CantidadContada] >= 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Conteo_Numero", "[NumeroConteo] >= 1"));
    }
}
