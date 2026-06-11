using Trading.Analytics.Metrics;
using Trading.Analytics.Trades;

namespace Trading.Analytics.MonteCarlo;

/// <summary>
/// Block bootstrap sobre los trades del OOS.
/// Supuesto: los trades están ordenados por ExitTime. Se agrupan en bloques
/// consecutivos para preservar la autocorrelación de corto plazo.
/// Nota: posiciones simultáneas (BTC/ETH/SOL abiertas a la vez) se tratan
/// como trades secuenciales — simplificación aceptable para distribuciones
/// de drawdown y Sharpe relativo. El efecto es conservador (subestima
/// diversificación temporal).
/// </summary>
public static class MonteCarloEngine
{
    private const int DefaultSimulations = 10_000;
    private const int DefaultBlockSize = 5;
    private const decimal StartEquity = 100_000m;

    public static MonteCarloResult Run(
        List<CompletedTrade> trades,
        int simulations = DefaultSimulations,
        int blockSize = DefaultBlockSize,
        int seed = 42)
    {
        if (trades.Count < blockSize * 2)
            return MonteCarloResult.Insufficient;

        var random = new Random(seed);
        var pnlArray = trades.Select(t => t.PnlUsdt).ToArray();

        // Construir bloques solapados (ventana deslizante)
        var blocks = new List<decimal[]>();
        for (int i = 0; i <= pnlArray.Length - blockSize; i++)
        {
            var block = new decimal[blockSize];
            Array.Copy(pnlArray, i, block, 0, blockSize);
            blocks.Add(block);
        }

        var sharpeValues = new double[simulations];
        var maxDDValues = new double[simulations];
        var cagrValues = new double[simulations];

        // Años del período OOS para anualizar CAGR
        var periodYears = Math.Max(
            (trades[^1].ExitTime - trades[0].EntryTime).TotalDays / 365.25, 0.1);

        for (int sim = 0; sim < simulations; sim++)
        {
            // Resamplear bloques con reemplazo hasta cubrir N trades
            var simulatedPnl = new List<decimal>(pnlArray.Length + blockSize);
            while (simulatedPnl.Count < pnlArray.Length)
                simulatedPnl.AddRange(blocks[random.Next(blocks.Count)]);

            // Equity curve indexada por trade
            var equity = StartEquity;
            var peak = equity;
            var maxDD = 0.0;
            var dailyReturns = new double[pnlArray.Length];

            for (int i = 0; i < pnlArray.Length; i++)
            {
                var prev = equity;
                equity += simulatedPnl[i];
                if (equity > peak) peak = equity;
                if (peak > 0) maxDD = Math.Max(maxDD, (double)((peak - equity) / peak));
                dailyReturns[i] = prev > 0 ? (double)((equity - prev) / prev) : 0;
            }

            sharpeValues[sim] = ComputeSharpeFromReturns(dailyReturns, periodYears, pnlArray.Length);
            maxDDValues[sim] = maxDD;
            cagrValues[sim] = equity > 0
                ? Math.Pow((double)(equity / StartEquity), 1.0 / periodYears) - 1
                : -1.0;
        }

        Array.Sort(sharpeValues);
        Array.Sort(maxDDValues);
        Array.Sort(cagrValues);

        int idx(double pct) => Math.Clamp((int)(simulations * pct), 0, simulations - 1);

        return new MonteCarloResult(
            Simulations: simulations,
            SharpeP5: sharpeValues[idx(0.05)],
            SharpeP50: sharpeValues[idx(0.50)],
            SharpeP95: sharpeValues[idx(0.95)],
            MaxDDP50: maxDDValues[idx(0.50)],
            MaxDDP95: maxDDValues[idx(0.95)],
            CagrP5: cagrValues[idx(0.05)],
            CagrP50: cagrValues[idx(0.50)],
            CagrP95: cagrValues[idx(0.95)],
            ProbabilityNegativeSharpe: (double)sharpeValues.Count(s => s < 0) / simulations,
            ProbabilityNegativeCagr: (double)cagrValues.Count(c => c < 0) / simulations);
    }

    /// <summary>
    /// Sharpe anualizado desde retornos por-trade.
    /// Se escala por sqrt(tradesPerYear) en lugar de sqrt(252) ya que la
    /// unidad de observación es el trade, no el día calendario.
    /// </summary>
    private static double ComputeSharpeFromReturns(
        double[] returns, double periodYears, int tradeCount)
    {
        if (returns.Length < 2) return 0;
        var mean = returns.Average();
        var variance = returns.Sum(r => Math.Pow(r - mean, 2)) / (returns.Length - 1);
        var std = Math.Sqrt(variance);
        if (std <= 0) return 0;
        var tradesPerYear = tradeCount / periodYears;
        return mean / std * Math.Sqrt(tradesPerYear);
    }
}
