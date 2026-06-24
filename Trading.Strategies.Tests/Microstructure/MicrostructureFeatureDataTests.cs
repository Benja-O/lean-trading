using System;
using FluentAssertions;
using QuantConnect;
using QuantConnect.Data;
using Trading.Strategies.Microstructure;
using Xunit;

namespace Trading.Strategies.Tests.Microstructure
{
    public class MicrostructureFeatureDataTests
    {
        // Misma barra lógica en los dos formatos. Sirve para parsear y para el test de paridad cruzada.
        // Research (16 col): bar,open,high,low,close,volume,buy_volume,sell_volume,trade_count,
        //                    mean_trade_size,ofi,buy_sell_ratio,cvd_delta,arrival_rate,price_return,cvd
        private const string ResearchRow =
            "2021-01-01 00:00:00+00:00,29000.5,29100.0,28900.0,29050.25,1234.5,700.0,534.5,5000,0.2469,0.134,1.310,165.5,5000,0.001724,1000.5";
        private const string ResearchHeader =
            "bar,open,high,low,close,volume,buy_volume,sell_volume,trade_count,mean_trade_size,ofi,buy_sell_ratio,cvd_delta,arrival_rate,price_return,cvd";

        // Store (13 col): bar_utc,ofi,cvd_delta,cvd,arrival_rate,mean_trade_size,buy_sell_ratio,
        //                 price_return,open,high,low,close,volume
        private const string StoreRow =
            "2021-01-01T00:00:00Z,0.134,165.5,1000.5,5000,0.2469,1.310,0.001724,29000.5,29100.0,28900.0,29050.25,1234.5";
        private const string StoreHeader =
            "bar_utc,ofi,cvd_delta,cvd,arrival_rate,mean_trade_size,buy_sell_ratio,price_return,open,high,low,close,volume";

