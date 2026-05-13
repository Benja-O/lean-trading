using System;

namespace Trading.Domain.Events
{
    /// <summary>
    /// Motivo concreto por el cual el kill switch se activó.
    /// El enum permite que el agregador de métricas categorice activaciones sin parsear texto.
    /// </summary>
    public enum RiskLimitBreachReason
    {
        /// <summary>Drawdown desde el máximo histórico superó el umbral configurado.</summary>
        MaximumDrawdownExceeded,

        /// <summary>Cantidad de pérdidas consecutivas alcanzó el umbral configurado.</summary>
        ConsecutiveLossesExceeded,

        /// <summary>Activación manual o por motivo no categorizado.</summary>
        Manual
    }

    /// <summary>
    /// Se emite cuando el RiskOrchestrator activa el kill switch.
    /// Incluye el motivo tipado y una descripción humana para diagnóstico.
    /// </summary>
    public sealed record RiskLimitBreachedEvent(
        DateTime TimestampUtc,
        RiskLimitBreachReason Reason,
        string Description) : IDomainEvent;
}
