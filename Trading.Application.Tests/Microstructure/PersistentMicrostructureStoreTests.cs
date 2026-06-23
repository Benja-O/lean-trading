using System;
using System.IO;
using FluentAssertions;
using Trading.Application.Microstructure;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Microstructure
{
    public class PersistentMicrostructureStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly PersistentMicrostructureStore _store;
        private static readonly InstrumentId Btc = new("BTCUSDT");

        public PersistentMicrostructureStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"ms_store_test_{Guid.NewGuid():N}");
            _store   = new PersistentMicrostructureStore(_tempDir, "1h");
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        [Fact]
        public void Append_y_LoadRecent_roundtrip_conserva_todos_los_campos()
        {
            var bar = Bar(new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc),
                ofi: 0.234, cvdDelta: 1500.5, cvd: 98765.3, arrivalRate: 2341,
                meanTradeSize: 0.0423, buySellRatio: 1.023, priceReturn: 0.00123);

            _store.Append(bar);
            // Ventana amplia: el test verifica roundtrip de campos, no el filtrado temporal.
            // (Con hours fijo dependería del wall-clock — prohibido por AI.md.)
            var loaded = _store.LoadRecent(Btc, hours: 24 * 3650);

            loaded.Should().HaveCount(1);
            var r = loaded[0];
            r.InstrumentId.Should().Be(Btc);
            r.BarUtc.Should().Be(bar.BarUtc);
            r.Ofi.Should().BeApproximately(0.234, 1e-12);
            r.CvdDelta.Should().BeApproximately(1500.5, 1e-9);
            r.Cvd.Should().BeApproximately(98765.3, 1e-9);
            r.ArrivalRate.Should().BeApproximately(2341, 1e-9);
            r.MeanTradeSize.Should().BeApproximately(0.0423, 1e-12);
            r.BuySellRatio.Should().BeApproximately(1.023, 1e-12);
            r.PriceReturn.Should().BeApproximately(0.00123, 1e-14);
        }

        [Fact]
        public void LoadRecent_filtra_barras_fuera_de_la_ventana()
        {
            var old    = Bar(DateTime.UtcNow.AddHours(-100), ofi: 1);
            var recent = Bar(DateTime.UtcNow.AddHours(-2),   ofi: 2);

            _store.Append(old);
            _store.Append(recent);

            var loaded = _store.LoadRecent(Btc, hours: 72);

            loaded.Should().HaveCount(1);
            loaded[0].Ofi.Should().BeApproximately(2, 1e-12);
        }

        [Fact]
        public void GetLastBarUtc_retorna_timestamp_de_la_ultima_barra()
        {
            var t1 = new DateTime(2026, 6, 17, 8,  0, 0, DateTimeKind.Utc);
            var t2 = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);

            _store.Append(Bar(t1));
            _store.Append(Bar(t2));

            _store.GetLastBarUtc(Btc).Should().Be(t2);
        }

        [Fact]
        public void GetLastBarUtc_retorna_null_si_no_hay_archivo()
        {
            _store.GetLastBarUtc(Btc).Should().BeNull();
        }

        [Fact]
        public void TrimOlderThan_elimina_filas_antiguas_y_conserva_recientes()
        {
            var cutoff = new DateTime(2026, 6, 17, 0, 0, 0, DateTimeKind.Utc);
            _store.Append(Bar(cutoff.AddHours(-2)));  // vieja
            _store.Append(Bar(cutoff));               // en cutoff → conservar
            _store.Append(Bar(cutoff.AddHours(1)));   // nueva → conservar

            _store.TrimOlderThan(Btc, cutoff);

            var loaded = _store.LoadRecent(Btc, hours: 9999);
            loaded.Should().HaveCount(2);
            loaded[0].BarUtc.Should().Be(cutoff);
            loaded[1].BarUtc.Should().Be(cutoff.AddHours(1));
        }

        [Fact]
        public void TrimOlderThan_no_falla_si_el_archivo_no_existe()
        {
            var act = () => _store.TrimOlderThan(Btc, DateTime.UtcNow);
            act.Should().NotThrow();
        }

        [Fact]
        public void NaN_en_buy_sell_ratio_sobrevive_roundtrip()
        {
            var bar = Bar(new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc), buySellRatio: double.NaN);
            _store.Append(bar);

            // Ventana amplia (ver nota arriba): roundtrip de NaN, no filtrado temporal.
            var loaded = _store.LoadRecent(Btc, hours: 24 * 3650);
            loaded.Should().HaveCount(1);
            double.IsNaN(loaded[0].BuySellRatio).Should().BeTrue();
        }

        [Fact]
        public void Multiple_appends_generan_multiples_filas()
        {
            for (int i = 0; i < 5; i++)
                _store.Append(Bar(DateTime.UtcNow.AddHours(-i - 1)));

            _store.LoadRecent(Btc, hours: 72).Should().HaveCount(5);
        }

        [Fact]
        public void Append_y_LoadAll_roundtrip_conserva_OHLCV()
        {
            var bar = new MicrostructureBar(
                Btc, new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc),
                ofi: 0.1, cvdDelta: 1, cvd: 2, arrivalRate: 3,
                meanTradeSize: 0.04, buySellRatio: 1.1, priceReturn: 0.001)
            {
                Open = 100.5m, High = 110.25m, Low = 99.75m, Close = 105.5m, Volume = 1234.5m,
            };

            _store.Append(bar);
            var loaded = _store.LoadAll(Btc);

            loaded.Should().HaveCount(1);
            var r = loaded[0];
            r.Open.Should().Be(100.5m);
            r.High.Should().Be(110.25m);
            r.Low.Should().Be(99.75m);
            r.Close.Should().Be(105.5m);
            r.Volume.Should().Be(1234.5m);
            // Las features siguen intactas (OHLCV se apenda, no reemplaza).
            r.Ofi.Should().BeApproximately(0.1, 1e-12);
            r.CvdDelta.Should().BeApproximately(1, 1e-12);
        }

        [Fact]
        public void LoadAll_de_archivo_viejo_sin_OHLC_devuelve_OHLCV_en_cero()
        {
            // Compat hacia atrás: un store de 8 columnas (formato previo) se lee sin romper,
            // con OHLCV en 0 → el warmup lo detecta como "sin precio" y warmea parcial.
            var file = Path.Combine(_tempDir, "BTCUSDT_1h_live.csv");
            File.WriteAllLines(file, new[]
            {
                "bar_utc,ofi,cvd_delta,cvd,arrival_rate,mean_trade_size,buy_sell_ratio,price_return",
                "2026-06-17T10:00:00Z,0.1,1,2,3,0.04,1.1,0.001",
            });

            var loaded = _store.LoadAll(Btc);

            loaded.Should().HaveCount(1);
            loaded[0].Close.Should().Be(0m);
            loaded[0].Open.Should().Be(0m);
            loaded[0].Ofi.Should().BeApproximately(0.1, 1e-12); // features sí se leen
        }

        [Fact]
        public void LoadAll_devuelve_todas_las_barras_en_orden_cronologico()
        {
            var t1 = new DateTime(2026, 6, 17, 8,  0, 0, DateTimeKind.Utc);
            var t2 = new DateTime(2026, 6, 17, 9,  0, 0, DateTimeKind.Utc);
            var t3 = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);

            _store.Append(BarWithClose(t1, 100m));
            _store.Append(BarWithClose(t2, 101m));
            _store.Append(BarWithClose(t3, 102m));

            var loaded = _store.LoadAll(Btc);

            loaded.Should().HaveCount(3);
            loaded[0].BarUtc.Should().Be(t1);
            loaded[1].BarUtc.Should().Be(t2);
            loaded[2].BarUtc.Should().Be(t3);
            loaded[0].Close.Should().Be(100m);
            loaded[2].Close.Should().Be(102m);
        }

        [Fact]
        public void LoadAll_retorna_vacio_si_no_hay_archivo()
        {
            _store.LoadAll(Btc).Should().BeEmpty();
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static MicrostructureBar BarWithClose(DateTime barUtc, decimal close) =>
            new(Btc, DateTime.SpecifyKind(barUtc, DateTimeKind.Utc),
                ofi: 0, cvdDelta: 0, cvd: 0, arrivalRate: 100,
                meanTradeSize: 1.0, buySellRatio: 1.0, priceReturn: 0)
            {
                Open = close, High = close, Low = close, Close = close, Volume = 1m,
            };

        private static MicrostructureBar Bar(
            DateTime barUtc,
            double ofi = 0, double cvdDelta = 0, double cvd = 0,
            double arrivalRate = 100, double meanTradeSize = 1.0,
            double buySellRatio = 1.0, double priceReturn = 0) =>
            new(
                instrumentId:  Btc,
                barUtc:        DateTime.SpecifyKind(barUtc, DateTimeKind.Utc),
                ofi:           ofi,
                cvdDelta:      cvdDelta,
                cvd:           cvd,
                arrivalRate:   arrivalRate,
                meanTradeSize: meanTradeSize,
                buySellRatio:  buySellRatio,
                priceReturn:   priceReturn
            );
    }
}
