using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class AlmacenConfiguration : IEntityTypeConfiguration<Almacen>
{
    public void Configure(EntityTypeBuilder<Almacen> builder)
    {
        builder.ToTable("Almacen");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CodigoAlmacen).HasMaxLength(30).IsRequired();
        builder.Property(a => a.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(a => a.ProveedorNombre).HasMaxLength(150);
        builder.Property(a => a.ProveedorContacto).HasMaxLength(150);
        builder.Property(a => a.ProveedorDireccion).HasMaxLength(255);

        builder.HasIndex(a => a.CodigoAlmacen).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(a => a.Pais)
            .WithMany()
            .HasForeignKey(a => a.PaisId)
            .OnDelete(DeleteBehavior.Restrict);

        // Propio (1) nunca lleva datos de proveedor; Externo (2) exige al menos el
        // nombre (Contacto/Dirección quedan opcionales, no siempre se conocen de entrada).
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Almacen_Proveedor",
            "([TipoAlmacen] = 1 AND [ProveedorNombre] IS NULL) OR ([TipoAlmacen] = 2 AND [ProveedorNombre] IS NOT NULL)"
        ));

        // Semilla: el almacén propio de Bolivia, para que las Ubicacion ya existentes
        // (creadas antes de este cambio) tengan a dónde apuntar en el backfill.
        builder.HasData(new Almacen
        {
            Id = 1,
            CodigoAlmacen = "BO-PROPIO",
            Nombre = "Almacén Nestlé Bolivia",
            PaisId = 1,
            TipoAlmacen = TipoAlmacen.Propio,
            Activo = true,
        });
    }
}
