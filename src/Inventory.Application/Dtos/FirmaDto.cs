namespace Inventory.Application.Dtos;

// La firma propia (pantalla «Mi perfil»). PngBase64 es null si todavía no registró ninguna.
public record FirmaPropiaDto(bool Tiene, string? PngBase64, DateTime? FechaRegistro);

// Registrar o reemplazar la firma (PNG en base64). Cada quien guarda solo la suya: el usuario sale del token.
public record GuardarFirmaDto(string PngBase64);

// Firmas del formulario de una solicitud: quien la pidió y quien la autorizó (solo si está aprobada). Cargo = rol.
public record FirmasSolicitudDto(
    string? SolicitantePngBase64, string? SolicitanteCargo,
    string? AutorizaPngBase64, string? AutorizaCargo
);
