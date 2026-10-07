namespace Inventory.Domain.Entities;

// La firma manuscrita (dibujada con el mouse o el dedo) de una persona. Vive en su propia tabla, no en Usuario, por la
// misma razón que ProductoImagen: casi toda consulta carga un Usuario por su nombre y no debe arrastrar la imagen.
// Se guardan VERSIONES: al registrar una firma nueva la anterior queda inactiva (no se borra), porque una solicitud ya
// aprobada apunta a la versión con la que se firmó y un formulario reimpreso tiene que seguir viéndose igual.
public class FirmaUsuario
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public byte[] Datos { get; set; } = [];   // PNG con fondo transparente
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Una sola activa por persona (índice único filtrado). Las demás son historia.
    public bool Activa { get; set; } = true;
}
