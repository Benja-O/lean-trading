using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Vista del portfolio desde la perspectiva del dominio.
    /// Expone solo lo que la lógica de riesgo y sizing necesita; nada más.
    /// 
    /// La implementación concreta (LeanPortfolioAdapter) traduce de QuantConnect.SecurityPortfolioManager
    /// a estas operaciones puras. Tests de dominio pueden usar fakes triviales.
    /// </summary>
    public interface IPortfolioState
    {
        /// <summary>
        /// Valor total del portfolio en la moneda de cuenta (cash + posiciones marked-to-market).
        /// Equivale a Lean's Portfolio.TotalPortfolioValue.
        /// </summary>
        decimal TotalPortfolioValue { get; }

        /// <summary>
        /// Indica si existe una posición abierta (long o short) en el instrumento dado.
        /// Equivale a Lean's Portfolio[symbol].Invested.
        /// </summary>
        bool IsInvested(InstrumentId instrumentId);

        /// <summary>
        /// Cantidad neta de la posición abierta en el instrumento dado. Positivo si long, negativo si short,
        /// cero si no hay posición. Equivale a Lean's Portfolio[symbol].Quantity. Necesario para construir
        /// órdenes de cierre explícitas que no dependan de Liquidate().
        /// </summary>
        decimal GetPositionQuantity(InstrumentId instrumentId);
    }
}
