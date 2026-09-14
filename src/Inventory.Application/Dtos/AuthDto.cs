namespace Inventory.Application.Dtos;

public record LoginDto(string Email, string Password);

public record LoginResultDto(string Token, DateTime ExpiraEn, UsuarioDto Usuario);

public record UsuarioDto(
    int Id, string Email, string NombreCompleto, int RolId, string RolNombre,
    bool Activo, DateTime CreadoEn, DateTime? UltimoLoginEn, IReadOnlyList<string> Permisos
);

public record CrearUsuarioDto(string Email, string NombreCompleto, string Password, int RolId);

public record ActualizarUsuarioDto(string NombreCompleto, int RolId, bool Activo);

public record PermisoDto(string Codigo, string Modulo, string Descripcion);

public record RolDto(int Id, string Nombre, string? Descripcion, bool Activo, IReadOnlyList<string> PermisoCodigos);

public record CrearRolDto(string Nombre, string? Descripcion, IReadOnlyList<string> PermisoCodigos);

public record ActualizarRolDto(string Nombre, string? Descripcion, bool Activo, IReadOnlyList<string> PermisoCodigos);
