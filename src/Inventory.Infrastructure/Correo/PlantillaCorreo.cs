using System.Net;

namespace Inventory.Infrastructure.Correo;

// Cuerpo del correo: HTML simple con estilos en línea (los clientes de correo ignoran las hojas de estilo) y una
// versión en texto plano para los que no muestran HTML.
public static class PlantillaCorreo
{
    public static string Html(string titulo, string mensaje, string? enlace, string paraNombre, string? destinatarioReal)
    {
        string E(string t) => WebUtility.HtmlEncode(t);
        var boton = enlace is null ? "" :
            $"<p style=\"margin:24px 0 8px\"><a href=\"{E(enlace)}\" style=\"background:#1d4ed8;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:6px;font-weight:600;display:inline-block\">Abrir en el sistema</a></p>";
        var prueba = destinatarioReal is null ? "" :
            $"<p style=\"margin:16px 0 0;padding:8px 12px;background:#fef3c7;color:#92400e;border-radius:6px;font-size:12px\">Correo de prueba: en producción le llegaría a {E(paraNombre)} ({E(destinatarioReal)}).</p>";

        return $@"<div style=""font-family:Arial,Helvetica,sans-serif;background:#f4f5f7;padding:24px"">
  <div style=""max-width:520px;margin:0 auto;background:#ffffff;border:1px solid #e2e5ea;border-radius:8px;padding:24px"">
    <p style=""margin:0 0 4px;font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:#6b7280"">Inventario de Material Promocional</p>
    <h2 style=""margin:0 0 12px;font-size:18px;color:#111827"">{E(titulo)}</h2>
    <p style=""margin:0 0 8px;font-size:14px;color:#374151"">Hola {E(paraNombre)},</p>
    <p style=""margin:0;font-size:14px;line-height:1.5;color:#374151"">{E(mensaje)}</p>
    {boton}
    {prueba}
    <p style=""margin:20px 0 0;font-size:11px;color:#9ca3af"">Aviso automático del sistema de inventario. No respondas a este correo.</p>
  </div>
</div>";
    }

    public static string Texto(string titulo, string mensaje, string? enlace, string paraNombre) =>
        $"Hola {paraNombre},\n\n{titulo}\n{mensaje}\n" + (enlace is null ? "" : $"\nAbrir en el sistema: {enlace}\n") +
        "\nAviso automático del sistema de inventario. No respondas a este correo.\n";
}
