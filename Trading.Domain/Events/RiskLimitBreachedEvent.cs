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
        RegimeIncompatibility,

        /// <summary>
        /// Una estrategia individual cruzó alguno de los umbrales U1-U4 de POLICY sección 3.
        /// NO activa el kill switch global: solo liquida la posición de la estrategia y la
        /// excluye de generación de señales hasta reinicio manual del proceso.
        /// Emitido por StrategyHealthMonitor (OPS-2).
        /// </summary>
        StrategyDegradation
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
