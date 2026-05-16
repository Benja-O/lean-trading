using System;
using FluentAssertions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Domain.Tests
{
    /// <summary>
    /// Tests del modelo MarketBar.
    ///
    /// Cobertura:
    /// - Constructor OHLCV completo: asigna todos los campos correctamente.
    /// - Constructor OHLCV: lanza ArgumentNullException si InstrumentId es null.
    /// - Constructor obsoleto: delega al completo inicializando Open/High/Low con close y Volume en 0.
    /// </summary>
    public class MarketBarTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime SampleTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Constructor_OHLCV_AsignaTodosLosCamposCorrectamente()
        {
            var bar = new MarketBar(
                instrumentId: Btc,
                open: 100m,
                high: 110m,
                low: 95m,
                close: 105m,
                volume: 1500m,
                timestampUtc: SampleTime);

            bar.InstrumentId.Should().Be(Btc);
            bar.Open.Should().Be(100m);
            bar.High.Should().Be(110m);
            bar.Low.Should().Be(95m);
            bar.Close.Should().Be(105m);
            bar.Volume.Should().Be(1500m);
            bar.TimestampUtc.Should().Be(SampleTime);
        }

        [Fact]
        public void Constructor_OHLCV_LanzaSiInstrumentIdEsNull()
        {
            Action act = () => new MarketBar(
                instrumentId: null!,
                open: 100m,
                high: 110m,
                low: 95m,
                close: 105m,
                volume: 1500m,
                timestampUtc: SampleTime);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
#pragma warning disable CS0618 // probando deliberadamente el constructor obsoleto
        public void ConstructorObsoleto_DelegaCorrectamenteAlCompleto()
        {
            var bar = new MarketBar(Btc, close: 105m, timestampUtc: SampleTime);

            bar.InstrumentId.Should().Be(Btc);
            bar.Open.Should().Be(105m);
            bar.High.Should().Be(105m);
            bar.Low.Should().Be(105m);
            bar.Close.Should().Be(105m);
            bar.Volume.Should().Be(0m);
            bar.TimestampUtc.Should().Be(SampleTime);
        }
#pragma warning restore CS0618
    }
}
