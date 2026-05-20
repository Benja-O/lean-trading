using System;
using System.Collections.Generic;
using FluentAssertions;
using Trading.Application.Eventing;
using Trading.Application.Execution;
using Trading.Application.Regimes;
using Trading.Application.Risk;
using Trading.Application.Sizing;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Abstractions;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Events;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Execution
{
    /// <summary>
    /// Verifica que BarProcessingService emite BarProcessedEvent solo en el camino exitoso
    /// (orden enviada), y NO en los caminos de early-return (régimen incompatible, sizing fallido).
    /// </summary>
    public class BarProcessingServiceBarProcessedEventTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime SampleTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        private const string ExecutorId = "EmaCross_BTCUSDT_1h";

        private readonly FakeClock _clock = new() { UtcNow = SampleTime };
        private readonly FakeTradingLogger _logger = new();
        private readonly FakeOrderRouter _orderRouter = new();
        private readonly FakeInstrumentMetadata _instrumentMetadata = new();

        private MarketBar BuildBar() =>
            new MarketBar(Btc, 48_000m, 50_000m, 46_000m, 50_000m, 10m, SampleTime);

        private StrategyExecutor BuildExecutor(IStrategy strategy)
        {
            var definition = new StrategyDefinition
            {
                StrategyName = "EmaCross",
                Symbol = "BTCUSDT",
                StopLossPercentage = 3.0m,
                TakeProfitPercentage = 6.0m,
                RiskPerTradePercentage = 2.0m,
                CombineWithTimeExit = false,
                MaxBars = 100
            };
            var riskParams = RiskParameters.FromPercentages(3.0m, 6.0m, 2.0m);
            return new StrategyExecutor(definition, "1h", Btc, strategy, riskParams);
        }

        private (BarProcessingService service, CapturingEventSubscriber<BarProcessedEvent> capturer)
            BuildService(
                FakePortfolioState portfolioState,
                MarketRegimeRegistry? registry = null,
                IReadOnlyDictionary<string, StrategyRegimeCompatibility>? compatibilities = null)
        {
            var eventBus = new DomainEventBus(_logger);
            var capturer = new CapturingEventSubscriber<BarProcessedEvent>(eventBus);

            var coolingOff = new CoolingOffTracker(_clock, TimeSpan.FromHours(24));
            var orchestrator = new RiskOrchestrator(
                System.Array.Empty<IRiskMonitor>(),
                new FakeRiskAction(),
                coolingOff, _clock, _logger, eventBus);

            var sizer = new PositionSizer(portfolioState, _instrumentMetadata, _logger);

            registry ??= new MarketRegimeRegistry(
                System.Array.Empty<IMarketRegimeClassifier>(), _clock, _logger);
            compatibilities ??= new Dictionary<string, StrategyRegimeCompatibility>();

            var service = new BarProcessingService(
                portfolioState, _orderRouter, orchestrator, sizer,
                _logger, eventBus, _clock, registry, compatibilities);

            return (service, capturer);
        }

        [Fact]
        public void Process_HappyPath_PublishesBarProcessedEvent()
        {
            var portfolio = new FakePortfolioState { TotalPortfolioValue = 100_000m };
            var (service, capturer) = BuildService(portfolio);
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            service.ProcessBar(BuildBar(), new[] { executor });

            capturer.CapturedEvents.Should().ContainSingle(e =>
                e.InstrumentId == Btc &&
                e.BarTimestampUtc == SampleTime);
        }

        [Fact]
        public void Process_WhenSkippedByRegimeFilter_DoesNotPublishBarProcessedEvent()
        {
            // Classifier devuelve HighVolatility; estrategia solo acepta [Trend]
            var portfolio = new FakePortfolioState { TotalPortfolioValue = 100_000m };
            var classifier = new ConfigurableMarketRegimeClassifier(Btc, RegimeLabel.HighVolatility, _clock);
            var registry = new MarketRegimeRegistry(new[] { classifier }, _clock, _logger);
            registry.ClassifyBar(BuildBar());

            var allowed = new System.Collections.Generic.HashSet<RegimeLabel> { RegimeLabel.Trend };
            var compatibilities = new Dictionary<string, StrategyRegimeCompatibility>
            {
                [ExecutorId] = new StrategyRegimeCompatibility(ExecutorId, allowed)
            };

            var (service, capturer) = BuildService(portfolio, registry, compatibilities);
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            service.ProcessBar(BuildBar(), new[] { executor });

            capturer.CapturedEvents.Should().BeEmpty();
        }

        [Fact]
        public void Process_WhenSizingFails_DoesNotPublishBarProcessedEvent()
        {
            // TotalPortfolioValue = 0 → quantity = 0 → QuantityRoundsToZero
            var portfolio = new FakePortfolioState { TotalPortfolioValue = 0m };
            var (service, capturer) = BuildService(portfolio);
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            service.ProcessBar(BuildBar(), new[] { executor });

            capturer.CapturedEvents.Should().BeEmpty();
        }
    }

    internal sealed class ConfigurableSignalStrategy : IStrategy
    {
        private readonly SignalDirection _signal;
        public ConfigurableSignalStrategy(SignalDirection signal) => _signal = signal;
        public SignalDirection EvaluateSignal(MarketBar marketBar) => _signal;
    }
}
