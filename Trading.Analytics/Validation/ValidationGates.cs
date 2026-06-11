using Trading.Analytics.Metrics;
using Trading.Analytics.MonteCarlo;

namespace Trading.Analytics.Validation;

/// <summary>
/// Criterios de aprobación IS/OOS para Hito G.
///
/// Gate 1 — métricas del período OOS (todos deben pasar):
///   Sharpe ≥ 0.3       Degradación ~40% respecto al IS=0.503 es aceptable
///   Profit factor ≥ 1.1
///   Expectancy > 0
///   Trades ≥ 50        Mínimo estadístico
///   Net profit > 0
///
/// Gate 2 — distribución Monte Carlo (todos deben pasar):
///   P(Sharpe < 0) ≤ 20%
///   Mediana Max DD ≤ 55%
///   CAGR P5 > -5%
/// </summary>
public static class ValidationGates
{
    private const int MinTradeCount = 50;
    private const double MinSharpOos = 0.3;
    private const double MinProfitFactor = 1.1;
    private const double MaxProbNegativeSharpe = 0.20;
    private const double MaxMedianMaxDD = 0.55;
    private const double MinCagrP5 = -0.05;

    public static Gate1Result EvaluateGate1(MetricsSummary oos)
    {
        var checks = new List<(string Name, bool Pass, string Detail)>
        {
            ("Trades ≥ 50",
                oos.TradeCount >= MinTradeCount,
                $"{oos.TradeCount}"),

            ("Net profit > 0",
                oos.NetProfit > 0,
                $"{oos.NetProfit:P2}"),

            ("Sharpe ≥ 0.30",
                (double)oos.Sharpe >= MinSharpOos,
                $"{oos.Sharpe:F3}"),

            ("Profit factor ≥ 1.10",
                (double)oos.ProfitFactor >= MinProfitFactor,
                $"{oos.ProfitFactor:F3}"),

            ("Expectancy > 0",
                oos.Expectancy > 0,
                $"{oos.Expectancy:P3}"),
        };

        return new Gate1Result(checks.All(c => c.Pass), checks);
    }

    public static Gate2Result EvaluateGate2(MonteCarloResult mc)
    {
        if (mc.Simulations == 0)
        {
            return new Gate2Result(false,
                [("Monte Carlo", false, "Insuficientes trades para simular")]);
        }

        var checks = new List<(string Name, bool Pass, string Detail)>
        {
            ("P(Sharpe < 0) ≤ 20%",
                mc.ProbabilityNegativeSharpe <= MaxProbNegativeSharpe,
                $"{mc.ProbabilityNegativeSharpe:P1}"),

            ("Mediana Max DD ≤ 55%",
                mc.MaxDDP50 <= MaxMedianMaxDD,
                $"{mc.MaxDDP50:P1}"),

            ("CAGR P5 > -5%",
                mc.CagrP5 >= MinCagrP5,
                $"{mc.CagrP5:P1}"),
        };

        return new Gate2Result(checks.All(c => c.Pass), checks);
    }
}
