using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Punto de envío de órdenes desde el dominio. Encapsula MarketOrder, StopMarketOrder, LimitOrder
    /// y operaciones de liquidación, devolviendo IOrderHandle en lugar del OrderTicket de Lean.
    ///
    /// La implementación concreta (LeanOrderRouter) hace las llamadas a QCAlgorithm.MarketOrder, etc.
    /// </summary>
    public interface IOrderRouter
    {
        IOrderHandle SubmitMarketOrder(
            InstrumentId instrumentId, decimal quantity, OrderPurpose purpose, string executorIdentifier);

        IOrderHandle SubmitStopMarketOrder(
            InstrumentId instrumentId, decimal quantity, decimal stopPrice,
            OrderPurpose purpose, string executorIdentifier);

        IOrderHandle SubmitLimitOrder(
            InstrumentId instrumentId, decimal quantity, decimal limitPrice,
            OrderPurpose purpose, string executorIdentifier);

        /// <summary>
        /// Liquida la posición de un instrumento específico (kill por estrategia / time exit).
        /// </summary>
        void LiquidateInstrument(InstrumentId instrumentId, OrderPurpose purpose, string executorIdentifier);

        /// <summary>
        /// Liquida toda la cartera. Reservado para kill switch global.
        /// </summary>
        void LiquidateAll();

        /// <summary>
        /// Indica si existen órdenes abiertas para el instrumento dado.
        /// Necesario para evitar duplicar entradas mientras una orden previa está pending.
        /// </summary>
        bool HasOpenOrders(InstrumentId instrumentId);
    }
}
