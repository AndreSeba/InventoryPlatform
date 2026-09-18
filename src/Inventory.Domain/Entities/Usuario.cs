namespace Inventory.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // BCrypt

    public int RolId { get; set; }
    public Rol? Rol { get; set; }

    // A qué país pertenece esta cuenta — asignado automático desde el claim de la
    // sesión al crear (ver UsuarioService), nunca elegido a mano. Un usuario pertenece
    // a un solo país: el mismo email puede existir como cuentas distintas en países
    // distintos (Email es único por país, no global — ver UsuarioConfiguration).
    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoLoginEn { get; set; }
}
