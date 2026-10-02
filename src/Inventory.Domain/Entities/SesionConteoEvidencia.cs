namespace Inventory.Domain.Entities;

// Foto/PDF/Excel del documento físico del conteo. Tabla aparte (igual que ProductoImagen)
// para que cargar un conteo o listarlos no arrastre los bytes de los archivos.
public class SesionConteoEvidencia
{
    public int Id { get; set; }

    public int SesionConteoId { get; set; }
    public SesionConteo? SesionConteo { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public byte[] Datos { get; set; } = [];

    public int SubidoPorId { get; set; }
    public Usuario? SubidoPor { get; set; }
    public string SubidoPorNombre { get; set; } = string.Empty;
    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
}
