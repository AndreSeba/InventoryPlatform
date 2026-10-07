namespace Inventory.Infrastructure.Controles;

// Interruptores de módulos ya construidos que todavía no se habilitaron (sección «Funciones» de appsettings).
public class FuncionesOptions
{
    public const string SectionName = "Funciones";

    // Revisión periódica de accesos: apagada = no genera avisos en la campanita (el frontend la muestra como
    // «Planificada»). Encenderla cuando se confirme su puesta en marcha.
    public bool RevisionAccesos { get; set; }
}
