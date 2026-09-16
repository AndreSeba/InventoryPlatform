using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class SolicitudConfiguration : IEntityTypeConfiguration<Solicitud>
{
    public void Configure(EntityTypeBuilder<Solicitud> builder)
    {
        builder.ToTable("Solicitud");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.NumeroSolicitud).HasMaxLength(50).IsRequired();
        builder.Property(s => s.SolicitadoPorNombre).HasMaxLength(150).IsRequired();
        builder.Property(s => s.AprobadoPorNombre).HasMaxLength(150);
        builder.Property(s => s.MotivoRechazo).HasMaxLength(2000);

        builder.HasIndex(s => s.NumeroSolicitud).IsUnique();

        builder.HasOne(s => s.Area)
            .WithMany()
            .HasForeignKey(s => s.AreaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.SolicitadoPor)
            .WithMany()
            .HasForeignKey(s => s.SolicitadoPorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.AprobadoPor)
            .WithMany()
            .HasForeignKey(s => s.AprobadoPorId)
            .OnDelete(DeleteBehavior.Restrict);

        // EstadoSolicitud.Rechazada = 4 — CK_Solicitud_Aprobacion (guía v4, 5.11):
        // una solicitud rechazada exige motivo.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Solicitud_MotivoRechazo",
            "[Estado] <> 4 OR [MotivoRechazo] IS NOT NULL"
        ));
    }
}
