using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class AuditoriaConfiguration : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> builder)
    {
        builder.ToTable("Auditoria");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UsuarioNombre).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Entidad).HasMaxLength(80).IsRequired();
        builder.Property(a => a.EntidadId).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Accion).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Motivo).HasMaxLength(300);

        builder.HasOne(a => a.Usuario)
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.Entidad, a.EntidadId });
        builder.HasIndex(a => a.UsuarioId);
    }
}
