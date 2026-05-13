using System;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Events
{
    /// <summary>
    /// Se emite cuando llega un evento Filled del exchange y el dominio lo procesa.
    /// Permite a métricas calcular fills por estrategia, slippage, costo de ejecución, etc.
    /// </summary>
    public sealed record OrderFilledEvent(
        DateTime TimestampUtc,
        string ExecutorIdentifier,
        InstrumentId InstrumentId,
        OrderPurpose Purpose,
        decimal FillQuantity,
        decimal FillPrice) : IDomainEvent;
}
