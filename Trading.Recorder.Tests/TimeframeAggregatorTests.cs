using System;
using System.Collections.Generic;
using FluentAssertions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Recorder;

namespace Trading.Recorder.Tests
{
    /// <summary>
    /// Tests de TimeframeAggregator.
    /// Verifica que las ventanas se cierran correctamente y que las features computadas
    /// tienen paridad con MicrostructureFeatureComputer (golden source).
    /// </summary>
    public class TimeframeAggregatorTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");

        // Hora base: 2026-06-17 10:00:00 UTC
        private static readonly DateTime Hour0 = new(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);

        private static long ToMs(DateTime utc) =>
            (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;

        // ── helpers ──────────────────────────────────────────────────────────

        private static (TimeframeAggregator Agg, List<MicrostructureBar> Bars) MakeAggregator(
            string timeframe = "1h", double cvdSeed = 0.0)
        {
            var bars = new List<MicrostructureBar>();
            var agg  = new TimeframeAggregator(Btc, timeframe, cvdSeed);
            agg.BarClosed += (_, __, bar) => bars.Add(bar);
            return (agg, bars);
        }

        // ── tests ─────────────────────────────────────────────────────────────

        [Fact]
        public void Primera_ventana_no_cierra_hasta_recibir_trade_de_ventana_siguiente()
        {
            var (agg, bars) = MakeAggregator("1h");

            // Un solo trade en Hour0 — la ventana no ha cerrado todavía
            agg.OnTrade(price: 100m, qty: 1m, isBuyerMaker: false, tradeTimeMs: ToMs(Hour0));

            bars.Should().BeEmpty();
        }

        [Fact]
        public void Ventana_cierra_cuando_llega_trade_de_la_siguiente_hora()
        {
            var (agg, bars) = MakeAggregator("1h");

            // Trade en Hour0
            agg.OnTrade(100m, 1m, false, ToMs(Hour0));
            // Trade en Hour0+1h — dispara el cierre de Hour0
            agg.OnTrade(101m, 0.5m, true, ToMs(Hour0.AddHours(1)));

            bars.Should().HaveCount(1);
            bars[0].BarUtc.Should().Be(Hour0);
        }

        [Fact]
        public void Features_computadas_tienen_paridad_con_golden_source()
        {
            // buy_vol=1.0, sell_vol=0.5, total=1.5
            // ofi=(1.0-0.5)/1.5=0.3333, buy_sell_ratio=2.0, cvd_delta=0.5
            // arrival_rate=2, mean_trade_size=1.5/2=0.75, price_return=(101-100)/100=0.01
            var (agg, bars) = MakeAggregator("1h", cvdSeed: 10.0);

            agg.OnTrade(100m, 1.0m, false, ToMs(Hour0.AddMinutes(10)));   // buy
            agg.OnTrade(101m, 0.5m, true,  ToMs(Hour0.AddMinutes(30)));   // sell
            // Cierre
            agg.OnTrade(102m, 0.1m, false, ToMs(Hour0.AddHours(1)));

            var bar = bars.Should().ContainSingle().Subject;
            bar.BarUtc.Should().Be(Hour0);
            bar.Ofi.Should().BeApproximately(0.5 / 1.5, 1e-9);            // (1.0-0.5)/1.5
            bar.CvdDelta.Should().BeApproximately(0.5, 1e-9);
            bar.Cvd.Should().BeApproximately(10.5, 1e-9);                  // seed 10 + delta 0.5
            bar.ArrivalRate.Should().BeApproximately(2, 1e-9);
            bar.MeanTradeSize.Should().BeApproximately(0.75, 1e-9);        // 1.5/2
            bar.BuySellRatio.Should().BeApproximately(2.0, 1e-9);
            bar.PriceReturn.Should().BeApproximately(0.01, 1e-12);
        }

        [Fact]
        public void CVD_se_acumula_entre_ventanas()
        {
            var (agg, bars) = MakeAggregator("1h", cvdSeed: 0.0);

            // Hora 0: buy 2.0 → cvd_delta=2.0, Cvd=2.0
            agg.OnTrade(100m, 2.0m, false, ToMs(Hour0));
            // Hora 1: sell 1.0 → cvd_delta=-1.0, Cvd=1.0
            agg.OnTrade(100m, 1.0m, true, ToMs(Hour0.AddHours(1)));
            // Hora 2: cierre
            agg.OnTrade(100m, 0.1m, false, ToMs(Hour0.AddHours(2)));

            bars.Should().HaveCount(2);
            bars[0].Cvd.Should().BeApproximately(2.0, 1e-9);
            bars[1].Cvd.Should().BeApproximately(1.0, 1e-9);
        }

        [Fact]
        public void Ventana_sin_datos_por_gap_no_emite_barra()
        {
            // Si el grabador se reconecta y pierde una hora, no emite barra para esa hora
            var (agg, bars) = MakeAggregator("1h");

            agg.OnTrade(100m, 1.0m, false, ToMs(Hour0));
            // Salto de 2 horas (gap de reconexión)
            agg.OnTrade(100m, 1.0m, false, ToMs(Hour0.AddHours(3)));
            // Cierre de la hora 3
            agg.OnTrade(100m, 0.1m, false, ToMs(Hour0.AddHours(4)));

            // Solo 2 barras: Hour0 y Hour3. Hour1/Hour2 no existen.
            bars.Should().HaveCount(2);
            bars[0].BarUtc.Should().Be(Hour0);
            bars[1].BarUtc.Should().Be(Hour0.AddHours(3));
        }

        [Fact]
        public void Timeframe_5m_cierra_ventanas_cada_5_minutos()
        {
            var (agg, bars) = MakeAggregator("5m");
            var t0 = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);

            agg.OnTrade(100m, 1m, false, ToMs(t0.AddMinutes(1)));
            agg.OnTrade(101m, 1m, false, ToMs(t0.AddMinutes(5)));   // cierre ventana 10:00
            agg.OnTrade(102m, 1m, false, ToMs(t0.AddMinutes(10)));  // cierre ventana 10:05

            bars.Should().HaveCount(2);
            bars[0].BarUtc.Should().Be(t0);
            bars[1].BarUtc.Should().Be(t0.AddMinutes(5));
        }

        [Fact]
        public void Bucket_solo_sell_produce_NaN_en_buy_sell_ratio()
        {
            var (agg, bars) = MakeAggregator("1h");

            agg.OnTrade(100m, 1.0m, true, ToMs(Hour0));     // sell
            agg.OnTrade(101m, 0.1m, false, ToMs(Hour0.AddHours(1)));

            var bar = bars.Should().ContainSingle().Subject;
            // buy_vol=0 → ofi = (0-1)/1 = -1.0; sell_vol≠0 → buy_sell_ratio = 0/1 = 0
            bar.Ofi.Should().BeApproximately(-1.0, 1e-9);
            bar.BuySellRatio.Should().BeApproximately(0.0, 1e-9);
        }
    }
}
