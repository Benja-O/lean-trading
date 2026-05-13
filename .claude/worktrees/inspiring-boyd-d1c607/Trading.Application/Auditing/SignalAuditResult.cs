using System.Collections.Generic;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Auditing
{
    /// <summary>
    /// Resultado de auditar una señal. Inmutable. Generado por SignalAuditor.AuditSignal.
    ///
    /// Si IsConsistent es true, todas las claves declaradas por la estrategia coincidieron
    /// con el recálculo independiente dentro de la tolerancia configurada.
    ///
    /// Si IsConsistent es false, Discrepancies contiene una entrada por cada clave divergente
    /// con los valores declarado y recalculado.
    /// </summary>
    public sealed class SignalAuditResult
    {
        public string ExecutorIdentifier { get; }
        public SignalDirection Direction { get; }
        public bool IsConsistent { get; }
        public IReadOnlyList<SignalDiscrepancy> Discrepancies { get; }

        public SignalAuditResult(
            string executorIdentifier,
            SignalDirection direction,
            bool isConsistent,
            IReadOnlyList<SignalDiscrepancy> discrepancies)
        {
            ExecutorIdentifier = executorIdentifier;
            Direction = direction;
            IsConsistent = isConsistent;
            Discrepancies = discrepancies;
        }
    }

    /// <summary>
    /// Diferencia detectada en una clave entre el valor declarado por la estrategia
    /// y el recalculado independientemente por el auditor.
    /// </summary>
    public sealed record SignalDiscrepancy(
        string Key,
        decimal DeclaredValue,
        decimal RecomputedValue,
        decimal AbsoluteDifference);
}
