using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class MovimientoConfiguration : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> builder)
    {
        builder.ToTable("Movimiento");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.NumeroMovimiento).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Cantidad).HasColumnType("decimal(18,3)");
        builder.Property(m => m.CantidadEfectiva).HasColumnType("decimal(18,3)");
        builder.Property(m => m.UbicacionExterna).HasMaxLength(255);
        builder.Property(m => m.RegistradoPor).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Motivo).HasMaxLength(2000);

        builder.HasIndex(m => m.NumeroMovimiento).IsUnique();
        builder.HasIndex(m => new { m.ProductoId, m.FechaMovimiento });
        builder.HasIndex(m => new { m.UbicacionId, m.ProductoId });

        // Nunca se borra un movimiento confirmado (regla del proyecto original) — Restrict
        // en todas las FK, además de evitar los ciclos de cascada que SQL Server rechaza
        // al tener varias referencias encadenadas dentro de la misma tabla/related tables.
        builder.HasOne(m => m.Producto)
            .WithMany(p => p.Movimientos)
            .HasForeignKey(m => m.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Ubicacion)
            .WithMany()
            .HasForeignKey(m => m.UbicacionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.MovimientoOrigen)
            .WithMany()
            .HasForeignKey(m => m.MovimientoOrigenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.SolicitudDetalle)
            .WithMany(sd => sd.Movimientos)
            .HasForeignKey(m => m.SolicitudDetalleId)
            .OnDelete(DeleteBehavior.Restrict);

        // TipoMovimiento: Entrada=1, Salida=2, AjustePositivo=3, AjusteNegativo=4.
        builder.ToTable(t => t.HasCheckConstraint("CK_Movimiento_Cantidad", "[Cantidad] > 0"));

        // CK_Movimiento_Signo (guía v3/v4): sin esto, un solo registro con el signo
        // invertido corrompe el stock de forma permanente y silenciosa.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Movimiento_Signo",
            "([TipoMovimiento] IN (1,3) AND [CantidadEfectiva] = [Cantidad]) OR ([TipoMovimiento] IN (2,4) AND [CantidadEfectiva] = -[Cantidad])"
        ));

        // Retorna/UbicacionExterna/FechaRetornoEsperada solo tienen sentido en una Salida.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Movimiento_RetornaSoloSalida",
            "[TipoMovimiento] = 2 OR ([Retorna] = 0 AND [UbicacionExterna] IS NULL AND [FechaRetornoEsperada] IS NULL)"
        ));

        // Una devolución (MovimientoOrigenId seteado) siempre es una Entrada.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Movimiento_OrigenSoloEntrada",
            "[MovimientoOrigenId] IS NULL OR [TipoMovimiento] = 1"
        ));

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Movimiento_NoAutoOrigen",
            "[MovimientoOrigenId] IS NULL OR [MovimientoOrigenId] <> [Id]"
        ));

        // Una Salida solo se liga a una línea de solicitud (nunca una Entrada/Ajuste).
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Movimiento_SolicitudSoloSalida",
            "[SolicitudDetalleId] IS NULL OR [TipoMovimiento] = 2"
        ));
    }
}
