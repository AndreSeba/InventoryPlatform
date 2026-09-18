using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.Property(u => u.NombreCompleto).HasMaxLength(150).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();

        // Único POR País, ya no global — el mismo email puede existir como cuentas
        // distintas en países distintos (ver comentario en Usuario.cs).
        builder.HasIndex(u => new { u.PaisId, u.Email }).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(u => u.Rol).WithMany(r => r.Usuarios).HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Pais).WithMany().HasForeignKey(u => u.PaisId).OnDelete(DeleteBehavior.Restrict);

        // Sin seed de usuario acá a propósito: el hash de contraseña necesita BCrypt en
        // tiempo de ejecución, no un valor estático de migración. El admin por defecto lo
        // crea Infrastructure/Seed/DatabaseSeeder.cs al arrancar la API si Usuario está vacía.
    }
}
