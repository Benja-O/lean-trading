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
    /// Tests de MarketRegimeRegistry.
    ///
    /// Cobertura:
    /// - ClassifyBar llama al classifier del instrumento correspondiente.
    /// - ClassifyBar para instrumento sin clasificador devuelve Unknown y loguea Debug.
    /// - GetLastClassification devuelve Unknown si nunca se procesó una barra.
    /// - GetLastClassification devuelve la última clasificación procesada.
    /// - Constructor lanza si hay dos classifiers para el mismo instrumento.
    /// - HasClassifier devuelve true para instrumento registrado.
    /// </summary>
    public class MarketRegimeRegistryTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly InstrumentId Eth = new("ETHUSDT");
        private static readonly DateTime SampleTime = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private readonly FakeClock _clock = new();
        private readonly FakeTradingLogger _logger = new();

        private MarketBar BuildBar(InstrumentId instrumentId, DateTime timestamp) =>
            new MarketBar(instrumentId, 100m, 110m, 90m, 105m, 1000m, timestamp);

        private MarketRegimeRegistry BuildRegistry(RegimeLabel label = RegimeLabel.Trend)
        {
            var classifier = new ConfigurableMarketRegimeClassifier(Btc, label, _clock);
            return new MarketRegimeRegistry(new[] { classifier }, _clock, _logger);
        }

        [Fact]
        public void ClassifyBar_LlamaAlClassifierCorrespondiente()
        {
            var registry = BuildRegistry(RegimeLabel.Squeeze);
            var bar = BuildBar(Btc, SampleTime);

            var result = registry.ClassifyBar(bar);

            result.Label.Should().Be(RegimeLabel.Squeeze);
            result.Instrument.Should().Be(Btc);
        }

        [Fact]
        public void ClassifyBar_ParaInstrumentoSinClasificador_DevuelveUnknown()
        {
            var registry = BuildRegistry();
            var bar = BuildBar(Eth, SampleTime);

            var result = registry.ClassifyBar(bar);

            result.Label.Should().Be(RegimeLabel.Unknown);
            result.Instrument.Should().Be(Eth);
        }

        [Fact]
        public void ClassifyBar_ParaInstrumentoSinClasificador_LogueaDebug()
        {
            var registry = BuildRegistry();
            var bar = BuildBar(Eth, SampleTime);

            registry.ClassifyBar(bar);

            _logger.DebugEntries.Should().ContainSingle(entry =>
                entry.MessageTemplate.Contains("no hay clasificador registrado"));
        }

        [Fact]
        public void GetLastClassification_DevuelveUnknownSiNuncaSeClasificoNada()
        {
            var registry = BuildRegistry();

            var result = registry.GetLastClassification(Eth);

            result.Label.Should().Be(RegimeLabel.Unknown);
            result.Instrument.Should().Be(Eth);
        }

        [Fact]
        public void GetLastClassification_DevuelveLaUltimaClasificacionProcesada()
        {
            var registry = BuildRegistry(RegimeLabel.MeanReverting);
            var bar = BuildBar(Btc, SampleTime);
            registry.ClassifyBar(bar);

            var result = registry.GetLastClassification(Btc);

            result.Label.Should().Be(RegimeLabel.MeanReverting);
        }

        [Fact]
        public void Constructor_LanzaSiHayDosClassifiersParaElMismoInstrumento()
        {
            var classifier1 = new ConfigurableMarketRegimeClassifier(Btc, RegimeLabel.Trend, _clock);
            var classifier2 = new ConfigurableMarketRegimeClassifier(Btc, RegimeLabel.Squeeze, _clock);

            Action act = () => new MarketRegimeRegistry(new[] { classifier1, classifier2 }, _clock, _logger);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*ya existe un clasificador registrado*");
        }

        [Fact]
        public void HasClassifier_DevuelveTrueParaInstrumentoRegistrado()
        {
            var registry = BuildRegistry();

            registry.HasClassifier(Btc).Should().BeTrue();
            registry.HasClassifier(Eth).Should().BeFalse();
        }
    }
}
