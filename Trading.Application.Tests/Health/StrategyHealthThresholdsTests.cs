using System;
using FluentAssertions;
using Trading.Application.Health;
using Xunit;

namespace Trading.Application.Tests.Health
{
    public class StrategyHealthThresholdsTests
    {
        [Fact]
        public void FromPolicyDefaults_RetornaValoresDePolicy3_1()
        {
            var t = StrategyHealthThresholds.FromPolicyDefaults();

            t.AbsoluteDrawdownFromAthFraction.Should().Be(0.25m);
            t.RollingDrawdownThirtyDaysFraction.Should().Be(0.15m);
            t.RollingDrawdownSustainedDays.Should().Be(5);
            t.RollingProfitFactorThreshold.Should().Be(1.0m);
            t.RollingProfitFactorSustainedTrades.Should().Be(10);
            t.RollingExpectancyThreshold.Should().Be(0m);
            t.RollingExpectancySustainedTrades.Should().Be(10);
            t.MinimumTradesToArmRollingThresholds.Should().Be(50);
            t.RollingWindowTrades.Should().Be(30);
            t.RollingWindowDays.Should().Be(30);
        }

        [Fact]
        public void Constructor_ConValorNegativoEnDdAbsoluto_LanzaExcepcion()
        {
            var act = () => new StrategyHealthThresholds(
                absoluteDrawdownFromAthFraction: -0.1m,
                rollingDrawdownThirtyDaysFraction: 0.15m,
                rollingDrawdownSustainedDays: 5,
                rollingProfitFactorThreshold: 1.0m,
                rollingProfitFactorSustainedTrades: 10,
                rollingExpectancyThreshold: 0m,
                rollingExpectancySustainedTrades: 10,
                minimumTradesToArmRollingThresholds: 50,
                rollingWindowTrades: 30,
                rollingWindowDays: 30);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*AbsoluteDrawdownFromAthFraction*");
        }

        [Fact]
        public void Constructor_ConSustainedDaysCero_LanzaExcepcion()
        {
            var act = () => new StrategyHealthThresholds(
                absoluteDrawdownFromAthFraction: 0.25m,
                rollingDrawdownThirtyDaysFraction: 0.15m,
                rollingDrawdownSustainedDays: 0,
                rollingProfitFactorThreshold: 1.0m,
                rollingProfitFactorSustainedTrades: 10,
                rollingExpectancyThreshold: 0m,
                rollingExpectancySustainedTrades: 10,
                minimumTradesToArmRollingThresholds: 50,
                rollingWindowTrades: 30,
                rollingWindowDays: 30);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*RollingDrawdownSustainedDays*");
        }

        [Fact]
        public void Constructor_ConExpectancyThresholdCero_NoLanza()
        {
            // 0m es el valor de POLICY 3.1, debe ser válido.
            var act = () => StrategyHealthThresholds.FromPolicyDefaults();

            act.Should().NotThrow();
        }
    }
}
