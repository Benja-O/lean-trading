using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Models;

namespace Trading.Strategies.Regimes
{
    /// <summary>
    /// Cálculo de features para el HMM. Compartido entre el HmmTrainer (offline)
    /// y el AccordHmmClassifier (runtime) para garantizar que la misma barra produce
    /// exactamente las mismas features en ambos contextos.
    ///
    /// Features (3 por barra):
    /// 1. return_log = ln(close[t] / close[t-1])
    /// 2. vol_20     = stddev(return_log[t-19 .. t])
    /// 3. momentum_ratio = SMA(close, 20)[t] / SMA(close, 50)[t] - 1
    ///
    /// El warm-up de features es 50 barras: hasta no tener al menos 50 cierres previos,
    /// no se puede calcular SMA50 ni el ratio. Las primeras 50 barras del training set se
    /// descartan; en runtime, el classifier devuelve UnknownFor hasta superar ese umbral.
    /// </summary>
    public static class FeatureExtractor
    {
        public const int FeatureWarmUpBars = 50;
        public const int FeatureCount = 3;
        private const int ShortSmaPeriod = 20;
        private const int LongSmaPeriod = 50;
        private const int VolatilityWindow = 20;

        /// <summary>
        /// Calcula las 3 features para la barra cuyo close es <paramref name="closes"/>[index].
        /// <paramref name="closes"/> es el array de cierres en orden cronológico ascendente.
        /// El índice <paramref name="index"/> debe ser ≥ <see cref="FeatureWarmUpBars"/>.
        /// </summary>
        public static double[] ExtractAt(IReadOnlyList<double> closes, int index)
        {
            if (closes is null) throw new ArgumentNullException(nameof(closes));
            if (index < FeatureWarmUpBars)
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    $"Se requieren al menos {FeatureWarmUpBars} barras previas para calcular features. " +
                    $"Índice solicitado: {index}.");
            if (index >= closes.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Índice {index} fuera de rango (count={closes.Count}).");

            double previousClose = closes[index - 1];
            double currentClose = closes[index];
            if (previousClose <= 0 || currentClose <= 0)
                throw new InvalidOperationException(
                    $"Cierres no positivos detectados (prev={previousClose}, curr={currentClose}) en índice {index}. " +
                    "El cálculo de retornos logarítmicos exige precios estrictamente positivos.");

            double returnLog = Math.Log(currentClose / previousClose);

            double volatility20 = ComputeRollingVolatility(closes, index, VolatilityWindow);
            double shortSma = ComputeSimpleMovingAverage(closes, index, ShortSmaPeriod);
            double longSma = ComputeSimpleMovingAverage(closes, index, LongSmaPeriod);
            if (longSma == 0)
                throw new InvalidOperationException(
                    $"SMA{LongSmaPeriod} = 0 en índice {index}; división por cero en momentum ratio.");

            double momentumRatio = shortSma / longSma - 1.0;

            return new[] { returnLog, volatility20, momentumRatio };
        }

        /// <summary>
        /// Calcula todas las features para una serie completa. Devuelve la matriz [N - FeatureWarmUpBars] x 3
        /// y el vector con los índices originales de las barras correspondientes (para alinear timestamps).
        /// </summary>
        public static FeatureMatrix ExtractAll(IReadOnlyList<MarketBar> bars)
        {
            if (bars is null) throw new ArgumentNullException(nameof(bars));
            if (bars.Count <= FeatureWarmUpBars)
                throw new ArgumentException(
                    $"Se requieren más de {FeatureWarmUpBars} barras para extraer features. " +
                    $"Recibidas: {bars.Count}.",
                    nameof(bars));

            var closes = bars.Select(bar => (double)bar.Close).ToArray();
            int firstIndex = FeatureWarmUpBars;
            int featureRowCount = closes.Length - firstIndex;
            var features = new double[featureRowCount][];
            var timestamps = new DateTime[featureRowCount];

            for (int barIndex = firstIndex; barIndex < closes.Length; barIndex++)
            {
                int row = barIndex - firstIndex;
                features[row] = ExtractAt(closes, barIndex);
                timestamps[row] = bars[barIndex].TimestampUtc;
            }
            return new FeatureMatrix(features, timestamps);
        }

        private static double ComputeSimpleMovingAverage(IReadOnlyList<double> closes, int endIndex, int period)
        {
            double sum = 0;
            for (int i = endIndex - period + 1; i <= endIndex; i++)
                sum += closes[i];
            return sum / period;
        }

        private static double ComputeRollingVolatility(IReadOnlyList<double> closes, int endIndex, int window)
        {
            // Desvío estándar de retornos log sobre la ventana terminada en endIndex.
            // window retornos requieren window+1 cierres consecutivos (closes[endIndex - window .. endIndex]).
            var returns = new double[window];
            for (int k = 0; k < window; k++)
            {
                double previousClose = closes[endIndex - window + k];
                double currentClose = closes[endIndex - window + k + 1];
                if (previousClose <= 0 || currentClose <= 0)
                    throw new InvalidOperationException(
                        $"Cierres no positivos al calcular volatilidad rolling en ventana terminada en índice {endIndex}.");
                returns[k] = Math.Log(currentClose / previousClose);
            }
            double mean = 0;
            for (int k = 0; k < window; k++) mean += returns[k];
            mean /= window;
            double sumSquaredDeviations = 0;
            for (int k = 0; k < window; k++)
            {
                double deviation = returns[k] - mean;
                sumSquaredDeviations += deviation * deviation;
            }
            // Desvío estándar muestral (denominador N-1) para no sesgar con ventanas chicas.
            return Math.Sqrt(sumSquaredDeviations / (window - 1));
        }
    }

    /// <summary>Matriz de features alineada con los timestamps de las barras que las originaron.</summary>
    public sealed record FeatureMatrix(double[][] Rows, DateTime[] TimestampsUtc);
}
