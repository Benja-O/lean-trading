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
        Manual,

        /// <summary>
        /// Una estrategia intentó emitir una señal en un régimen de mercado declarado incompatible.
        /// Este valor NO activa el kill switch global: el filtro de régimen rechaza la señal
        /// específica en BarProcessingService como un guard pre-orden. El valor existe en el
        /// enum para que las emisiones diagnósticas del filtro (futuras, no en este paso)
        /// puedan categorizarse junto a las otras razones.
        /// </summary>
        RegimeIncompatibility
    }

    /// <summary>
    /// Se emite cuando el KillSwitchManager activa el kill switch.
    /// Incluye el motivo tipado y una descripción humana para diagnóstico.
    /// </summary>
    public sealed record RiskLimitBreachedEvent(
        DateTime TimestampUtc,
        RiskLimitBreachReason Reason,
        string Description) : IDomainEvent;
}
