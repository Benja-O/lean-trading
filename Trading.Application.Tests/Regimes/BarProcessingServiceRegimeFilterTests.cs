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
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Regimes
{
    /// <summary>
    /// Tests del filtro de régimen pre-orden en BarProcessingService.
    ///
    /// Cobertura:
    /// - Régimen compatible → orden enviada.
    /// - Régimen incompatible → señal descartada, sin orden.
    /// - Executor no registrado en strategyCompatibilities → orden enviada (fail-safe).
    /// - Registry devuelve Unknown (sin clasificaciones previas) → orden enviada (fail-safe).
    /// - Señal Flat → filtro de régimen no se evalúa (no aparece log de descarte).
    /// </summary>
    public class BarProcessingServiceRegimeFilterTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime SampleTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        private const string ExecutorId = "EmaCross_BTCUSDT_1h";

        private readonly FakeClock _clock = new() { UtcNow = SampleTime };
        private readonly FakeTradingLogger _logger = new();
        private readonly FakePortfolioState _portfolioState = new() { TotalPortfolioValue = 100_000m };
        private readonly FakeOrderRouter _orderRouter = new();
        private readonly FakeInstrumentMetadata _instrumentMetadata = new();

        private MarketBar BuildBar() =>
            new MarketBar(Btc, 48_000m, 50_000m, 46_000m, 50_000m, 10m, SampleTime);

        private RiskOrchestrator BuildInactiveOrchestrator()
        {
            var eventBus = new DomainEventBus(_logger);
            var coolingOffTracker = new CoolingOffTracker(_clock, TimeSpan.FromHours(24));
            return new RiskOrchestrator(
                Array.Empty<IRiskMonitor>(), new FakeRiskAction(), coolingOffTracker, _clock, _logger, eventBus);
        }

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
            var riskParameters = RiskParameters.FromPercentages(3.0m, 6.0m, 2.0m);
            return new StrategyExecutor(definition, "1h", Btc, strategy, riskParameters);
        }

        private BarProcessingService BuildService(
            MarketRegimeRegistry registry,
            IReadOnlyDictionary<string, StrategyRegimeCompatibility> compatibilities)
        {
            var eventBus = new DomainEventBus(_logger);
            var sizer = new PositionSizer(_portfolioState, _instrumentMetadata, _logger);
            var orchestrator = BuildInactiveOrchestrator();
            var healthMonitor = new FakeStrategyHealthMonitor();

            return new BarProcessingService(
                _portfolioState, _orderRouter, orchestrator, sizer,
                _logger, eventBus, _clock,
                registry, compatibilities, healthMonitor);
        }

        private MarketRegimeRegistry BuildRegistry(RegimeLabel label)
        {
            var classifier = new ConfigurableMarketRegimeClassifier(Btc, label, _clock);
            var registry = new MarketRegimeRegistry(new[] { classifier }, _clock, _logger);
            registry.ClassifyBar(BuildBar());
            return registry;
        }

        private MarketRegimeRegistry BuildEmptyRegistry()
        {
            return new MarketRegimeRegistry(Array.Empty<IMarketRegimeClassifier>(), _clock, _logger);
        }

        private static IReadOnlyDictionary<string, StrategyRegimeCompatibility> CompatibilityFor(
            string executorId, params RegimeLabel[] allowedRegimes)
        {
            var allowed = new HashSet<RegimeLabel>(allowedRegimes);
            return new Dictionary<string, StrategyRegimeCompatibility>
            {
                [executorId] = new StrategyRegimeCompatibility(executorId, allowed)
            };
        }

        [Fact]
        public void ProcessBar_SiRegimenEsCompatible_PermiteEnvioDeOrden()
        {
            // Arrange: classifier devuelve Trend, estrategia acepta [Trend].
            var registry = BuildRegistry(RegimeLabel.Trend);
            var compatibilities = CompatibilityFor(ExecutorId, RegimeLabel.Trend);
            var service = BuildService(registry, compatibilities);
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            // Act
            service.ProcessBar(BuildBar(), new[] { executor });

            // Assert
            _orderRouter.SubmittedOrders.Should().ContainSingle(order =>
                order.Purpose == OrderPurpose.Entry);
        }

        [Fact]
        public void ProcessBar_SiRegimenEsIncompatible_DescartaSenal()
        {
            // Arrange: classifier devuelve HighVolatility, estrategia acepta [Trend].
            var registry = BuildRegistry(RegimeLabel.HighVolatility);
            var compatibilities = CompatibilityFor(ExecutorId, RegimeLabel.Trend);
            var service = BuildService(registry, compatibilities);
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            // Act
            service.ProcessBar(BuildBar(), new[] { executor });

            // Assert: ninguna orden enviada.
            _orderRouter.SubmittedOrders.Should().BeEmpty();
            // Assert: log de régimen descartado presente.
            _logger.DebugEntries.Should().ContainSingle(entry =>
                entry.MessageTemplate.Contains("señal") && entry.MessageTemplate.Contains("descartada"));
        }

        [Fact]
        public void ProcessBar_SiEstrategiaNoTieneCompatibilityRegistrada_PermiteOrden()
        {
            // Arrange: registry devuelve HighVolatility, pero el executor NO está en el diccionario.
            var registry = BuildRegistry(RegimeLabel.HighVolatility);
            var compatibilities = new Dictionary<string, StrategyRegimeCompatibility>();
            var service = BuildService(registry, compatibilities);
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            // Act
            service.ProcessBar(BuildBar(), new[] { executor });

            // Assert: fail-safe — sin registro de compatibilidad, pasa la señal.
            _orderRouter.SubmittedOrders.Should().ContainSingle(order =>
                order.Purpose == OrderPurpose.Entry);
        }

        [Fact]
        public void ProcessBar_SiRegistryDevuelveUnknown_PermiteOrden()
        {
            // Arrange: registry sin clasificadores → GetLastClassification devuelve Unknown.
            var registry = BuildEmptyRegistry();
            var compatibilities = CompatibilityFor(ExecutorId, RegimeLabel.Trend);
            var service = BuildService(registry, compatibilities);
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            // Act
            service.ProcessBar(BuildBar(), new[] { executor });

            // Assert: Unknown siempre pasa (fail-safe).
            _orderRouter.SubmittedOrders.Should().ContainSingle(order =>
                order.Purpose == OrderPurpose.Entry);
        }

        [Fact]
        public void ProcessBar_FiltroDeRegimen_NoSeEvaluaSiSenalEsFlat()
        {
            // Arrange: régimen incompatible con la estrategia, pero la señal es Flat.
            // Si el filtro se saltea correctamente al detectar Flat antes, no debe haber
            // log de descarte por régimen.
            var registry = BuildRegistry(RegimeLabel.HighVolatility);
            var compatibilities = CompatibilityFor(ExecutorId, RegimeLabel.Trend);
            var service = BuildService(registry, compatibilities);
            var executor = BuildExecutor(new FakeStrategy()); // FakeStrategy siempre devuelve Flat.

            // Act
            service.ProcessBar(BuildBar(), new[] { executor });

            // Assert: sin orden (Flat no genera orden).
            _orderRouter.SubmittedOrders.Should().BeEmpty();
            // Assert: sin log de descarte por régimen (el filtro no debe haberse evaluado).
            _logger.DebugEntries.Should().NotContain(entry =>
                entry.MessageTemplate.Contains("descartada") && entry.MessageTemplate.Contains("Régimen"));
        }
    }

    /// <summary>
    /// Estrategia fake configurable que devuelve una señal fija independientemente de la barra.
    /// Necesaria para los tests de BarProcessingService que requieren señales distintas a Flat.
    /// </summary>
    internal sealed class ConfigurableSignalStrategy : IStrategy
    {
        private readonly SignalDirection _signal;

        public ConfigurableSignalStrategy(SignalDirection signal)
        {
            _signal = signal;
        }

        public int WarmUpBars => 0;
        public SignalDirection EvaluateSignal(MarketBar marketBar) => _signal;
    }
}
