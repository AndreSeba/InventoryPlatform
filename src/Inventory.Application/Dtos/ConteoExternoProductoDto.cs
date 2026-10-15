using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record ConteoExternoProductoDto(
    int Id, string Codigo, string Nombre, string Familia, OrigenConteoExternoProducto Origen,
    string? UnidadCodigo, decimal? CostoReferencia, int PaisId, string PaisNombre, bool Activo
);

// Sin UnidadCodigo ni CostoReferencia a propósito: la unidad la fija el primer lote de
// existencia (Fase 2) y el costo se completa ahí o al importar. Pedirlos acá produciría
// un producto con una unidad "adivinada" que después puede no coincidir con lo real.
public record CrearConteoExternoProductoDto(string Codigo, string Nombre, string Familia);

// Familia/Nombre editables; el código NO (cambiarlo movería existencias/costos, caso que
// esta fase no necesita). CostoReferencia sí se puede ajustar a mano en cualquier momento.
public record ActualizarConteoExternoProductoDto(string Nombre, string Familia, decimal? CostoReferencia, bool Activo);
