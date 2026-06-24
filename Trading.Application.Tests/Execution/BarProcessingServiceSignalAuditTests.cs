using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Trading.Application.Tests.Execution
{
    /// <summary>
    /// ADR-052 — Observabilidad de señal. Verifica que BarProcessingService emite un evento de log
    /// "SignalEmitted" estructurado por cada señal no-Flat, con las features microestructurales de la
    /// barra (del provider, genérico) y la condición evaluada por la estrategia (si expone
    /// ISignalDiagnosticsProvider). No debe emitirse en Flat ni durante warmup.
    /// </summary>
    public class BarProcessingServiceSignalAuditTests
    {
        private static readonly InstrumentId Eth = new("ETHUSDT");
        private static readonly DateTime SampleTime = new DateTime(2026, 6, 23, 16, 0, 0, DateTimeKind.Utc);

        private readonly FakeClock _clock = new() { UtcNow = SampleTime };
        private readonly FakeTradingLogger _logger = new();
        private readonly FakeOrderRouter _orderRouter = new();
        private readonly FakeInstrumentMetadata _instrumentMetadata = new();

        private MarketBar BuildBar() =>
            new MarketBar(Eth, 1650m, 1660m, 1648m, 1658.9m, 123.4m, SampleTime);

        private MicrostructureBar BuildMsBar() =>
            new MicrostructureBar(Eth, SampleTime,
                ofi: 0.045,
                cvdDelta: 3659.0,
                cvd: 120000.0,
                arrivalRate: 1500.0,
                meanTradeSize: 2.78,
                buySellRatio: 1.045,
                priceReturn: 0.0053);

        private StrategyExecutor BuildExecutor(IStrategy strategy)
        {
            var definition = new StrategyDefinition
            {
                StrategyName = "TestStrat",
                Symbol = "ETHUSDT",
                StopLossPercentage = 5.0m,
                TakeProfitPercentage = 10.0m,
                RiskPerTradePercentage = 2.0m,
                CombineWithTimeExit = false,
                MaxBars = 8
            };
            var riskParams = RiskParameters.FromPercentages(5.0m, 10.0m, 2.0m);
            return new StrategyExecutor(definition, "1h", Eth, strategy, riskParams);
        }

        private BarProcessingService BuildService(
            IReadOnlyDictionary<string, IMicrostructureProvider>? microstructureByTimeframe)
        {
            var portfolioState = new FakePortfolioState { TotalPortfolioValue = 100_000m };
            var eventBus = new DomainEventBus(_logger);
            var coolingOff = new CoolingOffTracker(_clock, TimeSpan.FromHours(24));
            var orchestrator = new RiskOrchestrator(
                Array.Empty<IRiskMonitor>(), new FakeRiskAction(), coolingOff, _clock, _logger, eventBus);
            var sizer = new PositionSizer(portfolioState, _instrumentMetadata, _logger);
            var registry = new MarketRegimeRegistry(
                Array.Empty<IMarketRegimeClassifier>(), _clock, _logger);
            var compatibilities = new Dictionary<string, StrategyRegimeCompatibility>();
            var healthMonitor = new FakeStrategyHealthMonitor();

            return new BarProcessingService(
                portfolioState, _orderRouter, orchestrator, sizer,
                _logger, eventBus, _clock, registry, compatibilities, healthMonitor,
                microstructureByTimeframe);
        }

        private IReadOnlyDictionary<string, IMicrostructureProvider> MapWith(MicrostructureBar bar)
        {
            var provider = new FakeMicrostructureProvider();
            provider.Add(bar);
            return new Dictionary<string, IMicrostructureProvider> { ["1h"] = provider };
        }

        private CapturedLogEntry? SignalEmittedEntry() =>
            _logger.InfoEntries.FirstOrDefault(e => e.MessageTemplate.StartsWith("SignalEmitted"));

        [Fact]
        public void SeñalNoFlat_LogueaSignalEmittedConFeaturesDelProvider()
        {
            var service = BuildService(MapWith(BuildMsBar()));
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            service.ProcessBar(BuildBar(), new[] { executor });

            var entry = SignalEmittedEntry();
            entry.Should().NotBeNull();
            // Identidad + dirección + OHLC + features quedan en los argumentos posicionales (properties JSONL).
            entry!.Arguments.Should().Contain(executor.ExecutorIdentifier);
            entry.Arguments.Should().Contain(SignalDirection.Long);
            entry.Arguments.Should().Contain(1658.9m);   // close
            entry.Arguments.Should().Contain(3659.0);    // cvdDelta
            entry.Arguments.Should().Contain(2.78);      // meanTradeSize
            entry.Arguments.Should().Contain(1.045);     // buySellRatio
            entry.Arguments.Should().Contain(true);      // tieneMicroestructura
        }

        [Fact]
        public void SeñalFlat_NoLogueaSignalEmitted()
        {
            var service = BuildService(MapWith(BuildMsBar()));
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Flat));

            service.ProcessBar(BuildBar(), new[] { executor });

            SignalEmittedEntry().Should().BeNull();
        }

        [Fact]
        public void DuranteWarmup_NoLogueaSignalEmitted()
        {
            var service = BuildService(MapWith(BuildMsBar()));
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            service.ProcessBar(BuildBar(), new[] { executor }, isWarmingUp: true);

            SignalEmittedEntry().Should().BeNull();
        }

        [Fact]
        public void EstrategiaConDiagnostics_IncluyeLaCondicionEnElLog()
        {
            var diagnostics = new SignalDiagnostics(
                "TestStrat Long: condición cumplida.",
                new List<SignalCondition>
                {
                    new("CloseIsMin48", 1658.9, "<=", 1653.63, false),
                });
            var service = BuildService(MapWith(BuildMsBar()));
            var executor = BuildExecutor(
                new ConfigurableDiagnosticsStrategy(SignalDirection.Long, diagnostics));

            service.ProcessBar(BuildBar(), new[] { executor });

            var entry = SignalEmittedEntry();
            entry.Should().NotBeNull();
            string conditions = entry!.Arguments.OfType<string>()
                .Single(a => a.Contains("CloseIsMin48"));
            conditions.Should().Contain("TestStrat Long: condición cumplida.");
            conditions.Should().Contain("CloseIsMin48");
            conditions.Should().Contain("1653.63");
        }

        [Fact]
        public void EstrategiaSinDiagnostics_LogueaCondicionNoDisponible()
        {
            var service = BuildService(MapWith(BuildMsBar()));
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            service.ProcessBar(BuildBar(), new[] { executor });

            var entry = SignalEmittedEntry();
            entry.Should().NotBeNull();
            entry!.Arguments.Should().Contain("(n/d)");
        }

        [Fact]
        public void SinProviderDeMicroestructura_LogueaSignalSinFeatures()
        {
            // Sin mapa de providers (ej. deployment sin microestructura): la señal igual se audita,
            // con tieneMicroestructura=false y features null.
            var service = BuildService(microstructureByTimeframe: null);
            var executor = BuildExecutor(new ConfigurableSignalStrategy(SignalDirection.Long));

            service.ProcessBar(BuildBar(), new[] { executor });

            var entry = SignalEmittedEntry();
            entry.Should().NotBeNull();
            entry!.Arguments.Should().Contain(false);            // tieneMicroestructura
            entry.Arguments.Should().Contain(1658.9m);           // el OHLC sigue presente
        }
    }

    internal sealed class ConfigurableDiagnosticsStrategy : IStrategy, ISignalDiagnosticsProvider
    {
        private readonly SignalDirection _signal;
        private readonly SignalDiagnostics _diagnostics;

        public ConfigurableDiagnosticsStrategy(SignalDirection signal, SignalDiagnostics diagnostics)
        {
            _signal = signal;
            _diagnostics = diagnostics;
        }

        public int WarmUpBars => 0;
        public SignalDirection EvaluateSignal(MarketBar marketBar) => _signal;
        public SignalDiagnostics? DescribeLastEvaluation() => _diagnostics;
    }
}
