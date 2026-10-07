using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class FirmaUsuarioConfiguration : IEntityTypeConfiguration<FirmaUsuario>
{
    public void Configure(EntityTypeBuilder<FirmaUsuario> builder)
    {
        builder.ToTable("FirmaUsuario");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Datos).IsRequired();

        builder.HasOne(f => f.Usuario)
            .WithMany()
            .HasForeignKey(f => f.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una firma activa por persona; las anteriores quedan como historia.
        builder.HasIndex(f => f.UsuarioId).IsUnique().HasFilter("[Activa] = 1");
    }
}
