using System;
using System.Collections.Generic;
using Trading.Application.Auditing;
using Trading.Domain.Models;

namespace Trading.Strategies.Auditing
{
    /// <summary>
    /// Recálculo independiente de los indicadores que EmaCrossStrategy usa internamente.
    ///
    /// Replica EXACTAMENTE el algoritmo de QuantConnect's ExponentialMovingAverage:
    ///   Samples == 1: EMA(1) = price(1)              (inicialización con primer precio)
    ///   Samples >= 2: EMA(n) = price(n) * α + EMA(n-1) * (1 - α)
    ///   α = 2 / (period + 1)
    ///
    /// NO usa seed SMA. QC tampoco lo hace. Cualquier divergencia de algoritmo entre el
    /// recomputer y la estrategia produciría diferencias permanentes (no decae con el tiempo
    /// en escala financieramente significativa). Por eso replicamos el algoritmo bit-a-bit
    /// salvo por la diferencia de precisión (decimal vs double).
    ///
    /// El SignalAuditor controla la cobertura mínima vía auditWarmUpBars (default 200). Por eso
    /// no hace falta verificar Count >= SlowEmaPeriod acá: si el caller llega con buffer chico,
    /// el cálculo aún es matemáticamente válido (el algoritmo no requiere mínimo), y de todos
    /// modos el auditor habrá ya descartado señales tempranas vía warm-up.
    ///
    /// Esto detecta:
    /// - Bugs de flujo de control en la estrategia (saltó alguna actualización, estado stale).
    /// - Discrepancias entre las barras que la estrategia procesó y las que dice haber procesado.
    ///
    /// NO detecta:
    /// - Bugs en el algoritmo de EMA de QC mismo (porque estamos replicándolo).
    ///   Esa garantía requeriría auditor independiente en otro motor (ej. TA-Lib Python).
    /// </summary>
    public sealed class EmaCrossIndicatorRecomputer : IIndicatorRecomputer
    {
        private const int FastEmaPeriod = 30;
        private const int SlowEmaPeriod = 60;

        public string StrategyName => "EmaCrossStrategy";

        public IReadOnlyDictionary<string, decimal> Recompute(IReadOnlyList<MarketBar> observedBars)
        {
            if (observedBars == null) throw new ArgumentNullException(nameof(observedBars));

            var result = new Dictionary<string, decimal>();

            if (observedBars.Count == 0)
            {
                // Caso degenerado: sin barras no hay nada que recalcular.
                result["EmaFast"] = 0m;
                result["EmaSlow"] = 0m;
                return result;
            }

            result["EmaFast"] = ComputeExponentialMovingAverage(observedBars, FastEmaPeriod);
            result["EmaSlow"] = ComputeExponentialMovingAverage(observedBars, SlowEmaPeriod);
            // PreviousSignal deliberadamente omitido: requiere replicar el estado histórico
            // completo de la estrategia. Limitación conocida del recomputer documentada en ADR-010.

            return result;
        }

        /// <summary>
        /// Calcula EMA siguiendo exactamente el algoritmo de QuantConnect's ExponentialMovingAverage.
        ///
        /// Fórmula:
        ///   EMA(1) = price(1)
        ///   EMA(n) = price(n) * α + EMA(n-1) * (1 - α)
        ///   α = 2 / (period + 1)
        ///
        /// La única fuente de error esperada vs QC es la conversión double → decimal: QC opera
        /// internamente en double, mientras que acá operamos en decimal. El error relativo
        /// acumulado típico es del orden de 10⁻¹⁰ a 10⁻⁸, muy por debajo de la tolerancia 1e-6.
        /// </summary>
        private static decimal ComputeExponentialMovingAverage(IReadOnlyList<MarketBar> bars, int period)
        {
            decimal alpha = 2m / (period + 1m);
            decimal currentEma = bars[0].Close;

            for (int barIndex = 1; barIndex < bars.Count; barIndex++)
            {
                currentEma = bars[barIndex].Close * alpha + currentEma * (1m - alpha);
            }

            return currentEma;
        }
    }
}
