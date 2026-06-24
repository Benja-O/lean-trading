using FluentAssertions;
using System;
using System.Linq;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Implementations;
using Xunit;

namespace Trading.Application.Tests.Strategies
{
    public class TradeSizeInstitutionalStrategyTests
    {
        private static readonly InstrumentId BtcUsdt = new("BTCUSDT");
        private static readonly DateTime BaseTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static MarketBar BuildBar(decimal close, int barIndex) =>
            new(BtcUsdt, close, BaseTime.AddHours(barIndex));

        private static MicrostructureBar BuildMsBar(int barIndex, double meanTradeSize, double buySellRatio) =>
            new(BtcUsdt, BaseTime.AddHours(barIndex),
                ofi: 0, cvdDelta: 0, cvd: 0, arrivalRate: 100,
                meanTradeSize: meanTradeSize, buySellRatio: buySellRatio, priceReturn: 0);

        [Fact]
        public void WarmUpBars_ReturnsExpectedValue()
        {
            var strategy = new TradeSizeInstitutionalStrategy();
            strategy.WarmUpBars.Should().BeGreaterThan(0);
        }

        [Fact]
        public void EvaluateSignal_DuringWarmUp_ReturnsFlat()
        {
            var strategy = new TradeSizeInstitutionalStrategy();
            for (int i = 0; i < strategy.WarmUpBars - 1; i++)
            {
                strategy.EvaluateSignal(BuildBar(100m, i))
                    .Should().Be(SignalDirection.Flat,
                        because: $"Barra {i} esta en periodo de warm-up.");
            }
        }

        [Fact]
        public void EvaluateSignal_InstitutionalAccumulation_ReturnsLong()
        {
            // H5: MeanTradeSize en P90 del historial Y BuySellRatio > 1.02 → Long.
            // Se llenan 25 barras con meanTradeSize=1.0 (baseline) para que la siguiente
            // barra con meanTradeSize=10.0 caiga en el percentil 100 de la ventana de 24.
            var provider = new FakeMicrostructureProvider();
            var strategy = new TradeSizeInstitutionalStrategy(provider);

            for (int i = 0; i < 25; i++)
            {
                provider.Add(BuildMsBar(i, meanTradeSize: 1.0, buySellRatio: 1.0));
                strategy.EvaluateSignal(BuildBar(100m, i));
            }

            provider.Add(BuildMsBar(25, meanTradeSize: 10.0, buySellRatio: 1.05));
            strategy.EvaluateSignal(BuildBar(100m, 25))
                .Should().Be(SignalDirection.Long);
        }

        [Fact]
        public void EvaluateSignal_LowBuySellRatio_ReturnsFlat()
        {
            // MeanTradeSize institucional pero BSR <= threshold → no hay acumulación neta → Flat.
            var provider = new FakeMicrostructureProvider();
            var strategy = new TradeSizeInstitutionalStrategy(provider);

            for (int i = 0; i < 25; i++)
            {
                provider.Add(BuildMsBar(i, meanTradeSize: 1.0, buySellRatio: 1.0));
                strategy.EvaluateSignal(BuildBar(100m, i));
            }

            provider.Add(BuildMsBar(25, meanTradeSize: 10.0, buySellRatio: 1.0));
            strategy.EvaluateSignal(BuildBar(100m, 25))
                .Should().Be(SignalDirection.Flat);
        }

        [Fact]
        public void DescribeLastEvaluation_AlDispararLong_ExponeAmbasCondicionesSatisfechas()
        {
            // ADR-052: rationale con mean_trade_size≥P90 (satisfecho) y bsr>1.02 (satisfecho).
            var provider = new FakeMicrostructureProvider();
            var strategy = new TradeSizeInstitutionalStrategy(provider);

            for (int i = 0; i < 25; i++)
            {
                provider.Add(BuildMsBar(i, meanTradeSize: 1.0, buySellRatio: 1.0));
                strategy.EvaluateSignal(BuildBar(100m, i));
            }
            provider.Add(BuildMsBar(25, meanTradeSize: 10.0, buySellRatio: 1.05));
            strategy.EvaluateSignal(BuildBar(100m, 25));

            var diagnostics = ((ISignalDiagnosticsProvider)strategy).DescribeLastEvaluation();
            diagnostics.Should().NotBeNull();
            var sizeCond = diagnostics!.Conditions.Single(c => c.Name == "MeanTradeSizeGeP90");
            sizeCond.Value.Should().Be(10.0);
            sizeCond.Satisfied.Should().BeTrue();
            diagnostics.Conditions.Single(c => c.Name == "BuySellRatioAboveThreshold").Satisfied.Should().BeTrue();
        }

        [Fact]
        public void DescribeLastEvaluation_BsrBajo_MarcaBuySellRatioComoNoSatisfecho()
        {
            var provider = new FakeMicrostructureProvider();
            var strategy = new TradeSizeInstitutionalStrategy(provider);

            for (int i = 0; i < 25; i++)
            {
                provider.Add(BuildMsBar(i, meanTradeSize: 1.0, buySellRatio: 1.0));
                strategy.EvaluateSignal(BuildBar(100m, i));
            }
            provider.Add(BuildMsBar(25, meanTradeSize: 10.0, buySellRatio: 1.0));
            strategy.EvaluateSignal(BuildBar(100m, 25));

            var diagnostics = ((ISignalDiagnosticsProvider)strategy).DescribeLastEvaluation();
            diagnostics.Should().NotBeNull();
            diagnostics!.Conditions.Single(c => c.Name == "MeanTradeSizeGeP90").Satisfied.Should().BeTrue();
            diagnostics.Conditions.Single(c => c.Name == "BuySellRatioAboveThreshold").Satisfied.Should().BeFalse();
        }
    }
}
