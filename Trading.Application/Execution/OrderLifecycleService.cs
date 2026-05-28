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
    /// Emite OrderFilledEvent, OrderCanceledEvent y OrderSubmittedEvent (SL/TP) al bus.
    /// </summary>
    public class OrderLifecycleService
    {
        private readonly IReadOnlyList<StrategyExecutor> _strategyExecutors;
        private readonly ConsecutiveLossesMonitor _consecutiveLossesMonitor;
        private readonly IOrderRouter _orderRouter;
        private readonly IPriceRounder _priceRounder;
        private readonly ITradingLogger _logger;
        private readonly IDomainEventBus _eventBus;
        private readonly IClock _clock;
        private readonly RiskOrchestrator _riskOrchestrator;

        public OrderLifecycleService(
            IReadOnlyList<StrategyExecutor> strategyExecutors,
            ConsecutiveLossesMonitor consecutiveLossesMonitor,
            IOrderRouter orderRouter,
            IPriceRounder priceRounder,
            ITradingLogger logger,
            IDomainEventBus eventBus,
            IClock clock,
            RiskOrchestrator riskOrchestrator)
        {
            _strategyExecutors = strategyExecutors;
            _consecutiveLossesMonitor = consecutiveLossesMonitor;
            _orderRouter = orderRouter;
            _priceRounder = priceRounder;
            _logger = logger;
            _eventBus = eventBus;
            _clock = clock;
            _riskOrchestrator = riskOrchestrator
                ?? throw new System.ArgumentNullException(nameof(riskOrchestrator));
        }

        public void Handle(OrderLifecycleEvent lifecycleEvent)
        {
            var strategyExecutor = _strategyExecutors.FirstOrDefault(
                executor => executor.ExecutorIdentifier == lifecycleEvent.ExecutorIdentifier);

            if (strategyExecutor == null)
            {
                if (lifecycleEvent.Purpose == OrderPurpose.Liquidate &&
                    lifecycleEvent.Status == OrderEventStatus.Filled)
                {
                    foreach (var exec in _strategyExecutors
                        .Where(e => e.InstrumentId == lifecycleEvent.InstrumentId))
                    {
                        _eventBus.Publish(new OrderFilledEvent(
                            TimestampUtc: _clock.UtcNow,
                            ExecutorIdentifier: exec.ExecutorIdentifier,
                            InstrumentId: lifecycleEvent.InstrumentId,
                            Purpose: OrderPurpose.Liquidate,
                            FillQuantity: lifecycleEvent.FillQuantity,
                            FillPrice: lifecycleEvent.FillPrice));
                    }
                    return;
                }

                _logger.Error(
                    "OrderLifecycleService: ExecutorIdentifier '{ExecutorIdentifier}' no encontrado. " +
                    "Evento ignorado (Status={Status}, Purpose={Purpose}).",
                    lifecycleEvent.ExecutorIdentifier, lifecycleEvent.Status, lifecycleEvent.Purpose);
                return;
            }

            if (lifecycleEvent.Status == OrderEventStatus.Canceled ||
                lifecycleEvent.Status == OrderEventStatus.Invalid)
            {
                _eventBus.Publish(new OrderCanceledEvent(
                    TimestampUtc: _clock.UtcNow,
                    ExecutorIdentifier: strategyExecutor.ExecutorIdentifier,
                    InstrumentId: lifecycleEvent.InstrumentId,
                    Purpose: lifecycleEvent.Purpose,
                    WasInvalid: lifecycleEvent.Status == OrderEventStatus.Invalid));

                strategyExecutor.ResetState();
                return;
            }

            // Filled: publicar evento ANTES de procesar para que las métricas registren el fill
            // independientemente de qué decida hacer la lógica de negocio después.
            _eventBus.Publish(new OrderFilledEvent(
                TimestampUtc: _clock.UtcNow,
                ExecutorIdentifier: strategyExecutor.ExecutorIdentifier,
                InstrumentId: lifecycleEvent.InstrumentId,
                Purpose: lifecycleEvent.Purpose,
                FillQuantity: lifecycleEvent.FillQuantity,
                FillPrice: lifecycleEvent.FillPrice));

            switch (lifecycleEvent.Purpose)
            {
                case OrderPurpose.Entry:
                    HandleEntryFill(strategyExecutor, lifecycleEvent);
                    break;
                case OrderPurpose.StopLoss:
                    _consecutiveLossesMonitor.RegisterLoss();
                    _logger.Info(
                        "Cancelando TakeProfit de '{ExecutorIdentifier}' por {Reason}.",
                        strategyExecutor.ExecutorIdentifier, "Stop Loss Hit");
                    strategyExecutor.TakeProfitOrderHandle?.Cancel("Stop Loss Hit");
                    strategyExecutor.ResetState();
                    break;
                case OrderPurpose.TakeProfit:
                    _consecutiveLossesMonitor.RegisterWin();
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
                case OrderPurpose.Liquidate:
                    _logger.Info(
                        "Cancelando StopLoss y TakeProfit de '{ExecutorIdentifier}' por {Reason}.",
                        strategyExecutor.ExecutorIdentifier, "Liquidate Hit");
                    strategyExecutor.StopLossOrderHandle?.Cancel("Liquidate Hit");
                    strategyExecutor.TakeProfitOrderHandle?.Cancel("Liquidate Hit");
                    strategyExecutor.ResetState();
                    break;
            }
        }

        private void HandleEntryFill(StrategyExecutor strategyExecutor, OrderLifecycleEvent lifecycleEvent)
        {
            strategyExecutor.SetEntryState();

            var instrumentId = lifecycleEvent.InstrumentId;
            decimal fillQuantity = lifecycleEvent.FillQuantity;

            // Defensa contra race entre submit Entry y disparo del kill switch:
            // si un Entry se llena durante el cooling-off, liquidar inmediatamente
            // sin emitir brackets. El RiskOrchestrator no puede detectarlo en
            // LiquidateAllRiskAction porque ese se ejecuta una sola vez al momento
            // del breach, antes de que el Entry pending se haya materializado en
            // IPortfolioState.IsInvested. Ver ADR-029.
            if (_riskOrchestrator.IsKillSwitchActivated)
            {
                _logger.Critical(
                    "Entry filled durante cooling-off para '{ExecutorIdentifier}' " +
                    "(instrumentId={InstrumentId}, qty={FillQuantity}). " +
                    "Liquidando inmediatamente sin emitir SL/TP.",
                    strategyExecutor.ExecutorIdentifier,
                    instrumentId.Ticker,
                    fillQuantity);

                _orderRouter.LiquidateInstrument(
                    instrumentId,
                    OrderPurpose.Liquidate,
                    strategyExecutor.ExecutorIdentifier);

                _eventBus.Publish(new OrderSubmittedEvent(
                    TimestampUtc: _clock.UtcNow,
                    ExecutorIdentifier: strategyExecutor.ExecutorIdentifier,
                    InstrumentId: instrumentId,
                    Purpose: OrderPurpose.Liquidate,
                    Quantity: 0m,
                    LimitPrice: null,
                    StopPrice: null,
                    ClientTag: string.Empty));

                return;
            }

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

            _eventBus.Publish(new OrderSubmittedEvent(
                TimestampUtc: _clock.UtcNow,
                ExecutorIdentifier: strategyExecutor.ExecutorIdentifier,
                InstrumentId: instrumentId,
                Purpose: OrderPurpose.StopLoss,
                Quantity: -fillQuantity,
                LimitPrice: null,
                StopPrice: stopLossPrice,
                ClientTag: string.Empty));

            strategyExecutor.TakeProfitOrderHandle = _orderRouter.SubmitLimitOrder(
                instrumentId, -fillQuantity, takeProfitPrice,
                OrderPurpose.TakeProfit, strategyExecutor.ExecutorIdentifier);

            _eventBus.Publish(new OrderSubmittedEvent(
                TimestampUtc: _clock.UtcNow,
                ExecutorIdentifier: strategyExecutor.ExecutorIdentifier,
                InstrumentId: instrumentId,
                Purpose: OrderPurpose.TakeProfit,
                Quantity: -fillQuantity,
                LimitPrice: takeProfitPrice,
                StopPrice: null,
                ClientTag: string.Empty));
        }
    }
}
