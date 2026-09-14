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

        builder.HasIndex(u => u.Email).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(u => u.Rol).WithMany(r => r.Usuarios).HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);

        // Sin seed de usuario acá a propósito: el hash de contraseña necesita BCrypt en
        // tiempo de ejecución, no un valor estático de migración. El admin por defecto lo
        // crea Infrastructure/Seed/DatabaseSeeder.cs al arrancar la API si Usuario está vacía.
    }
}
