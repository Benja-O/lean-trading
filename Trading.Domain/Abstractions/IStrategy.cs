using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato de una estrategia de trading. Recibe una MarketBar consolidada
    /// y emite una SignalDirection con la dirección sugerida.
    /// </summary>
    public interface IStrategy
    {
        /// <summary>
        /// Cantidad de barras necesarias para que los indicadores internos estén calentados.
        /// El host usa este valor (multiplicado por el timeframe) para calcular el SetWarmUp
        /// del algoritmo, garantizando que EvaluateSignal reciba historia suficiente desde
        /// el primer bar real.
        /// </summary>
        int WarmUpBars { get; }

        SignalDirection EvaluateSignal(MarketBar marketBar);
    }
}
