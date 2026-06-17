using System;
using FluentAssertions;
using Trading.Application.Microstructure;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Microstructure
{
    /// <summary>
    /// Tests de paridad: MicrostructureFeatureComputer debe producir valores idénticos
    /// a la función _agg_1h() de Trading.Research/download_aggtrades.py para el mismo
    /// conjunto de aggTrades.
    ///
    /// Cada caso documenta la aritmética esperada para facilitar la verificación manual
    /// contra el CSV de referencia.
    ///
    /// Reglas de Python replicadas (ver _agg_1h):
    ///   buy_qty  = qty where NOT is_buyer_maker  (buyer agresivo / taker)
    ///   sell_qty = qty where     is_buyer_maker  (seller agresivo / buyer es maker)
    ///   ofi            = (buy_vol - sell_vol) / total_vol  → NaN si total_vol=0
    ///   buy_sell_ratio = buy_vol / sell_vol                → NaN si sell_vol=0
    ///   cvd_delta      = buy_vol - sell_vol
    ///   cvd            = cvdRunning + cvd_delta            (acumulativo)
    ///   arrival_rate   = trade_count                       (conteo entero, no rata)
    ///   mean_trade_size = sum(qty) / count
    ///   price_return   = (close - open) / open             → NaN si open=0
    /// </summary>
    public class MicrostructureFeatureComputerParityTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime BarUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── Caso 1: mezcla de compras y ventas ───────────────────────────────────────

        [Fact]
        public void Compute_MixedBuysAndSells_ReplicatesPythonFormulas()
        {
            // Datos:
            //   buy trades (is_buyer_maker=false): (100, 1.0), (101, 2.0), (102, 3.0)
            //   sell trades (is_buyer_maker=true):  (103, 1.5), (99, 0.5)
            //
            // buy_volume  = 1+2+3 = 6.0
            // sell_volume = 1.5+0.5 = 2.0
            // total_vol   = 8.0
            // trade_count = 5
            // sum_qty     = 8.0   → mean_trade_size = 8.0/5 = 1.6
            // ofi         = (6-2)/8 = 0.5
            // buy_sell_r  = 6/2 = 3.0
            // cvd_delta   = 6-2 = 4.0
            // cvd         = 10.0 + 4.0 = 14.0
            // open        = 100 (first price)
            // close       = 99  (last price)
            // price_return = (99-100)/100 = -0.01
            // arrival_rate = 5
            var bucket = new AggTradeBucket();
            bucket.Add(100m, 1.0m, isBuyerMaker: false);
            bucket.Add(101m, 2.0m, isBuyerMaker: false);
            bucket.Add(102m, 3.0m, isBuyerMaker: false);
            bucket.Add(103m, 1.5m, isBuyerMaker: true);
            bucket.Add(99m,  0.5m, isBuyerMaker: true);

            var (bar, newCvd) = MicrostructureFeatureComputer.Compute(Btc, BarUtc, bucket, cvdRunning: 10.0);

            bar.Ofi.Should().BeApproximately(0.5, 1e-9);
            bar.BuySellRatio.Should().BeApproximately(3.0, 1e-9);
            bar.CvdDelta.Should().BeApproximately(4.0, 1e-9);
            bar.Cvd.Should().BeApproximately(14.0, 1e-9);
            bar.ArrivalRate.Should().BeApproximately(5.0, 1e-9);
            bar.MeanTradeSize.Should().BeApproximately(1.6, 1e-9);
            bar.PriceReturn.Should().BeApproximately(-0.01, 1e-9);
            newCvd.Should().BeApproximately(14.0, 1e-9);
        }

        // ── Caso 2: solo compras → buy_sell_ratio NaN (sell_vol=0) ─────────────────

        [Fact]
        public void Compute_OnlyBuys_BuySellRatioIsNaN()
        {
            // sell_volume=0 → sv=NaN → buy_sell_ratio = buy_vol / NaN = NaN
            // ofi = (5-0)/5 = 1.0
            var bucket = new AggTradeBucket();
            bucket.Add(100m, 2.0m, isBuyerMaker: false);
            bucket.Add(101m, 3.0m, isBuyerMaker: false);

            var (bar, _) = MicrostructureFeatureComputer.Compute(Btc, BarUtc, bucket, cvdRunning: 0.0);

            bar.Ofi.Should().BeApproximately(1.0, 1e-9);
            bar.BuySellRatio.Should().Be(double.NaN);
            bar.CvdDelta.Should().BeApproximately(5.0, 1e-9);
            bar.ArrivalRate.Should().BeApproximately(2.0, 1e-9);
            bar.MeanTradeSize.Should().BeApproximately(2.5, 1e-9);
        }

        // ── Caso 3: solo ventas → ofi=-1.0, buy_sell_ratio=0 ───────────────────────

        [Fact]
        public void Compute_OnlySells_OfiIsMinusOne()
        {
            // buy_volume=0, sell_volume=4.0
            // ofi = (0-4)/4 = -1.0
            // buy_sell_ratio = 0/4 = 0.0   (Python: 0/4 = 0.0, no NaN porque sell_vol>0)
            var bucket = new AggTradeBucket();
            bucket.Add(100m, 1.5m, isBuyerMaker: true);
            bucket.Add(99m,  2.5m, isBuyerMaker: true);

            var (bar, _) = MicrostructureFeatureComputer.Compute(Btc, BarUtc, bucket, cvdRunning: 0.0);

            bar.Ofi.Should().BeApproximately(-1.0, 1e-9);
            bar.BuySellRatio.Should().BeApproximately(0.0, 1e-9);
            bar.CvdDelta.Should().BeApproximately(-4.0, 1e-9);
        }

        // ── Caso 4: CVD acumulativo correcto entre dos barras consecutivas ──────────

        [Fact]
        public void Compute_TwoConsecutiveBars_CvdAccumulatesCorrectly()
        {
            var bucket1 = new AggTradeBucket();
            bucket1.Add(100m, 6.0m, isBuyerMaker: false); // buy_vol=6, sell_vol=0 → cvd_delta=6
            bucket1.Add(99m,  2.0m, isBuyerMaker: true);  // buy_vol=6, sell_vol=2 → cvd_delta=4

            var (bar1, cvdAfter1) = MicrostructureFeatureComputer.Compute(Btc, BarUtc, bucket1, cvdRunning: 0.0);

            var bucket2 = new AggTradeBucket();
            bucket2.Add(98m, 1.0m, isBuyerMaker: true);  // sell
            bucket2.Add(97m, 3.0m, isBuyerMaker: true);  // sell → cvd_delta = 0 - 4 = -4

            var (bar2, cvdAfter2) = MicrostructureFeatureComputer.Compute(Btc, BarUtc.AddHours(1), bucket2, cvdRunning: cvdAfter1);

            bar1.CvdDelta.Should().BeApproximately(4.0, 1e-9);
            bar1.Cvd.Should().BeApproximately(4.0, 1e-9);
            cvdAfter1.Should().BeApproximately(4.0, 1e-9);

            bar2.CvdDelta.Should().BeApproximately(-4.0, 1e-9);
            bar2.Cvd.Should().BeApproximately(0.0, 1e-9);   // 4 + (-4) = 0
            cvdAfter2.Should().BeApproximately(0.0, 1e-9);
        }

        // ── Caso 5: price_return correcto ────────────────────────────────────────────

        [Fact]
        public void Compute_PriceReturn_UsesFirstAndLastPrice()
        {
            // open=100 (primer precio), close=110 (último precio)
            // price_return = (110-100)/100 = 0.10
            var bucket = new AggTradeBucket();
            bucket.Add(100m, 1.0m, isBuyerMaker: false);
            bucket.Add(105m, 1.0m, isBuyerMaker: false);
            bucket.Add(110m, 1.0m, isBuyerMaker: false);

            var (bar, _) = MicrostructureFeatureComputer.Compute(Btc, BarUtc, bucket, cvdRunning: 0.0);

            bar.PriceReturn.Should().BeApproximately(0.10, 1e-9);
        }

        // ── Caso 6: mean_trade_size = sum(qty)/count exacto (no buy_vol/count) ──────

        [Fact]
        public void Compute_MeanTradeSize_IsAverageOfAllQtysNotJustBuys()
        {
            // Python: g["qty"].mean() = (1+2+3+4) / 4 = 2.5
            // (no distingue entre buys y sells para el mean)
            var bucket = new AggTradeBucket();
            bucket.Add(100m, 1.0m, isBuyerMaker: false);
            bucket.Add(100m, 2.0m, isBuyerMaker: true);
            bucket.Add(100m, 3.0m, isBuyerMaker: false);
            bucket.Add(100m, 4.0m, isBuyerMaker: true);

            var (bar, _) = MicrostructureFeatureComputer.Compute(Btc, BarUtc, bucket, cvdRunning: 0.0);

            bar.MeanTradeSize.Should().BeApproximately(2.5, 1e-9);
        }

        // ── Caso 7: arrival_rate = conteo de trades (no una rata por segundo) ────────

        [Fact]
        public void Compute_ArrivalRate_IsRawTradeCount()
        {
            var bucket = new AggTradeBucket();
            for (int i = 0; i < 7; i++)
                bucket.Add(100m, 1.0m, isBuyerMaker: i % 2 == 0);

            var (bar, _) = MicrostructureFeatureComputer.Compute(Btc, BarUtc, bucket, cvdRunning: 0.0);

            bar.ArrivalRate.Should().BeApproximately(7.0, 1e-9);
        }

        // ── Caso 8: barUtc y instrumentId propagados correctamente ──────────────────

        [Fact]
        public void Compute_PreservesInstrumentIdAndBarUtc()
        {
            var bucket = new AggTradeBucket();
            bucket.Add(100m, 1.0m, isBuyerMaker: false);
            var expected = new DateTime(2024, 6, 15, 8, 0, 0, DateTimeKind.Utc);

            var (bar, _) = MicrostructureFeatureComputer.Compute(Btc, expected, bucket, cvdRunning: 0.0);

            bar.InstrumentId.Should().Be(Btc);
            bar.BarUtc.Should().Be(expected);
        }
    }
}
