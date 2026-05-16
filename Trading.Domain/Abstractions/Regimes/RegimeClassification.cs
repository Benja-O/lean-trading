using System;
using System.Collections.Generic;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions.Regimes
{
    /// <summary>
    /// Resultado inmutable de una clasificación de régimen. Incluye la etiqueta más probable
    /// y la distribución completa de probabilidades para que los consumers downstream puedan
    /// implementar políticas graduadas en el futuro (ej. position sizing proporcional a la
    /// probabilidad del régimen). En el Paso 2 solo se consulta Label; las probabilidades
    /// son parte del contrato para que el Paso 3 (HMM real) no requiera cambiar la interfaz.
    /// </summary>
    public sealed record RegimeClassification(
        InstrumentId Instrument,
        RegimeLabel Label,
        IReadOnlyDictionary<RegimeLabel, double> Probabilities,
        DateTime ClassifiedAtUtc)
    {
        /// <summary>
        /// Construye una clasificación "Unknown" para un instrumento. Política del sistema:
        /// Unknown se interpreta como "compatible con cualquier estrategia" (fail-safe).
        /// </summary>
        public static RegimeClassification UnknownFor(InstrumentId instrument, DateTime classifiedAtUtc) =>
            new(
                instrument,
                RegimeLabel.Unknown,
                new Dictionary<RegimeLabel, double> { [RegimeLabel.Unknown] = 1.0 },
                classifiedAtUtc);
    }
}
