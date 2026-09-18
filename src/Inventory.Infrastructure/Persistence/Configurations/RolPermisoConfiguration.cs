using Inventory.Domain.Entities;
using Inventory.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    // Roles seed — Ids fijos para poder referenciarlos desde el seed de RolPermiso.
    // Bolivia (PaisId=1) conserva los 4 Ids originales; Perú (PaisId=2) suma los 4
    // siguientes — ver RolConfiguration.HasData. Un país agregado después de la
    // migración (vía /paises) no pasa por acá, lo siembra PaisService.CrearAsync
    // con Ids que EF asigna solo, usando la misma RolesPorDefecto.
    public const int RolAdministradorBoliviaId = 1;
    public const int RolOperadorBoliviaId = 2;
    public const int RolConsultaBoliviaId = 3;
    public const int RolSolicitanteBoliviaId = 4;
    public const int RolAdministradorPeruId = 5;
    public const int RolOperadorPeruId = 6;
    public const int RolConsultaPeruId = 7;
    public const int RolSolicitantePeruId = 8;

    public void Configure(EntityTypeBuilder<RolPermiso> builder)
    {
        builder.ToTable("RolPermiso");
        builder.HasKey(rp => new { rp.RolId, rp.PermisoId });

        builder.HasOne(rp => rp.Rol).WithMany(r => r.RolPermisos).HasForeignKey(rp => rp.RolId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(rp => rp.Permiso).WithMany(p => p.RolPermisos).HasForeignKey(rp => rp.PermisoId).OnDelete(DeleteBehavior.Cascade);

        var codigoAId = Permisos.Catalogo
            .Select((p, i) => (p.Codigo, Id: i + 1))
            .ToDictionary(x => x.Codigo, x => x.Id);

        var seed = new List<RolPermiso>();
        void SembrarPais(int administradorId, int operadorId, int consultaId, int solicitanteId)
        {
            // Administrador: todos los permisos del catálogo.
            seed.AddRange(codigoAId.Values.Select(permisoId => new RolPermiso { RolId = administradorId, PermisoId = permisoId }));
            seed.AddRange(RolesPorDefecto.PermisosOperador.Select(codigo => new RolPermiso { RolId = operadorId, PermisoId = codigoAId[codigo] }));
            seed.AddRange(RolesPorDefecto.PermisosConsulta.Select(codigo => new RolPermiso { RolId = consultaId, PermisoId = codigoAId[codigo] }));
            seed.AddRange(RolesPorDefecto.PermisosSolicitante.Select(codigo => new RolPermiso { RolId = solicitanteId, PermisoId = codigoAId[codigo] }));
        }

        SembrarPais(RolAdministradorBoliviaId, RolOperadorBoliviaId, RolConsultaBoliviaId, RolSolicitanteBoliviaId);
        SembrarPais(RolAdministradorPeruId, RolOperadorPeruId, RolConsultaPeruId, RolSolicitantePeruId);

        builder.HasData(seed);
    }
}
