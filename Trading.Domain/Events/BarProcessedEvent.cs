using System;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Events
{
    /// <summary>
    /// Se emite al final del procesamiento exitoso de una barra por BarProcessingService.
    /// NO se emite en los caminos de early-return (skip por config, error de sizing, etc).
    /// Consumido por HealthHeartbeatTracker para mantener el timestamp de la última
    /// barra procesada exitosamente, insumo del heartbeat de liveness del feed de mercado.
    /// </summary>
    public sealed record BarProcessedEvent(
        DateTime TimestampUtc,
        DateTime BarTimestampUtc,
        InstrumentId InstrumentId) : IDomainEvent;
}
