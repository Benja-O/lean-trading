using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato de una estrategia de trading. Recibe una MarketBar consolidada
    /// y emite una SignalDirection con la dirección sugerida.
    ///
    /// Tras EvaluateSignal, el caller puede invocar GetLastDiagnostics() para obtener
    /// los valores de indicadores que la estrategia usó para decidir. Esto se utiliza
    /// por el SignalAuditor cuando audita correctness de señales no-Flat.
    /// </summary>
    public interface IStrategy
    {
        SignalDirection EvaluateSignal(MarketBar marketBar);

        /// <summary>
        /// Devuelve los diagnostics correspondientes a la última invocación de EvaluateSignal
        /// para el mismo InstrumentId de la MarketBar pasada. Si la estrategia no expone
        /// diagnostics todavía, devolver SignalDiagnostics.Empty.
        /// </summary>
        SignalDiagnostics GetLastDiagnostics(MarketBar marketBar);
    }
}
