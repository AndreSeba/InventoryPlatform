namespace Inventory.Domain.Entities;

public class Pais
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // ISO 3166-1 alpha-2 ("BO", "PE") — inmutable tras crear (ver PaisService):
    // queda embebido en el claim "pais" del JWT vigente al momento de loguearse, y es
    // la base del cálculo de la bandera (par de "regional indicator symbols" Unicode).
    public string CodigoIso { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}
