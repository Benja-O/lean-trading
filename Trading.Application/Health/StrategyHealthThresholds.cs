using System;

namespace Trading.Application.Health
{
    /// <summary>
    /// Umbrales numéricos por estrategia, derivados literalmente de POLICY sección 3.1.
    /// Inmutable. Sin reconfiguración runtime. Si POLICY cambia, recompilación es la única vía.
    /// Documentado en ADR-023.
    /// </summary>
    public sealed class StrategyHealthThresholds
    {
        public decimal AbsoluteDrawdownFromAthFraction { get; }       // U1: 0.25m
        public decimal RollingDrawdownThirtyDaysFraction { get; }     // U2: 0.15m
        public int    RollingDrawdownSustainedDays { get; }           // U2: 5
        public decimal RollingProfitFactorThreshold { get; }          // U3: 1.0m
        public int    RollingProfitFactorSustainedTrades { get; }     // U3: 10
        public decimal RollingExpectancyThreshold { get; }            // U4: 0m
        public int    RollingExpectancySustainedTrades { get; }       // U4: 10
        public int    MinimumTradesToArmRollingThresholds { get; }    // U3/U4 arming: 50
        public int    RollingWindowTrades { get; }                    // 30
        public int    RollingWindowDays { get; }                      // 30

        public StrategyHealthThresholds(
            decimal absoluteDrawdownFromAthFraction,
            decimal rollingDrawdownThirtyDaysFraction,
            int rollingDrawdownSustainedDays,
            decimal rollingProfitFactorThreshold,
            int rollingProfitFactorSustainedTrades,
            decimal rollingExpectancyThreshold,
            int rollingExpectancySustainedTrades,
            int minimumTradesToArmRollingThresholds,
            int rollingWindowTrades,
            int rollingWindowDays)
        {
            if (absoluteDrawdownFromAthFraction <= 0m)
                throw new ArgumentException(
                    $"AbsoluteDrawdownFromAthFraction debe ser > 0. Recibido: {absoluteDrawdownFromAthFraction}.",
                    nameof(absoluteDrawdownFromAthFraction));
            if (rollingDrawdownThirtyDaysFraction <= 0m)
                throw new ArgumentException(
                    $"RollingDrawdownThirtyDaysFraction debe ser > 0. Recibido: {rollingDrawdownThirtyDaysFraction}.",
                    nameof(rollingDrawdownThirtyDaysFraction));
            if (rollingDrawdownSustainedDays <= 0)
                throw new ArgumentException(
                    $"RollingDrawdownSustainedDays debe ser > 0. Recibido: {rollingDrawdownSustainedDays}.",
                    nameof(rollingDrawdownSustainedDays));
            if (rollingProfitFactorThreshold <= 0m)
                throw new ArgumentException(
                    $"RollingProfitFactorThreshold debe ser > 0. Recibido: {rollingProfitFactorThreshold}.",
                    nameof(rollingProfitFactorThreshold));
            if (rollingProfitFactorSustainedTrades <= 0)
                throw new ArgumentException(
                    $"RollingProfitFactorSustainedTrades debe ser > 0. Recibido: {rollingProfitFactorSustainedTrades}.",
                    nameof(rollingProfitFactorSustainedTrades));
            // rollingExpectancyThreshold puede ser 0 (caso válido borderline de POLICY)
            if (rollingExpectancySustainedTrades <= 0)
                throw new ArgumentException(
                    $"RollingExpectancySustainedTrades debe ser > 0. Recibido: {rollingExpectancySustainedTrades}.",
                    nameof(rollingExpectancySustainedTrades));
            if (minimumTradesToArmRollingThresholds <= 0)
                throw new ArgumentException(
                    $"MinimumTradesToArmRollingThresholds debe ser > 0. Recibido: {minimumTradesToArmRollingThresholds}.",
                    nameof(minimumTradesToArmRollingThresholds));
            if (rollingWindowTrades <= 0)
                throw new ArgumentException(
                    $"RollingWindowTrades debe ser > 0. Recibido: {rollingWindowTrades}.",
                    nameof(rollingWindowTrades));
            if (rollingWindowDays <= 0)
                throw new ArgumentException(
                    $"RollingWindowDays debe ser > 0. Recibido: {rollingWindowDays}.",
                    nameof(rollingWindowDays));

            AbsoluteDrawdownFromAthFraction = absoluteDrawdownFromAthFraction;
            RollingDrawdownThirtyDaysFraction = rollingDrawdownThirtyDaysFraction;
            RollingDrawdownSustainedDays = rollingDrawdownSustainedDays;
            RollingProfitFactorThreshold = rollingProfitFactorThreshold;
            RollingProfitFactorSustainedTrades = rollingProfitFactorSustainedTrades;
            RollingExpectancyThreshold = rollingExpectancyThreshold;
            RollingExpectancySustainedTrades = rollingExpectancySustainedTrades;
            MinimumTradesToArmRollingThresholds = minimumTradesToArmRollingThresholds;
            RollingWindowTrades = rollingWindowTrades;
            RollingWindowDays = rollingWindowDays;
        }

        /// <summary>
        /// Factory con los defaults literales de POLICY 3.1 al momento de OPS-2.
        /// Cualquier cambio a POLICY 3.1 exige actualizar estos defaults + ADR.
        /// </summary>
        public static StrategyHealthThresholds FromPolicyDefaults() =>
            new(
                absoluteDrawdownFromAthFraction: 0.25m,
                rollingDrawdownThirtyDaysFraction: 0.15m,
                rollingDrawdownSustainedDays: 5,
                rollingProfitFactorThreshold: 1.0m,
                rollingProfitFactorSustainedTrades: 10,
                rollingExpectancyThreshold: 0m,
                rollingExpectancySustainedTrades: 10,
                minimumTradesToArmRollingThresholds: 50,
                rollingWindowTrades: 30,
                rollingWindowDays: 30);
    }
}
