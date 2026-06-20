using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Trading.Application.Microstructure;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Recorder.Feed;
using Trading.Recorder.Seeding;

namespace Trading.Recorder.Tests.Seeding
{
    /// <summary>
    /// Tests del gap-fill de arranque (<see cref="StartupSeeder"/>): siembra desde Vision el
    /// hueco que falta y deja el cursor REST en el último aggId sembrado (puente con el vivo).
    /// Usa downloader y cursor store fakes (sin red); store real en directorio temporal.
    /// </summary>
    public class StartupSeederTests : IDisposable
    {
        private static readonly DateOnly Today = new(2026, 6, 20);
        private static readonly InstrumentId Btc = new("BTCUSDT");

        private readonly string _tempDir;
        private readonly PersistentMicrostructureStore _store;
        private readonly Dictionary<string, PersistentMicrostructureStore> _storeByTimeframe;

        public StartupSeederTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"startupseeder_test_{Guid.NewGuid():N}");
            _store   = new PersistentMicrostructureStore(_tempDir, "1h");
            _storeByTimeframe = new Dictionary<string, PersistentMicrostructureStore> { ["1h"] = _store };
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        [Fact]
        public async Task Store_vacio_siembra_la_ventana_y_puentea_el_cursor()
        {
            var cursor = new FakeCursorStore();
            // seedDays=2, today=20 → siembra 18 y 19. aggIds: día 18 → 1,2 ; día 19 → 3,4.
            var seeder = MakeSeeder(new Dictionary<DateOnly, IEnumerable<(long, decimal, decimal, long, bool)>>
            {
                [new(2026, 6, 18)] = new[] { (1L, 100m, 1m, Ms(2026, 6, 18, 10), false), (2L, 101m, 1m, Ms(2026, 6, 18, 11), true) },
                [new(2026, 6, 19)] = new[] { (3L, 102m, 1m, Ms(2026, 6, 19, 10), false), (4L, 103m, 1m, Ms(2026, 6, 19, 11), true) },
            });
            var startup = new StartupSeeder(seeder, cursor);

            await startup.SeedGapsAsync(Streams(), _storeByTimeframe, Today, seedDays: 2, CancellationToken.None);

            _store.GetLastBar(Btc).Should().NotBeNull();
            _store.GetLastBar(Btc)!.BarUtc.Should().Be(new DateTime(2026, 6, 19, 11, 0, 0, DateTimeKind.Utc));
            cursor.Load("BTCUSDT").Should().Be(4); // puente al último aggId sembrado
        }

        [Fact]
        public async Task Store_al_dia_no_siembra_ni_toca_el_cursor()
        {
            // Última barra = ayer (19). fromDate = 20 >= today=20 → no siembra.
            _store.Append(Bar(new DateTime(2026, 6, 19, 23, 0, 0, DateTimeKind.Utc), cvd: 42.0));
            var cursor = new FakeCursorStore();
            var seeder = MakeSeeder(new Dictionary<DateOnly, IEnumerable<(long, decimal, decimal, long, bool)>>()); // sin URLs

            var startup = new StartupSeeder(seeder, cursor);

            await startup.SeedGapsAsync(Streams(), _storeByTimeframe, Today, seedDays: 7, CancellationToken.None);

            cursor.Load("BTCUSDT").Should().BeNull();             // cursor intacto → resume por el vivo
            _store.GetLastBar(Btc)!.BarUtc.Should().Be(new DateTime(2026, 6, 19, 23, 0, 0, DateTimeKind.Utc));
        }

        [Fact]
        public async Task Store_con_hueco_siembra_desde_el_dia_siguiente_a_la_ultima_barra()
        {
            // Última barra = 17. Debe sembrar desde 18 (no re-sembrar el 17) hasta 19.
            _store.Append(Bar(new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc), cvd: 10.0));
            var cursor = new FakeCursorStore();
            var seeder = MakeSeeder(new Dictionary<DateOnly, IEnumerable<(long, decimal, decimal, long, bool)>>
            {
                [new(2026, 6, 18)] = new[] { (7L, 100m, 1m, Ms(2026, 6, 18, 10), false) },
                [new(2026, 6, 19)] = new[] { (8L, 101m, 1m, Ms(2026, 6, 19, 10), true) },
            });
            var startup = new StartupSeeder(seeder, cursor);

            await startup.SeedGapsAsync(Streams(), _storeByTimeframe, Today, seedDays: 7, CancellationToken.None);

            cursor.Load("BTCUSDT").Should().Be(8);
            _store.GetLastBar(Btc)!.BarUtc.Should().Be(new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc));
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static IReadOnlyList<(string, string)> Streams() => new[] { ("BTCUSDT", "1h") };

        private static long Ms(int y, int mo, int d, int h) =>
            (long)(new DateTime(y, mo, d, h, 0, 0, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalMilliseconds;

        private static MicrostructureBar Bar(DateTime barUtc, double cvd) =>
            new(Btc, barUtc, ofi: 0, cvdDelta: 0, cvd: cvd, arrivalRate: 1, meanTradeSize: 1, buySellRatio: 1, priceReturn: 0);

        private static BinanceVisionSeeder MakeSeeder(
            Dictionary<DateOnly, IEnumerable<(long aggId, decimal price, decimal qty, long ts, bool maker)>> byDate)
        {
            return new BinanceVisionSeeder(url =>
            {
                foreach (var (date, rows) in byDate)
                {
                    string expected = $"https://data.binance.vision/data/futures/um/daily/aggTrades/BTCUSDT/BTCUSDT-aggTrades-{date:yyyy-MM-dd}.zip";
                    if (url == expected)
                        return Task.FromResult(BuildZip(rows));
                }
                throw new FileNotFoundException($"URL no registrada: {url}");
            });
        }

        private static Stream BuildZip(IEnumerable<(long aggId, decimal price, decimal qty, long ts, bool maker)> rows)
        {
            var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("BTCUSDT-aggTrades.csv");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.WriteLine("agg_trade_id,price,quantity,first_trade_id,last_trade_id,transact_time,is_buyer_maker");
                foreach (var (aggId, price, qty, ts, maker) in rows)
                {
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{0},{0},{3},{4}", aggId, price, qty, ts, maker ? "True" : "False"));
                }
            }
            memory.Position = 0;
            return memory;
        }

        private sealed class FakeCursorStore : IAggTradeCursorStore
        {
            private readonly Dictionary<string, long> _map = new(StringComparer.OrdinalIgnoreCase);
            public long? Load(string symbol) => _map.TryGetValue(symbol, out var value) ? value : null;
            public void Save(string symbol, long lastAggregateTradeId) => _map[symbol] = lastAggregateTradeId;
        }
    }
}
