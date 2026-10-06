using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class AvisoDevolucionConfiguration : IEntityTypeConfiguration<AvisoDevolucion>
{
    public void Configure(EntityTypeBuilder<AvisoDevolucion> builder)
    {
        builder.ToTable("AvisoDevolucion");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Notas).HasMaxLength(500);
        builder.Property(a => a.AvisadoPorNombre).HasMaxLength(150).IsRequired();
        builder.Property(a => a.ResueltoPorNombre).HasMaxLength(150);
        builder.Property(a => a.MotivoCancelacion).HasMaxLength(500);

        builder.HasIndex(a => a.Codigo).IsUnique();
        builder.HasIndex(a => new { a.PaisId, a.Estado });
        builder.HasIndex(a => a.MovimientoOrigenId);
        builder.HasIndex(a => a.AvisadoPorId);
        // Una entrada de devolución cierra a lo sumo UN aviso.
        builder.HasIndex(a => a.MovimientoDevolucionId).IsUnique().HasFilter("[MovimientoDevolucionId] IS NOT NULL");

        builder.HasOne(a => a.Pais).WithMany().HasForeignKey(a => a.PaisId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.MovimientoOrigen).WithMany().HasForeignKey(a => a.MovimientoOrigenId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.MovimientoDevolucion).WithMany().HasForeignKey(a => a.MovimientoDevolucionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AvisadoPor).WithMany().HasForeignKey(a => a.AvisadoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ResueltoPor).WithMany().HasForeignKey(a => a.ResueltoPorId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_AvisoDevolucion_Estado", "[Estado] IN (1, 2, 3)");
            t.HasCheckConstraint("CK_AvisoDevolucion_Cantidad", "[Cantidad] > 0");
            // Recibido/Cancelado siempre llevan fecha de resolución; Recibido, además, la entrada y la
            // cantidad recibida; Cancelado, el motivo.
            t.HasCheckConstraint("CK_AvisoDevolucion_Resolucion", "[Estado] = 1 OR [FechaResolucion] IS NOT NULL");
            t.HasCheckConstraint("CK_AvisoDevolucion_Recibido", "[Estado] <> 2 OR ([MovimientoDevolucionId] IS NOT NULL AND [CantidadRecibida] > 0)");
            t.HasCheckConstraint("CK_AvisoDevolucion_MotivoCancelacion", "[Estado] <> 3 OR [MotivoCancelacion] IS NOT NULL");
        });
    }
}
