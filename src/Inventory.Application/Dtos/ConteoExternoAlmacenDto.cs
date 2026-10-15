namespace Inventory.Application.Dtos;

public record ConteoExternoAlmacenDto(int Id, string Codigo, string Nombre, int PaisId, string PaisNombre, bool Activo);

public record CrearConteoExternoAlmacenDto(string Codigo, string Nombre);

// El Código es inmutable, igual que en Unidad — ya es la clave que usan
// ConteoExternoExistencia/ConteoExternoLinea; este catálogo solo le pone un nombre
// legible, nunca reemplaza esa identidad.
public record ActualizarConteoExternoAlmacenDto(string Nombre, bool Activo);
