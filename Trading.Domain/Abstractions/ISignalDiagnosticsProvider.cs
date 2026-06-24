using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Estrategia que expone el rationale de su última evaluación de señal.
    /// Implementada opcionalmente junto a IStrategy (patrón espejo de IAtrProvider) cuando se
    /// quiere auditar EN VIVO qué condiciones evaluó la estrategia y contra qué umbrales.
    ///
    /// El contrato base IStrategy.EvaluateSignal no cambia: sigue devolviendo solo SignalDirection.
    /// Una estrategia que NO implementa esta interfaz no aporta condiciones, pero el sistema igual
    /// loguea las features microestructurales genéricas (open-closed). Una que sí la implementa
    /// guarda en estado interno el diagnóstico de la última llamada a EvaluateSignal y lo devuelve acá.
    /// </summary>
    public interface ISignalDiagnosticsProvider
    {
        /// <summary>
        /// Diagnóstico de la evaluación de señal más reciente (la última llamada a EvaluateSignal).
        /// Retorna null si la estrategia aún no evaluó ninguna barra (sin estado que reportar).
        /// El caller debe leerlo inmediatamente después de EvaluateSignal del mismo executor.
        /// </summary>
        SignalDiagnostics? DescribeLastEvaluation();
    }
}
