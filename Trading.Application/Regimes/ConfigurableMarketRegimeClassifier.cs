using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Regimes
{
    /// <summary>
    /// Clasificador determinista que devuelve siempre la misma etiqueta con probabilidad 1.0.
    /// Útil para:
    /// - Validar el wiring completo del filtro de régimen sin necesidad de un modelo real.
    /// - Tests del MarketRegimeRegistry y del filtro en BarProcessingService.
    /// - Backtests de control donde se quiere forzar un régimen específico para comparar.
    ///
    /// IsWarmedUp es siempre true porque no hay nada que "warmar".
    ///
    /// En el Paso 3 de Hito B este classifier coexiste con AccordHmmClassifier: ambos implementan
    /// IMarketRegimeClassifier y son intercambiables en el wiring.
    /// </summary>
    public sealed class ConfigurableMarketRegimeClassifier : IMarketRegimeClassifier
    {
        private readonly RegimeLabel _fixedLabel;
        private readonly IClock _clock;

        public InstrumentId Instrument { get; }
        public bool IsWarmedUp => true;

        public ConfigurableMarketRegimeClassifier(InstrumentId instrument, RegimeLabel fixedLabel, IClock clock)
        {
            Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            if (fixedLabel == RegimeLabel.Unknown)
                throw new ArgumentException(
                    "ConfigurableMarketRegimeClassifier no debe configurarse con Unknown: " +
                    "si querés clasificación Unknown, no registres un classifier para el instrumento " +
                    "(el registry devuelve UnknownFor automáticamente).",
                    nameof(fixedLabel));
            _fixedLabel = fixedLabel;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public RegimeClassification Classify(MarketBar bar)
        {
            // El fake no usa los datos de la barra; solo devuelve la etiqueta fija.
            // Igual respeta el contrato de timestamp para que los consumers vean coherencia temporal.
            return new RegimeClassification(
                Instrument,
                _fixedLabel,
                new Dictionary<RegimeLabel, double> { [_fixedLabel] = 1.0 },
                bar.TimestampUtc);
        }
    }
}
