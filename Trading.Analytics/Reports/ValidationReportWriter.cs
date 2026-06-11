using System.Text;
using Trading.Analytics.Metrics;
using Trading.Analytics.MonteCarlo;
using Trading.Analytics.Validation;

namespace Trading.Analytics.Reports;

public static class ValidationReportWriter
{
    public static string Generate(
        string strategyName,
        string isPeriod,
        string oosPeriod,
        MetricsSummary isMetrics,
        MetricsSummary oosMetrics,
        Gate1Result gate1,
        MonteCarloResult mc,
        Gate2Result gate2)
    {
        var sb = new StringBuilder();
        var overallPass = gate1.Passed && gate2.Passed;

        sb.AppendLine($"# Validation Report — {strategyName}");
        sb.AppendLine($"Generado: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        sb.AppendLine("## Resultado final");
        sb.AppendLine($"**{(overallPass ? "✅ APROBADA" : "❌ RECHAZADA")}**");
        sb.AppendLine();
        sb.AppendLine($"| | Estado |");
        sb.AppendLine($"|---|---|");
        sb.AppendLine($"| Gate 1 — métricas OOS | {(gate1.Passed ? "✅ PASS" : "❌ FAIL")} |");
        sb.AppendLine($"| Gate 2 — Monte Carlo  | {(gate2.Passed ? "✅ PASS" : "❌ FAIL")} |");
        sb.AppendLine();

        sb.AppendLine("## Comparativa IS vs OOS");
        sb.AppendLine($"- IS: {isPeriod}  ({isMetrics.TradeCount} trades)");
        sb.AppendLine($"- OOS: {oosPeriod}  ({oosMetrics.TradeCount} trades)");
        sb.AppendLine();
        sb.AppendLine("| Métrica | IS | OOS | Δ |");
        sb.AppendLine("|---|---|---|---|");
        AppendRow(sb, "Sharpe", isMetrics.Sharpe, oosMetrics.Sharpe, fmt: "F3", higherIsBetter: true);
        AppendRow(sb, "Sortino", isMetrics.Sortino, oosMetrics.Sortino, fmt: "F3", higherIsBetter: true);
        AppendRow(sb, "Calmar", isMetrics.Calmar, oosMetrics.Calmar, fmt: "F3", higherIsBetter: true);
        AppendRow(sb, "Profit Factor", isMetrics.ProfitFactor, oosMetrics.ProfitFactor, fmt: "F3", higherIsBetter: true);
        AppendRow(sb, "Expectancy", isMetrics.Expectancy, oosMetrics.Expectancy, fmt: "P3", higherIsBetter: true);
        AppendRow(sb, "Win Rate", isMetrics.WinRate, oosMetrics.WinRate, fmt: "P1", higherIsBetter: true);
        AppendRow(sb, "Max DD", isMetrics.MaxDrawdown, oosMetrics.MaxDrawdown, fmt: "P1", higherIsBetter: false);
        AppendRow(sb, "CAGR", isMetrics.Cagr, oosMetrics.Cagr, fmt: "P2", higherIsBetter: true);
        AppendRow(sb, "Net Profit", isMetrics.NetProfit, oosMetrics.NetProfit, fmt: "P2", higherIsBetter: true);
        AppendRow(sb, "Recovery Factor", isMetrics.RecoveryFactor, oosMetrics.RecoveryFactor, fmt: "F2", higherIsBetter: true);
        sb.AppendLine();

        sb.AppendLine("## Gate 1 — Criterios OOS");
        foreach (var (name, pass, detail) in gate1.Checks)
            sb.AppendLine($"- {(pass ? "✅" : "❌")} **{name}**: {detail}");
        sb.AppendLine();

        sb.AppendLine("## Gate 2 — Monte Carlo");
        sb.AppendLine($"_{mc.Simulations:N0} simulaciones, block-bootstrap tamaño 5_");
        sb.AppendLine();
        sb.AppendLine("| Métrica | P5 | P50 | P95 |");
        sb.AppendLine("|---|---|---|---|");
        sb.AppendLine($"| Sharpe | {mc.SharpeP5:F3} | {mc.SharpeP50:F3} | {mc.SharpeP95:F3} |");
        sb.AppendLine($"| Max DD | — | {mc.MaxDDP50:P1} | {mc.MaxDDP95:P1} |");
        sb.AppendLine($"| CAGR | {mc.CagrP5:P1} | {mc.CagrP50:P1} | {mc.CagrP95:P1} |");
        sb.AppendLine();
        sb.AppendLine($"- P(Sharpe < 0): **{mc.ProbabilityNegativeSharpe:P1}**");
        sb.AppendLine($"- P(CAGR < 0): **{mc.ProbabilityNegativeCagr:P1}**");
        sb.AppendLine();
        foreach (var (name, pass, detail) in gate2.Checks)
            sb.AppendLine($"- {(pass ? "✅" : "❌")} **{name}**: {detail}");

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string name,
        decimal isVal, decimal oosVal, string fmt, bool higherIsBetter)
    {
        var delta = isVal != 0 ? (oosVal - isVal) / Math.Abs(isVal) : 0m;
        var sign = higherIsBetter ? delta >= -0.5m : delta <= 0.5m;
        var arrow = sign ? "✅" : "⚠️";
        sb.AppendLine(
            $"| {name} | {isVal.ToString(fmt)} | {oosVal.ToString(fmt)} | {arrow} {delta:P0} |");
    }
}
