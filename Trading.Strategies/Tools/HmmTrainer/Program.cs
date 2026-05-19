using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Accord.MachineLearning;
using Accord.Statistics.Distributions.Fitting;
using Accord.Statistics.Distributions.Multivariate;
using Accord.Statistics.Models.Markov;
using Accord.Statistics.Models.Markov.Learning;
using Accord.Statistics.Models.Markov.Topology;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Regimes;

namespace Trading.Strategies.Tools.HmmTrainer
{
    /// <summary>
    /// Trainer offline del HMM para clasificación de régimen de mercado.
    ///
    /// Parametrizado (hardcoded por simplicidad del Paso 3): BTCUSDT perpetual Binance 4h,
    /// ventana 2020-01-01 a 2024-12-31 UTC, K ∈ {2, 3, 4}, semilla 42, warm-up del modelo 100 barras.
    /// </summary>
    public static class Program
    {
        private const string InstrumentIdentifier = "BTCUSDT";
        private const string Exchange = "Binance";
        private const string ContractType = "perpetual";
        private const string Timeframe = "4h";
        private const int WarmUpBarsForRuntime = 100;
        private const int RandomSeed = 42;
        private const int MinimumRequiredBars = 10000;

        private static readonly int[] CandidateNumbersOfStates = { 2, 3, 4 };
        private static readonly DateTime TrainingWindowStartUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime TrainingWindowEndUtc = new(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        public static int Main(string[] arguments)
        {
            try
            {
                string defaultDataDirectory = @"F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\4h\BTCUSDT";
                string defaultOutputPath = ResolveDefaultOutputPath();

                string dataDirectory = arguments.Length > 0 ? arguments[0] : defaultDataDirectory;
                string outputPath = arguments.Length > 1 ? arguments[1] : defaultOutputPath;

                Console.WriteLine($"[HmmTrainer] Iniciando entrenamiento. Datos: '{dataDirectory}', salida: '{outputPath}'.");

                Accord.Math.Random.Generator.Seed = RandomSeed;

                var instrument = new InstrumentId(InstrumentIdentifier);
                var parser = new BinanceKlinesParser(instrument);
                var bars = parser.ParseDirectory(dataDirectory, TrainingWindowStartUtc, TrainingWindowEndUtc);
                Console.WriteLine($"[HmmTrainer] Barras parseadas en ventana de entrenamiento: {bars.Count}.");
                if (bars.Count < MinimumRequiredBars)
                    throw new InvalidOperationException(
                        $"Set de entrenamiento insuficiente: {bars.Count} barras. Mínimo requerido: {MinimumRequiredBars}. " +
                        $"Verificar contenido del directorio '{dataDirectory}' y la ventana solicitada.");

                var featureMatrix = FeatureExtractor.ExtractAll(bars);
                Console.WriteLine($"[HmmTrainer] Features extraídas: {featureMatrix.Rows.Length} filas, dimensión {FeatureExtractor.FeatureCount}.");

                var scaler = FeatureScaler.Fit(featureMatrix.Rows);
                var normalizedRows = scaler.TransformAll(featureMatrix.Rows);

                var candidateResults = new List<CandidateResult>();
                foreach (int numberOfStates in CandidateNumbersOfStates)
                {
                    Console.WriteLine($"[HmmTrainer] Entrenando candidato K={numberOfStates}...");
                    var result = TrainCandidate(numberOfStates, normalizedRows);
                    Console.WriteLine($"[HmmTrainer] K={numberOfStates}: logLikelihood={result.LogLikelihood:F4}, BIC={result.Bic:F4}");
                    candidateResults.Add(result);
                }

                var selectedCandidate = candidateResults.OrderBy(candidate => candidate.Bic).First();
                Console.WriteLine($"[HmmTrainer] BIC mínimo: K={selectedCandidate.NumberOfStates} (BIC={selectedCandidate.Bic:F4}).");

                int[] decodedStates = selectedCandidate.Model.Decide(normalizedRows);
                var stateStatistics = ComputeStateStatistics(selectedCandidate.Model, selectedCandidate.NumberOfStates, normalizedRows, decodedStates);
                var mapper = SemanticStateMapper.Build(stateStatistics);

                LogStateStatistics(stateStatistics, mapper);

                var persisted = BuildPersistedModel(
                    selectedCandidate.Model,
                    selectedCandidate.NumberOfStates,
                    selectedCandidate.Bic,
                    scaler,
                    mapper);

                HmmModelSerializer.Save(persisted, outputPath);
                Console.WriteLine($"[HmmTrainer] Modelo serializado en '{outputPath}'.");

                Console.WriteLine(
                    string.Format(CultureInfo.InvariantCulture,
                        "[HmmTrainer] Trained HMM: instrument={0}, K={1}, BIC={2:F4}, training_window=[{3:yyyy-MM-dd}, {4:yyyy-MM-dd}], n_bars={5}, mapping={{{6}}}",
                        InstrumentIdentifier,
                        selectedCandidate.NumberOfStates,
                        selectedCandidate.Bic,
                        TrainingWindowStartUtc,
                        TrainingWindowEndUtc,
                        normalizedRows.Length,
                        string.Join(", ", mapper.StateToLabel.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"))));

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"[HmmTrainer] ERROR: {exception.GetType().Name}: {exception.Message}");
                Console.Error.WriteLine(exception.StackTrace);
                return 1;
            }
        }

        private static string ResolveDefaultOutputPath()
        {
            // El proyecto vive en {repoRoot}/Trading.Strategies/Tools/HmmTrainer.
            // Subir tres niveles desde AppContext.BaseDirectory no funciona porque está en bin/.
            // Resolvemos partiendo de la ubicación conocida del .csproj relativa al proyecto.
            string assemblyDirectory = AppContext.BaseDirectory;
            // Subir cinco niveles para llegar al repo root cuando se ejecuta desde bin/Debug/net10.0/.
            var candidate = new DirectoryInfo(assemblyDirectory);
            while (candidate != null && !File.Exists(Path.Combine(candidate.FullName, "QuantConnect.Lean.sln")))
                candidate = candidate.Parent;
            if (candidate is null)
                throw new InvalidOperationException(
                    "No pude localizar el repo root (QuantConnect.Lean.sln) subiendo desde el directorio de ejecución. " +
                    "Pasá el output path como segundo argumento.");
            return Path.Combine(candidate.FullName, "models", "regime", $"{InstrumentIdentifier}-perp-binance.hmm.json");
        }

        private static CandidateResult TrainCandidate(int numberOfStates, double[][] normalizedObservations)
        {
            // Inicialización canónica HMM-GMM: k-means clustering de las observaciones para
            // arrancar BaumWelch con emisiones diferenciadas (rompe simetría inicial sin la cual
            // el algoritmo queda en óptimo trivial con todas las transiciones uniformes).
            var kmeans = new KMeans(k: numberOfStates);
            var clustering = kmeans.Learn(normalizedObservations);
            var emissions = new MultivariateNormalDistribution[numberOfStates];
            for (int stateIndex = 0; stateIndex < numberOfStates; stateIndex++)
            {
                var clusterMean = clustering.Centroids[stateIndex];
                var clusterCovariance = BuildClusterCovariance(normalizedObservations, clustering.Decide(normalizedObservations), stateIndex, clusterMean);
                emissions[stateIndex] = new MultivariateNormalDistribution(clusterMean, clusterCovariance);
            }

            double[] initialProbabilities = Enumerable.Repeat(1.0 / numberOfStates, numberOfStates).ToArray();
            double[,] transitionMatrix = new double[numberOfStates, numberOfStates];
            for (int fromState = 0; fromState < numberOfStates; fromState++)
                for (int toState = 0; toState < numberOfStates; toState++)
                    transitionMatrix[fromState, toState] = 1.0 / numberOfStates;

            var model = new HiddenMarkovModel<MultivariateNormalDistribution, double[]>(
                transitions: transitionMatrix,
                emissions: emissions,
                probabilities: initialProbabilities);

            var teacher = new BaumWelchLearning<MultivariateNormalDistribution, double[]>(model)
            {
                Tolerance = 1e-5,
                MaxIterations = 200,
                FittingOptions = new NormalOptions { Regularization = 1e-6 }
            };

            teacher.Learn(new[] { normalizedObservations });

            double logLikelihood = model.LogLikelihood(normalizedObservations);
            double bic = BicCalculator.Compute(
                numberOfStates: numberOfStates,
                featureDimension: FeatureExtractor.FeatureCount,
                observationCount: normalizedObservations.Length,
                finalLogLikelihood: logLikelihood);

            return new CandidateResult(numberOfStates, model, logLikelihood, bic);
        }

        private static double[,] BuildClusterCovariance(
            double[][] observations, int[] clusterAssignment, int targetCluster, double[] clusterMean)
        {
            int dimension = clusterMean.Length;
            var covariance = new double[dimension, dimension];
            int count = 0;
            for (int observationIndex = 0; observationIndex < observations.Length; observationIndex++)
            {
                if (clusterAssignment[observationIndex] != targetCluster) continue;
                count++;
                var deviation = new double[dimension];
                for (int featureIndex = 0; featureIndex < dimension; featureIndex++)
                    deviation[featureIndex] = observations[observationIndex][featureIndex] - clusterMean[featureIndex];
                for (int rowIndex = 0; rowIndex < dimension; rowIndex++)
                    for (int columnIndex = 0; columnIndex < dimension; columnIndex++)
                        covariance[rowIndex, columnIndex] += deviation[rowIndex] * deviation[columnIndex];
            }
            int denominator = Math.Max(1, count - 1);
            for (int rowIndex = 0; rowIndex < dimension; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < dimension; columnIndex++)
                    covariance[rowIndex, columnIndex] /= denominator;
                // Regularización mínima para garantizar matriz definida positiva ante clusters pequeños.
                covariance[rowIndex, rowIndex] += 1e-6;
            }
            return covariance;
        }

        private static IReadOnlyList<SemanticStateMapper.StateStatistics> ComputeStateStatistics(
            HiddenMarkovModel<MultivariateNormalDistribution, double[]> model,
            int numberOfStates,
            double[][] normalizedObservations,
            int[] decodedStates)
        {
            // El "retorno" para mapeo semántico es el primer elemento de la feature CRUDA, no la normalizada.
            // Reconstruimos retornos log a partir del path decodificado: para el bar t en estado s, el return
            // viene del feature row[0] sin normalizar. Como no guardamos el row sin normalizar, lo recuperamos
            // multiplicando por StdDev[0] y sumando Mean[0]. Esto es exacto (z * σ + μ = x).
            // Pero en realidad acá solo tenemos las normalizadas; el caller debe entregar también las crudas.
            // Para simplificar, calculamos μ y σ del estado sobre la primera feature normalizada y los
            // des-normalizamos al final.
            var perStateNormalizedReturns = new List<double>[numberOfStates];
            for (int stateIndex = 0; stateIndex < numberOfStates; stateIndex++)
                perStateNormalizedReturns[stateIndex] = new List<double>();

            for (int observationIndex = 0; observationIndex < decodedStates.Length; observationIndex++)
            {
                int stateForBar = decodedStates[observationIndex];
                perStateNormalizedReturns[stateForBar].Add(normalizedObservations[observationIndex][0]);
            }

            // Comodidad: la media de retornos del estado en escala original ≈ μ_state[0] · σ_global[0] + μ_global[0].
            // Pero el SemanticStateMapper compara umbrales en escala original (|μ| > 0.001 son retornos log
            // 4h). Para preservar la semántica del brief y no acoplar el mapper al scaler, usamos las
            // emisiones del modelo (medias en espacio normalizado) y las des-normalizamos.
            var statistics = new SemanticStateMapper.StateStatistics[numberOfStates];
            for (int stateIndex = 0; stateIndex < numberOfStates; stateIndex++)
            {
                double meanNormalized = perStateNormalizedReturns[stateIndex].Count > 0
                    ? perStateNormalizedReturns[stateIndex].Average()
                    : 0;
                double stdDevNormalized = perStateNormalizedReturns[stateIndex].Count > 1
                    ? Sqrt(VarianceOf(perStateNormalizedReturns[stateIndex]))
                    : 0;

                double selfTransitionProbability = Math.Exp(model.LogTransitions[stateIndex][stateIndex]);

                statistics[stateIndex] = new SemanticStateMapper.StateStatistics(
                    StateIndex: stateIndex,
                    MeanReturn: meanNormalized,           // ya está en escala estandarizada
                    StdDevReturn: stdDevNormalized,        // ya está en escala estandarizada
                    SelfTransitionProbability: selfTransitionProbability);
            }
            return statistics;
        }

        private static double Sqrt(double value) => Math.Sqrt(value);

        private static double VarianceOf(IReadOnlyList<double> values)
        {
            double mean = values.Average();
            double sumSquared = 0;
            for (int i = 0; i < values.Count; i++)
            {
                double dev = values[i] - mean;
                sumSquared += dev * dev;
            }
            return sumSquared / (values.Count - 1);
        }

        private static void LogStateStatistics(
            IReadOnlyList<SemanticStateMapper.StateStatistics> statistics,
            SemanticStateMapper mapper)
        {
            Console.WriteLine("[HmmTrainer] Estadísticas por estado y mapeo semántico:");
            foreach (var stat in statistics)
            {
                var label = mapper.MapState(stat.StateIndex);
                Console.WriteLine(
                    string.Format(CultureInfo.InvariantCulture,
                        "  state={0} μ={1:F6} σ={2:F6} ρ={3:F4} → {4}",
                        stat.StateIndex, stat.MeanReturn, stat.StdDevReturn, stat.SelfTransitionProbability, label));
            }
        }

        private static PersistedHmmModel BuildPersistedModel(
            HiddenMarkovModel<MultivariateNormalDistribution, double[]> model,
            int numberOfStates,
            double bic,
            FeatureScaler scaler,
            SemanticStateMapper mapper)
        {
            var initialProbabilities = new double[numberOfStates];
            for (int stateIndex = 0; stateIndex < numberOfStates; stateIndex++)
                initialProbabilities[stateIndex] = Math.Exp(model.LogInitial[stateIndex]);

            var transitionMatrix = new double[numberOfStates][];
            for (int fromState = 0; fromState < numberOfStates; fromState++)
            {
                transitionMatrix[fromState] = new double[numberOfStates];
                for (int toState = 0; toState < numberOfStates; toState++)
                    transitionMatrix[fromState][toState] = Math.Exp(model.LogTransitions[fromState][toState]);
            }

            var emissionMeans = new double[numberOfStates][];
            var emissionCovariances = new double[numberOfStates][][];
            for (int stateIndex = 0; stateIndex < numberOfStates; stateIndex++)
            {
                var distribution = model.Emissions[stateIndex];
                emissionMeans[stateIndex] = distribution.Mean.ToArray();
                var covariance = distribution.Covariance;
                int dimension = emissionMeans[stateIndex].Length;
                emissionCovariances[stateIndex] = new double[dimension][];
                for (int rowIndex = 0; rowIndex < dimension; rowIndex++)
                {
                    emissionCovariances[stateIndex][rowIndex] = new double[dimension];
                    for (int columnIndex = 0; columnIndex < dimension; columnIndex++)
                        emissionCovariances[stateIndex][rowIndex][columnIndex] = covariance[rowIndex, columnIndex];
                }
            }

            var stateToLabel = new Dictionary<int, string>();
            foreach (var pair in mapper.StateToLabel)
                stateToLabel[pair.Key] = pair.Value.ToString();

            return new PersistedHmmModel(
                InstrumentIdentifier: InstrumentIdentifier,
                Exchange: Exchange,
                ContractType: ContractType,
                Timeframe: Timeframe,
                TrainedAtUtc: DateTime.UtcNow,
                TrainingWindowStartUtc: TrainingWindowStartUtc,
                TrainingWindowEndUtc: TrainingWindowEndUtc,
                NumberOfStates: numberOfStates,
                FinalBic: bic,
                InitialProbabilities: initialProbabilities,
                TransitionMatrix: transitionMatrix,
                EmissionMeans: emissionMeans,
                EmissionCovariances: emissionCovariances,
                FeatureScalerMeans: scaler.Means,
                FeatureScalerStdDevs: scaler.StdDevs,
                StateToRegimeLabel: stateToLabel,
                WarmUpBars: WarmUpBarsForRuntime);
        }

        private sealed record CandidateResult(
            int NumberOfStates,
            HiddenMarkovModel<MultivariateNormalDistribution, double[]> Model,
            double LogLikelihood,
            double Bic);
    }
}
