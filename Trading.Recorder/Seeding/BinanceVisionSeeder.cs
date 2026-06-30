using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Trading.Application.Microstructure;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Recorder.Seeding
{
    /// <summary>
    /// Siembra barras históricas de microestructura para un símbolo descargando
    /// los archivos diarios de aggTrades desde https://data.binance.vision.
    ///
    /// El repositorio público de Binance provee archivos ZIP con aggTrades día a día
    /// (hasta T-1) con paridad exacta al stream WebSocket en vivo. No tiene rate limits
    /// ni riesgo de ban de IP — es un depósito estático S3/CDN.
    ///
    /// URL de un archivo: https://data.binance.vision/data/futures/um/daily/aggTrades/{SYMBOL}/{SYMBOL}-aggTrades-{YYYY-MM-DD}.zip
    ///
    /// Formato CSV dentro del ZIP:
    ///   agg_trade_id,price,quantity,first_trade_id,last_trade_id,transact_time,is_buyer_maker
    ///   0,50000.10,0.001,0,0,1609459200000,False
    ///
    /// Uso típico (alta de un activo nuevo):
    ///   var seeder = new BinanceVisionSeeder(httpClient);
    ///   await seeder.SeedAsync(
    ///       instrumentId: new InstrumentId("BTCUSDT"),
    ///       timeframe: "1h",
    ///       store: store,
    ///       fromDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
    ///       toDate: DateOnly.FromDateTime(DateTime.UtcNow),   // exclusivo (T-1 es el último disponible)
    ///       cvdSeed: 0.0);
    /// </summary>
    public sealed class BinanceVisionSeeder
    {
        private readonly Func<string, Task<Stream>> _downloader;
        private const string BaseUrl = "https://data.binance.vision/data/futures/um/daily/aggTrades";

        public BinanceVisionSeeder(HttpClient httpClient)
            : this(async url =>
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStreamAsync();
            })
        { }

        // Constructor para tests: inyecta el downloader sin HttpClient real.
        public BinanceVisionSeeder(Func<string, Task<Stream>> downloader)
        {
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        }

        /// <summary>
        /// Descarga aggTrades para cada día en [fromDate, toDate) y computa las barras
        /// de microestructura del timeframe indicado. Las barras se persisten al store.
        /// </summary>
        /// <param name="instrumentId">InstrumentId del símbolo (p.ej. BTCUSDT).</param>
        /// <param name="timeframe">Timeframe destino (p.ej. "1h", "5m").</param>
        /// <param name="store">Store donde persitir las barras computadas.</param>
        /// <param name="fromDate">Primer día a descargar (inclusive).</param>
        /// <param name="toDate">Límite (exclusivo); típicamente DateOnly.FromDateTime(DateTime.UtcNow).</param>
        /// <param name="cvdSeed">CVD acumulado al inicio del período.</param>
        /// <param name="skipBefore">Si se indica, ignora trades cuya ventana de timeframe sea
        /// &lt;= este timestamp. Usado cuando fromDate coincide con el día de la última barra del
        /// store (re-seed parcial de día): evita duplicar barras ya persistidas sin perder el
        /// tramo final del día que quedó con gap tras un reinicio.</param>
        /// <param name="ct">Token de cancelación.</param>
        /// <returns>Lista de barras computadas y persistidas (en orden cronológico).</returns>
        public async Task<SeedResult> SeedAsync(
            InstrumentId instrumentId,
            string timeframe,
            PersistentMicrostructureStore store,
            DateOnly fromDate,
            DateOnly toDate,
            double cvdSeed = 0.0,
            DateTime? skipBefore = null,
            CancellationToken ct = default)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));
            if (store is null)        throw new ArgumentNullException(nameof(store));

            var windowSize = RecorderConfig.ParseTimeframe(timeframe);
            var aggregators = new Dictionary<DateTime, AggTradeBucket>();
            var result = new List<MicrostructureBar>();
            double cvdRunning = cvdSeed;
            long? lastAggregateTradeId = null;

            for (var date = fromDate; date < toDate; date = date.AddDays(1))
            {
                ct.ThrowIfCancellationRequested();

                string symbol  = instrumentId.Ticker.ToUpperInvariant();
                string dateStr = date.ToString("yyyy-MM-dd");
                string url     = $"{BaseUrl}/{symbol}/{symbol}-aggTrades-{dateStr}.zip";

                Console.WriteLine($"[Seeder] Descargando {symbol} {dateStr}...");

                IReadOnlyList<AggTradeRow>? rows;
                try
                {
                    using var zipStream = await _downloader(url);
                    rows = ParseZip(zipStream);
                }
                catch (Exception ex)
                {
                    // Día no disponible (demasiado reciente, activo sin historial, etc.) — skip.
                    Console.Error.WriteLine($"[Seeder] {symbol} {dateStr}: no disponible ({ex.Message})");
                    continue;
                }

                // Acumular en buckets por ventana de timeframe
                foreach (var row in rows)
                {
                    var tradeUtc    = DateTimeOffset.FromUnixTimeMilliseconds(row.TransactTime).UtcDateTime;
                    var windowStart = FloorToWindow(tradeUtc, windowSize);

                    // El aggId se rastrea siempre (incluso en trades saltados) para que el
                    // cursor bridge al feed REST quede en el último aggId real del ZIP.
                    if (lastAggregateTradeId is null || row.AggregateTradeId > lastAggregateTradeId)
                        lastAggregateTradeId = row.AggregateTradeId;

                    // Saltar trades de ventanas ya presentes en el store (re-seed parcial).
                    if (skipBefore.HasValue && windowStart <= skipBefore.Value)
                        continue;

                    if (!aggregators.TryGetValue(windowStart, out var bucket))
                    {
                        bucket = new AggTradeBucket();
                        aggregators[windowStart] = bucket;
                    }
                    bucket.Add(row.Price, row.Quantity, row.IsBuyerMaker);
                }
            }

            // Computar barras en orden cronológico y persistir
            var sortedWindows = new List<DateTime>(aggregators.Keys);
            sortedWindows.Sort();

            foreach (var windowStart in sortedWindows)
            {
                var bucket = aggregators[windowStart];
                if (!bucket.HasData) continue;

                var (bar, newCvd) = MicrostructureFeatureComputer.Compute(
                    instrumentId, windowStart, bucket, cvdRunning);
                cvdRunning = newCvd;

                store.Append(bar);
                result.Add(bar);
            }

            Console.WriteLine(
                $"[Seeder] {instrumentId.Ticker}/{timeframe}: {result.Count} barras sembradas " +
                $"({fromDate} → {toDate.AddDays(-1)}). Último aggId={lastAggregateTradeId?.ToString() ?? "—"}.");

            return new SeedResult(result, lastAggregateTradeId);
        }

        // ── privado ──────────────────────────────────────────────────────────

        private static DateTime FloorToWindow(DateTime utc, TimeSpan windowSize)
        {
            long ticks  = utc.Ticks;
            long wTicks = windowSize.Ticks;
            return new DateTime((ticks / wTicks) * wTicks, DateTimeKind.Utc);
        }

        /// <summary>
        /// Parsea el ZIP descargado de data.binance.vision.
        /// El ZIP contiene exactamente un CSV con el formato documentado arriba.
        /// </summary>
        private static IReadOnlyList<AggTradeRow> ParseZip(Stream zipStream)
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);

            if (archive.Entries.Count == 0)
                throw new InvalidDataException("ZIP sin entradas.");

            // El ZIP contiene un solo CSV (p.ej. BTCUSDT-aggTrades-2024-01-01.csv)
            var entry = archive.Entries[0];
            using var csvStream = entry.Open();
            using var reader    = new StreamReader(csvStream, Encoding.UTF8);

            var rows = new List<AggTradeRow>();
            bool firstLine = true;

            while (reader.ReadLine() is { } line)
            {
                // Primera línea puede ser header o datos; si comienza con dígito, es dato.
                if (firstLine)
                {
                    firstLine = false;
                    if (!char.IsDigit(line[0])) continue; // saltar header si lo hay
                }

                var row = ParseCsvLine(line);
                if (row.HasValue)
                    rows.Add(row.Value);
            }

            return rows;
        }

        private static AggTradeRow? ParseCsvLine(string line)
        {
            try
            {
                // agg_trade_id,price,quantity,first_trade_id,last_trade_id,transact_time,is_buyer_maker
                var parts = line.Split(',');
                if (parts.Length < 7) return null;

                long aggregateTradeId = long.Parse(parts[0], CultureInfo.InvariantCulture);
                decimal price    = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
                decimal quantity = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
                long transactMs  = long.Parse(parts[5], CultureInfo.InvariantCulture);
                // "True" / "False" (Python-style) o "true" / "false"
                bool isBuyerMaker = string.Equals(parts[6].Trim(), "True", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(parts[6].Trim(), "true", StringComparison.OrdinalIgnoreCase);

                return new AggTradeRow(aggregateTradeId, price, quantity, transactMs, isBuyerMaker);
            }
            catch
            {
                return null;
            }
        }

        private readonly record struct AggTradeRow(
            long AggregateTradeId,
            decimal Price,
            decimal Quantity,
            long TransactTime,
            bool IsBuyerMaker);
    }

    /// <summary>
    /// Resultado de una siembra: las barras computadas y persistidas, y el mayor
    /// <c>aggId</c> visto en los archivos de Vision (null si no se sembró nada). Ese aggId es
    /// el puente con el feed REST en vivo: el cursor <c>fromId</c> arranca justo después.
    /// </summary>
    public readonly record struct SeedResult(
        IReadOnlyList<MicrostructureBar> Bars,
        long? LastAggregateTradeId);
}
