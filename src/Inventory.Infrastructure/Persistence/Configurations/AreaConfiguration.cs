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

        builder.HasIndex(a => a.CodigoArea).IsUnique().HasFilter("[Activo] = 1");
    }
}
