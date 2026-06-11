using Trading.Analytics.Metrics;
using Trading.Analytics.MonteCarlo;
using Trading.Analytics.Parsing;
using Trading.Analytics.Reports;
using Trading.Analytics.Trades;
using Trading.Analytics.Validation;

// ── CLI ──────────────────────────────────────────────────────────────────────
string isLogPath = "", oosLogPath = "";
string strategyName = "Strategy";
string outputDir = Directory.GetCurrentDirectory();

for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--is-log":   isLogPath    = args[++i]; break;
        case "--oos-log":  oosLogPath   = args[++i]; break;
        case "--strategy": strategyName = args[++i]; break;
        case "--output":   outputDir    = args[++i]; break;
    }
}

if (!File.Exists(isLogPath))
{
    Console.Error.WriteLine($"[ERROR] IS log no encontrado: {isLogPath}");
    Console.Error.WriteLine("Uso: Trading.Analytics --is-log <path> --oos-log <path> [--strategy <name>] [--output <dir>]");
    return 1;
}
if (!File.Exists(oosLogPath))
{
    Console.Error.WriteLine($"[ERROR] OOS log no encontrado: {oosLogPath}");
    return 1;
}

// ── Parse ────────────────────────────────────────────────────────────────────
Console.WriteLine($"=== Trading.Analytics — {strategyName} ===");
Console.WriteLine("Leyendo transaction logs...");

var isFills  = LeanTransactionLogParser.Parse(isLogPath);
var oosFills = LeanTransactionLogParser.Parse(oosLogPath);

var isTrades  = TradePairer.Pair(isFills);
var oosTrades = TradePairer.Pair(oosFills);

Console.WriteLine($"IS:  {isTrades.Count} trades completados");
Console.WriteLine($"OOS: {oosTrades.Count} trades completados");

if (oosTrades.Count == 0)
{
    Console.Error.WriteLine("[ERROR] El OOS no tiene trades. Verificar fechas del backtest y datos disponibles.");
    return 1;
}

// ── Métricas ─────────────────────────────────────────────────────────────────
Console.WriteLine("Calculando métricas...");
var isMetrics  = MetricsCalculator.Compute(isTrades);
var oosMetrics = MetricsCalculator.Compute(oosTrades);

// ── Monte Carlo ───────────────────────────────────────────────────────────────
Console.WriteLine("Corriendo Monte Carlo (10,000 simulaciones)...");
var mcResult = MonteCarloEngine.Run(oosTrades);

// ── Gates ─────────────────────────────────────────────────────────────────────
var gate1 = ValidationGates.EvaluateGate1(oosMetrics);
var gate2 = ValidationGates.EvaluateGate2(mcResult);

// ── Reporte ───────────────────────────────────────────────────────────────────
string Period(List<TradeFill> fills) => fills.Count > 0
    ? $"{fills.Min(f => f.Timestamp):yyyy-MM-dd} → {fills.Max(f => f.Timestamp):yyyy-MM-dd}"
    : "Desconocido";

var report = ValidationReportWriter.Generate(
    strategyName,
    isPeriod:   Period(isFills),
    oosPeriod:  Period(oosFills),
    isMetrics,
    oosMetrics,
    gate1,
    mcResult,
    gate2);

Console.WriteLine();
Console.WriteLine(report);

Directory.CreateDirectory(outputDir);
var reportName = $"validation-{strategyName.ToLower().Replace(" ", "-")}-{DateTime.UtcNow:yyyyMMdd}.md";
var reportPath = Path.Combine(outputDir, reportName);
File.WriteAllText(reportPath, report);
Console.WriteLine($"Reporte guardado: {reportPath}");

return (gate1.Passed && gate2.Passed) ? 0 : 1;
