using Inventory.Domain.Entities;
using Inventory.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class PermisoConfiguration : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> builder)
    {
        builder.ToTable("Permiso");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).HasMaxLength(60).IsRequired();
        builder.Property(p => p.Modulo).HasMaxLength(60).IsRequired();
        builder.Property(p => p.Descripcion).HasMaxLength(300).IsRequired();

        builder.HasIndex(p => p.Codigo).IsUnique();

        // Seed determinístico (mismo Id siempre) a partir del catálogo único de
        // Inventory.Domain.Security.Permisos — agregar un permiso nuevo ahí alcanza,
        // no hace falta tocar esto.
        var seed = Permisos.Catalogo
            .Select((p, i) => new Permiso { Id = i + 1, Codigo = p.Codigo, Modulo = p.Modulo, Descripcion = p.Descripcion })
            .ToArray();
        builder.HasData(seed);
    }
}
