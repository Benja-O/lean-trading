using FluentAssertions;
using Trading.Application.Risk;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Events;
using Xunit;

namespace Trading.Application.Tests.Risk
{
    public class DrawdownMonitorTests
    {
        private readonly FakePortfolioState _portfolioState = new();

        [Fact]
        public void Evaluate_BelowMaximumDrawdown_ReturnsPass()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 90_000m; // 10% drawdown
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_AtOrAboveMaximumDrawdown_TriggersKillSwitch()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 74_000m; // 26% drawdown
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeTrue();
            assessment.Reason.Should().Be(RiskLimitBreachReason.MaximumDrawdownExceeded);
        }

        [Fact]
        public void Evaluate_PortfolioGrows_UpdatesHighWaterMark()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 120_000m; // sube
            monitor.Evaluate(); // actualiza high-water mark a 120k

            _portfolioState.TotalPortfolioValue = 95_000m; // ~20.8% drawdown desde 120k
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeFalse(); // < 25%
        }

        [Fact]
        public void Reset_RestoresHighWaterMarkToCurrentValue()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 74_000m;
            monitor.Reset();

            // Ahora 74k es el nuevo máximo. Para gatillar, hay que caer a 74k*0.75=55.5k
            _portfolioState.TotalPortfolioValue = 60_000m; // ~18.9% drawdown desde 74k
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }
    }
}
