using System;
using System.Collections.Generic;
using Trading.Domain.Exceptions;

namespace Trading.Domain.ValueObjects
{
    /// <summary>
    /// Parámetros de riesgo de una estrategia, expresados como fracciones (no porcentajes).
    /// Inmutable y verificado por construcción: si una instancia existe, sus invariantes están garantizadas.
    /// 
    /// Convención de unidades: todos los campos son fracciones decimales.
    /// Ejemplo: 0.03 representa 3%, no 3.0.
    /// La conversión desde porcentajes (formato del JSON de configuración) ocurre exclusivamente
    /// en el factory method FromPercentages, eliminando dispersión de la lógica de conversión.
    /// 
    /// Invariantes verificadas:
    /// - StopLossFraction estrictamente entre 0 y 1 (exclusivo).
    /// - TakeProfitFraction estrictamente positivo, sin cota superior.
    /// - RiskPerTradeFraction estrictamente entre 0 y MaximumAllowedRiskPerTradeFraction (10% por defecto).
    /// - TakeProfitFraction >= StopLossFraction (reward/risk >= 1, expectancy no degenerada por construcción).
    /// 
    /// Cualquier violación lanza InvalidRiskParametersException con detalle del campo y valor ofensor.
    /// </summary>
    public sealed class RiskParameters
    {
        /// <summary>
        /// Cota superior del riesgo por trade aceptable sin escalación explícita.
        /// 10% del portfolio es el límite institucional convencional para estrategias single-trade;
        /// configurar más requiere modificar esta constante explícitamente, forzando la conversación con risk.
        /// </summary>
        public const decimal MaximumAllowedRiskPerTradeFraction = 0.10m;

        public decimal StopLossFraction { get; }
        public decimal TakeProfitFraction { get; }
        public decimal RiskPerTradeFraction { get; }

        /// <summary>
        /// Reward/Risk ratio derivado. Por invariante de construcción, siempre &gt;= 1.
        /// </summary>
        public decimal RewardRiskRatio => TakeProfitFraction / StopLossFraction;

        private RiskParameters(decimal stopLossFraction, decimal takeProfitFraction, decimal riskPerTradeFraction)
        {
            StopLossFraction = stopLossFraction;
            TakeProfitFraction = takeProfitFraction;
            RiskPerTradeFraction = riskPerTradeFraction;
        }

        /// <summary>
        /// Constructor primario desde fracciones. Verifica todas las invariantes.
        /// Lanza InvalidRiskParametersException con la lista completa de violaciones detectadas
        /// (no falla en la primera; reporta todo lo que esté mal de una vez para facilitar diagnóstico).
        /// </summary>
        public static RiskParameters Create(
            decimal stopLossFraction,
            decimal takeProfitFraction,
            decimal riskPerTradeFraction)
        {
            var violations = new List<string>();

            if (stopLossFraction <= 0m || stopLossFraction >= 1m)
            {
                violations.Add($"StopLossFraction debe estar estrictamente entre 0 y 1 (exclusivo); recibido: {stopLossFraction}");
            }

            if (takeProfitFraction <= 0m)
            {
                violations.Add($"TakeProfitFraction debe ser estrictamente positivo; recibido: {takeProfitFraction}");
            }

            if (riskPerTradeFraction <= 0m)
            {
                violations.Add($"RiskPerTradeFraction debe ser estrictamente positivo; recibido: {riskPerTradeFraction}");
            }
            else if (riskPerTradeFraction > MaximumAllowedRiskPerTradeFraction)
            {
                violations.Add(
                    $"RiskPerTradeFraction excede el límite institucional de {MaximumAllowedRiskPerTradeFraction:P0}; " +
                    $"recibido: {riskPerTradeFraction:P2}. Para superar este límite, modifique " +
                    $"RiskParameters.MaximumAllowedRiskPerTradeFraction explícitamente.");
            }

            // Reward/Risk solo se verifica si los valores base son individualmente válidos,
            // para no producir mensajes redundantes ante un valor degenerado.
            bool stopLossEsValido = stopLossFraction > 0m && stopLossFraction < 1m;
            bool takeProfitEsValido = takeProfitFraction > 0m;
            if (stopLossEsValido && takeProfitEsValido && takeProfitFraction < stopLossFraction)
            {
                violations.Add(
                    $"TakeProfitFraction ({takeProfitFraction}) debe ser >= StopLossFraction ({stopLossFraction}); " +
                    $"reward/risk < 1 implica expectancy negativo por construcción.");
            }

            if (violations.Count > 0)
            {
                throw new InvalidRiskParametersException(violations);
            }

            return new RiskParameters(stopLossFraction, takeProfitFraction, riskPerTradeFraction);
        }

        /// <summary>
        /// Factory desde porcentajes (formato del JSON de configuración).
        /// Ejemplo: FromPercentages(stopLossPercentage: 3.0, takeProfitPercentage: 6.0, riskPerTradePercentage: 2.0)
        /// produce fracciones (0.03, 0.06, 0.02).
        /// 
        /// Esta es la ÚNICA división por 100 del dominio. El resto del sistema trabaja exclusivamente con fracciones.
        /// </summary>
        public static RiskParameters FromPercentages(
            decimal stopLossPercentage,
            decimal takeProfitPercentage,
            decimal riskPerTradePercentage)
        {
            return Create(
                stopLossFraction: stopLossPercentage / 100m,
                takeProfitFraction: takeProfitPercentage / 100m,
                riskPerTradeFraction: riskPerTradePercentage / 100m);
        }

        public override string ToString() =>
            $"RiskParameters(SL={StopLossFraction:P2}, TP={TakeProfitFraction:P2}, " +
            $"Risk/Trade={RiskPerTradeFraction:P2}, R/R={RewardRiskRatio:F2})";
    }
}
