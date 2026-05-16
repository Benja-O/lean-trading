using System;
using FluentAssertions;
using Trading.Application.Regimes;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Regimes
{
    /// <summary>
    /// Tests de ConfigurableMarketRegimeClassifier.
    ///
    /// Cobertura:
    /// - Classify devuelve siempre la etiqueta fija, para múltiples barras.
    /// - IsWarmedUp es siempre true.
    /// - Constructor lanza si fixedLabel es Unknown.
    /// - Constructor lanza si instrument es null.
    /// - Classify propaga TimestampUtc de la barra a ClassifiedAtUtc.
    /// </summary>
    public class ConfigurableMarketRegimeClassifierTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly FakeClock Clock = new();

        private static MarketBar BuildBar(DateTime timestamp) =>
            new MarketBar(Btc, 100m, 110m, 90m, 105m, 1000m, timestamp);

        [Fact]
        public void Classify_DevuelveSiempreLaEtiquetaFija()
        {
            var classifier = new ConfigurableMarketRegimeClassifier(Btc, RegimeLabel.Trend, Clock);
            var time = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int barIndex = 0; barIndex < 5; barIndex++)
            {
                var bar = BuildBar(time.AddHours(barIndex));
                var result = classifier.Classify(bar);

                result.Label.Should().Be(RegimeLabel.Trend,
                    because: $"en la barra {barIndex} el classifier fake debe devolver siempre Trend");
            }
        }

        [Fact]
        public void IsWarmedUp_EsSiempreTrue()
        {
            var classifier = new ConfigurableMarketRegimeClassifier(Btc, RegimeLabel.Squeeze, Clock);

            classifier.IsWarmedUp.Should().BeTrue();
        }

        [Fact]
        public void Constructor_LanzaSiFixedLabelEsUnknown()
        {
            Action act = () => new ConfigurableMarketRegimeClassifier(Btc, RegimeLabel.Unknown, Clock);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*Unknown*");
        }

        [Fact]
        public void Constructor_LanzaSiInstrumentEsNulo()
        {
            Action act = () => new ConfigurableMarketRegimeClassifier(null!, RegimeLabel.Trend, Clock);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Classify_PropagaTimestampDeLaBarra()
        {
            var classifier = new ConfigurableMarketRegimeClassifier(Btc, RegimeLabel.HighVolatility, Clock);
            var expectedTime = new DateTime(2025, 3, 15, 9, 30, 0, DateTimeKind.Utc);
            var bar = BuildBar(expectedTime);

            var result = classifier.Classify(bar);

            result.ClassifiedAtUtc.Should().Be(expectedTime);
        }
    }
}
