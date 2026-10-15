using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ConteoExternoUnidadConfiguration : IEntityTypeConfiguration<ConteoExternoUnidad>
{
    public void Configure(EntityTypeBuilder<ConteoExternoUnidad> builder)
    {
        builder.ToTable("ConteoExternoUnidad");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Codigo).HasMaxLength(10).IsRequired();

        builder.HasIndex(u => new { u.PaisId, u.Codigo }).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(u => u.Pais)
            .WithMany()
            .HasForeignKey(u => u.PaisId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ConteoExternoUnidad_Decimales",
            "[DecimalesPermitidos] >= 0 AND [DecimalesPermitidos] <= 4"
        ));
    }
}
