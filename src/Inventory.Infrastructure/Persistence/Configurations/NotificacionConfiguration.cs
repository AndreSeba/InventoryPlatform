using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> builder)
    {
        builder.ToTable("Notificacion");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Clave).HasMaxLength(100).IsRequired();
        builder.Property(n => n.Titulo).HasMaxLength(150).IsRequired();
        builder.Property(n => n.Mensaje).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Url).HasMaxLength(200);

        // Una sola notificación ACTIVA por persona y clave: dos sincronizaciones simultáneas no pueden duplicarla.
        builder.HasIndex(n => new { n.UsuarioId, n.Clave }).IsUnique().HasFilter("[Resuelta] = 0");
        builder.HasIndex(n => new { n.UsuarioId, n.Resuelta, n.FechaCreacion });
        builder.HasIndex(n => new { n.PaisId, n.Resuelta });

        builder.HasOne(n => n.Pais).WithMany().HasForeignKey(n => n.PaisId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(n => n.Usuario).WithMany().HasForeignKey(n => n.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Notificacion_Categoria", "[Categoria] IN (1, 2, 3, 4)");
            t.HasCheckConstraint("CK_Notificacion_Severidad", "[Severidad] IN (1, 2, 3)");
            t.HasCheckConstraint("CK_Notificacion_Resolucion", "[Resuelta] = 0 OR [FechaResolucion] IS NOT NULL");
        });
    }
}
