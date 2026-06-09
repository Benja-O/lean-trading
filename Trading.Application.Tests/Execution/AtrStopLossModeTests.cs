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
    /// Verifica el flujo completo del modo ATR SL/TP:
    /// 1. BarProcessingService computa PendingStopLossPrice / PendingTakeProfitPrice al momento de la señal.
    /// 2. OrderLifecycleService usa esos precios (en lugar de RiskParameters) al recibir el fill de entrada.
    /// 3. En modo Percentage, el comportamiento previo no cambia.
    /// </summary>
    public class AtrStopLossModeTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime SampleTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private const string AtrExecutorId = "AtrBreakout_BTCUSDT_4h";
        private const string PctExecutorId  = "EmaCross_BTCUSDT_1h";

        private readonly FakeClock _clock = new() { UtcNow = SampleTime };
        private readonly FakeTradingLogger _logger = new();
        private readonly FakeOrderRouter _orderRouter = new();
        private readonly FakeInstrumentMetadata _instrumentMetadata = new();

        // ─── Helpers ─────────────────────────────────────────────────────────

        private MarketBar BuildBar(decimal close = 50_000m) =>
            new MarketBar(Btc, 48_000m, 51_000m, 47_000m, close, 10m, SampleTime);

        private StrategyExecutor BuildAtrExecutor(IStrategy strategy, decimal slMult = 2.5m, decimal tpMult = 3.5m)
        {
            var def = new StrategyDefinition
            {
                StrategyName = "AtrBreakout",
                Symbol = "BTCUSDT",
                StopTakeMode = "Atr",
                StopLossPercentage = 3.5m,
                TakeProfitPercentage = 6.0m,
                StopLossAtrMultiplier = slMult,
                TakeProfitAtrMultiplier = tpMult,
                RiskPerTradePercentage = 2.0m,
                CombineWithTimeExit = false,
                MaxBars = 100
            };
            return new StrategyExecutor(def, "4h", Btc, strategy, RiskParameters.FromPercentages(3.5m, 6.0m, 2.0m));
        }

        private StrategyExecutor BuildPercentageExecutor(IStrategy strategy)
        {
            var def = new StrategyDefinition
            {
                StrategyName = "EmaCross",
                Symbol = "BTCUSDT",
                StopTakeMode = "Percentage",
                StopLossPercentage = 2.0m,
                TakeProfitPercentage = 4.0m,
                StopLossAtrMultiplier = 0m,
                TakeProfitAtrMultiplier = 0m,
                RiskPerTradePercentage = 2.0m,
                CombineWithTimeExit = false,
                MaxBars = 100
            };
            return new StrategyExecutor(def, "1h", Btc, strategy, RiskParameters.FromPercentages(2.0m, 4.0m, 2.0m));
        }

        private FakePortfolioState BuildPortfolioState()
        {
            var state = new FakePortfolioState { TotalPortfolioValue = 100_000m };
            _instrumentMetadata.SetLotSize(Btc, 0.001m);
            return state;
        }

        private RiskOrchestrator BuildRiskOrchestrator(IDomainEventBus bus) =>
            new RiskOrchestrator(
                new List<IRiskMonitor>(),
                new FakeRiskAction(),
                new CoolingOffTracker(_clock, TimeSpan.Zero),
                _clock, _logger, bus);

        private BarProcessingService BuildBarService(FakePortfolioState portfolioState)
        {
            var bus = new DomainEventBus(_logger);
            var sizer = new PositionSizer(portfolioState, _instrumentMetadata, _logger);
            return new BarProcessingService(
                portfolioState, _orderRouter, BuildRiskOrchestrator(bus), sizer,
                _logger, bus, _clock,
                new MarketRegimeRegistry(new List<IMarketRegimeClassifier>(), _clock, _logger),
                new Dictionary<string, StrategyRegimeCompatibility>(),
                new FakeStrategyHealthMonitor());
        }

        private OrderLifecycleService BuildLifecycleService(IReadOnlyList<StrategyExecutor> executors)
        {
            var bus = new DomainEventBus(_logger);
            return new OrderLifecycleService(
                executors,
                new ConsecutiveLossesMonitor(5),
                _orderRouter,
                new LocalFakePriceRounder(),
                _logger, bus, _clock, BuildRiskOrchestrator(bus));
        }

        private OrderLifecycleEvent BuildEntryFillEvent(string executorId, decimal fillPrice, decimal fillQty) =>
            new OrderLifecycleEvent(
                status: OrderEventStatus.Filled,
                purpose: OrderPurpose.Entry,
                executorIdentifier: executorId,
                instrumentId: Btc,
                fillQuantity: fillQty,
                fillPrice: fillPrice,
                timestampUtc: SampleTime);

        // ─── Tests: BarProcessingService captura pending prices ───────────────

        [Fact]
        public void BarProcessing_ModoAtr_LongSignal_SetsPendingPricesCorrectamente()
        {
            const decimal atr = 1_000m;
            const decimal close = 50_000m;
            var strategy = new LocalFakeAtrStrategy(SignalDirection.Long, atr);
            var executor = BuildAtrExecutor(strategy, slMult: 2.5m, tpMult: 3.5m);
            var portfolioState = BuildPortfolioState();
            var service = BuildBarService(portfolioState);

            service.ProcessBar(BuildBar(close), new[] { executor });

            executor.PendingStopLossPrice.Should().Be(close - 2.5m * atr);   // 47_500
            executor.PendingTakeProfitPrice.Should().Be(close + 3.5m * atr); // 53_500
        }

        [Fact]
        public void BarProcessing_ModoAtr_ShortSignal_SetsPendingPricesCorrectamente()
        {
            const decimal atr = 1_000m;
            const decimal close = 50_000m;
            var strategy = new LocalFakeAtrStrategy(SignalDirection.Short, atr);
            var executor = BuildAtrExecutor(strategy, slMult: 2.5m, tpMult: 3.5m);
            var portfolioState = BuildPortfolioState();
            var service = BuildBarService(portfolioState);

            service.ProcessBar(BuildBar(close), new[] { executor });

            executor.PendingStopLossPrice.Should().Be(close + 2.5m * atr);   // 52_500
            executor.PendingTakeProfitPrice.Should().Be(close - 3.5m * atr); // 46_500
        }

        [Fact]
        public void BarProcessing_ModoPercentage_NoCambiaComportamiento_NoPendingPrices()
        {
            var strategy = new LocalFakeAtrStrategy(SignalDirection.Long, atr: 999m);
            var executor = BuildPercentageExecutor(strategy);
            var portfolioState = BuildPortfolioState();
            var service = BuildBarService(portfolioState);

            service.ProcessBar(BuildBar(), new[] { executor });

            executor.PendingStopLossPrice.Should().BeNull();
            executor.PendingTakeProfitPrice.Should().BeNull();
        }

        [Fact]
        public void BarProcessing_ModoAtr_AtrCero_NoPendingPrices()
        {
            var strategy = new LocalFakeAtrStrategy(SignalDirection.Long, atr: 0m);
            var executor = BuildAtrExecutor(strategy);
            var portfolioState = BuildPortfolioState();
            var service = BuildBarService(portfolioState);

            service.ProcessBar(BuildBar(), new[] { executor });

            executor.PendingStopLossPrice.Should().BeNull();
            executor.PendingTakeProfitPrice.Should().BeNull();
        }

        // ─── Tests: OrderLifecycleService usa pending prices ─────────────────

        [Fact]
        public void OrderLifecycle_CuandoPendingPricesSet_UsaPreciosATR()
        {
            var executor = BuildAtrExecutor(new FakeStrategy());
            executor.PendingStopLossPrice = 47_500m;
            executor.PendingTakeProfitPrice = 53_500m;

            BuildLifecycleService(new[] { executor })
                .Handle(BuildEntryFillEvent(AtrExecutorId, fillPrice: 50_100m, fillQty: 0.1m));

            _orderRouter.SubmittedOrders
                .Should().Contain(o => o.Purpose == OrderPurpose.StopLoss && o.Price == 47_500m);
            _orderRouter.SubmittedOrders
                .Should().Contain(o => o.Purpose == OrderPurpose.TakeProfit && o.Price == 53_500m);
        }

        [Fact]
        public void OrderLifecycle_CuandoPendingPricesSet_LimpiaPendingPreciosTrasElFill()
        {
            var executor = BuildAtrExecutor(new FakeStrategy());
            executor.PendingStopLossPrice = 47_500m;
            executor.PendingTakeProfitPrice = 53_500m;

            BuildLifecycleService(new[] { executor })
                .Handle(BuildEntryFillEvent(AtrExecutorId, fillPrice: 50_100m, fillQty: 0.1m));

            executor.PendingStopLossPrice.Should().BeNull();
            executor.PendingTakeProfitPrice.Should().BeNull();
        }

        [Fact]
        public void OrderLifecycle_SinPendingPrices_UsaRiskParametersPorcentaje()
        {
            // SL=2%, TP=4% sobre fill de 50_000
            var executor = BuildPercentageExecutor(new FakeStrategy());

            BuildLifecycleService(new[] { executor })
                .Handle(BuildEntryFillEvent(PctExecutorId, fillPrice: 50_000m, fillQty: 0.1m));

            _orderRouter.SubmittedOrders
                .Should().Contain(o => o.Purpose == OrderPurpose.StopLoss  && o.Price == 49_000m);
            _orderRouter.SubmittedOrders
                .Should().Contain(o => o.Purpose == OrderPurpose.TakeProfit && o.Price == 52_000m);
        }

        // ─── Tests: StrategyExecutor.ResetState ──────────────────────────────

        [Fact]
        public void ResetState_LimpiaPendingPrecios()
        {
            var executor = BuildAtrExecutor(new FakeStrategy());
            executor.PendingStopLossPrice = 47_500m;
            executor.PendingTakeProfitPrice = 53_500m;

            executor.ResetState();

            executor.PendingStopLossPrice.Should().BeNull();
            executor.PendingTakeProfitPrice.Should().BeNull();
        }
    }

    // ─── Fakes locales ────────────────────────────────────────────────────────

    internal sealed class LocalFakeAtrStrategy : IStrategy, IAtrProvider
    {
        private readonly SignalDirection _direction;
        private readonly decimal _atr;

        internal LocalFakeAtrStrategy(SignalDirection direction, decimal atr)
        {
            _direction = direction;
            _atr = atr;
        }

        public int WarmUpBars => 0;
        public SignalDirection EvaluateSignal(MarketBar marketBar) => _direction;
        public decimal GetLastAtr(string ticker) => _atr;
    }

    internal sealed class LocalFakePriceRounder : IPriceRounder
    {
        public decimal Round(InstrumentId instrumentId, decimal price) => price;
    }
}
