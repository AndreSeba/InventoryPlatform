using Inventory.Domain.Entities;
using Inventory.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    // Roles seed — Id fijo para poder referenciarlos desde el seed de RolPermiso.
    public const int RolAdministradorId = 1;
    public const int RolOperadorId = 2;
    public const int RolConsultaId = 3;

    private static readonly string[] PermisosOperador =
    [
        Permisos.ProductosVer,
        Permisos.MovimientosVer, Permisos.MovimientosEntrada, Permisos.MovimientosSalida,
        Permisos.MovimientosAjuste, Permisos.MovimientosDevolucion,
        Permisos.SolicitudesVer, Permisos.SolicitudesCrear, Permisos.SolicitudesEntregar,
        Permisos.ConteosVer, Permisos.ConteosRegistrar,
        Permisos.CategoriasVer, Permisos.AreasVer, Permisos.UbicacionesVer,
    ];

    private static readonly string[] PermisosConsulta =
    [
        Permisos.ProductosVer, Permisos.MovimientosVer, Permisos.SolicitudesVer,
        Permisos.ConteosVer, Permisos.CategoriasVer, Permisos.AreasVer, Permisos.UbicacionesVer,
    ];

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
        // Administrador: todos los permisos del catálogo.
        seed.AddRange(codigoAId.Values.Select(permisoId => new RolPermiso { RolId = RolAdministradorId, PermisoId = permisoId }));
        seed.AddRange(PermisosOperador.Select(codigo => new RolPermiso { RolId = RolOperadorId, PermisoId = codigoAId[codigo] }));
        seed.AddRange(PermisosConsulta.Select(codigo => new RolPermiso { RolId = RolConsultaId, PermisoId = codigoAId[codigo] }));

        builder.HasData(seed);
    }
}
