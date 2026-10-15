using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ConteoExternoSesionConfiguration : IEntityTypeConfiguration<ConteoExternoSesion>
{
    public void Configure(EntityTypeBuilder<ConteoExternoSesion> builder)
    {
        builder.ToTable("ConteoExternoSesion");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(s => s.CreadoPorNombre).HasMaxLength(150).IsRequired();
        builder.Property(s => s.CerradoPorNombre).HasMaxLength(150);

        builder.HasOne(s => s.Pais).WithMany().HasForeignKey(s => s.PaisId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.CreadoPor).WithMany().HasForeignKey(s => s.CreadoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.CerradoPor).WithMany().HasForeignKey(s => s.CerradoPorId).OnDelete(DeleteBehavior.Restrict);
    }
}
