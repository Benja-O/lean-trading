using System;
using System.Collections.Generic;
using System.IO;
using Accord.Statistics.Distributions.Multivariate;
using Accord.Statistics.Models.Markov;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Regimes
{
    /// <summary>
    /// Reconstruye un <see cref="AccordHmmClassifier"/> a partir del archivo JSON serializado
    /// por el <c>HmmTrainer</c>. Único entrypoint público que usa el host en runtime.
    /// </summary>
    public static class AccordHmmClassifierFactory
    {
        public static AccordHmmClassifier Load(string modelJsonPath)
        {
            var persisted = HmmModelSerializer.Load(modelJsonPath);
            return Build(persisted);
        }

        public static AccordHmmClassifier Build(PersistedHmmModel persisted)
        {
            if (persisted is null) throw new ArgumentNullException(nameof(persisted));
            if (persisted.NumberOfStates <= 0)
                throw new InvalidDataException("NumberOfStates debe ser positivo.");

            var emissions = new MultivariateNormalDistribution[persisted.NumberOfStates];
            for (int stateIndex = 0; stateIndex < persisted.NumberOfStates; stateIndex++)
            {
                var mean = persisted.EmissionMeans[stateIndex];
                var covariance = ToMatrix(persisted.EmissionCovariances[stateIndex]);
                emissions[stateIndex] = new MultivariateNormalDistribution(mean, covariance);
            }

            var transitions = ToMatrix(persisted.TransitionMatrix);
            var initial = persisted.InitialProbabilities;

            var model = new HiddenMarkovModel<MultivariateNormalDistribution, double[]>(
                transitions: transitions,
                emissions: emissions,
                probabilities: initial);

            var scaler = new FeatureScaler(persisted.FeatureScalerMeans, persisted.FeatureScalerStdDevs);

            var labelMapping = new Dictionary<int, RegimeLabel>();
            foreach (var pair in persisted.StateToRegimeLabel)
                labelMapping[pair.Key] = ParseRegimeLabel(pair.Value);
            var mapper = new SemanticStateMapper(labelMapping);

            var instrument = new InstrumentId(persisted.InstrumentIdentifier);
            return new AccordHmmClassifier(instrument, model, mapper, scaler, persisted.WarmUpBars);
        }

        private static RegimeLabel ParseRegimeLabel(string raw)
        {
            if (Enum.TryParse<RegimeLabel>(raw, ignoreCase: false, out var parsed))
                return parsed;
            throw new InvalidDataException(
                $"Valor de RegimeLabel inválido en el modelo serializado: '{raw}'. " +
                "Valores aceptados: Unknown, Trend, MeanReverting, HighVolatility, Squeeze.");
        }

        private static double[,] ToMatrix(double[][] jagged)
        {
            if (jagged is null) throw new ArgumentNullException(nameof(jagged));
            if (jagged.Length == 0) throw new ArgumentException("Matriz vacía.", nameof(jagged));
            int rows = jagged.Length;
            int columns = jagged[0].Length;
            var matrix = new double[rows, columns];
            for (int rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                if (jagged[rowIndex].Length != columns)
                    throw new ArgumentException(
                        $"Fila {rowIndex} con {jagged[rowIndex].Length} columnas; esperadas {columns}.",
                        nameof(jagged));
                for (int columnIndex = 0; columnIndex < columns; columnIndex++)
                    matrix[rowIndex, columnIndex] = jagged[rowIndex][columnIndex];
            }
            return matrix;
        }
    }
}
