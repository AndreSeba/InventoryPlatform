namespace Inventory.Infrastructure.Correo;

// Sección «Correo» de appsettings. Apagado por defecto. La CLAVE nunca va en appsettings.json: se guarda con
// `dotnet user-secrets set "Correo:Clave" "..."` o en la variable de entorno Correo__Clave.
public class CorreoOptions
{
    public const string SectionName = "Correo";

    public bool Habilitado { get; set; }

    public string Host { get; set; } = "smtp.gmail.com";
    public int Puerto { get; set; } = 587;
    // Seguridad de la conexión: "StartTls" (puerto 587, lo normal en Gmail/Outlook), "Ssl" (TLS directo, puerto 465) o
    // "Ninguna" (solo para un servidor de pruebas local).
    public string Seguridad { get; set; } = "StartTls";

    public string Usuario { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;

    // Si está vacío se usa Usuario. En Gmail el remitente real siempre termina siendo la cuenta autenticada.
    public string RemitenteEmail { get; set; } = string.Empty;
    public string RemitenteNombre { get; set; } = "Inventario Marketing";

    // PRUEBAS: si tiene valor, TODOS los correos se mandan a esta dirección (con el destinatario real indicado en
    // el asunto) en vez de a cada persona. Sirve para probar con un correo personal sin escribirle a nadie más.
    public string DestinatarioDePrueba { get; set; } = string.Empty;

    // Dirección de la web, para el botón «Abrir en el sistema» del correo (ej. https://localhost:7126).
    public string UrlBaseWeb { get; set; } = "https://localhost:7126";

    // Cada cuántos segundos se revisan las novedades para avisar aunque nadie tenga el sistema abierto.
    public int SincronizarCadaSegundos { get; set; } = 45;
}
