namespace Inventory.Application.Dtos;

// La firma propia (pantalla «Mi perfil»). PngBase64 es null si todavía no registró ninguna.
public record FirmaPropiaDto(bool Tiene, string? PngBase64, DateTime? FechaRegistro);

// Registrar o reemplazar la firma. La contraseña se pide a propósito: con la sesión abierta en una PC compartida nadie
// debería poder cambiar la firma de otra persona.
public record GuardarFirmaDto(string PngBase64, string Password);

// Firmas del formulario de una solicitud: quien la pidió y quien la autorizó (solo si está aprobada). Cargo = rol.
public record FirmasSolicitudDto(
    string? SolicitantePngBase64, string? SolicitanteCargo,
    string? AutorizaPngBase64, string? AutorizaCargo
);
