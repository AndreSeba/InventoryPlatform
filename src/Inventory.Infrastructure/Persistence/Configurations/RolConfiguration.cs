using Inventory.Domain.Entities;
using Inventory.Domain.Security;
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

        // Único POR País, ya no global — Bolivia y Perú tienen cada uno su propio
        // "Administrador"/"Operador"/etc.
        builder.HasIndex(r => new { r.PaisId, r.Nombre }).IsUnique().HasFilter("[Activo] = 1");

        builder.HasOne(r => r.Pais)
            .WithMany()
            .HasForeignKey(r => r.PaisId)
            .OnDelete(DeleteBehavior.Restrict);

        const string descAdmin = "Acceso completo, incluida la gestión de usuarios y roles.";
        const string descOperador = "Opera el día a día: productos, movimientos, solicitudes y conteo. Sin gestión de catálogos ni usuarios.";
        const string descConsulta = "Solo lectura en todos los módulos.";
        const string descSolicitante = "Solo puede crear y ver sus propias solicitudes de material.";

        builder.HasData(
            // Bolivia (PaisId=1) — Ids originales, sin cambiar.
            new Rol { Id = RolPermisoConfiguration.RolAdministradorBoliviaId, Nombre = RolesPorDefecto.Administrador, Descripcion = descAdmin, PaisId = 1, Activo = true },
            new Rol { Id = RolPermisoConfiguration.RolOperadorBoliviaId, Nombre = RolesPorDefecto.Operador, Descripcion = descOperador, PaisId = 1, Activo = true },
            new Rol { Id = RolPermisoConfiguration.RolConsultaBoliviaId, Nombre = RolesPorDefecto.Consulta, Descripcion = descConsulta, PaisId = 1, Activo = true },
            new Rol { Id = RolPermisoConfiguration.RolSolicitanteBoliviaId, Nombre = RolesPorDefecto.Solicitante, Descripcion = descSolicitante, PaisId = 1, Activo = true },
            // Perú (PaisId=2) — Ids nuevos, mismos permisos (ver RolPermisoConfiguration).
            new Rol { Id = RolPermisoConfiguration.RolAdministradorPeruId, Nombre = RolesPorDefecto.Administrador, Descripcion = descAdmin, PaisId = 2, Activo = true },
            new Rol { Id = RolPermisoConfiguration.RolOperadorPeruId, Nombre = RolesPorDefecto.Operador, Descripcion = descOperador, PaisId = 2, Activo = true },
            new Rol { Id = RolPermisoConfiguration.RolConsultaPeruId, Nombre = RolesPorDefecto.Consulta, Descripcion = descConsulta, PaisId = 2, Activo = true },
            new Rol { Id = RolPermisoConfiguration.RolSolicitantePeruId, Nombre = RolesPorDefecto.Solicitante, Descripcion = descSolicitante, PaisId = 2, Activo = true }
        );
    }
}
