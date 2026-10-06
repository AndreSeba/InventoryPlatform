using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class RevisionAccesoConfiguration : IEntityTypeConfiguration<RevisionAcceso>
{
    public void Configure(EntityTypeBuilder<RevisionAcceso> builder)
    {
        builder.ToTable("RevisionAcceso");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Notas).HasMaxLength(500);
        builder.Property(r => r.MotivoCancelacion).HasMaxLength(500);
        builder.Property(r => r.IniciadaPorNombre).HasMaxLength(150).IsRequired();
        builder.Property(r => r.CerradaPorNombre).HasMaxLength(150);

        builder.HasIndex(r => r.Codigo).IsUnique();
        builder.HasIndex(r => new { r.PaisId, r.FechaInicio });

        // A lo sumo UNA campaña en curso por país y alcance: dos clics seguidos en "Iniciar
        // revisión" no deben abrir dos campañas sobre las mismas cuentas.
        builder.HasIndex(r => new { r.PaisId, r.Alcance }).IsUnique().HasFilter("[Estado] = 1");

        builder.HasOne(r => r.Pais).WithMany().HasForeignKey(r => r.PaisId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.IniciadaPor).WithMany().HasForeignKey(r => r.IniciadaPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CerradaPor).WithMany().HasForeignKey(r => r.CerradaPorId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_RevisionAcceso_Estado", "[Estado] IN (1, 2, 3)");
            t.HasCheckConstraint("CK_RevisionAcceso_Alcance", "[Alcance] IN (1, 2)");
            // Cerrada/Cancelada siempre llevan fecha de resolución; Cancelada, además, el motivo.
            t.HasCheckConstraint("CK_RevisionAcceso_Resolucion", "[Estado] = 1 OR [FechaCierre] IS NOT NULL");
            t.HasCheckConstraint("CK_RevisionAcceso_MotivoCancelacion", "[Estado] <> 3 OR [MotivoCancelacion] IS NOT NULL");
        });
    }
}

public class RevisionAccesoLineaConfiguration : IEntityTypeConfiguration<RevisionAccesoLinea>
{
    public void Configure(EntityTypeBuilder<RevisionAccesoLinea> builder)
    {
        builder.ToTable("RevisionAccesoLinea");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.UsuarioNombre).HasMaxLength(150).IsRequired();
        builder.Property(l => l.UsuarioEmail).HasMaxLength(200).IsRequired();
        builder.Property(l => l.RolNombre).HasMaxLength(100).IsRequired();
        builder.Property(l => l.RolNuevoNombre).HasMaxLength(100);
        builder.Property(l => l.Comentario).HasMaxLength(500);
        builder.Property(l => l.RevisadoPorNombre).HasMaxLength(150);

        builder.HasOne(l => l.RevisionAcceso).WithMany(r => r.Lineas).HasForeignKey(l => l.RevisionAccesoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.Usuario).WithMany().HasForeignKey(l => l.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.RolNuevo).WithMany().HasForeignKey(l => l.RolNuevoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.RevisadoPor).WithMany().HasForeignKey(l => l.RevisadoPorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.RevisionAccesoId, l.UsuarioId }).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_RevisionAccesoLinea_Decision", "[Decision] IN (0, 1, 2, 3)");
            // Cambiar de rol exige el rol nuevo; cualquier otra decisión no lo lleva.
            t.HasCheckConstraint("CK_RevisionAccesoLinea_RolNuevo", "([Decision] = 3 AND [RolNuevoId] IS NOT NULL) OR ([Decision] <> 3 AND [RolNuevoId] IS NULL)");
        });
    }
}
