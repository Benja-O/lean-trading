using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Trading.Application.Microstructure;
using Trading.Domain.ValueObjects;
using Trading.Recorder.Seeding;

namespace Trading.Recorder.Tests
{
    /// <summary>
    /// Tests de BinanceVisionSeeder usando un downloader inyectado (sin HTTP real).
    /// Verifica paridad con MicrostructureFeatureComputer y correcta acumulación de CVD.
    /// </summary>
    public class BinanceVisionSeederTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly PersistentMicrostructureStore _store;
        private static readonly InstrumentId Btc = new("BTCUSDT");

        public BinanceVisionSeederTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"seeder_test_{Guid.NewGuid():N}");
            _store   = new PersistentMicrostructureStore(_tempDir, "1h");
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Construye un ZIP en memoria con un CSV de aggTrades en el formato de data.binance.vision.
        /// </summary>
        private static Stream BuildZip(IEnumerable<(decimal price, decimal qty, long transactMs, bool isBuyerMaker)> rows)
        {
            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("BTCUSDT-aggTrades-2026-06-17.csv");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                // Header
                writer.WriteLine("agg_trade_id,price,quantity,first_trade_id,last_trade_id,transact_time,is_buyer_maker");
                long id = 1;
                foreach (var (price, qty, ts, maker) in rows)
                {
                    // Usar InvariantCulture para evitar que decimales con coma (locale español)
                    // rompan el parsing CSV del seeder.
                    string line = string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6}",
                        id, price, qty, id, id, ts, maker ? "True" : "False");
                    writer.WriteLine(line);
                    id++;
                }
            }
            ms.Position = 0;
            return ms;
        }

        private static long ToMs(DateTime utc) =>
            (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;

        private static BinanceVisionSeeder MakeSeeder(Dictionary<string, Stream> responses)
        {
            return new BinanceVisionSeeder(url =>
            {
                if (responses.TryGetValue(url, out var stream))
                    return Task.FromResult(stream);
                throw new FileNotFoundException($"URL no registrada en el stub: {url}");
            });
        }

        // ── tests ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task SeedAsync_un_dia_computa_barras_1h_correctamente()
        {
            // Un día con 2 trades en la misma hora 10:00-11:00
            // buy_vol=2.0, sell_vol=0.5, ofi=(1.5)/2.5=0.6, cvd_delta=1.5
            var hour = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);
            var date = DateOnly.FromDateTime(hour);

            string url = $"https://data.binance.vision/data/futures/um/daily/aggTrades/BTCUSDT/BTCUSDT-aggTrades-2026-06-17.zip";
            var zip = BuildZip(new[]
            {
                (price: 100m, qty: 2.0m, transactMs: ToMs(hour.AddMinutes(10)), isBuyerMaker: false), // buy
                (price: 101m, qty: 0.5m, transactMs: ToMs(hour.AddMinutes(30)), isBuyerMaker: true),  // sell
            });

            var seeder = MakeSeeder(new Dictionary<string, Stream> { [url] = zip });
            var (bars, lastAggregateTradeId) = await seeder.SeedAsync(Btc, "1h", _store, date, date.AddDays(1), cvdSeed: 0.0);

            bars.Should().HaveCount(1);
            var bar = bars[0];
            bar.BarUtc.Should().Be(hour);
            bar.CvdDelta.Should().BeApproximately(1.5, 1e-9);
            bar.Cvd.Should().BeApproximately(1.5, 1e-9);
            bar.ArrivalRate.Should().BeApproximately(2, 1e-9);
            bar.MeanTradeSize.Should().BeApproximately(2.5 / 2.0, 1e-9);
            lastAggregateTradeId.Should().Be(2); // 2 trades en el zip, aggId 1 y 2
        }

        [Fact]
        public async Task SeedAsync_cvd_seed_se_propaga_entre_barras()
        {
            // seed=500. Barra: cvd_delta=1.0 → Cvd=501.0
            var hour = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);
            var date = DateOnly.FromDateTime(hour);
            string url = $"https://data.binance.vision/data/futures/um/daily/aggTrades/BTCUSDT/BTCUSDT-aggTrades-2026-06-17.zip";
            var zip = BuildZip(new[] { (100m, 1.0m, ToMs(hour), false) });

            var seeder = MakeSeeder(new Dictionary<string, Stream> { [url] = zip });
            var (bars, _) = await seeder.SeedAsync(Btc, "1h", _store, date, date.AddDays(1), cvdSeed: 500.0);

            bars[0].Cvd.Should().BeApproximately(501.0, 1e-9);
        }

        [Fact]
        public async Task SeedAsync_dia_no_disponible_se_omite_sin_excepcion()
        {
            // La URL del día 17 no existe en el stub → se omite
            var date = new DateOnly(2026, 6, 17);
            var seeder = MakeSeeder(new Dictionary<string, Stream>());

            var (bars, lastAggregateTradeId) = await seeder.SeedAsync(Btc, "1h", _store, date, date.AddDays(1));

            bars.Should().BeEmpty();
            lastAggregateTradeId.Should().BeNull();
        }

        [Fact]
        public async Task SeedAsync_persiste_barras_en_el_store()
        {
            var date = new DateOnly(2026, 6, 17);
            string url = $"https://data.binance.vision/data/futures/um/daily/aggTrades/BTCUSDT/BTCUSDT-aggTrades-2026-06-17.zip";
            var zip = BuildZip(new[]
            {
                (100m, 1.0m, ToMs(new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc)), false),
            });

            var seeder = MakeSeeder(new Dictionary<string, Stream> { [url] = zip });
            await seeder.SeedAsync(Btc, "1h", _store, date, date.AddDays(1));

            // Verificar que el store tiene la barra persitida
            var stored = _store.LoadRecent(Btc, hours: 9999);
            stored.Should().HaveCount(1);
        }

        [Fact]
        public async Task SeedAsync_5m_agrupa_trades_en_ventanas_correctas()
        {
            var date = new DateOnly(2026, 6, 17);
            string url = $"https://data.binance.vision/data/futures/um/daily/aggTrades/BTCUSDT/BTCUSDT-aggTrades-2026-06-17.zip";

            var t0 = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);
            var zip = BuildZip(new[]
            {
                (100m, 1.0m, ToMs(t0.AddMinutes(1)), false),   // ventana 10:00
                (101m, 1.0m, ToMs(t0.AddMinutes(6)), true),    // ventana 10:05
            });

            var store5m = new PersistentMicrostructureStore(_tempDir, "5m");
            var seeder  = MakeSeeder(new Dictionary<string, Stream> { [url] = zip });
            var (bars, _) = await seeder.SeedAsync(Btc, "5m", store5m, date, date.AddDays(1));

            bars.Should().HaveCount(2);
            bars[0].BarUtc.Should().Be(t0);
            bars[1].BarUtc.Should().Be(t0.AddMinutes(5));
        }
    }
}
