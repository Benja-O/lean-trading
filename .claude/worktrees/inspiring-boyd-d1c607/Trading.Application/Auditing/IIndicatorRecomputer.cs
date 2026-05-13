using System.Collections.Generic;
using Trading.Domain.Models;

namespace Trading.Application.Auditing
{
    /// <summary>
    /// Recalcula independientemente los indicadores de una estrategia a partir de una serie
    /// de MarketBar observadas. El auditor compara el resultado con los SignalDiagnostics
    /// declarados por la estrategia.
    ///
    /// Implementación clave: NO debe reusar las instancias de indicadores de la estrategia.
    /// Debe construir su propia secuencia de cálculo desde los datos crudos. Esto es lo que
    /// hace que el chequeo detecte bugs de estado interno o de flujo de control en la estrategia.
    /// </summary>
    public interface IIndicatorRecomputer
    {
        /// <summary>
        /// Nombre de la estrategia que este recomputer audita. Debe coincidir EXACTAMENTE
        /// con el StrategyName configurado en strategies.json (case-insensitive aceptable
        /// si el registry normaliza).
        /// </summary>
        string StrategyName { get; }

        /// <summary>
        /// Recalcula los indicadores desde la serie observada y devuelve los valores
        /// esperados en el momento de la última barra.
        ///
        /// La serie viene en orden cronológico ascendente. La última barra es la que
        /// disparó la señal a auditar.
        /// </summary>
        IReadOnlyDictionary<string, decimal> Recompute(IReadOnlyList<MarketBar> observedBars);
    }
}
