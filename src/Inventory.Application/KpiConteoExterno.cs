namespace Inventory.Application;

// Motor de KPIs del módulo de conteo externo. Siempre sobre TODAS las líneas de una
// sesión, nunca sobre un filtro de pantalla — lo llama quien pide los KPIs
// (ConteoExternoSesionService), nunca una página con un subconjunto filtrado.
public record KpiConteoExternoLinea(string ProductoCodigo, decimal? Diferencia, decimal? DiferenciaValor, decimal ValorExistencia);

public record KpiConteoExterno(
    int LotesInventariados, int LotesCorrectos, decimal? IraLotes,
    int CodigosInventariados, int CodigosCorrectos, decimal? IraCodigos,
    decimal ValorInventario, decimal DiferenciaNetaBob, decimal DiferenciaAbsolutaBob,
    decimal? ExactitudValor, decimal? ExactitudValorAbsoluta,
    int SinContar
)
{
    public bool Completo => SinContar == 0;
}

public static class KpiConteoExternoCalculador
{
    // Las líneas sin contar (Diferencia null) no entran a ningún KPI — no se tratan
    // como 0, se informan aparte en SinContar. "Código correcto" = la suma de la
    // diferencia en Bs de TODOS los lotes/almacenes de ese código da exactamente 0
    // (una faltante en un lote puede compensarse con una sobrante en otro).
    public static KpiConteoExterno Calcular(IEnumerable<KpiConteoExternoLinea> lineas)
    {
        var todas = lineas.ToList();
        var contadas = todas.Where(l => l.Diferencia is not null).ToList();

        var lotesCorrectos = contadas.Count(l => l.Diferencia == 0m);

        var porCodigo = contadas
            .GroupBy(l => l.ProductoCodigo)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.DiferenciaValor ?? 0m));
        var codigosOk = porCodigo.Values.Count(v => v == 0m);

        var valor = contadas.Sum(l => l.ValorExistencia);
        var neta = contadas.Sum(l => l.DiferenciaValor ?? 0m);
        var absoluta = contadas.Sum(l => Math.Abs(l.DiferenciaValor ?? 0m));

        return new KpiConteoExterno(
            LotesInventariados: contadas.Count,
            LotesCorrectos: lotesCorrectos,
            IraLotes: Ira(contadas.Count, lotesCorrectos),
            CodigosInventariados: porCodigo.Count,
            CodigosCorrectos: codigosOk,
            IraCodigos: Ira(porCodigo.Count, codigosOk),
            ValorInventario: valor,
            DiferenciaNetaBob: neta,
            DiferenciaAbsolutaBob: absoluta,
            ExactitudValor: valor == 0m ? null : 1m - Math.Abs(neta) / valor,
            ExactitudValorAbsoluta: valor == 0m ? null : 1m - absoluta / valor,
            SinContar: todas.Count - contadas.Count
        );
    }

    private static decimal? Ira(int total, int correctos)
        => total == 0 ? null : 1m - (decimal)(total - correctos) / total;
}
