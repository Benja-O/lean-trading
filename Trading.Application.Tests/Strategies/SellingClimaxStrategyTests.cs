using FluentAssertions;
using System;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Implementations;
using Xunit;

namespace Trading.Application.Tests.Strategies
{
    public class SellingClimaxStrategyTests
    {
        private static readonly InstrumentId BtcUsdt = new("BTCUSDT");

        private static MarketBar BuildBar(decimal close, int barIndex) =>
            new(BtcUsdt, close,
                new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(barIndex));

        [Fact]
        public void WarmUpBars_ReturnsExpectedValue()
        {
            var strategy = new SellingClimaxStrategy();
            strategy.WarmUpBars.Should().BeGreaterThan(0);
        }

        [Fact]
        public void EvaluateSignal_DuringWarmUp_ReturnsFlat()
        {
            var strategy = new SellingClimaxStrategy();
            for (int i = 0; i < strategy.WarmUpBars - 1; i++)
            {
                strategy.EvaluateSignal(BuildBar(100m, i))
                    .Should().Be(SignalDirection.Flat,
                        because: $"Barra {i} esta en periodo de warm-up.");
            }
        }

        [Fact]
        public void EvaluateSignal_TODO_DescribeScenario()
        {
            // TODO: implementar test para el escenario principal de senal
            var strategy = new SellingClimaxStrategy();
            Assert.True(false, "Test pendiente de implementacion.");
        }
    }
}