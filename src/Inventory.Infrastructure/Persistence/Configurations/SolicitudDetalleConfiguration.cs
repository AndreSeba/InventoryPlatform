using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class SolicitudDetalleConfiguration : IEntityTypeConfiguration<SolicitudDetalle>
{
    public void Configure(EntityTypeBuilder<SolicitudDetalle> builder)
    {
        builder.ToTable("SolicitudDetalle");
        builder.HasKey(sd => sd.Id);

        builder.HasOne(sd => sd.Solicitud)
            .WithMany(s => s.Detalles)
            .HasForeignKey(sd => sd.SolicitudId)
            .OnDelete(DeleteBehavior.Cascade); // borrar el encabezado borra sus líneas (solo aplica en Borrador)

        builder.HasOne(sd => sd.Producto)
            .WithMany()
            .HasForeignKey(sd => sd.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        // La guía v4 aclara que "un producto no se repite en una solicitud" es una
        // clave compuesta que SharePoint no puede exigir — en SQL Server sí podemos.
        builder.HasIndex(sd => new { sd.SolicitudId, sd.ProductoId }).IsUnique();

        builder.Property(sd => sd.UbicacionExterna).HasMaxLength(255);

        builder.ToTable(t => t.HasCheckConstraint("CK_SolicitudDetalle_Solicitada", "[CantidadSolicitada] > 0"));
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_SolicitudDetalle_Aprobada",
            "[CantidadAprobada] IS NULL OR [CantidadAprobada] <= [CantidadSolicitada]"
        ));
        builder.ToTable(t => t.HasCheckConstraint("CK_SolicitudDetalle_Entregada", "[CantidadEntregada] >= 0"));

        // UbicacionExterna/FechaRetornoEsperada solo tienen sentido si Retorna = 1 —
        // mismo criterio que CK_Movimiento_RetornaSoloSalida (MovimientoConfiguration).
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_SolicitudDetalle_RetornaSoloConDatos",
            "[Retorna] = 1 OR ([UbicacionExterna] IS NULL AND [FechaRetornoEsperada] IS NULL)"
        ));
    }
}
