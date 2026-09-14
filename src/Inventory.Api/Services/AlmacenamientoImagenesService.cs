using Inventory.Application.Exceptions;

namespace Inventory.Api.Services;

// Guarda las imágenes subidas desde el gestor de archivos del navegador en disco local
// (carpeta "uploads/" en la raíz del proyecto, servida como estática desde Program.cs)
// y devuelve la ruta relativa para guardar en Producto.ImagenUrl. Sin storage en la nube
// a propósito — es un solo proceso en dev/piloto, no hace falta más que esto todavía.
public class AlmacenamientoImagenesService
{
    private static readonly HashSet<string> ExtensionesPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private const long TamanoMaximoBytes = 5 * 1024 * 1024; // 5 MB

    private readonly string _carpetaProductos;

    public AlmacenamientoImagenesService(IWebHostEnvironment env)
    {
        _carpetaProductos = Path.Combine(env.ContentRootPath, "uploads", "productos");
    }

    public async Task<string> GuardarImagenProductoAsync(IFormFile archivo, CancellationToken ct)
    {
        if (archivo.Length == 0)
            throw new ArchivoInvalidoException("El archivo está vacío.");
        if (archivo.Length > TamanoMaximoBytes)
            throw new ArchivoInvalidoException("La imagen no puede superar los 5 MB.");

        var extension = Path.GetExtension(archivo.FileName);
        if (!ExtensionesPermitidas.Contains(extension))
            throw new ArchivoInvalidoException("Formato no permitido. Usá JPG, PNG o WEBP.");

        Directory.CreateDirectory(_carpetaProductos);

        var nombreArchivo = $"{Guid.NewGuid()}{extension}";
        var rutaFisica = Path.Combine(_carpetaProductos, nombreArchivo);

        await using (var stream = new FileStream(rutaFisica, FileMode.Create))
            await archivo.CopyToAsync(stream, ct);

        return $"/uploads/productos/{nombreArchivo}";
    }
}
