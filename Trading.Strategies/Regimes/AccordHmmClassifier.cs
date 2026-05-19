using System;
using System.Collections.Generic;
using System.Linq;
using Accord.Statistics.Distributions.Multivariate;
using Accord.Statistics.Models.Markov;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Regimes
{
    /// <summary>
    /// Implementación de <see cref="IMarketRegimeClassifier"/> sobre un Hidden Markov Model
    /// continuo (Accord.NET) con emisiones Multivariate Gaussian.
    ///
    /// Pipeline por barra:
    /// <list type="number">
    ///   <item>La barra se agrega a un buffer rolling de barras crudas.</item>
    ///   <item>Si todavía no hay <see cref="FeatureExtractor.FeatureWarmUpBars"/> barras previas para
    ///         calcular features con SMA50, se devuelve <c>UnknownFor</c>.</item>
    ///   <item>Se calculan las 3 features de la barra actual y se normalizan con el scaler
    ///         entrenado offline.</item>
    ///   <item>La feature normalizada se agrega al buffer rolling de features.</item>
    ///   <item>Si <see cref="IsWarmedUp"/> es false (menos de <c>WarmUpBars</c> features acumuladas),
    ///         se devuelve <c>UnknownFor</c>.</item>
    ///   <item>Se ejecuta Viterbi sobre toda la secuencia del buffer y forward filtering para
    ///         obtener la distribución posterior del último paso.</item>
    ///   <item>El estado más probable y el dict de probabilidades se mapean a <see cref="RegimeLabel"/>
    ///         vía <see cref="SemanticStateMapper"/>; las probabilidades se agregan por label.</item>
    /// </list>
    /// </summary>
    public sealed class AccordHmmClassifier : IMarketRegimeClassifier
    {
        private readonly HiddenMarkovModel<MultivariateNormalDistribution, double[]> _model;
        private readonly SemanticStateMapper _semanticMapper;
        private readonly FeatureScaler _scaler;
        private readonly int _warmUpBars;
        private readonly LinkedList<decimal> _rawCloseBuffer;
        private readonly LinkedList<double[]> _normalizedFeatureBuffer;

        public InstrumentId Instrument { get; }
        public bool IsWarmedUp => _normalizedFeatureBuffer.Count >= _warmUpBars;

        public AccordHmmClassifier(
            InstrumentId instrument,
            HiddenMarkovModel<MultivariateNormalDistribution, double[]> model,
            SemanticStateMapper semanticMapper,
            FeatureScaler scaler,
            int warmUpBars)
        {
            Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _semanticMapper = semanticMapper ?? throw new ArgumentNullException(nameof(semanticMapper));
            _scaler = scaler ?? throw new ArgumentNullException(nameof(scaler));
            if (warmUpBars <= 0) throw new ArgumentOutOfRangeException(nameof(warmUpBars), "Debe ser > 0.");
            _warmUpBars = warmUpBars;
            _rawCloseBuffer = new LinkedList<decimal>();
            _normalizedFeatureBuffer = new LinkedList<double[]>();
        }

        public RegimeClassification Classify(MarketBar bar)
        {
            if (bar is null) throw new ArgumentNullException(nameof(bar));
            if (!bar.InstrumentId.Equals(Instrument))
                return RegimeClassification.UnknownFor(bar.InstrumentId, bar.TimestampUtc);

            AppendRawClose(bar.Close);

            int requiredHistory = FeatureExtractor.FeatureWarmUpBars + 1;
            if (_rawCloseBuffer.Count < requiredHistory)
                return RegimeClassification.UnknownFor(Instrument, bar.TimestampUtc);

            var closesArray = _rawCloseBuffer.Select(value => (double)value).ToArray();
            var rawFeatures = FeatureExtractor.ExtractAt(closesArray, closesArray.Length - 1);
            var normalizedFeatures = _scaler.Transform(rawFeatures);

            AppendNormalizedFeature(normalizedFeatures);

            if (!IsWarmedUp)
                return RegimeClassification.UnknownFor(Instrument, bar.TimestampUtc);

            var observationSequence = _normalizedFeatureBuffer.ToArray();
            int[] decodedStates = _model.Decide(observationSequence);
            int lastState = decodedStates[^1];

            double[][] posteriorMatrix = _model.Posterior(observationSequence);
            int lastStepIndex = observationSequence.Length - 1;
            double[] lastStepPosterior = posteriorMatrix[lastStepIndex];
            int stateCount = lastStepPosterior.Length;

            var aggregatedProbabilities = new Dictionary<RegimeLabel, double>();
            for (int stateIndex = 0; stateIndex < stateCount; stateIndex++)
            {
                double stateProbability = lastStepPosterior[stateIndex];
                RegimeLabel label = _semanticMapper.MapState(stateIndex);
                if (aggregatedProbabilities.TryGetValue(label, out double accumulated))
                    aggregatedProbabilities[label] = accumulated + stateProbability;
                else
                    aggregatedProbabilities[label] = stateProbability;
            }

            RegimeLabel dominantLabel = _semanticMapper.MapState(lastState);
            return new RegimeClassification(Instrument, dominantLabel, aggregatedProbabilities, bar.TimestampUtc);
        }

        private void AppendRawClose(decimal close)
        {
            _rawCloseBuffer.AddLast(close);
            int maxRawBufferSize = FeatureExtractor.FeatureWarmUpBars + 1;
            while (_rawCloseBuffer.Count > maxRawBufferSize)
                _rawCloseBuffer.RemoveFirst();
        }

        private void AppendNormalizedFeature(double[] features)
        {
            _normalizedFeatureBuffer.AddLast(features);
            while (_normalizedFeatureBuffer.Count > _warmUpBars)
                _normalizedFeatureBuffer.RemoveFirst();
        }
    }
}
