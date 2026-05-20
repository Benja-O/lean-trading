using System;

namespace Trading.Application.Health
{
    /// <summary>
    /// Snapshot inmutable del estado de salud del sistema en un instante.
    /// Serializado a JSON por HeartbeatFileWriter. Todos los campos nullable
    /// reflejan "todavía no ocurrió" (estado normal al inicio del proceso).
    /// </summary>
    public sealed record HealthSnapshot(
        DateTime CurrentUtc,
        DateTime ProcessStartedUtc,
        DateTime? LastBarProcessedUtc,
        DateTime? LastBarTimestampUtc,
        DateTime? LastOrderSubmittedUtc,
        DateTime? LastOrderFilledUtc,
        DateTime? LastRiskBreachUtc,
        string? LastRiskBreachReason,
        bool KillSwitchActive);
}
