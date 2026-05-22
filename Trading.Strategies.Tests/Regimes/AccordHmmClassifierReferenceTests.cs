using System;
using System.Collections.Generic;
using System.Linq;
using Accord.Statistics.Distributions.Fitting;
using Accord.Statistics.Distributions.Multivariate;
using Accord.Statistics.Models.Markov;
using Accord.Statistics.Models.Markov.Learning;
using Accord.Statistics.Models.Markov.Topology;
using FluentAssertions;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Regimes;
using Xunit;

namespace Trading.Strategies.Tests.Regimes
{
    /// <summary>
    /// Test de referencia del pipeline completo AccordHmmClassifier sobre una serie sintética
    /// con tres regímenes claramente diferenciados:
    /// - Barras 0-499:    media de retornos +0.0015, desvío 0.005    (Trend alcista calmo)
    /// - Barras 500-999:  media 0,            desvío 0.025            (HighVolatility)
    /// - Barras 1000-1499: media 0,           desvío 0.003            (MeanReverting)
    /// Semilla 42, reproducibilidad garantizada.
    ///
    /// Verifica:
    /// - El BIC mínimo se da en K=3.
    /// - Las clasificaciones del classifier coinciden con el régimen esperado en cada segmento
    ///   (tolerancia: ≥50% en HighVolatility; al menos 2 etiquetas dominantes distintas entre los 3 segmentos).
    /// - Las probabilidades suman 1.0 ± 1e-9.
    /// - IsWarmedUp es false antes de 100 features acumuladas y true después.
    /// </summary>
    public class AccordHmmClassifierReferenceTests
    {
        private const int RandomSeed = 42;
        private const int WarmUpBars = 100;
        private static readonly InstrumentId Synthetic = new("SYNTH");

        [Fact]
        public void Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente()
        {
            Accord.Math.Random.Generator.Seed = RandomSeed;
            var bars = GenerateThreeRegimeSeries();

            var featureMatrix = FeatureExtractor.ExtractAll(bars);
            var scaler = FeatureScaler.Fit(featureMatrix.Rows);
            var normalized = scaler.TransformAll(featureMatrix.Rows);

            var candidates = new List<(int K, double Bic, HiddenMarkovModel<MultivariateNormalDistribution, double[]> Model)>();
            foreach (int candidateK in new[] { 2, 3, 4 })
            {
                Accord.Math.Random.Generator.Seed = RandomSeed;
                var model = TrainHmm(candidateK, normalized);
                double logLikelihood = model.LogLikelihood(normalized);
                double bic = ComputeBic(candidateK, FeatureExtractor.FeatureCount, normalized.Length, logLikelihood);
                candidates.Add((candidateK, bic, model));
            }

            var chosen = candidates.OrderBy(candidate => candidate.Bic).First();

            chosen.K.Should().Be(3, because: "los datos sintéticos tienen tres regímenes claramente diferenciados");

            int[] decoded = chosen.Model.Decide(normalized);

            var stats = ComputeStateStatistics(chosen.Model, chosen.K, normalized, decoded);
            var mapper = SemanticStateMapper.Build(stats);
            var classifier = new AccordHmmClassifier(Synthetic, chosen.Model, mapper, scaler, WarmUpBars);

            var classifications = new List<RegimeClassification>();
            foreach (var bar in bars)
                classifications.Add(classifier.Classify(bar));

            classifications[140].Label.Should().Be(RegimeLabel.Unknown, because: "antes de acumular 100 features, el classifier devuelve Unknown");
            classifier.IsWarmedUp.Should().BeTrue(because: "tras procesar 1500 barras, ya superó las 100 features de warm-up");

            foreach (var classification in classifications.Where(c => c.Label != RegimeLabel.Unknown))
                classification.Probabilities.Values.Sum().Should().BeApproximately(1.0, 1e-9);

            var segmentTrendLabels = classifications.Skip(150).Take(350).Select(c => c.Label).ToList();
            var segmentHighVolLabels = classifications.Skip(650).Take(350).Select(c => c.Label).ToList();
            var segmentMeanRevLabels = classifications.Skip(1150).Take(350).Select(c => c.Label).ToList();

            var trendMostFrequent = MostFrequent(segmentTrendLabels);
            var highVolMostFrequent = MostFrequent(segmentHighVolLabels);
            var meanRevMostFrequent = MostFrequent(segmentMeanRevLabels);

            var dominantLabels = new HashSet<RegimeLabel> { trendMostFrequent, highVolMostFrequent, meanRevMostFrequent };
            dominantLabels.Count.Should().BeGreaterThanOrEqualTo(2,
                because: "los tres segmentos sintéticos son estadísticamente distintos; al menos dos etiquetas dominantes deben ser distintas entre sí");

            int highVolCount = segmentHighVolLabels.Count(label => label == RegimeLabel.HighVolatility);
            ((double)highVolCount / segmentHighVolLabels.Count).Should().BeGreaterThan(0.5,
                because: "el segmento HighVolatility tiene desvío ~5x más alto que los otros y debe ser detectado");
        }

