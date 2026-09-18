using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class UbicacionConfiguration : IEntityTypeConfiguration<Ubicacion>
{
    public void Configure(EntityTypeBuilder<Ubicacion> builder)
    {
        builder.ToTable("Ubicacion");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nro).HasMaxLength(20).IsRequired();
        builder.Property(u => u.Lado).HasMaxLength(20).IsRequired();
        builder.Property(u => u.Nivel).HasMaxLength(20);
        builder.Property(u => u.CodigoUbicacion).HasMaxLength(60).IsRequired();

        // Único POR Almacén, ya no global — con varios almacenes/países coexistiendo, dos
        // de ellos legítimamente pueden repetir un código de rack (ej. "A-04-01").
        builder.HasIndex(u => new { u.AlmacenId, u.CodigoUbicacion }).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(u => u.Almacen)
            .WithMany()
            .HasForeignKey(u => u.AlmacenId)
            .OnDelete(DeleteBehavior.Restrict);

        // CK_Ubicacion_Nivel (guía v4, 5.9): un RACK exige Nivel, un MUEBLE no lo lleva.
        // TipoUbicacion: Rack=1, Mueble=2.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Ubicacion_Nivel",
            "([TipoUbicacion] = 1 AND [Nivel] IS NOT NULL) OR ([TipoUbicacion] = 2 AND [Nivel] IS NULL)"
        ));
    }
}
