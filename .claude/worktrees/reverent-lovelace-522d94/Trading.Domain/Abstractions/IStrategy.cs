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
        SignalDirection EvaluateSignal(MarketBar marketBar);
    }
}
