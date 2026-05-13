using QuantConnect.Algorithm;
using QuantConnect.Orders;
using Trading.Application.Execution;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Traduce un QuantConnect.Orders.OrderEvent + OrderTicket a un OrderLifecycleEvent del dominio.
    ///
    /// Filtra estados no relevantes (Submitted, PartiallyFilled, etc.). El dominio solo reacciona
    /// a Filled, Canceled, Invalid.
    ///
    /// Resuelve el propósito y executor vía OrderRegistry. Si el tag no se encuentra, distingue
    /// dos escenarios esperados (evento residual post-cierre vs tag externo) y los registra en Debug
    /// con mensajes distintos para facilitar diagnóstico.
    /// </summary>
    public static class OrderEventMapper
    {
        // Prefijo de los tags generados por nuestro OrderRegistry.
        // Se duplica aquí en lugar de exponerlo desde OrderRegistry para no acoplar Application
        // a Strategies a través de una constante de formato; el mapper es consumidor del formato.
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
                    // Caso esperado: evento residual emitido por Lean DESPUÉS de que el primer
                    // evento terminal del tag fue procesado y el registro fue olvidado.
                    // Sucede típicamente en rollover de futuros, fills parciales post-cierre,
                    // o cancelaciones automáticas del motor tras cerrar la posición.
                    logger.Debug(
                        "OrderEventMapper: tag '{ClientTag}' ya fue procesado (Forget previo). " +
                        "Status={Status}. Evento residual esperado, ignorado por el dominio.",
                        clientTag, orderEvent.Status);
                }
                else
                {
                    // Tag con formato distinto al nuestro: orden no originada por el sistema
                    // (típicamente liquidación global del kill switch, intervención manual del motor).
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

            // Cleanup: evento terminal, liberar registración.
            orderRegistry.Forget(clientTag);

            return lifecycleEvent;
        }
    }
}
