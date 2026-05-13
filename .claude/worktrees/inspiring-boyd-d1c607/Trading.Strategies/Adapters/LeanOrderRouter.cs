using QuantConnect.Algorithm;
using Trading.Application.Execution;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Adapta los métodos de envío de órdenes de QCAlgorithm al contrato IOrderRouter del dominio.
    /// Registra cada orden en OrderRegistry para asociar el tag opaco con (Purpose, ExecutorIdentifier, InstrumentId).
    /// </summary>
    public sealed class LeanOrderRouter : IOrderRouter
    {
        private readonly QCAlgorithm _algorithm;
        private readonly LeanInstrumentResolver _instrumentResolver;
        private readonly OrderRegistry _orderRegistry;

        public LeanOrderRouter(
            QCAlgorithm algorithm,
            LeanInstrumentResolver instrumentResolver,
            OrderRegistry orderRegistry)
        {
            _algorithm = algorithm;
            _instrumentResolver = instrumentResolver;
            _orderRegistry = orderRegistry;
        }

        public IOrderHandle SubmitMarketOrder(
            InstrumentId instrumentId, decimal quantity, OrderPurpose purpose, string executorIdentifier)
        {
            string clientTag = _orderRegistry.Register(purpose, executorIdentifier, instrumentId);
            var symbol = _instrumentResolver.Resolve(instrumentId);
            var orderTicket = _algorithm.MarketOrder(symbol, quantity, tag: clientTag);
            return new LeanOrderHandle(orderTicket);
        }

        public IOrderHandle SubmitStopMarketOrder(
            InstrumentId instrumentId, decimal quantity, decimal stopPrice,
            OrderPurpose purpose, string executorIdentifier)
        {
            string clientTag = _orderRegistry.Register(purpose, executorIdentifier, instrumentId);
            var symbol = _instrumentResolver.Resolve(instrumentId);
            var orderTicket = _algorithm.StopMarketOrder(symbol, quantity, stopPrice, tag: clientTag);
            return new LeanOrderHandle(orderTicket);
        }

        public IOrderHandle SubmitLimitOrder(
            InstrumentId instrumentId, decimal quantity, decimal limitPrice,
            OrderPurpose purpose, string executorIdentifier)
        {
            string clientTag = _orderRegistry.Register(purpose, executorIdentifier, instrumentId);
            var symbol = _instrumentResolver.Resolve(instrumentId);
            var orderTicket = _algorithm.LimitOrder(symbol, quantity, limitPrice, tag: clientTag);
            return new LeanOrderHandle(orderTicket);
        }

        public void LiquidateInstrument(
            InstrumentId instrumentId, OrderPurpose purpose, string executorIdentifier)
        {
            string clientTag = _orderRegistry.Register(purpose, executorIdentifier, instrumentId);
            var symbol = _instrumentResolver.Resolve(instrumentId);
            _algorithm.Liquidate(symbol, tag: clientTag);
        }

        public void LiquidateAll()
        {
            // Liquidación global: NO se registra (no hay executor único).
            // Los eventos resultantes serán ignorados por OrderEventMapper con log de advertencia.
            _algorithm.Liquidate();
        }

        public bool HasOpenOrders(InstrumentId instrumentId)
        {
            var symbol = _instrumentResolver.Resolve(instrumentId);
            return _algorithm.Transactions.GetOpenOrders(symbol).Count > 0;
        }
    }
}
