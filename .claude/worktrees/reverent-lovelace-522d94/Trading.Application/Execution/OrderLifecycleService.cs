using System.Collections.Generic;
using System.Linq;
using Trading.Application.Risk;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Execution
{
    /// <summary>
    /// Procesa eventos de ciclo de vida de órdenes (Filled / Canceled / Invalid).
    ///
    /// Reemplazó a OrderEventHandler. Sin acoplamiento a QuantConnect.
    /// El propósito de cada orden se resuelve via OrderPurpose (enum) en lugar de parsear strings.
    /// </summary>
    public class OrderLifecycleService
    {
        private readonly IReadOnlyList<StrategyExecutor> _strategyExecutors;
        private readonly KillSwitchManager _killSwitchManager;
        private readonly IOrderRouter _orderRouter;
        private readonly IPriceRounder _priceRounder;
        private readonly ITradingLogger _logger;

        public OrderLifecycleService(
            IReadOnlyList<StrategyExecutor> strategyExecutors,
            KillSwitchManager killSwitchManager,
            IOrderRouter orderRouter,
            IPriceRounder priceRounder,
            ITradingLogger logger)
        {
            _strategyExecutors = strategyExecutors;
            _killSwitchManager = killSwitchManager;
            _orderRouter = orderRouter;
            _priceRounder = priceRounder;
            _logger = logger;
        }

        public void Handle(OrderLifecycleEvent lifecycleEvent)
        {
            var strategyExecutor = _strategyExecutors.FirstOrDefault(
                executor => executor.ExecutorIdentifier == lifecycleEvent.ExecutorIdentifier);

            if (strategyExecutor == null)
            {
                _logger.Error(
                    "OrderLifecycleService: ExecutorIdentifier '{ExecutorIdentifier}' no encontrado. " +
                    "Evento ignorado (Status={Status}, Purpose={Purpose}).",
                    lifecycleEvent.ExecutorIdentifier, lifecycleEvent.Status, lifecycleEvent.Purpose);
                return;
            }

            if (lifecycleEvent.Status == OrderEventStatus.Canceled ||
                lifecycleEvent.Status == OrderEventStatus.Invalid)
            {
                strategyExecutor.ResetState();
                return;
            }

            // Status == Filled
            switch (lifecycleEvent.Purpose)
            {
                case OrderPurpose.Entry:
                    HandleEntryFill(strategyExecutor, lifecycleEvent);
                    break;
                case OrderPurpose.StopLoss:
                    _killSwitchManager.RegisterLoss();
                    _logger.Info(
                        "Cancelando TakeProfit de '{ExecutorIdentifier}' por {Reason}.",
                        strategyExecutor.ExecutorIdentifier, "Stop Loss Hit");
                    strategyExecutor.TakeProfitOrderHandle?.Cancel("Stop Loss Hit");
                    strategyExecutor.ResetState();
                    break;
                case OrderPurpose.TakeProfit:
                    _killSwitchManager.ResetLossCounter();
                    _logger.Info(
                        "Cancelando StopLoss de '{ExecutorIdentifier}' por {Reason}.",
                        strategyExecutor.ExecutorIdentifier, "Take Profit Hit");
                    strategyExecutor.StopLossOrderHandle?.Cancel("Take Profit Hit");
                    strategyExecutor.ResetState();
                    break;
                case OrderPurpose.TimeExit:
                    _logger.Info(
                        "Cancelando StopLoss y TakeProfit de '{ExecutorIdentifier}' por {Reason}.",
                        strategyExecutor.ExecutorIdentifier, "Time Exit Hit");
                    strategyExecutor.StopLossOrderHandle?.Cancel("Time Exit Hit");
                    strategyExecutor.TakeProfitOrderHandle?.Cancel("Time Exit Hit");
                    strategyExecutor.ResetState();
                    break;
            }
        }

        private void HandleEntryFill(StrategyExecutor strategyExecutor, OrderLifecycleEvent lifecycleEvent)
        {
            strategyExecutor.SetEntryState();

            var instrumentId = lifecycleEvent.InstrumentId;
            decimal fillQuantity = lifecycleEvent.FillQuantity;
            decimal entryPrice = lifecycleEvent.FillPrice;

            decimal stopLossFraction = strategyExecutor.RiskParameters.StopLossFraction;
            decimal takeProfitFraction = strategyExecutor.RiskParameters.TakeProfitFraction;

            decimal stopLossPrice;
            decimal takeProfitPrice;

            if (fillQuantity > 0)
            {
                stopLossPrice = _priceRounder.Round(instrumentId, entryPrice * (1 - stopLossFraction));
                takeProfitPrice = _priceRounder.Round(instrumentId, entryPrice * (1 + takeProfitFraction));
            }
            else
            {
                stopLossPrice = _priceRounder.Round(instrumentId, entryPrice * (1 + stopLossFraction));
                takeProfitPrice = _priceRounder.Round(instrumentId, entryPrice * (1 - takeProfitFraction));
            }

            strategyExecutor.StopLossOrderHandle = _orderRouter.SubmitStopMarketOrder(
                instrumentId, -fillQuantity, stopLossPrice,
                OrderPurpose.StopLoss, strategyExecutor.ExecutorIdentifier);

            strategyExecutor.TakeProfitOrderHandle = _orderRouter.SubmitLimitOrder(
                instrumentId, -fillQuantity, takeProfitPrice,
                OrderPurpose.TakeProfit, strategyExecutor.ExecutorIdentifier);
        }
    }
}
