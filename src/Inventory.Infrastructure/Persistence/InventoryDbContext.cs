using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Unidad> Unidades => Set<Unidad>();
    public DbSet<Pais> Paises => Set<Pais>();
    public DbSet<Almacen> Almacenes => Set<Almacen>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Ubicacion> Ubicaciones => Set<Ubicacion>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoImagen> ProductoImagenes => Set<ProductoImagen>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<SolicitudDetalle> SolicitudDetalles => Set<SolicitudDetalle>();
    public DbSet<SesionConteo> SesionesConteo => Set<SesionConteo>();
    public DbSet<SesionConteoLinea> SesionConteoLineas => Set<SesionConteoLinea>();
    public DbSet<SesionConteoEvidencia> SesionConteoEvidencias => Set<SesionConteoEvidencia>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<FirmaUsuario> Firmas => Set<FirmaUsuario>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<AvisoDevolucion> AvisosDevolucion => Set<AvisoDevolucion>();
    public DbSet<RevisionAcceso> RevisionesAcceso => Set<RevisionAcceso>();
    public DbSet<RevisionAccesoLinea> RevisionAccesoLineas => Set<RevisionAccesoLinea>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }
}
