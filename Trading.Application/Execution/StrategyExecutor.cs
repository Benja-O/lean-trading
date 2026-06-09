using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Execution
{
    /// <summary>
    /// Asocia una estrategia con un instrumento y timeframe, manteniendo el estado runtime
    /// de la operación: handles a las órdenes vivas y contador de barras transcurridas.
    /// 
    /// Sin acoplamiento a Lean: las propiedades de tickets son IOrderHandle (interfaz del dominio).
    /// La identidad de instrumento es InstrumentId.
    /// </summary>
    public class StrategyExecutor
    {
        public string ExecutorIdentifier { get; }
        public string Timeframe { get; }
        public InstrumentId InstrumentId { get; }
        public IStrategy Strategy { get; }
        public StrategyDefinition Definition { get; }
        public RiskParameters RiskParameters { get; }

        public IOrderHandle EntryOrderHandle { get; set; }
        public IOrderHandle StopLossOrderHandle { get; set; }
        public IOrderHandle TakeProfitOrderHandle { get; set; }

        /// <summary>
        /// Precio de SL calculado en el momento de la señal (modo ATR).
        /// Consumido y limpiado por OrderLifecycleService al procesar el fill de entrada.
        /// </summary>
        public decimal? PendingStopLossPrice { get; set; }

        /// <summary>
        /// Precio de TP calculado en el momento de la señal (modo ATR).
        /// Consumido y limpiado por OrderLifecycleService al procesar el fill de entrada.
        /// </summary>
        public decimal? PendingTakeProfitPrice { get; set; }

        public int BarsHeld { get; private set; }

        /// <summary>
        /// Fuente única de verdad: hay posición activa si hay órdenes de salida vivas.
        /// </summary>
        public bool HasActivePosition =>
            StopLossOrderHandle != null || TakeProfitOrderHandle != null;

        public StrategyExecutor(
            StrategyDefinition definition,
            string timeframe,
            InstrumentId instrumentId,
            IStrategy strategy,
            RiskParameters riskParameters)
        {
            Definition = definition;
            ExecutorIdentifier = $"{definition.StrategyName}_{instrumentId.Ticker}_{timeframe}";
            Timeframe = timeframe;
            InstrumentId = instrumentId;
            Strategy = strategy;
            RiskParameters = riskParameters;
            BarsHeld = 0;
        }

        public void SetEntryState()
        {
            BarsHeld = 0;
        }

        public void IncrementBarsHeld()
        {
            BarsHeld++;
        }

        public void ResetState()
        {
            BarsHeld = 0;
            ResetOrderHandles();
            PendingStopLossPrice = null;
            PendingTakeProfitPrice = null;
        }

        public void ResetOrderHandles()
        {
            EntryOrderHandle = null;
            StopLossOrderHandle = null;
            TakeProfitOrderHandle = null;
        }
    }
}
