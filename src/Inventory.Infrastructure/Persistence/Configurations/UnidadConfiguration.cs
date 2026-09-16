using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class UnidadConfiguration : IEntityTypeConfiguration<Unidad>
{
    public void Configure(EntityTypeBuilder<Unidad> builder)
    {
        builder.ToTable("Unidad");
        builder.HasKey(u => u.Id);

        // 10 chars = el mismo largo que ya tiene Producto.UnidadMedida, que guarda
        // este código. No agrandarlo sin agrandar también esa columna.
        builder.Property(u => u.CodigoUnidad).HasMaxLength(10).IsRequired();
        builder.Property(u => u.Nombre).HasMaxLength(80).IsRequired();

        builder.HasIndex(u => u.CodigoUnidad).IsUnique().HasFilter("[Activo] = 1");

        // Seed de las 3 unidades que antes estaban hardcodeadas en el CHECK
        // CK_Producto_Unidad — así los productos ya existentes siguen siendo válidos
        // apenas se aplica la migración, sin necesidad de convertir datos.
        builder.HasData(
            new Unidad { Id = 1, CodigoUnidad = "UNI", Nombre = "Unidad", Activo = true },
            new Unidad { Id = 2, CodigoUnidad = "CAJA", Nombre = "Caja", Activo = true },
            new Unidad { Id = 3, CodigoUnidad = "PQTS", Nombre = "Paquete", Activo = true }
        );
    }
}
