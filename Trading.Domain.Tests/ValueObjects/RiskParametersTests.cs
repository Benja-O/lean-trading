using System.Linq;
using Trading.Domain.Exceptions;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Domain.Tests.ValueObjects
{
    /// <summary>
    /// Tests del value object RiskParameters.
    /// 
    /// Cobertura de invariantes (cada test apunta a una invariante específica):
    /// - StopLossFraction estrictamente en (0, 1).
    /// - TakeProfitFraction estrictamente positivo.
    /// - RiskPerTradeFraction estrictamente en (0, MaximumAllowedRiskPerTradeFraction].
    /// - TakeProfitFraction >= StopLossFraction (R/R >= 1).
    /// - Reporte agregado: errores múltiples se reportan todos a la vez.
    /// - Conversión porcentaje->fracción: ocurre exactamente en FromPercentages.
    /// </summary>
    public class RiskParametersTests
    {
        // ===== Camino feliz =====

        [Fact]
        public void Create_WithValidFractions_ReturnsInstance()
        {
            var riskParameters = RiskParameters.Create(
                stopLossFraction: 0.03m,
                takeProfitFraction: 0.06m,
                riskPerTradeFraction: 0.02m);

            Assert.Equal(0.03m, riskParameters.StopLossFraction);
            Assert.Equal(0.06m, riskParameters.TakeProfitFraction);
            Assert.Equal(0.02m, riskParameters.RiskPerTradeFraction);
            Assert.Equal(2m, riskParameters.RewardRiskRatio);
        }

        [Fact]
        public void FromPercentages_ConvertsToFractionsCorrectly()
        {
            var riskParameters = RiskParameters.FromPercentages(
                stopLossPercentage: 3.0m,
                takeProfitPercentage: 6.0m,
                riskPerTradePercentage: 2.0m);

            Assert.Equal(0.03m, riskParameters.StopLossFraction);
            Assert.Equal(0.06m, riskParameters.TakeProfitFraction);
            Assert.Equal(0.02m, riskParameters.RiskPerTradeFraction);
        }

        [Fact]
        public void Create_WithEqualStopLossAndTakeProfit_IsAllowed()
        {
            // R/R == 1 es válido (cota inferior inclusive).
            var riskParameters = RiskParameters.Create(
                stopLossFraction: 0.03m,
                takeProfitFraction: 0.03m,
                riskPerTradeFraction: 0.01m);

            Assert.Equal(1m, riskParameters.RewardRiskRatio);
        }

        // ===== StopLossFraction invariante =====

        [Theory]
        [InlineData(0)]
        [InlineData(-0.01)]
        [InlineData(-1)]
        public void Create_WithNonPositiveStopLoss_Throws(decimal invalidStopLossFraction)
        {
            var exception = Assert.Throws<InvalidRiskParametersException>(() =>
                RiskParameters.Create(
                    stopLossFraction: invalidStopLossFraction,
                    takeProfitFraction: 0.06m,
                    riskPerTradeFraction: 0.02m));

            Assert.Contains(exception.Violations, v => v.Contains("StopLossFraction"));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(1.5)]
        [InlineData(2)]
        public void Create_WithStopLossGreaterOrEqualOne_Throws(decimal invalidStopLossFraction)
        {
            var exception = Assert.Throws<InvalidRiskParametersException>(() =>
                RiskParameters.Create(
                    stopLossFraction: invalidStopLossFraction,
                    takeProfitFraction: 0.06m,
                    riskPerTradeFraction: 0.02m));

            Assert.Contains(exception.Violations, v => v.Contains("StopLossFraction"));
        }

        // ===== TakeProfitFraction invariante =====

        [Theory]
        [InlineData(0)]
        [InlineData(-0.01)]
        public void Create_WithNonPositiveTakeProfit_Throws(decimal invalidTakeProfitFraction)
        {
            var exception = Assert.Throws<InvalidRiskParametersException>(() =>
                RiskParameters.Create(
                    stopLossFraction: 0.03m,
                    takeProfitFraction: invalidTakeProfitFraction,
                    riskPerTradeFraction: 0.02m));

            Assert.Contains(exception.Violations, v => v.Contains("TakeProfitFraction"));
        }

        [Fact]
        public void Create_WithVeryLargeTakeProfit_IsAllowed()
        {
            // No hay cota superior para TP — estrategias de tendencia legítimamente apuntan lejos.
            var riskParameters = RiskParameters.Create(
                stopLossFraction: 0.02m,
                takeProfitFraction: 5.0m,
                riskPerTradeFraction: 0.01m);

            Assert.Equal(5.0m, riskParameters.TakeProfitFraction);
        }

        // ===== RiskPerTradeFraction invariante =====

        [Theory]
        [InlineData(0)]
        [InlineData(-0.01)]
        public void Create_WithNonPositiveRiskPerTrade_Throws(decimal invalidRiskPerTradeFraction)
        {
            var exception = Assert.Throws<InvalidRiskParametersException>(() =>
                RiskParameters.Create(
                    stopLossFraction: 0.03m,
                    takeProfitFraction: 0.06m,
                    riskPerTradeFraction: invalidRiskPerTradeFraction));

            Assert.Contains(exception.Violations, v => v.Contains("RiskPerTradeFraction"));
        }

        [Fact]
        public void Create_WithRiskPerTradeAtInstitutionalLimit_IsAllowed()
        {
            // 10% es exactamente la cota superior aceptada (inclusiva).
            var riskParameters = RiskParameters.Create(
                stopLossFraction: 0.03m,
                takeProfitFraction: 0.06m,
                riskPerTradeFraction: RiskParameters.MaximumAllowedRiskPerTradeFraction);

            Assert.Equal(RiskParameters.MaximumAllowedRiskPerTradeFraction, riskParameters.RiskPerTradeFraction);
        }

        [Fact]
        public void Create_WithRiskPerTradeAboveInstitutionalLimit_Throws()
        {
            decimal aboveLimit = RiskParameters.MaximumAllowedRiskPerTradeFraction + 0.001m;

            var exception = Assert.Throws<InvalidRiskParametersException>(() =>
                RiskParameters.Create(
                    stopLossFraction: 0.03m,
                    takeProfitFraction: 0.06m,
                    riskPerTradeFraction: aboveLimit));

            Assert.Contains(exception.Violations, v => v.Contains("límite institucional"));
        }

        // ===== Reward/Risk invariante =====

        [Fact]
        public void Create_WithRewardRiskBelowOne_Throws()
        {
            // TP=0.02 < SL=0.03  →  R/R = 0.66 < 1  →  expectancy negativo por construcción.
            var exception = Assert.Throws<InvalidRiskParametersException>(() =>
                RiskParameters.Create(
                    stopLossFraction: 0.03m,
                    takeProfitFraction: 0.02m,
                    riskPerTradeFraction: 0.02m));

            Assert.Contains(exception.Violations, v => v.Contains("expectancy"));
        }

        [Fact]
        public void Create_RewardRiskCheckSkipped_WhenBaseValuesInvalid()
        {
            // Si SL ya es inválido, no queremos un mensaje redundante sobre R/R.
            var exception = Assert.Throws<InvalidRiskParametersException>(() =>
                RiskParameters.Create(
                    stopLossFraction: 0m,
                    takeProfitFraction: 0.06m,
                    riskPerTradeFraction: 0.02m));

            // Solo debe haber violación de StopLoss, no de R/R.
            Assert.Single(exception.Violations);
            Assert.Contains(exception.Violations, v => v.Contains("StopLossFraction"));
            Assert.DoesNotContain(exception.Violations, v => v.Contains("expectancy"));
        }

        // ===== Reporte agregado =====

        [Fact]
        public void Create_WithMultipleViolations_ReportsAllAtOnce()
        {
            // SL inválido + TP inválido + Risk inválido → debe reportar las tres.
            var exception = Assert.Throws<InvalidRiskParametersException>(() =>
                RiskParameters.Create(
                    stopLossFraction: -1m,
                    takeProfitFraction: -1m,
                    riskPerTradeFraction: -1m));

            Assert.Equal(3, exception.Violations.Count);
            Assert.Contains(exception.Violations, v => v.Contains("StopLossFraction"));
            Assert.Contains(exception.Violations, v => v.Contains("TakeProfitFraction"));
            Assert.Contains(exception.Violations, v => v.Contains("RiskPerTradeFraction"));
        }

        [Fact]
        public void InvalidRiskParametersException_MessageIncludesAllViolations()
        {
            var exception = Assert.Throws<InvalidRiskParametersException>(() =>
                RiskParameters.Create(
                    stopLossFraction: 0m,
                    takeProfitFraction: 0m,
                    riskPerTradeFraction: 0m));

            // El mensaje agregado debe contener cada violación individual.
            Assert.All(exception.Violations, violation =>
                Assert.Contains(violation, exception.Message));
        }

        // ===== ToString =====

        [Fact]
        public void ToString_ProducesReadableInstitutionalFormat()
        {
            var riskParameters = RiskParameters.Create(
                stopLossFraction: 0.03m,
                takeProfitFraction: 0.06m,
                riskPerTradeFraction: 0.02m);

            string representation = riskParameters.ToString();

            Assert.Contains("SL=", representation);
            Assert.Contains("TP=", representation);
            Assert.Contains("R/R=", representation);
        }
    }
}