        private static readonly DateTime ExpectedStart = new(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static SubscriptionDataConfig Config()
        {
            var symbol = Symbol.Create("BTCUSDT", SecurityType.Base, Market.Binance);
            return new SubscriptionDataConfig(
                typeof(MicrostructureFeatureData), symbol, Resolution.Minute,
                TimeZones.Utc, TimeZones.Utc,
                fillForward: false, extendedHours: false, isInternalFeed: false, isCustom: true);
        }

        private static MicrostructureFeatureData Read(string line, bool isLiveMode) =>
            (MicrostructureFeatureData)new MicrostructureFeatureData()
                .Reader(Config(), line, ExpectedStart, isLiveMode);

        [Fact]
        public void Reader_FormatoResearch_ParseaOhlcvYFeatures()
        {
            var bar = Read(ResearchRow, isLiveMode: false);

            bar.Should().NotBeNull();
            bar.Open.Should().Be(29000.5m);
            bar.High.Should().Be(29100.0m);
            bar.Low.Should().Be(28900.0m);
            bar.Close.Should().Be(29050.25m);
            bar.Volume.Should().Be(1234.5m);
            bar.Value.Should().Be(29050.25m); // BaseData.Value = Close

            bar.Ofi.Should().BeApproximately(0.134, 1e-9);
            bar.CvdDelta.Should().BeApproximately(165.5, 1e-9);
            bar.Cvd.Should().BeApproximately(1000.5, 1e-9);
            bar.ArrivalRate.Should().BeApproximately(5000, 1e-9);
            bar.MeanTradeSize.Should().BeApproximately(0.2469, 1e-9);
            bar.BuySellRatio.Should().BeApproximately(1.310, 1e-9);
            bar.PriceReturn.Should().BeApproximately(0.001724, 1e-9);
        }

        [Fact]
        public void Reader_FormatoStore_ParseaOhlcvYFeatures()
        {
            var bar = Read(StoreRow, isLiveMode: true);

            bar.Should().NotBeNull();
            bar.Open.Should().Be(29000.5m);
            bar.High.Should().Be(29100.0m);
            bar.Low.Should().Be(28900.0m);
            bar.Close.Should().Be(29050.25m);
            bar.Volume.Should().Be(1234.5m);

            bar.Ofi.Should().BeApproximately(0.134, 1e-9);
            bar.CvdDelta.Should().BeApproximately(165.5, 1e-9);
            bar.Cvd.Should().BeApproximately(1000.5, 1e-9);
            bar.ArrivalRate.Should().BeApproximately(5000, 1e-9);
            bar.MeanTradeSize.Should().BeApproximately(0.2469, 1e-9);
            bar.BuySellRatio.Should().BeApproximately(1.310, 1e-9);
            bar.PriceReturn.Should().BeApproximately(0.001724, 1e-9);
        }

        [Fact]
        public void Reader_TimestampEsInicioDeBarra_EndTimeMasUnaHora()
        {
            // Ambos formatos etiquetan por el INICIO de la barra (floor de la hora).
            foreach (var (line, live) in new[] { (ResearchRow, false), (StoreRow, true) })
            {
                var bar = Read(line, live);
                bar.Time.Should().Be(ExpectedStart, $"isLiveMode={live}");
                bar.EndTime.Should().Be(ExpectedStart.AddHours(1), $"isLiveMode={live}");
            }
        }

        [Fact]
        public void Reader_ParidadCruzada_MismaBarraEnAmbosFormatosDaResultadoIdentico()
        {
            // El núcleo de ADR-053: backtest (research) y live (store) deben producir la MISMA barra
            // para el mismo dato lógico. Si esto se rompe, la paridad backtest/live se rompe.
            var research = Read(ResearchRow, isLiveMode: false);
            var store    = Read(StoreRow,    isLiveMode: true);

            store.Time.Should().Be(research.Time);
            store.EndTime.Should().Be(research.EndTime);
            store.Open.Should().Be(research.Open);
            store.High.Should().Be(research.High);
            store.Low.Should().Be(research.Low);
            store.Close.Should().Be(research.Close);
            store.Volume.Should().Be(research.Volume);
            store.Ofi.Should().Be(research.Ofi);
            store.CvdDelta.Should().Be(research.CvdDelta);
            store.Cvd.Should().Be(research.Cvd);
            store.ArrivalRate.Should().Be(research.ArrivalRate);
            store.MeanTradeSize.Should().Be(research.MeanTradeSize);
            store.BuySellRatio.Should().Be(research.BuySellRatio);
            store.PriceReturn.Should().Be(research.PriceReturn);
        }

        [Theory]
        [InlineData(ResearchHeader, false)]
        [InlineData(StoreHeader, true)]
        [InlineData("", false)]
        [InlineData("   ", true)]
        public void Reader_HeaderOLineaVacia_RetornaNull(string line, bool isLiveMode)
        {
            new MicrostructureFeatureData()
                .Reader(Config(), line, ExpectedStart, isLiveMode)
                .Should().BeNull();
        }

        [Theory]
        // Columnas insuficientes para cada formato → descartar (no fabricar barra con OHLCV en 0).
        [InlineData("2021-01-01 00:00:00+00:00,1,2,3,4,5", false)]
        [InlineData("2021-01-01T00:00:00Z,0.1,1,2", true)]
        public void Reader_ColumnasInsuficientes_RetornaNull(string line, bool isLiveMode)
        {
            new MicrostructureFeatureData()
                .Reader(Config(), line, ExpectedStart, isLiveMode)
                .Should().BeNull();
        }

        [Fact]
        public void Reader_FeatureNaN_SeParseaComoNaN()
        {
            // El pipeline de research emite "nan" en barras sin trades de algún lado.
            var row = ResearchRow.Replace(",0.134,", ",nan,"); // ofi → nan
            var bar = Read(row, isLiveMode: false);

            bar.Should().NotBeNull();
            double.IsNaN(bar.Ofi).Should().BeTrue();
        }
    }
}
