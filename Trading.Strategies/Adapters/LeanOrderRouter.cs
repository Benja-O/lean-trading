using QuantConnect.Algorithm;
using QuantConnect.Orders;
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
        private readonly IPortfolioState _portfolioState;
        private readonly ITradingLogger _logger;

        public LeanOrderRouter(
            QCAlgorithm algorithm,
            LeanInstrumentResolver instrumentResolver,
            OrderRegistry orderRegistry,
            IPortfolioState portfolioState,
            ITradingLogger logger)
        {
            _algorithm = algorithm;
            _instrumentResolver = instrumentResolver;
            _orderRegistry = orderRegistry;
            _portfolioState = portfolioState;
            _logger = logger;
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
            // reduceOnly: orden protectiva nativa en el exchange que sólo puede cerrar la posición,
            // nunca abrir/voltear si la otra pierna ya cerró durante una desconexión (ADR-044).
            var orderTicket = _algorithm.StopMarketOrder(
                symbol, quantity, stopPrice, tag: clientTag,
                orderProperties: new BinanceOrderProperties { ReduceOnly = true });
            return new LeanOrderHandle(orderTicket);
        }

        public IOrderHandle SubmitLimitOrder(
            InstrumentId instrumentId, decimal quantity, decimal limitPrice,
            OrderPurpose purpose, string executorIdentifier)
        {
            string clientTag = _orderRegistry.Register(purpose, executorIdentifier, instrumentId);
            var symbol = _instrumentResolver.Resolve(instrumentId);
            // reduceOnly: ídem SubmitStopMarketOrder — la protectiva TP no puede voltear la
            // posición si la SL ya cerró durante una desconexión (ADR-044).
            var orderTicket = _algorithm.LimitOrder(
                symbol, quantity, limitPrice, tag: clientTag,
                orderProperties: new BinanceOrderProperties { ReduceOnly = true });
            return new LeanOrderHandle(orderTicket);
        }

        /// <summary>
        /// Cierra la posición del instrumento de forma explícita: primero cancela todas las órdenes abiertas
        /// (SL/TP) con su tag original ya registrado, luego emite un MarketOrder de cierre con tag propio.
        /// NO usa _algorithm.Liquidate(symbol, tag) porque Lean reutiliza ese mismo tag para el Canceled del SL,
        /// el Canceled del TP y el Filled del MarketOrder de cierre, haciendo que OrderEventMapper interprete el
        /// primer Canceled como cierre completo y descarte el Filled real del MarketOrder.
        /// </summary>
        public void LiquidateInstrument(
            InstrumentId instrumentId, OrderPurpose purpose, string executorIdentifier)
        {
            var symbol = _instrumentResolver.Resolve(instrumentId);

            // 1) Cancelar todas las órdenes abiertas del símbolo a través de su OrderTicket.
            //    Lean tiene un bug donde Transactions.CancelOrder(id) propaga el evento Canceled
            //    con OrderTicket.Tag VACÍO. Usar OrderTicket.Cancel() directamente preserva el
            //    tag original (registrado previamente como StopLoss/TakeProfit), de modo que el
            //    OrderEventMapper resuelve el tag, procesa el Canceled, y hace Forget del tag
            //    correspondiente a cada orden cancelada.
            var openOrders = _algorithm.Transactions.GetOpenOrders(symbol);
            foreach (var openOrder in openOrders)
            {
                var openTicket = _algorithm.Transactions.GetOrderTicket(openOrder.Id);
                openTicket?.Cancel();
            }

            decimal positionQty = _portfolioState.GetPositionQuantity(instrumentId);
            if (positionQty != 0m)
            {
                string clientTag = _orderRegistry.Register(purpose, executorIdentifier, instrumentId);
                _algorithm.MarketOrder(symbol, -positionQty, tag: clientTag);
            }
            else
            {
                _logger.Info(
                    "LiquidateInstrument: {ExecutorIdentifier} sin posición real en {InstrumentId}; sólo se cancelaron órdenes abiertas.",
                    executorIdentifier, instrumentId);
            }
        }

        public bool HasOpenOrders(InstrumentId instrumentId)
        {
            var symbol = _instrumentResolver.Resolve(instrumentId);
            return _algorithm.Transactions.GetOpenOrders(symbol).Count > 0;
        }
    }
}
