using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class PaisConfiguration : IEntityTypeConfiguration<Pais>
{
    public void Configure(EntityTypeBuilder<Pais> builder)
    {
        builder.ToTable("Pais");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nombre).HasMaxLength(80).IsRequired();
        builder.Property(p => p.CodigoIso).HasMaxLength(2).IsRequired();

        builder.HasIndex(p => p.CodigoIso).IsUnique().HasFilter("[Activo] = 1");

        // Semilla: los dos países donde opera hoy Nestlé con esta herramienta. Agregar
        // uno nuevo el día de mañana es una migración chica (InsertData), no hace falta
        // tocar código — ver PaisService para el alta desde /paises.
        builder.HasData(
            new Pais { Id = 1, Nombre = "Bolivia", CodigoIso = "BO", Activo = true },
            new Pais { Id = 2, Nombre = "Perú", CodigoIso = "PE", Activo = true }
        );
    }
}
