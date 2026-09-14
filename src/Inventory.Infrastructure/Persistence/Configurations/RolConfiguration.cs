using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("Rol");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Nombre).HasMaxLength(80).IsRequired();
        builder.Property(r => r.Descripcion).HasMaxLength(300);

        builder.HasIndex(r => r.Nombre).IsUnique().HasFilter("[Activo] = 1");

        builder.HasData(
            new Rol { Id = RolPermisoConfiguration.RolAdministradorId, Nombre = "Administrador", Descripcion = "Acceso completo, incluida la gestión de usuarios y roles.", Activo = true },
            new Rol { Id = RolPermisoConfiguration.RolOperadorId, Nombre = "Operador", Descripcion = "Opera el día a día: productos, movimientos, solicitudes y conteo. Sin gestión de catálogos ni usuarios.", Activo = true },
            new Rol { Id = RolPermisoConfiguration.RolConsultaId, Nombre = "Consulta", Descripcion = "Solo lectura en todos los módulos.", Activo = true }
        );
    }
}
