namespace Trading.Domain.Abstractions.Regimes
{
    /// <summary>
    /// Etiqueta semántica del régimen de mercado. La semántica es estable entre instrumentos:
    /// "Trend" significa lo mismo para BTCUSDT que para SOLUSDT, aunque los parámetros estadísticos
    /// que el clasificador subyacente aprende para detectarlo sean distintos.
    ///
    /// Los valores son fixed por design: agregar uno nuevo es un cambio de contrato del Domain
    /// que requiere un ADR.
    /// </summary>
    public enum RegimeLabel
    {
        /// <summary>
        /// El régimen no pudo determinarse: clasificador en warm-up, instrumento sin clasificador
        /// registrado, o error de inferencia. Política del sistema: Unknown se trata como
        /// "compatible con cualquier estrategia" (fail-safe: no filtramos si no sabemos).
        /// </summary>
        Unknown = 0,

        /// <summary>Tendencia sostenida (alcista o bajista). Estrategias trend-following esperan operar acá.</summary>
        Trend = 1,

        /// <summary>Mercado lateral con reversión a la media. Estrategias mean-reverting esperan operar acá.</summary>
        MeanReverting = 2,

        /// <summary>Alta volatilidad sin dirección clara. La mayoría de las estrategias deben evitar operar.</summary>
        HighVolatility = 3,

        /// <summary>Compresión de volatilidad, baja actividad. Pre-breakout típico.</summary>
        Squeeze = 4
    }

    /// <summary>
    /// Helpers de parsing/serialización para RegimeLabel. Encapsulados acá para que los consumers
    /// no se acoplen a Enum.TryParse y para que los mensajes de error tengan formato consistente.
    /// </summary>
    public static class RegimeLabelParser
    {
        /// <summary>
        /// Convierte un string (típicamente desde strategies.json) a RegimeLabel.
        /// Lanza ArgumentException si el string no corresponde a ningún valor del enum
        /// (incluyendo "Unknown", que es válido sintácticamente pero no debería usarse
        /// en configuración de estrategias — fail loud para forzar al operador a ser explícito).
        /// </summary>
        public static RegimeLabel Parse(string regimeName)
        {
            if (string.IsNullOrWhiteSpace(regimeName))
                throw new System.ArgumentException(
                    "El nombre del régimen no puede ser nulo ni vacío.", nameof(regimeName));

            if (!System.Enum.TryParse<RegimeLabel>(regimeName, ignoreCase: false, out var parsed))
                throw new System.ArgumentException(
                    $"'{regimeName}' no es un RegimeLabel válido. Valores aceptados: " +
                    "Trend, MeanReverting, HighVolatility, Squeeze. " +
                    "(Unknown no se acepta como configuración explícita: si querés que la estrategia " +
                    "opere en todos los regímenes, omití el campo CompatibleRegimes del JSON.)",
                    nameof(regimeName));

            if (parsed == RegimeLabel.Unknown)
                throw new System.ArgumentException(
                    "'Unknown' no se acepta como configuración explícita de CompatibleRegimes. " +
                    "Si querés que la estrategia opere en todos los regímenes, omití el campo del JSON.",
                    nameof(regimeName));

            return parsed;
        }
    }
}
