using System.Collections.Generic;
using FluentAssertions;
using Trading.Application.Risk;
using Trading.Application.Tests.Fakes;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Risk
{
    public class LiquidateAllRiskActionTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly InstrumentId Eth = new("ETHUSDT");
        private static readonly InstrumentId Trb = new("TRBUSDT");

        private LiquidateAllRiskAction BuildSut(
            FakeOrderRouter router,
            FakePortfolioState portfolio,
            IReadOnlyList<InstrumentId> instruments)
            => new(router, portfolio, instruments);

        [Fact]
        public void Execute_LiquidatesAll_WhenAllInstrumentsAreInvested()
        {
            var router = new FakeOrderRouter();
            var portfolio = new FakePortfolioState();
            portfolio.SetInvested(Btc, true);
            portfolio.SetInvested(Eth, true);
            portfolio.SetInvested(Trb, true);

            var sut = BuildSut(router, portfolio, new[] { Btc, Eth, Trb });
            sut.Execute();

            router.LiquidateInstrumentCalls.Should().HaveCount(3);
            router.LiquidateInstrumentCalls.Should().Contain(c => c.InstrumentId == Btc);
            router.LiquidateInstrumentCalls.Should().Contain(c => c.InstrumentId == Eth);
            router.LiquidateInstrumentCalls.Should().Contain(c => c.InstrumentId == Trb);
            router.LiquidateInstrumentCalls.Should().AllSatisfy(c =>
            {
                c.Purpose.Should().Be(OrderPurpose.Liquidate);
                c.ExecutorIdentifier.Should().Be("RiskOrchestrator_KillSwitch");
            });
        }

        [Fact]
        public void Execute_LiquidatesOnlyInvested_WhenSomeInstrumentsHaveNoPosition()
        {
            var router = new FakeOrderRouter();
            var portfolio = new FakePortfolioState();
            portfolio.SetInvested(Btc, true);
            portfolio.SetInvested(Eth, false);
            portfolio.SetInvested(Trb, true);

            var sut = BuildSut(router, portfolio, new[] { Btc, Eth, Trb });
            sut.Execute();

            router.LiquidateInstrumentCalls.Should().HaveCount(2);
            router.LiquidateInstrumentCalls.Should().Contain(c => c.InstrumentId == Btc);
            router.LiquidateInstrumentCalls.Should().NotContain(c => c.InstrumentId == Eth);
            router.LiquidateInstrumentCalls.Should().Contain(c => c.InstrumentId == Trb);
        }

        [Fact]
        public void Execute_EmitsNoCalls_WhenNoInstrumentsAreInvested()
        {
            var router = new FakeOrderRouter();
            var portfolio = new FakePortfolioState();

            var sut = BuildSut(router, portfolio, new[] { Btc, Eth, Trb });
            sut.Execute();

            router.LiquidateInstrumentCalls.Should().BeEmpty();
        }
    }
}
