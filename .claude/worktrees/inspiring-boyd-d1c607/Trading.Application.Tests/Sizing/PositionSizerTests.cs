using Trading.Application.Execution;
using Trading.Application.Sizing;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Sizing
{
    /// <summary>
    /// Tests del PositionSizer post-refactor B1. Verifica que CalculateQuantity y ValidateNotional
    /// devuelvan Result tipado con FailureReason correcto en cada escenario.
    /// </summary>
    public class PositionSizerTests
    {
        private readonly FakePortfolioState _portfolioState = new();
        private readonly FakeInstrumentMetadata _instrumentMetadata = new();
        private readonly FakeTradingLogger _logger = new();
        private readonly InstrumentId _btcUsdt = new("BTCUSDT");

        private PositionSizer BuildSizer()
            => new(_portfolioState, _instrumentMetadata, _logger);

        private StrategyExecutor BuildExecutor()
        {
            var definition = new StrategyDefinition
            {
                StrategyName = "EmaCross",
                Symbol = "BTCUSDT",
                StopLossPercentage = 3.0m,
                TakeProfitPercentage = 6.0m,
                RiskPerTradePercentage = 2.0m,
                CombineWithTimeExit = false,
                MaxBars = 100
            };
            var riskParameters = RiskParameters.FromPercentages(3.0m, 6.0m, 2.0m);
            return new StrategyExecutor(definition, "5m", _btcUsdt, new FakeStrategy(), riskParameters);
        }

        // ===== CalculateQuantity =====

        [Fact]
        public void CalculateQuantity_WithValidInputs_ReturnsSuccess()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            _instrumentMetadata.SetLotSize(_btcUsdt, 0.001m);

            var sizer = BuildSizer();
            var executor = BuildExecutor();

            var result = sizer.CalculateQuantity(executor, price: 50_000m);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value > 0m);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-0.01)]
        public void CalculateQuantity_WithNonPositivePrice_FailsWithInvalidPrice(decimal invalidPrice)
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            _instrumentMetadata.SetLotSize(_btcUsdt, 0.001m);

            var sizer = BuildSizer();
            var executor = BuildExecutor();

            var result = sizer.CalculateQuantity(executor, invalidPrice);

            Assert.True(result.IsFailure);
            Assert.Equal(SizingFailureReason.InvalidPrice, result.FailureReason);
            Assert.NotEmpty(result.FailureDescription);
        }

        [Fact]
        public void CalculateQuantity_WhenRoundingProducesZero_FailsWithQuantityRoundsToZero()
        {
            // Portfolio chico + lot size grande fuerza redondeo a cero.
            _portfolioState.TotalPortfolioValue = 100m;
            _instrumentMetadata.SetLotSize(_btcUsdt, 1m);

            var sizer = BuildSizer();
            var executor = BuildExecutor();

            var result = sizer.CalculateQuantity(executor, price: 50_000m);

            Assert.True(result.IsFailure);
            Assert.Equal(SizingFailureReason.QuantityRoundsToZero, result.FailureReason);
        }

        // ===== ValidateNotional =====

        [Fact]
        public void ValidateNotional_AboveMinimum_ReturnsSuccess()
        {
            _instrumentMetadata.SetMinimumNotional(_btcUsdt, 10m);
            var sizer = BuildSizer();

            var result = sizer.ValidateNotional(_btcUsdt, quantity: 0.01m, price: 50_000m);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void ValidateNotional_AtOrBelowMinimum_FailsWithBelowMinimumNotional()
        {
            _instrumentMetadata.SetMinimumNotional(_btcUsdt, 1000m);
            var sizer = BuildSizer();

            // Notional = 0.0001 * 50000 = 5, Minimum = 1000 → falla
            var result = sizer.ValidateNotional(_btcUsdt, quantity: 0.0001m, price: 50_000m);

            Assert.True(result.IsFailure);
            Assert.Equal(SizingFailureReason.BelowMinimumNotional, result.FailureReason);
        }

        [Fact]
        public void ValidateNotional_NoMinimumDeclared_UsesDefaultFloor()
        {
            // Sin minimumNotional declarado → usa 5 como floor.
            var sizer = BuildSizer();

            var resultBelow = sizer.ValidateNotional(_btcUsdt, quantity: 0.0001m, price: 10m);
            // Notional = 0.001, floor = 5 → falla
            Assert.True(resultBelow.IsFailure);

            var resultAbove = sizer.ValidateNotional(_btcUsdt, quantity: 1m, price: 100m);
            // Notional = 100, floor = 5 → success
            Assert.True(resultAbove.IsSuccess);
        }

        [Fact]
        public void ValidateNotional_WithNegativeQuantity_UsesAbsoluteValue()
        {
            // Las shorts pasan quantity negativa. El validador debe considerar la magnitud.
            _instrumentMetadata.SetMinimumNotional(_btcUsdt, 10m);
            var sizer = BuildSizer();

            var result = sizer.ValidateNotional(_btcUsdt, quantity: -0.01m, price: 50_000m);

            Assert.True(result.IsSuccess);
        }
    }
}
