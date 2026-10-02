using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class SesionConteoConfiguration : IEntityTypeConfiguration<SesionConteo>
{
    public void Configure(EntityTypeBuilder<SesionConteo> builder)
    {
        builder.ToTable("SesionConteo");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Nombre).HasMaxLength(150);
        builder.Property(s => s.Notas).HasMaxLength(500);
        builder.Property(s => s.MotivoCancelacion).HasMaxLength(500);
        builder.Property(s => s.CreadoPorNombre).HasMaxLength(150).IsRequired();
        builder.Property(s => s.CerradoPorNombre).HasMaxLength(150);

        builder.HasIndex(s => s.Codigo).IsUnique();
        builder.HasIndex(s => new { s.PaisId, s.FechaCreacion });

        // Un conteo cerrado puede tener a lo sumo UN reconteo en curso: dos clics seguidos
        // en "Reconteo" no deben crear dos conteos abiertos del mismo origen.
        builder.HasIndex(s => s.ConteoOrigenId).IsUnique().HasFilter("[Estado] = 1 AND [ConteoOrigenId] IS NOT NULL");

        builder.HasOne(s => s.Pais).WithMany().HasForeignKey(s => s.PaisId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.CreadoPor).WithMany().HasForeignKey(s => s.CreadoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.CerradoPor).WithMany().HasForeignKey(s => s.CerradoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.ConteoOrigen).WithMany().HasForeignKey(s => s.ConteoOrigenId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_SesionConteo_Estado", "[Estado] IN (1, 2, 3)");
            // Cerrado/Cancelado siempre llevan fecha de resolución; Cancelado, además, el motivo.
            t.HasCheckConstraint("CK_SesionConteo_Resolucion", "[Estado] = 1 OR [FechaCierre] IS NOT NULL");
            t.HasCheckConstraint("CK_SesionConteo_MotivoCancelacion", "[Estado] <> 3 OR [MotivoCancelacion] IS NOT NULL");
        });
    }
}

public class SesionConteoLineaConfiguration : IEntityTypeConfiguration<SesionConteoLinea>
{
    public void Configure(EntityTypeBuilder<SesionConteoLinea> builder)
    {
        builder.ToTable("SesionConteoLinea");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ContadoPorNombre).HasMaxLength(150);

        builder.HasOne(l => l.SesionConteo).WithMany(s => s.Lineas).HasForeignKey(l => l.SesionConteoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.Producto).WithMany().HasForeignKey(l => l.ProductoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.Ubicacion).WithMany().HasForeignKey(l => l.UbicacionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.ContadoPor).WithMany().HasForeignKey(l => l.ContadoPorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.SesionConteoId, l.ProductoId, l.UbicacionId }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("CK_SesionConteoLinea_Cantidad", "[CantidadContada] IS NULL OR [CantidadContada] >= 0"));
    }
}

public class SesionConteoEvidenciaConfiguration : IEntityTypeConfiguration<SesionConteoEvidencia>
{
    public void Configure(EntityTypeBuilder<SesionConteoEvidencia> builder)
    {
        builder.ToTable("SesionConteoEvidencia");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.NombreArchivo).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Datos).IsRequired();
        builder.Property(e => e.SubidoPorNombre).HasMaxLength(150).IsRequired();

        builder.HasOne(e => e.SesionConteo).WithMany(s => s.Evidencias).HasForeignKey(e => e.SesionConteoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.SubidoPor).WithMany().HasForeignKey(e => e.SubidoPorId).OnDelete(DeleteBehavior.Restrict);
    }
}
