using System;
using System.Collections.Generic;
using FluentAssertions;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Domain.Tests
{
    /// <summary>
    /// Tests de RegimeClassification.
    ///
    /// Cobertura:
    /// - UnknownFor: label Unknown, probabilidad 1.0 en Unknown, timestamp correcto.
    /// - Constructor del record: asigna todos los campos.
    /// </summary>
    public class RegimeClassificationTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime SampleTime = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void UnknownFor_DevuelveClasificacionConLabelUnknown()
        {
            var classification = RegimeClassification.UnknownFor(Btc, SampleTime);

            classification.Label.Should().Be(RegimeLabel.Unknown);
        }

        [Fact]
        public void UnknownFor_DevuelveProbabilidadUnoEnUnknown()
        {
            var classification = RegimeClassification.UnknownFor(Btc, SampleTime);

            classification.Probabilities.Should().ContainKey(RegimeLabel.Unknown);
            classification.Probabilities[RegimeLabel.Unknown].Should().Be(1.0);
        }

        [Fact]
        public void UnknownFor_AsignaInstrumentoYTimestampCorrectamente()
        {
            var classification = RegimeClassification.UnknownFor(Btc, SampleTime);

            classification.Instrument.Should().Be(Btc);
            classification.ClassifiedAtUtc.Should().Be(SampleTime);
        }

        [Fact]
        public void Constructor_AsignaTodosLosCamposCorrectamente()
        {
            var probabilities = new Dictionary<RegimeLabel, double>
            {
                [RegimeLabel.Trend] = 0.8,
                [RegimeLabel.MeanReverting] = 0.2
            };

            var classification = new RegimeClassification(Btc, RegimeLabel.Trend, probabilities, SampleTime);

            classification.Instrument.Should().Be(Btc);
            classification.Label.Should().Be(RegimeLabel.Trend);
            classification.Probabilities.Should().BeEquivalentTo(probabilities);
            classification.ClassifiedAtUtc.Should().Be(SampleTime);
        }
    }
}
