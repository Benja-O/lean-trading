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
            var loaded = _store.LoadRecent(Btc, hours: 72);

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

            var loaded = _store.LoadRecent(Btc, hours: 72);
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

        // ── helpers ──────────────────────────────────────────────────────────

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
