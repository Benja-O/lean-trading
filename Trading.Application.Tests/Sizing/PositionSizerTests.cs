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

        private PositionSizer BuildMinimalSizer()
            => new(_portfolioState, _instrumentMetadata, _logger, minimalPositionMode: true);

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

        // ===== CalculateQuantity — minimal-position-mode =====

        [Fact]
        public void CalculateQuantity_MinimalMode_SizesToMinNotionalCeiledToLot()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            _instrumentMetadata.SetLotSize(_btcUsdt, 0.001m);
            _instrumentMetadata.SetMinimumNotional(_btcUsdt, 100m);

            var result = BuildMinimalSizer().CalculateQuantity(BuildExecutor(), price: 66_600m);

            // ceil(100 / 66600 / 0.001) * 0.001 = ceil(1.5015) * 0.001 = 0.002
            Assert.True(result.IsSuccess);
            Assert.Equal(0.002m, result.Value);
            Assert.True(result.Value * 66_600m > 100m, "el notional debe superar estrictamente el mínimo");
        }

        [Fact]
        public void CalculateQuantity_MinimalMode_IgnoresPortfolioValueAndRiskPercent()
        {
            // Portfolio gigante: el modo risk% daría una cantidad enorme; el modo mínimo no.
            _portfolioState.TotalPortfolioValue = 10_000_000m;
            _instrumentMetadata.SetLotSize(_btcUsdt, 0.001m);
            _instrumentMetadata.SetMinimumNotional(_btcUsdt, 100m);

            var minimal = BuildMinimalSizer().CalculateQuantity(BuildExecutor(), price: 66_600m);
            var riskBased = BuildSizer().CalculateQuantity(BuildExecutor(), price: 66_600m);

            Assert.True(minimal.IsSuccess);
            Assert.Equal(0.002m, minimal.Value);
            Assert.True(riskBased.Value > minimal.Value, "el modo mínimo debe ser mucho menor que el risk-based");
        }

        [Fact]
        public void CalculateQuantity_MinimalMode_WhenCeilLandsExactlyOnMinimum_BumpsOneLot()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            _instrumentMetadata.SetLotSize(_btcUsdt, 0.1m);
            _instrumentMetadata.SetMinimumNotional(_btcUsdt, 10m);

            // 10 / 100 / 0.1 = 1 (exacto) -> 0.1 -> notional 10 == mínimo -> bump a 0.2
            var result = BuildMinimalSizer().CalculateQuantity(BuildExecutor(), price: 100m);

            Assert.True(result.IsSuccess);
            Assert.Equal(0.2m, result.Value);
        }

        [Fact]
        public void CalculateQuantity_MinimalMode_WithNullMinNotional_UsesFloorOfFive()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            _instrumentMetadata.SetLotSize(_btcUsdt, 0.01m);
            // sin SetMinimumNotional -> GetMinimumNotional devuelve null -> floor 5

            var result = BuildMinimalSizer().CalculateQuantity(BuildExecutor(), price: 74m);

            // ceil(5 / 74 / 0.01) * 0.01 = ceil(6.756) * 0.01 = 0.07
            Assert.True(result.IsSuccess);
            Assert.Equal(0.07m, result.Value);
            Assert.True(result.Value * 74m > 5m);
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
