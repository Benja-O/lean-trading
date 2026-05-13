using Trading.Domain.Events;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Veredicto de un IRiskMonitor tras evaluar las condiciones actuales.
    ///
    /// Si ShouldTriggerKillSwitch es false, los otros campos no tienen significado
    /// (se ignoran). El monitor devolvió "todo bien por mi parte".
    ///
    /// Si es true, el orchestrator activa el kill switch con la razón y descripción
    /// reportadas.
    /// </summary>
    public readonly record struct RiskAssessment(
        bool ShouldTriggerKillSwitch,
        RiskLimitBreachReason Reason,
        string Description)
    {
        public static RiskAssessment Pass() => new(false, default, string.Empty);

        public static RiskAssessment Trigger(RiskLimitBreachReason reason, string description)
            => new(true, reason, description ?? string.Empty);
    }
}
