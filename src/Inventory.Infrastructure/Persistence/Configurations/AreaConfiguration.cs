using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Area");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CodigoArea).HasMaxLength(50).IsRequired();
        builder.Property(a => a.NombreArea).HasMaxLength(150).IsRequired();

        // Único POR País, ya no global — Bolivia y Perú pueden legítimamente repetir un
        // código de área (ej. "TM" de Trade Marketing en ambos).
        builder.HasIndex(a => new { a.PaisId, a.CodigoArea }).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(a => a.Pais)
            .WithMany()
            .HasForeignKey(a => a.PaisId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
