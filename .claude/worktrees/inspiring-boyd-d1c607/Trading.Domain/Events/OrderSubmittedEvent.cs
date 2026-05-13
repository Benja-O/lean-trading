using System;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Events
{
    /// <summary>
    /// Se emite cuando una orden se envía al IOrderRouter. NO indica que la orden haya sido
    /// aceptada por el exchange; solo que el sistema decidió emitirla y la entregó al adaptador.
    /// </summary>
    public sealed record OrderSubmittedEvent(
        DateTime TimestampUtc,
        string ExecutorIdentifier,
        InstrumentId InstrumentId,
        OrderPurpose Purpose,
        decimal Quantity,
        decimal? LimitPrice,
        decimal? StopPrice,
        string ClientTag) : IDomainEvent;
}
