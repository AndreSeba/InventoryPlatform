using System.Reflection;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Seed;

// Datos iniciales del módulo Conteo Externo (los mismos que trae por defecto la app de escritorio de Fagal):
// unidades con sus decimales, los almacenes de fábrica con su nombre y el catálogo «Base Datos» (414 códigos).
// Solo para Bolivia (CodigoIso "BO"), y cada bloque SOLO si ese país todavía no tiene nada de ese tipo:
// nunca pisa ni duplica lo que el usuario ya cargó o editó. Se llama una vez al arrancar la API (Program.cs).
public static class ConteoExternoSemilla
{
    private const string PaisCodigoIso = "BO";

    // Decimales permitidos por unidad (el Excel de SAP ya trae 2 decimales en CAJ/PACK y 3 en KG).
    private static readonly (string Codigo, int Decimales)[] Unidades = [("CAJ", 2), ("PACK", 2), ("KG", 3)];

    // Código de SAP → nombre que se ve en pantalla. Los códigos de la Tienda NESTLÉ (103020, 103025) no son de Fagal.
    private static readonly (string Codigo, string Nombre)[] Almacenes =
    [
        ("103001", "Alm. Prod. Calidad - Lotes"),
        ("103002", "Alm.- Sucursal 12 - SC - Lotes"),
        ("103003", "Alm. EXPORTACION P.T. - Lotes"),
        ("103004", "Alm. Prod. Terminado - Lotes"),
        ("103014", "Alm. Devoluciones Supermercado"),
        ("103015", "Alm. Auxiliar Bajas PT - Lotes"),
        ("103023", "Alm. Supermercados SCZ - Lotes"),
        ("103024", "Alm. Salvage SCZ - Lotes"),
    ];

    public static async Task SembrarAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ConteoExternoSemilla");

        var pais = await db.Paises.AsNoTracking().FirstOrDefaultAsync(p => p.CodigoIso == PaisCodigoIso && p.Activo);
        if (pais is null) return;

        if (!await db.ConteoExternoUnidades.AnyAsync(u => u.PaisId == pais.Id))
        {
            foreach (var (codigo, decimales) in Unidades)
                db.ConteoExternoUnidades.Add(new ConteoExternoUnidad { Codigo = codigo, DecimalesPermitidos = decimales, PaisId = pais.Id, Activo = true });
            logger.LogInformation("Conteo Externo: {N} unidades iniciales sembradas.", Unidades.Length);
        }

        if (!await db.ConteoExternoAlmacenes.AnyAsync(a => a.PaisId == pais.Id))
        {
            foreach (var (codigo, nombre) in Almacenes)
                db.ConteoExternoAlmacenes.Add(new ConteoExternoAlmacen { Codigo = codigo, Nombre = nombre, PaisId = pais.Id, Activo = true });
            logger.LogInformation("Conteo Externo: {N} almacenes iniciales sembrados.", Almacenes.Length);
        }

        if (!await db.ConteoExternoProductos.AnyAsync(p => p.PaisId == pais.Id))
        {
            var productos = LeerCatalogoSemilla(pais.Id);
            db.ConteoExternoProductos.AddRange(productos);
            logger.LogInformation("Conteo Externo: {N} productos iniciales sembrados.", productos.Count);
        }

        await db.SaveChangesAsync();
    }

    // CSV embebido: «codigo;nombre;familia» con encabezado. Los códigos repetidos (mismo CodigoNormalizado) se
    // ignoran: la hoja original trae 421 filas y 414 códigos únicos.
    private static List<ConteoExternoProducto> LeerCatalogoSemilla(int paisId)
    {
        var assembly = typeof(ConteoExternoSemilla).Assembly;
        var recurso = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("ConteoExternoProductosSemilla.csv", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Falta el recurso embebido ConteoExternoProductosSemilla.csv.");

        using var stream = assembly.GetManifestResourceStream(recurso)!;
        using var lector = new StreamReader(stream, System.Text.Encoding.UTF8);

        var vistos = new HashSet<string>();
        var productos = new List<ConteoExternoProducto>();
        var primera = true;
        string? linea;
        while ((linea = lector.ReadLine()) is not null)
        {
            if (primera) { primera = false; continue; }          // encabezado
            var partes = linea.Split(';');
            if (partes.Length < 3) continue;

            var codigo = partes[0].Trim();
            var nombre = partes[1].Trim();
            var familia = partes[2].Trim().ToUpperInvariant();
            if (codigo.Length == 0 || nombre.Length == 0) continue;

            var normalizado = CodigoConteoExterno.Normalizar(codigo);
            if (!vistos.Add(normalizado)) continue;

            productos.Add(new ConteoExternoProducto
            {
                Codigo = codigo[..Math.Min(codigo.Length, 30)],
                CodigoNormalizado = normalizado,
                Nombre = nombre[..Math.Min(nombre.Length, 200)],
                Familia = familia[..Math.Min(familia.Length, 100)],
                Origen = OrigenConteoExternoProducto.BaseDatos,
                PaisId = paisId,
                Activo = true,
            });
        }
        return productos;
    }
}
