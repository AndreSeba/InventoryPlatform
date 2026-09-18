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

        // Único POR País, ya no global — Bolivia y Perú pueden legítimamente repetir un
        // código de unidad.
        builder.HasIndex(u => new { u.PaisId, u.CodigoUnidad }).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(u => u.Pais)
            .WithMany()
            .HasForeignKey(u => u.PaisId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed de las 3 unidades que antes estaban hardcodeadas en el CHECK
        // CK_Producto_Unidad — así los productos ya existentes siguen siendo válidos
        // apenas se aplica la migración, sin necesidad de convertir datos. Quedan en
        // Bolivia (PaisId=1) nada más — Perú arranca sin unidades, se cargan a mano
        // desde /unidades (pedido explícito del usuario, mismo criterio que Categoria).
        builder.HasData(
            new Unidad { Id = 1, CodigoUnidad = "UNI", Nombre = "Unidad", Activo = true, PaisId = 1 },
            new Unidad { Id = 2, CodigoUnidad = "CAJA", Nombre = "Caja", Activo = true, PaisId = 1 },
            new Unidad { Id = 3, CodigoUnidad = "PQTS", Nombre = "Paquete", Activo = true, PaisId = 1 }
        );
    }
}
