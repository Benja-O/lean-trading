using System;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Events
{
    /// <summary>
    /// Estado relevante de una orden desde la perspectiva del dominio.
    /// Subset deliberado del OrderStatus de Lean: el dominio sólo reacciona a estos tres.
    /// Los estados intermedios (Submitted, PartiallyFilled, etc.) se filtran en el adaptador.
    /// </summary>
    public enum OrderEventStatus
    {
        Filled,
        Canceled,
        Invalid
    }

    /// <summary>
    /// Evento del ciclo de vida de una orden, modelado en términos del dominio.
    /// Reemplaza al QuantConnect.Orders.OrderEvent que llegaba directamente al dominio.
    ///
    /// El TradingAlgorithmHost (adaptador Lean) traduce el OrderEvent de Lean a este tipo
    /// antes de invocar al OrderLifecycleService.
    /// </summary>
    public sealed class OrderLifecycleEvent
    {
        public OrderEventStatus Status { get; }
        public OrderPurpose Purpose { get; }
        public string ExecutorIdentifier { get; }
        public InstrumentId InstrumentId { get; }
        public decimal FillQuantity { get; }
        public decimal FillPrice { get; }
        public DateTime TimestampUtc { get; }

        public OrderLifecycleEvent(
            OrderEventStatus status,
            OrderPurpose purpose,
            string executorIdentifier,
            InstrumentId instrumentId,
            decimal fillQuantity,
            decimal fillPrice,
            DateTime timestampUtc)
        {
            Status = status;
            Purpose = purpose;
            ExecutorIdentifier = executorIdentifier ?? throw new ArgumentNullException(nameof(executorIdentifier));
            InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
            FillQuantity = fillQuantity;
            FillPrice = fillPrice;
            TimestampUtc = timestampUtc;
        }
    }
}
