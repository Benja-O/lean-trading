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
#nullable enable

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

        private RiskOrchestrator BuildOrchestrator(bool activateKillSwitch = false)
        {
            var eventBus = new DomainEventBus(_logger);
            var orchestrator = new RiskOrchestrator(
                System.Array.Empty<IRiskMonitor>(),
                new FakeRiskAction(),
                new CoolingOffTracker(_clock, TimeSpan.FromDays(1)),
                _clock,
                _logger,
                eventBus);
            if (activateKillSwitch)
                orchestrator.ActivateKillSwitchManually("Test");
            return orchestrator;
        }

        private (OrderLifecycleService service, CapturingEventSubscriber<OrderFilledEvent> capturer)
            Build(IReadOnlyList<StrategyExecutor> executors, RiskOrchestrator? riskOrchestrator = null)
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
                _clock,
                riskOrchestrator ?? BuildOrchestrator());
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

        // ── Tests ADR-029: race Entry pending / kill switch ──────────────────

        [Fact]
        public void HandleEntryFill_WhenKillSwitchInactive_EmitsStopLossAndTakeProfit()
        {
            var executor = BuildExecutor("EmaCross", Eth, "1h");
            var (service, _) = Build(new[] { executor }, BuildOrchestrator(activateKillSwitch: false));

            var entryFill = new OrderLifecycleEvent(
                status: OrderEventStatus.Filled,
                purpose: OrderPurpose.Entry,
                executorIdentifier: executor.ExecutorIdentifier,
                instrumentId: Eth,
                fillQuantity: 10m,
                fillPrice: 3_000m,
                timestampUtc: _clock.UtcNow);

            service.Handle(entryFill);

            _orderRouter.SubmittedOrders.Should().Contain(o => o.Purpose == OrderPurpose.StopLoss);
            _orderRouter.SubmittedOrders.Should().Contain(o => o.Purpose == OrderPurpose.TakeProfit);
            _orderRouter.LiquidateInstrumentCalls.Should().BeEmpty();
        }

        [Fact]
        public void HandleEntryFill_WhenKillSwitchActive_LiquidatesAndDoesNotEmitBrackets()
        {
            var executor = BuildExecutor("EmaCross", Eth, "1h");
            var (service, _) = Build(new[] { executor }, BuildOrchestrator(activateKillSwitch: true));

            var entryFill = new OrderLifecycleEvent(
                status: OrderEventStatus.Filled,
                purpose: OrderPurpose.Entry,
                executorIdentifier: executor.ExecutorIdentifier,
                instrumentId: Eth,
                fillQuantity: 10m,
                fillPrice: 3_000m,
                timestampUtc: _clock.UtcNow);

            service.Handle(entryFill);

            _orderRouter.LiquidateInstrumentCalls.Should().ContainSingle(c =>
                c.InstrumentId == Eth &&
                c.Purpose == OrderPurpose.Liquidate &&
                c.ExecutorIdentifier == executor.ExecutorIdentifier);
            _orderRouter.SubmittedOrders.Should().NotContain(o =>
                o.Purpose == OrderPurpose.StopLoss || o.Purpose == OrderPurpose.TakeProfit);
            _logger.CriticalEntries.Should().ContainSingle(e => e.MessageTemplate.Contains("cooling-off"));
        }

        [Fact]
        public void Handle_LiquidateFilled_CancelsBracketsAndResetsState()
        {
            var executor = BuildExecutor("EmaCross", Eth, "1h");
            var (service, _) = Build(new[] { executor });

            var slHandle = new FakeOrderHandle();
            var tpHandle = new FakeOrderHandle();
            executor.StopLossOrderHandle = slHandle;
            executor.TakeProfitOrderHandle = tpHandle;

            var liquidateFill = new OrderLifecycleEvent(
                status: OrderEventStatus.Filled,
                purpose: OrderPurpose.Liquidate,
                executorIdentifier: executor.ExecutorIdentifier,
                instrumentId: Eth,
                fillQuantity: -10m,
                fillPrice: 2_950m,
                timestampUtc: _clock.UtcNow);

            service.Handle(liquidateFill);

            slHandle.CancelCallCount.Should().Be(1);
            tpHandle.CancelCallCount.Should().Be(1);
            executor.HasActivePosition.Should().BeFalse();
        }
    }

    internal sealed class PassThroughPriceRounder : IPriceRounder
    {
        public decimal Round(InstrumentId instrumentId, decimal price) => price;
    }
}
