using FluentAssertions;
using Trading.Application.Risk;
using Trading.Domain.Events;
using Xunit;

namespace Trading.Application.Tests.Risk
{
    public class ConsecutiveLossesMonitorTests
    {
        [Fact]
        public void Evaluate_NoLossesRegistered_ReturnsPass()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_BelowThreshold_ReturnsPass()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            monitor.RegisterLoss();
            monitor.RegisterLoss(); // 2 pérdidas, límite 3

            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_AtThreshold_TriggersKillSwitch()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            monitor.RegisterLoss();
            monitor.RegisterLoss();
            monitor.RegisterLoss();

            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeTrue();
            assessment.Reason.Should().Be(RiskLimitBreachReason.ConsecutiveLossesExceeded);
        }

        [Fact]
        public void RegisterWin_ResetsCounter()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            monitor.RegisterLoss();
            monitor.RegisterLoss();
            monitor.RegisterWin();
            monitor.RegisterLoss(); // counter ahora en 1

            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Reset_ClearsCounterAndTriggerState()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            monitor.RegisterLoss();
            monitor.RegisterLoss();
            monitor.RegisterLoss();
            monitor.Reset();

            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }
    }
}
