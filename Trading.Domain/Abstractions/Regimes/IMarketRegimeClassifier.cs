using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions.Regimes
{
    /// <summary>
    /// Contrato agnóstico del algoritmo de clasificación. El consumer downstream (registry,
    /// filtro en BarProcessingService) no sabe ni le importa si la implementación es HMM,
    /// k-means, red neuronal o ensemble.
    ///
    /// El clasificador es stateful: acumula barras internamente para construir la secuencia
    /// que el algoritmo subyacente necesita. Cada instancia clasifica un único instrumento.
    /// </summary>
    public interface IMarketRegimeClassifier
    {
        /// <summary>Instrumento para el cual este clasificador está entrenado/configurado.</summary>
        InstrumentId Instrument { get; }

        /// <summary>True cuando el clasificador tiene historia suficiente para clasificaciones confiables.</summary>
        bool IsWarmedUp { get; }

        /// <summary>
        /// Procesa una barra nueva y devuelve la clasificación actualizada.
        /// Si IsWarmedUp es false, debe retornar RegimeClassification.UnknownFor(...).
        /// Si la barra es de un instrumento distinto a Instrument, el comportamiento es
        /// implementation-defined (las implementaciones deben loguear y devolver UnknownFor).
        /// </summary>
        RegimeClassification Classify(MarketBar bar);
    }
}
