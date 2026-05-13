using System;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Events
{
    /// <summary>
    /// Se emite cuando una orden se cancela o resulta inválida. Cubre los dos estados terminales
    /// no-fill de Lean (Canceled, Invalid), distinguidos por el campo WasInvalid.
    /// </summary>
    public sealed record OrderCanceledEvent(
        DateTime TimestampUtc,
        string ExecutorIdentifier,
        InstrumentId InstrumentId,
        OrderPurpose Purpose,
        bool WasInvalid) : IDomainEvent;
}
