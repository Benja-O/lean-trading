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
    public class CvdSellExhaustionStrategyTests
    {
        private static readonly InstrumentId BtcUsdt = new("BTCUSDT");
        private static readonly DateTime BaseTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static MarketBar BuildBar(decimal close, int barIndex) =>
            new(BtcUsdt, close, BaseTime.AddHours(barIndex));

        private static MicrostructureBar BuildMsBar(int barIndex, double cvdDelta) =>
            new(BtcUsdt, BaseTime.AddHours(barIndex),
                ofi: 0, cvdDelta: cvdDelta, cvd: 0, arrivalRate: 100,
                meanTradeSize: 1.0, buySellRatio: 1.0, priceReturn: 0);

        [Fact]
        public void WarmUpBars_ReturnsExpectedValue()
        {
            var strategy = new CvdSellExhaustionStrategy();
            strategy.WarmUpBars.Should().BeGreaterThan(0);
        }

        [Fact]
        public void EvaluateSignal_DuringWarmUp_ReturnsFlat()
        {
            var strategy = new CvdSellExhaustionStrategy();
            for (int i = 0; i < strategy.WarmUpBars - 1; i++)
            {
                strategy.EvaluateSignal(BuildBar(100m, i))
                    .Should().Be(SignalDirection.Flat,
                        because: $"Barra {i} esta en periodo de warm-up.");
            }
        }

        [Fact]
        public void EvaluateSignal_SellExhaustion_ReturnsLong()
        {
            // H3: close = mínimo de 48 barras Y CvdDelta > 0 → vendedores agotados → Long.
            // 47 barras con close=100 llenan la cola; la barra 47 con close=50 es
            // el nuevo mínimo (50 < todos los 100) y CvdDelta=500 > 0.
            var provider = new FakeMicrostructureProvider();
            var strategy = new CvdSellExhaustionStrategy(provider);

            for (int i = 0; i < 47; i++)
            {
                provider.Add(BuildMsBar(i, cvdDelta: -100));
                strategy.EvaluateSignal(BuildBar(100m, i));
            }

            provider.Add(BuildMsBar(47, cvdDelta: 500));
            strategy.EvaluateSignal(BuildBar(50m, 47))
                .Should().Be(SignalDirection.Long);
        }

        [Fact]
        public void EvaluateSignal_NewMinimumButNegativeCvd_ReturnsFlat()
        {
            // Precio toca mínimo pero CvdDelta < 0 → vendedores siguen dominando → Flat.
            var provider = new FakeMicrostructureProvider();
            var strategy = new CvdSellExhaustionStrategy(provider);

            for (int i = 0; i < 47; i++)
            {
                provider.Add(BuildMsBar(i, cvdDelta: -100));
                strategy.EvaluateSignal(BuildBar(100m, i));
            }

            provider.Add(BuildMsBar(47, cvdDelta: -500));
            strategy.EvaluateSignal(BuildBar(50m, 47))
                .Should().Be(SignalDirection.Flat);
        }

        [Fact]
        public void DescribeLastEvaluation_AlDispararLong_ExponeAmbasCondicionesSatisfechas()
        {
            // ADR-052: el rationale debe reflejar close=min48 (satisfecho) y cvdDelta>0 (satisfecho).
            var provider = new FakeMicrostructureProvider();
            var strategy = new CvdSellExhaustionStrategy(provider);

            for (int i = 0; i < 47; i++)
            {
                provider.Add(BuildMsBar(i, cvdDelta: -100));
                strategy.EvaluateSignal(BuildBar(100m, i));
            }
            provider.Add(BuildMsBar(47, cvdDelta: 500));
            strategy.EvaluateSignal(BuildBar(50m, 47));

            var diagnostics = ((ISignalDiagnosticsProvider)strategy).DescribeLastEvaluation();
            diagnostics.Should().NotBeNull();
            var closeCond = diagnostics!.Conditions.Single(c => c.Name == "CloseIsMin48");
            closeCond.Value.Should().Be(50.0);
            closeCond.Threshold.Should().Be(100.0);   // mínimo de los 47 previos
            closeCond.Satisfied.Should().BeTrue();
            diagnostics.Conditions.Single(c => c.Name == "CvdDeltaPositive").Satisfied.Should().BeTrue();
        }

        [Fact]
        public void DescribeLastEvaluation_CvdNegativo_MarcaCvdDeltaPositiveComoNoSatisfecho()
        {
            var provider = new FakeMicrostructureProvider();
            var strategy = new CvdSellExhaustionStrategy(provider);

            for (int i = 0; i < 47; i++)
            {
                provider.Add(BuildMsBar(i, cvdDelta: -100));
                strategy.EvaluateSignal(BuildBar(100m, i));
            }
            provider.Add(BuildMsBar(47, cvdDelta: -500));
            strategy.EvaluateSignal(BuildBar(50m, 47));

            var diagnostics = ((ISignalDiagnosticsProvider)strategy).DescribeLastEvaluation();
            diagnostics.Should().NotBeNull();
            diagnostics!.Conditions.Single(c => c.Name == "CloseIsMin48").Satisfied.Should().BeTrue();
            diagnostics.Conditions.Single(c => c.Name == "CvdDeltaPositive").Satisfied.Should().BeFalse();
        }
    }
}
