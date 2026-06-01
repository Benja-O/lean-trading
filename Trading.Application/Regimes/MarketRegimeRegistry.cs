using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Regimes
{
    /// <summary>
    /// Registry de clasificadores por instrumento. Mantiene:
    /// - El mapa instrumento → IMarketRegimeClassifier.
    /// - La última clasificación procesada por instrumento (cache para consultas del filtro).
    ///
    /// Las barras se le pasan vía ClassifyBar (típicamente desde un consolidator del timeframe
    /// del régimen). Las consultas del filtro usan GetLastClassification para evitar
    /// re-clasificación en cada barra de timeframe inferior.
    ///
    /// Si un instrumento no tiene clasificador registrado, GetLastClassification devuelve
    /// RegimeClassification.UnknownFor(...): política fail-safe.
    /// </summary>
    public sealed class MarketRegimeRegistry
    {
        private readonly Dictionary<InstrumentId, IMarketRegimeClassifier> _classifiers;
        private readonly Dictionary<InstrumentId, RegimeClassification> _lastClassifications;
        private readonly IClock _clock;
        private readonly ITradingLogger _logger;

        public MarketRegimeRegistry(
            IEnumerable<IMarketRegimeClassifier> classifiers,
            IClock clock,
            ITradingLogger logger)
        {
            if (classifiers == null) throw new ArgumentNullException(nameof(classifiers));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _classifiers = new Dictionary<InstrumentId, IMarketRegimeClassifier>();
            _lastClassifications = new Dictionary<InstrumentId, RegimeClassification>();

            foreach (var classifier in classifiers)
            {
                if (_classifiers.ContainsKey(classifier.Instrument))
                    throw new InvalidOperationException(
                        $"MarketRegimeRegistry: ya existe un clasificador registrado para {classifier.Instrument}. " +
                        "Cada instrumento debe tener exactamente un clasificador.");
                _classifiers[classifier.Instrument] = classifier;
            }
        }

        /// <summary>
        /// Procesa una barra para el instrumento correspondiente y actualiza la última clasificación.
        /// Si no hay clasificador registrado para el instrumento de la barra, loguea Debug y no hace nada.
        /// Nunca lanza ante errores de instrumento desconocido (fail-safe).
        /// </summary>
        public RegimeClassification ClassifyBar(MarketBar bar)
        {
            if (bar == null) throw new ArgumentNullException(nameof(bar));

            if (!_classifiers.TryGetValue(bar.InstrumentId, out var classifier))
            {
                _logger.Debug(
                    "MarketRegimeRegistry: barra recibida para {InstrumentId} pero no hay clasificador registrado. Se ignora.",
                    bar.InstrumentId);
                var unknown = RegimeClassification.UnknownFor(bar.InstrumentId, bar.TimestampUtc);
                _lastClassifications[bar.InstrumentId] = unknown;
                return unknown;
            }

            var classification = classifier.Classify(bar);
            _lastClassifications[bar.InstrumentId] = classification;
            _logger.Debug(
                "MarketRegimeRegistry: {InstrumentId} → régimen {Regime}.",
                bar.InstrumentId, classification.Label);
            return classification;
        }

        /// <summary>
        /// Devuelve la última clasificación procesada para el instrumento. Si nunca se procesó
        /// una barra para ese instrumento, devuelve UnknownFor con timestamp del reloj actual.
        /// </summary>
        public RegimeClassification GetLastClassification(InstrumentId instrument)
        {
            if (instrument == null) throw new ArgumentNullException(nameof(instrument));

            if (_lastClassifications.TryGetValue(instrument, out var classification))
                return classification;

            return RegimeClassification.UnknownFor(instrument, _clock.UtcNow);
        }

        public bool HasClassifier(InstrumentId instrument)
        {
            if (instrument == null) throw new ArgumentNullException(nameof(instrument));
            return _classifiers.ContainsKey(instrument);
        }

        /// <summary>
        /// Devuelve los instrumentos para los cuales hay un classifier registrado.
        /// Útil para el wiring agnóstico del host: en lugar de hardcodear la lista
        /// de instrumentos del régimen, el host itera sobre los registrados.
        /// </summary>
        public IReadOnlySet<InstrumentId> GetRegisteredInstruments() =>
            new HashSet<InstrumentId>(_classifiers.Keys);
    }
}
