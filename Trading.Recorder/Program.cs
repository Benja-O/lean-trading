using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Trading.Application.Infrastructure;
using Trading.Application.Microstructure;
using Trading.Domain.ValueObjects;
using Trading.Recorder;
using Trading.Recorder.Adapters;
using Trading.Recorder.Feed;
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

// Por defecto se hace bypass del proxy de sistema del VPS (ver SystemWebSocketAdapter).
// Poner en "true"/"1" solo si el VPS exige el proxy para alcanzar la red externa.
string? wsProxyRaw = Environment.GetEnvironmentVariable("RECORDER_WS_USE_SYSTEM_PROXY");
bool useSystemProxy = string.Equals(wsProxyRaw, "true", StringComparison.OrdinalIgnoreCase)
                   || wsProxyRaw == "1";

// Transporte del feed: "rest" (default — el WS de fstream no entrega push a las redes del
// proyecto, ver ADR-049) o "ws" (válido en redes donde fstream sí entrega).
string feedMode = (Environment.GetEnvironmentVariable("RECORDER_FEED") ?? "rest")
    .Trim().ToLowerInvariant();

string restBaseUrl = Environment.GetEnvironmentVariable("RECORDER_REST_URL")
    ?? "https://fapi.binance.com";

int restPollSeconds = int.TryParse(Environment.GetEnvironmentVariable("RECORDER_REST_POLL_SECONDS"), out int rps) && rps > 0
    ? rps
    : 10;

string? healthchecksUrl = Environment.GetEnvironmentVariable("RECORDER_HEALTHCHECKS_URL");

Console.WriteLine($"[Recorder] strategies.json : {strategiesJsonPath}");
Console.WriteLine($"[Recorder] storageDir      : {storageDir}");
Console.WriteLine($"[Recorder] retentionDays   : {retentionDays}");
Console.WriteLine($"[Recorder] feed            : {feedMode}");

var config = RecorderConfig.FromStrategiesJson(strategiesJsonPath, storageDir, retentionDays);

// ── Healthchecks.io (dead-man's switch) ──────────────────────────────────────
var logger  = new ConsoleLogger();
var clock   = new SystemClock();
var pinger  = new HealthchecksIoPinger(healthchecksUrl, new HttpClient(), clock, logger);

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
var aggregatorsBySymbol = new Dictionary<string, List<TimeframeAggregator>>(StringComparer.OrdinalIgnoreCase);

using var cts = new CancellationTokenSource();

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

        // Dead-man's switch: ping por cada barra cerrada (throttle interno 5 min).
        _ = pinger.PingAsync(cts.Token);
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

// ── Símbolos a grabar ─────────────────────────────────────────────────────────
var symbols = aggregatorsBySymbol.Keys.ToList();
Console.WriteLine($"[Recorder] Simbolos: {string.Join(", ", symbols)}");

// Despacho de cada trade a los agregadores del símbolo. Firma estable (TradeHandler),
// idéntica para el adapter WS y el REST.
TradeHandler onTrade = (symbol, price, qty, isBuyerMaker, tradeTimeMs) =>
{
    if (!aggregatorsBySymbol.TryGetValue(symbol, out var aggregators)) return;
    foreach (var agg in aggregators)
        agg.OnTrade(price, qty, isBuyerMaker, tradeTimeMs);
};

// ── Apagado limpio ───────────────────────────────────────────────────────────
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("[Recorder] Ctrl+C recibido — apagando...");
    cts.Cancel();
};

// ── Feed (REST por default; WS opcional) ──────────────────────────────────────
ITradeFeed feed;
if (feedMode == "ws")
{
    var streamNames = symbols.Select(sym => $"{sym.ToLowerInvariant()}@aggTrade").ToList();
    feed = new BinanceAggTradeWebSocketClient(
        streamNames,
        onTrade,
        baseUrl: wsBaseUrl,
        wsFactory: () => new SystemWebSocketAdapter(useSystemProxy));
    Console.WriteLine($"[Recorder] Feed WebSocket — base={wsBaseUrl}, proxy={(useSystemProxy ? "system" : "bypass")}");
}
else
{
    var aggTradesApi = new BinanceFuturesAggTradesApi(new HttpClient(), restBaseUrl);
    var cursorStore  = new FileAggTradeCursorStore(storageDir);
    feed = new BinanceAggTradeRestFeed(
        symbols,
        onTrade,
        aggTradesApi,
        cursorStore,
        pollInterval: TimeSpan.FromSeconds(restPollSeconds));
    Console.WriteLine($"[Recorder] Feed REST polling — base={restBaseUrl}, cada {restPollSeconds}s");
}

Console.WriteLine("[Recorder] Iniciando feed...");
await feed.RunAsync(cts.Token);
Console.WriteLine("[Recorder] Apagado completo.");
