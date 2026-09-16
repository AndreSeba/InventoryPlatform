using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Producto");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ClaveProducto).HasMaxLength(120).IsRequired();
        builder.Property(p => p.CodigoProducto).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(255).IsRequired();
        builder.Property(p => p.UnidadMedida).HasMaxLength(10).IsRequired();
        builder.Property(p => p.CostoUnitario).HasColumnType("decimal(18,2)");
        builder.Property(p => p.StockMinimo).HasColumnType("decimal(18,3)");
        builder.Property(p => p.Detalle).HasMaxLength(2000);
        // Sin HasColumnType acá a propósito: EF ya mapea byte[] al tipo binario correcto
        // por proveedor (varbinary(max) en SQL Server, BLOB en SQLite) sin que haga falta
        // forzarlo — forzar "varbinary(max)" rompía los tests, que corren contra SQLite
        // en memoria y no entienden esa sintaxis (SQLite Error 1: 'near "max"').
        builder.Property(p => p.ImagenContentType).HasMaxLength(100);

        builder.HasOne(p => p.Categoria)
            .WithMany()
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Único solo entre productos activos (mismo criterio que en el resto del
        // proyecto): permite reutilizar la clave tras desactivar el producto anterior.
        builder.HasIndex(p => p.ClaveProducto).IsUnique().HasFilter("[Activo] = 1");

        // Sin CHECK de unidades: las unidades validas las define el catalogo Unidad
        // (tabla editable desde /unidades), y ProductoService valida contra el antes
        // de crear o actualizar. Un CHECK fijo obligaria a migrar la base cada vez
        // que se agrega una unidad nueva, que es justo lo que este modulo evita.
        builder.ToTable(t => t.HasCheckConstraint("CK_Producto_StockMin", "[StockMinimo] >= 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Producto_Costo", "[CostoUnitario] IS NULL OR [CostoUnitario] >= 0"));
    }
}
