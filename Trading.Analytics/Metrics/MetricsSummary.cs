namespace Trading.Analytics.Metrics;

public sealed record MetricsSummary(
    int TradeCount,
    decimal WinRate,
    decimal ProfitFactor,
    decimal Expectancy,
    decimal AverageWin,
    decimal AverageLoss,
    decimal Sharpe,
    decimal Sortino,
    decimal Calmar,
    decimal MaxDrawdown,
    decimal Cagr,
    decimal NetProfit,
    decimal RecoveryFactor,
    decimal StartEquity,
    decimal EndEquity)
{
    public static readonly MetricsSummary Empty =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
