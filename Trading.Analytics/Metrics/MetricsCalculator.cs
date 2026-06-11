using Trading.Analytics.Trades;

namespace Trading.Analytics.Metrics;

public static class MetricsCalculator
{
    private const decimal StartEquityDefault = 100_000m;

    public static MetricsSummary Compute(
        List<CompletedTrade> trades,
        decimal startEquity = StartEquityDefault)
    {
        if (trades.Count == 0) return MetricsSummary.Empty with { StartEquity = startEquity };

        // ── Equity curve (acumulación por día de cierre) ────────────────────
        var equityCurve = BuildEquityCurve(trades, startEquity);
        var dailyReturns = BuildDailyReturns(equityCurve);

        var finalEquity = equityCurve[^1].Equity;
        var netProfitFraction = (finalEquity - startEquity) / startEquity;

        var periodYears = Math.Max(
            (equityCurve[^1].Date - equityCurve[0].Date).TotalDays / 365.25, 0.01);
        var cagr = (decimal)(Math.Pow((double)(finalEquity / startEquity), 1.0 / periodYears) - 1);

        var maxDrawdown = ComputeMaxDrawdown(equityCurve);
        var calmar = maxDrawdown > 0 ? cagr / maxDrawdown : 0m;

        // ── Trade-level stats ───────────────────────────────────────────────
        var winners = trades.Where(t => t.PnlUsdt > 0).ToList();
        var losers = trades.Where(t => t.PnlUsdt < 0).ToList();

        var winRate = (decimal)winners.Count / trades.Count;
        var grossProfit = winners.Sum(t => t.PnlUsdt);
        var grossLoss = Math.Abs(losers.Sum(t => t.PnlUsdt));
        var profitFactor = grossLoss > 0 ? grossProfit / grossLoss : 0m;

        var avgWin = winners.Count > 0 ? winners.Average(t => t.ReturnFraction) : 0m;
        var avgLoss = losers.Count > 0 ? losers.Average(t => t.ReturnFraction) : 0m;
        var expectancy = winRate * avgWin + (1m - winRate) * avgLoss;

        var recoveryFactor = maxDrawdown > 0 ? netProfitFraction / maxDrawdown : 0m;

        var sharpe = (decimal)ComputeSharpe(dailyReturns);
        var sortino = (decimal)ComputeSortino(dailyReturns);

        return new MetricsSummary(
            TradeCount: trades.Count,
            WinRate: winRate,
            ProfitFactor: profitFactor,
            Expectancy: expectancy,
            AverageWin: avgWin,
            AverageLoss: avgLoss,
            Sharpe: sharpe,
            Sortino: sortino,
            Calmar: calmar,
            MaxDrawdown: maxDrawdown,
            Cagr: cagr,
            NetProfit: netProfitFraction,
            RecoveryFactor: recoveryFactor,
            StartEquity: startEquity,
            EndEquity: finalEquity);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    internal static List<(DateTime Date, decimal Equity)> BuildEquityCurve(
        List<CompletedTrade> trades, decimal startEquity)
    {
        // Agrupa P&L por día de cierre y construye curva diaria con carry-forward.
        var pnlByDay = trades
            .GroupBy(t => t.ExitTime.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.PnlUsdt));

        var result = new List<(DateTime Date, decimal Equity)>();
        var equity = startEquity;
        var current = pnlByDay.Keys.Min();
        var last = pnlByDay.Keys.Max();

        while (current <= last)
        {
            if (pnlByDay.TryGetValue(current, out var dailyPnl))
                equity += dailyPnl;
            result.Add((current, equity));
            current = current.AddDays(1);
        }

        return result;
    }

    private static List<double> BuildDailyReturns(List<(DateTime Date, decimal Equity)> curve)
    {
        var returns = new List<double>(curve.Count);
        for (int i = 1; i < curve.Count; i++)
        {
            if (curve[i - 1].Equity > 0)
                returns.Add((double)((curve[i].Equity - curve[i - 1].Equity) / curve[i - 1].Equity));
        }
        return returns;
    }

    private static decimal ComputeMaxDrawdown(List<(DateTime Date, decimal Equity)> curve)
    {
        var peak = curve[0].Equity;
        var maxDD = 0m;
        foreach (var (_, equity) in curve)
        {
            if (equity > peak) peak = equity;
            if (peak > 0)
            {
                var dd = (peak - equity) / peak;
                if (dd > maxDD) maxDD = dd;
            }
        }
        return maxDD;
    }

    internal static double ComputeSharpe(List<double> dailyReturns)
    {
        if (dailyReturns.Count < 2) return 0;
        var mean = dailyReturns.Average();
        var variance = dailyReturns.Sum(r => Math.Pow(r - mean, 2)) / (dailyReturns.Count - 1);
        var std = Math.Sqrt(variance);
        return std > 0 ? mean / std * Math.Sqrt(252) : 0;
    }

    private static double ComputeSortino(List<double> dailyReturns)
    {
        if (dailyReturns.Count < 2) return 0;
        var mean = dailyReturns.Average();
        // Downside deviation usa la media global como MAR=0 (convención estándar)
        var downsideVariance = dailyReturns.Sum(r => Math.Pow(Math.Min(r, 0), 2)) / dailyReturns.Count;
        var downsideStd = Math.Sqrt(downsideVariance);
        return downsideStd > 0 ? mean / downsideStd * Math.Sqrt(252) : 0;
    }
}
