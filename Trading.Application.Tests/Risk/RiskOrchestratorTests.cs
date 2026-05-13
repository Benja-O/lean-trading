using FluentAssertions;
using System;
using System.Collections.Generic;
using Trading.Application.Eventing;
using Trading.Application.Risk;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;
using Xunit;

namespace Trading.Application.Tests.Risk
{
    public class RiskOrchestratorTests
    {
        private readonly FakeClock _clock = new();
        private readonly FakeTradingLogger _logger = new();
        private readonly FakeRiskAction _riskAction = new();
        private readonly DomainEventBus _eventBus;

        public RiskOrchestratorTests()
        {
            _eventBus = new DomainEventBus(_logger);
        }

        private RiskOrchestrator BuildOrchestrator(IEnumerable<IRiskMonitor> monitors, TimeSpan? coolingOff = null)
        {
            var tracker = new CoolingOffTracker(_clock, coolingOff ?? TimeSpan.FromHours(24));
            return new RiskOrchestrator(monitors, _riskAction, tracker, _clock, _logger, _eventBus);
        }

        [Fact]
        public void EvaluateAllMonitors_AllPass_DoesNothing()
        {
            var monitor = new FakeRiskMonitor();
            var orchestrator = BuildOrchestrator(new[] { monitor });

            orchestrator.EvaluateAllMonitors();

            orchestrator.IsKillSwitchActivated.Should().BeFalse();
            _riskAction.ExecuteCallCount.Should().Be(0);
        }

        [Fact]
        public void EvaluateAllMonitors_OneTriggers_ActivatesKillSwitch()
        {
            var monitor = new FakeRiskMonitor("DrawdownFake")
            {
                NextAssessment = RiskAssessment.Trigger(RiskLimitBreachReason.MaximumDrawdownExceeded, "test")
            };
            var orchestrator = BuildOrchestrator(new[] { monitor });

            var captured = new CapturingEventSubscriber<RiskLimitBreachedEvent>(_eventBus);
            orchestrator.EvaluateAllMonitors();

            orchestrator.IsKillSwitchActivated.Should().BeTrue();
            _riskAction.ExecuteCallCount.Should().Be(1);
            captured.CapturedEvents.Should().HaveCount(1);
            captured.CapturedEvents[0].Reason.Should().Be(RiskLimitBreachReason.MaximumDrawdownExceeded);
        }

        [Fact]
        public void EvaluateAllMonitors_WhenKillSwitchActive_SkipsMonitorEvaluation()
        {
            var monitor = new FakeRiskMonitor
            {
                NextAssessment = RiskAssessment.Trigger(RiskLimitBreachReason.Manual, "")
            };
            var orchestrator = BuildOrchestrator(new[] { monitor });

            orchestrator.EvaluateAllMonitors(); // activa
            int callsBeforeSecondEvaluate = monitor.EvaluateCallCount;

            orchestrator.EvaluateAllMonitors(); // ya activo, no debe llamar a monitors
            monitor.EvaluateCallCount.Should().Be(callsBeforeSecondEvaluate);
        }

        [Fact]
        public void EvaluateAllMonitors_AfterCoolingOffExpires_DeactivatesAndResetsMonitors()
        {
            _clock.UtcNow = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var monitor = new FakeRiskMonitor
            {
                NextAssessment = RiskAssessment.Trigger(RiskLimitBreachReason.Manual, "")
            };
            var orchestrator = BuildOrchestrator(new[] { monitor }, coolingOff: TimeSpan.FromHours(1));

            orchestrator.EvaluateAllMonitors(); // activa
            orchestrator.IsKillSwitchActivated.Should().BeTrue();

            _clock.UtcNow = new DateTime(2025, 1, 1, 13, 1, 0, DateTimeKind.Utc); // +1h 1min
            orchestrator.EvaluateAllMonitors();

            orchestrator.IsKillSwitchActivated.Should().BeFalse();
            monitor.ResetCallCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public void ActivateKillSwitchManually_UsesManualReason()
        {
            var orchestrator = BuildOrchestrator(new List<IRiskMonitor>());
            var captured = new CapturingEventSubscriber<RiskLimitBreachedEvent>(_eventBus);

            orchestrator.ActivateKillSwitchManually("test reason");

            orchestrator.IsKillSwitchActivated.Should().BeTrue();
            captured.CapturedEvents[0].Reason.Should().Be(RiskLimitBreachReason.Manual);
            captured.CapturedEvents[0].Description.Should().Be("test reason");
        }
    }
}
