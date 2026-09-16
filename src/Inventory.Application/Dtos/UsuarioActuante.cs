namespace Inventory.Application.Dtos;

// Quién está ejecutando la operación. Reemplaza al `string usuarioId` que recibían los
// servicios y que en realidad nunca fue un id: era el NombreCompleto del usuario, sin
// ninguna garantía de que correspondiera a un usuario real (el fallback literal era
// "sistema"). Ahora viaja el id, que es lo que se guarda como FK, y el nombre, que se
// guarda como snapshot del momento — ver la sección "Quién hizo cada cosa" en CLAUDE.md.
public record UsuarioActuante(int Id, string Nombre);
