using QuantConnect.Algorithm;
using QuantConnect.Orders;
using Trading.Application.Execution;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Traduce un QuantConnect.Orders.OrderEvent + OrderTicket a un OrderLifecycleEvent del dominio.
    /// </summary>
    public static class OrderEventMapper
    {
        private const string OurTagPrefix = "ord_";

        public static OrderLifecycleEvent ToLifecycleEvent(
            OrderEvent orderEvent,
            QCAlgorithm algorithm,
            LeanInstrumentResolver instrumentResolver,
            OrderRegistry orderRegistry,
            ITradingLogger logger)
        {
            OrderEventStatus? domainStatus = orderEvent.Status switch
            {
                OrderStatus.Filled => OrderEventStatus.Filled,
                OrderStatus.Canceled => OrderEventStatus.Canceled,
                OrderStatus.Invalid => OrderEventStatus.Invalid,
                _ => (OrderEventStatus?)null
            };

            if (domainStatus == null) return null;

            var orderTicket = algorithm.Transactions.GetOrderTicket(orderEvent.OrderId);
            string clientTag = orderTicket?.Tag;

            if (string.IsNullOrEmpty(clientTag))
            {
                logger.Error(
                    "OrderEventMapper: evento sin tag (OrderId={OrderId}, Status={Status}). " +
                    "Posible orden externa o liquidación global. Ignorado.",
                    orderEvent.OrderId, orderEvent.Status);
                return null;
            }

            var registration = orderRegistry.Resolve(clientTag);

            if (registration == null)
            {
                if (clientTag.StartsWith(OurTagPrefix))
                {
                    logger.Debug(
                        "OrderEventMapper: tag '{ClientTag}' ya fue procesado (Forget previo). " +
                        "Status={Status}. Evento residual esperado, ignorado por el dominio.",
                        clientTag, orderEvent.Status);
                }
                else
                {
                    logger.Debug(
                        "OrderEventMapper: tag externo '{ClientTag}' no proviene del OrderRegistry. " +
                        "Status={Status}. Probablemente liquidación global. Ignorado por el dominio.",
                        clientTag, orderEvent.Status);
                }
                return null;
            }

            var lifecycleEvent = new OrderLifecycleEvent(
                status: domainStatus.Value,
                purpose: registration.Purpose,
                executorIdentifier: registration.ExecutorIdentifier,
                instrumentId: registration.InstrumentId,
                fillQuantity: orderEvent.FillQuantity,
                fillPrice: orderEvent.FillPrice,
                timestampUtc: orderEvent.UtcTime);

            orderRegistry.Forget(clientTag);

            return lifecycleEvent;
        }
    }
}
