using System;
using System.Linq;

namespace Trading.Strategies.Regimes
{
    /// <summary>
    /// Normalización z-score por feature: <c>x' = (x - μ) / σ</c>.
    ///
    /// Compartido entre el HmmTrainer (calcula μ y σ sobre el training set) y el
    /// AccordHmmClassifier (en runtime aplica esos mismos μ y σ sobre features nuevas).
    /// Los parámetros se serializan junto al modelo para garantizar consistencia trainer↔runtime.
    /// </summary>
    public sealed class FeatureScaler
    {
        public double[] Means { get; }
        public double[] StdDevs { get; }

        public int FeatureCount => Means.Length;

        public FeatureScaler(double[] means, double[] stdDevs)
        {
            if (means is null) throw new ArgumentNullException(nameof(means));
            if (stdDevs is null) throw new ArgumentNullException(nameof(stdDevs));
            if (means.Length != stdDevs.Length)
                throw new ArgumentException(
                    $"Means y StdDevs deben tener la misma longitud (means={means.Length}, stddev={stdDevs.Length}).");
            if (means.Length == 0)
                throw new ArgumentException("FeatureScaler requiere al menos una feature.", nameof(means));
            for (int featureIndex = 0; featureIndex < stdDevs.Length; featureIndex++)
            {
                if (stdDevs[featureIndex] <= 0)
                    throw new ArgumentException(
                        $"StdDev[{featureIndex}] debe ser estrictamente positivo (recibido: {stdDevs[featureIndex]}).",
                        nameof(stdDevs));
            }
            Means = means;
            StdDevs = stdDevs;
        }

        /// <summary>
        /// Construye un scaler calculando μ y σ por columna a partir del set de entrenamiento.
        /// </summary>
        public static FeatureScaler Fit(double[][] trainingFeatures)
        {
            if (trainingFeatures is null) throw new ArgumentNullException(nameof(trainingFeatures));
            if (trainingFeatures.Length == 0)
                throw new ArgumentException("No se puede ajustar el scaler con un set vacío.", nameof(trainingFeatures));

            int featureCount = trainingFeatures[0].Length;
            int rowCount = trainingFeatures.Length;

            var means = new double[featureCount];
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var row = trainingFeatures[rowIndex];
                if (row.Length != featureCount)
                    throw new ArgumentException(
                        $"Fila {rowIndex} tiene {row.Length} columnas; se esperaban {featureCount}.",
                        nameof(trainingFeatures));
                for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
                    means[featureIndex] += row[featureIndex];
            }
            for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
                means[featureIndex] /= rowCount;

            var stdDevs = new double[featureCount];
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var row = trainingFeatures[rowIndex];
                for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
                {
                    double deviation = row[featureIndex] - means[featureIndex];
                    stdDevs[featureIndex] += deviation * deviation;
                }
            }
            for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
            {
                double variance = stdDevs[featureIndex] / Math.Max(1, rowCount - 1);
                stdDevs[featureIndex] = Math.Sqrt(variance);
                if (stdDevs[featureIndex] == 0)
                    throw new InvalidOperationException(
                        $"Feature {featureIndex} tiene varianza cero en el training set; el HMM no puede entrenarse con una feature constante.");
            }

            return new FeatureScaler(means, stdDevs);
        }

        public double[] Transform(double[] features)
        {
            if (features is null) throw new ArgumentNullException(nameof(features));
            if (features.Length != FeatureCount)
                throw new ArgumentException(
                    $"Vector de features con dimensión {features.Length}; se esperaba {FeatureCount}.",
                    nameof(features));

            var transformed = new double[FeatureCount];
            for (int featureIndex = 0; featureIndex < FeatureCount; featureIndex++)
                transformed[featureIndex] = (features[featureIndex] - Means[featureIndex]) / StdDevs[featureIndex];
            return transformed;
        }

        public double[][] TransformAll(double[][] rows)
        {
            if (rows is null) throw new ArgumentNullException(nameof(rows));
            return rows.Select(Transform).ToArray();
        }
    }
}
