using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trading.Application.Microstructure;
using Trading.Domain.ValueObjects;
using Trading.Recorder;
using Trading.Recorder.Seeding;

// ── Configuración ────────────────────────────────────────────────────────────
string strategiesJsonPath = Environment.GetEnvironmentVariable("RECORDER_STRATEGIES_JSON")
    ?? Path.Combine(AppContext.BaseDirectory, "strategies.json");

string storageDir = Environment.GetEnvironmentVariable("RECORDER_STORAGE_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "microstructure-live");

int retentionDays = int.TryParse(Environment.GetEnvironmentVariable("RECORDER_RETENTION_DAYS"), out int rd)
    ? rd
    : 7;

string wsBaseUrl = Environment.GetEnvironmentVariable("RECORDER_WS_URL")
    ?? "wss://fstream.binance.com";

Console.WriteLine($"[Recorder] strategies.json : {strategiesJsonPath}");
Console.WriteLine($"[Recorder] storageDir      : {storageDir}");
Console.WriteLine($"[Recorder] retentionDays   : {retentionDays}");

var config = RecorderConfig.FromStrategiesJson(strategiesJsonPath, storageDir, retentionDays);

// ── Stores por timeframe (uno por timeframe único) ───────────────────────────
var storeByTimeframe = config.Streams
    .Select(s => s.Timeframe)
    .Distinct()
    .ToDictionary(tf => tf, tf => new PersistentMicrostructureStore(storageDir, tf));

// ── Trim inicial (rolling window) ────────────────────────────────────────────
var trimCutoff = DateTime.UtcNow.AddDays(-retentionDays);
foreach (var (symbol, timeframe) in config.Streams)
{
    storeByTimeframe[timeframe].TrimOlderThan(new InstrumentId(symbol), trimCutoff);
}
Console.WriteLine($"[Recorder] Trim inicial: barras anteriores a {trimCutoff:yyyy-MM-dd HH:mm} UTC eliminadas.");

// ── Agregadores por (símbolo, timeframe) ─────────────────────────────────────
// Clave: symbol (mayúsculas).
var aggregatorsBySymbol = new Dictionary<string, List<TimeframeAggregator>>(StringComparer.OrdinalIgnoreCase);

foreach (var (symbol, timeframe) in config.Streams)
{
    var instrumentId = new InstrumentId(symbol);
    var store        = storeByTimeframe[timeframe];

    // Semilla de CVD: último Cvd persistido en disco para este (símbolo, timeframe).
    var recentBars = store.LoadRecent(instrumentId, hours: 24);
    double cvdSeed = recentBars.Count > 0 ? recentBars[recentBars.Count - 1].Cvd : 0.0;

    var aggregator = new TimeframeAggregator(instrumentId, timeframe, cvdSeed);

    aggregator.BarClosed += (id, barUtc, bar) =>
    {
        store.Append(bar);
        Console.WriteLine(
            $"[Recorder] {id.Ticker}/{timeframe} {barUtc:yyyy-MM-dd HH:mm} UTC | " +
            $"OFI={bar.Ofi:F4} CVD∆={bar.CvdDelta:F0} MTS={bar.MeanTradeSize:F4}");
    };

    if (!aggregatorsBySymbol.TryGetValue(symbol, out var list))
    {
        list = new List<TimeframeAggregator>();
        aggregatorsBySymbol[symbol] = list;
    }
    list.Add(aggregator);

    Console.WriteLine(
        $"[Recorder] Agregador {symbol}/{timeframe} — CVD seed={cvdSeed:F4} ({recentBars.Count} barras recientes)");
}

// ── Stream names para el WebSocket ───────────────────────────────────────────
// Binance exige nombres en minúsculas: "btcusdt@aggTrade"
var streamNames = aggregatorsBySymbol.Keys
    .Select(sym => $"{sym.ToLowerInvariant()}@aggTrade")
    .ToList();

Console.WriteLine($"[Recorder] Streams: {string.Join(", ", streamNames)}");

// ── Apagado limpio ───────────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("[Recorder] Ctrl+C recibido — apagando...");
    cts.Cancel();
};

// ── WebSocket ────────────────────────────────────────────────────────────────
var wsClient = new BinanceAggTradeWebSocketClient(
    streamNames,
    onTrade: (symbol, price, qty, isBuyerMaker, tradeTimeMs) =>
    {
        if (!aggregatorsBySymbol.TryGetValue(symbol, out var aggregators)) return;
        foreach (var agg in aggregators)
            agg.OnTrade(price, qty, isBuyerMaker, tradeTimeMs);
    },
    baseUrl: wsBaseUrl);

Console.WriteLine("[Recorder] Iniciando WebSocket...");
await wsClient.RunAsync(cts.Token);
Console.WriteLine("[Recorder] Apagado completo.");
