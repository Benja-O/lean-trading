using System;
using System.Collections.Generic;
using FluentAssertions;
using Trading.Application.Eventing;
using Trading.Application.Execution;
using Trading.Application.Risk;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Execution
{
    public class OrderLifecycleServiceLiquidateTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly InstrumentId Eth = new("ETHUSDT");
        private static readonly DateTime SampleTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        private const string KillSwitchId = "RiskOrchestrator_KillSwitch";

        private readonly FakeClock _clock = new() { UtcNow = SampleTime };
        private readonly FakeTradingLogger _logger = new();
        private readonly FakeOrderRouter _orderRouter = new();

        private StrategyExecutor BuildExecutor(string strategyName, InstrumentId instrumentId, string timeframe)
        {
            var definition = new StrategyDefinition
            {
                StrategyName = strategyName,
                Symbol = instrumentId.Ticker,
                StopLossPercentage = 3.0m,
                TakeProfitPercentage = 6.0m,
                RiskPerTradePercentage = 2.0m,
                CombineWithTimeExit = false,
                MaxBars = 100
            };
            var riskParams = RiskParameters.FromPercentages(3.0m, 6.0m, 2.0m);
            return new StrategyExecutor(definition, timeframe, instrumentId, new FakeStrategy(), riskParams);
        }

        private (OrderLifecycleService service, CapturingEventSubscriber<OrderFilledEvent> capturer)
            Build(IReadOnlyList<StrategyExecutor> executors)
        {
            var eventBus = new DomainEventBus(_logger);
            var capturer = new CapturingEventSubscriber<OrderFilledEvent>(eventBus);
            var service = new OrderLifecycleService(
                executors,
                new ConsecutiveLossesMonitor(10),
                _orderRouter,
                new PassThroughPriceRounder(),
                _logger,
                eventBus,
                _clock);
            return (service, capturer);
        }

        private OrderLifecycleEvent LiquidateFill(InstrumentId instrumentId, decimal qty = -1m, decimal price = 95_000m)
            => new OrderLifecycleEvent(
                status: OrderEventStatus.Filled,
                purpose: OrderPurpose.Liquidate,
                executorIdentifier: KillSwitchId,
                instrumentId: instrumentId,
                fillQuantity: qty,
                fillPrice: price,
                timestampUtc: _clock.UtcNow);

        [Fact]
        public void LiquidateFill_BroadcastsToAllExecutorsWithMatchingInstrument()
        {
            var btc1h = BuildExecutor("EmaCross", Btc, "1h");
            var btc4h = BuildExecutor("EmaCross", Btc, "4h");
            var eth1h = BuildExecutor("EmaCross", Eth, "1h");

            var (service, capturer) = Build(new[] { btc1h, btc4h, eth1h });

            service.Handle(LiquidateFill(Btc));

            capturer.CapturedEvents.Should().HaveCount(2);
            capturer.CapturedEvents.Should().OnlyContain(e =>
                e.InstrumentId == Btc && e.Purpose == OrderPurpose.Liquidate);
            capturer.CapturedEvents.Should().Contain(e => e.ExecutorIdentifier == btc1h.ExecutorIdentifier);
            capturer.CapturedEvents.Should().Contain(e => e.ExecutorIdentifier == btc4h.ExecutorIdentifier);
            capturer.CapturedEvents.Should().NotContain(e => e.ExecutorIdentifier == eth1h.ExecutorIdentifier);
        }

        [Fact]
        public void LiquidateFill_NoEmiteError_CuandoKillSwitchIdNoEsExecutor()
        {
            var btc1h = BuildExecutor("EmaCross", Btc, "1h");
            var (service, _) = Build(new[] { btc1h });

            service.Handle(LiquidateFill(Btc));

            _logger.ErrorEntries.Should().BeEmpty();
        }

        [Fact]
        public void LiquidateFill_NoEmiteEventos_SiNoHayExecutoresParaElInstrumento()
        {
            var eth1h = BuildExecutor("EmaCross", Eth, "1h");
            var (service, capturer) = Build(new[] { eth1h });

            service.Handle(LiquidateFill(Btc));

            capturer.CapturedEvents.Should().BeEmpty();
            _logger.ErrorEntries.Should().BeEmpty();
        }

        [Fact]
        public void LiquidateCanceled_ExecutorDesconocido_LoguaError_NoBroadcast()
        {
            var btc1h = BuildExecutor("EmaCross", Btc, "1h");
            var (service, capturer) = Build(new[] { btc1h });

            var canceledLiquidate = new OrderLifecycleEvent(
                status: OrderEventStatus.Canceled,
                purpose: OrderPurpose.Liquidate,
                executorIdentifier: KillSwitchId,
                instrumentId: Btc,
                fillQuantity: 0m,
                fillPrice: 0m,
                timestampUtc: _clock.UtcNow);

            service.Handle(canceledLiquidate);

            capturer.CapturedEvents.Should().BeEmpty();
            _logger.ErrorEntries.Should().ContainSingle();
        }

        [Fact]
        public void ExecutorDesconocido_PropositoNoLiquidate_LoguaError()
        {
            var (service, _) = Build(new List<StrategyExecutor>());

            var unknownEvent = new OrderLifecycleEvent(
                status: OrderEventStatus.Filled,
                purpose: OrderPurpose.Entry,
                executorIdentifier: "Unknown_Executor",
                instrumentId: Btc,
                fillQuantity: 1m,
                fillPrice: 95_000m,
                timestampUtc: _clock.UtcNow);

            service.Handle(unknownEvent);

            _logger.ErrorEntries.Should().ContainSingle();
        }
    }

    internal sealed class PassThroughPriceRounder : IPriceRounder
    {
        public decimal Round(InstrumentId instrumentId, decimal price) => price;
    }
}