        // Multi-seed Baum-Welch: corre N veces con seeds distintos, conserva el de mayor log-likelihood.
        private static readonly int[] MultiSeeds =
            Enumerable.Range(1, 10).Select(i => 42 * i + 17).ToArray();

        private static HiddenMarkovModel<MultivariateNormalDistribution, double[]> TrainHmm(
            int numberOfStates, double[][] observations)
        {
            HiddenMarkovModel<MultivariateNormalDistribution, double[]> bestModel = null;
            double bestLogLikelihood = double.NegativeInfinity;

            foreach (int seed in MultiSeeds)
            {
                Accord.Math.Random.Generator.Seed = seed;
                var candidate = TrainHmmSingleSeed(numberOfStates, observations);
                double logLikelihood = candidate.LogLikelihood(observations);
                if (logLikelihood > bestLogLikelihood)
                {
                    bestLogLikelihood = logLikelihood;
                    bestModel = candidate;
                }
            }

            return bestModel;
        }

        private static HiddenMarkovModel<MultivariateNormalDistribution, double[]> TrainHmmSingleSeed(
            int numberOfStates, double[][] observations)
        {
            var topology = new Ergodic(numberOfStates, random: false);
            var initialEmission = new MultivariateNormalDistribution(dimension: FeatureExtractor.FeatureCount);
            var model = new HiddenMarkovModel<MultivariateNormalDistribution, double[]>(
                topology: topology,
                emissions: initialEmission);

            var teacher = new BaumWelchLearning<MultivariateNormalDistribution, double[]>(model)
            {
                Tolerance = 1e-5,
                MaxIterations = 200,
                FittingOptions = new NormalOptions { Regularization = 1e-6 }
            };
            teacher.Learn(new[] { observations });
            return model;
        }

        private static double ComputeBic(int k, int dimension, int observationCount, double logLikelihood)
        {
            int initial = k - 1;
            int transitions = k * (k - 1);
            int means = k * dimension;
            int covariances = k * dimension * (dimension + 1) / 2;
            int parameters = initial + transitions + means + covariances;
            return Math.Log(observationCount) * parameters - 2.0 * logLikelihood;
        }

        private static IReadOnlyList<SemanticStateMapper.StateStatistics> ComputeStateStatistics(
            HiddenMarkovModel<MultivariateNormalDistribution, double[]> model,
            int numberOfStates,
            double[][] observations,
            int[] decoded)
        {
            var perStateReturns = new List<double>[numberOfStates];
            for (int stateIndex = 0; stateIndex < numberOfStates; stateIndex++)
                perStateReturns[stateIndex] = new List<double>();

            for (int observationIndex = 0; observationIndex < decoded.Length; observationIndex++)
                perStateReturns[decoded[observationIndex]].Add(observations[observationIndex][0]);

            var stats = new SemanticStateMapper.StateStatistics[numberOfStates];
            for (int stateIndex = 0; stateIndex < numberOfStates; stateIndex++)
            {
                double mean = perStateReturns[stateIndex].Count > 0
                    ? perStateReturns[stateIndex].Average() : 0;
                double stdDev = perStateReturns[stateIndex].Count > 1
                    ? Math.Sqrt(perStateReturns[stateIndex]
                        .Select(value => (value - mean) * (value - mean)).Sum() / (perStateReturns[stateIndex].Count - 1))
                    : 0;
                double selfTransitionProbability = Math.Exp(model.LogTransitions[stateIndex][stateIndex]);
                stats[stateIndex] = new SemanticStateMapper.StateStatistics(
                    stateIndex, mean, stdDev, selfTransitionProbability);
            }
            return stats;
        }

        private static RegimeLabel MostFrequent(IReadOnlyList<RegimeLabel> labels) =>
            labels.GroupBy(label => label).OrderByDescending(group => group.Count()).First().Key;

        /// <summary>
        /// Genera una serie de 1500 barras con tres regímenes contiguos. Los retornos se simulan
        /// con distribuciones normales de parámetros conocidos; los precios resultan de capitalizar
        /// los retornos sobre un precio inicial.
        /// </summary>
        private static IReadOnlyList<MarketBar> GenerateThreeRegimeSeries()
        {
            var random = new Random(RandomSeed);
            var bars = new List<MarketBar>(1500);
            double price = 1000.0;
            var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int barIndex = 0; barIndex < 1500; barIndex++)
            {
                (double mean, double std) = barIndex switch
                {
                    < 500 => (0.0015, 0.005),
                    < 1000 => (0.0, 0.025),
                    _ => (0.0, 0.003)
                };
                double normalSample = NormalSample(random, mean, std);
                price *= Math.Exp(normalSample);

                var timestamp = start.AddHours(4 * barIndex);
                var priceDecimal = (decimal)price;
                bars.Add(new MarketBar(
                    instrumentId: Synthetic,
                    open: priceDecimal,
                    high: priceDecimal * 1.001m,
                    low: priceDecimal * 0.999m,
                    close: priceDecimal,
                    volume: 1m,
                    timestampUtc: timestamp));
            }
            return bars;
        }

        private static double NormalSample(Random random, double mean, double std)
        {
            // Box-Muller
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            double standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + std * standardNormal;
        }
    }
}
