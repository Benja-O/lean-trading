using System.Collections.Generic;

namespace Trading.Domain.ValueObjects
{
    /// <summary>
    /// Una condición elemental evaluada por una estrategia al decidir una señal.
    /// Captura el lado izquierdo (valor observado), el operador de comparación, el umbral
    /// contra el que se comparó, y si la condición se cumplió. Pensado para auditar EN VIVO
    /// por qué una estrategia disparó (o no) una señal, contrastando contra el store del recorder.
    ///
    /// Ejemplos:
    ///   close ≤ min48:        new("CloseIsMin48", 1658.9, "≤", 1653.63, false)
    ///   meanTradeSize ≥ P90:  new("MeanTradeSizeGePercentile", 2.78, "≥", 4.5, false)
    /// </summary>
    public sealed record SignalCondition(
        string Name,
        double Value,
        string Comparison,
        double Threshold,
        bool Satisfied);

    /// <summary>
    /// Diagnóstico del "por qué" de la última evaluación de una estrategia: un resumen legible
    /// más la lista de condiciones elementales que la estrategia chequeó, con sus valores y umbrales.
    ///
    /// Lo expone una estrategia opcionalmente vía ISignalDiagnosticsProvider (patrón espejo de
    /// IAtrProvider). El contrato base IStrategy no cambia: una estrategia que no implementa la
    /// interfaz simplemente no aporta rationale, y el sistema sigue logueando las features genéricas.
    ///
    /// Inmutable. Se reconstruye en cada llamada a EvaluateSignal; el caller lo lee inmediatamente
    /// después de la evaluación de ese mismo executor (flujo single-threaded del host).
    /// </summary>
    public sealed record SignalDiagnostics(
        string Summary,
        IReadOnlyList<SignalCondition> Conditions);
}
