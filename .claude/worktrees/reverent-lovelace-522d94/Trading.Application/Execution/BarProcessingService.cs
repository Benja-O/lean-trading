using System.Collections.Generic;
using Trading.Application.Risk;
using Trading.Application.Sizing;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Execution
{
    /// <summary>
    /// Procesa una MarketBar consolidada: gestiona time exits, evalúa señales y dispara entradas.
    /// 
    /// Reemplazó al ConsolidatedBarHandler. Sin acoplamiento a QuantConnect: opera sobre
    /// IPortfolioState (consultas de invested), IOrderRouter (envío y cancelación) y MarketBar.
    /// </summary>
    public class BarProcessingService
    {
        private readonly IPortfolioState _portfolioState;
        private readonly IOrderRouter _orderRouter;
        private readonly KillSwitchManager _killSwitchManager;
        private readonly PositionSizer _positionSizer;

        public BarProcessingService(
            IPortfolioState portfolioState,
            IOrderRouter orderRouter,
            KillSwitchManager killSwitchManager,
            PositionSizer positionSizer)
        {
            _portfolioState = portfolioState;
            _orderRouter = orderRouter;
            _killSwitchManager = killSwitchManager;
            _positionSizer = positionSizer;
        }

        public void ProcessBar(MarketBar marketBar, IReadOnlyList<StrategyExecutor> strategyExecutors)
        {
            foreach (var strategyExecutor in strategyExecutors)
            {
                var instrumentId = strategyExecutor.InstrumentId;

                // Time exit: HasActivePosition (tickets vivos) es la fuente primaria;
                // IsInvested es red de seguridad ante cierres externos del motor.
                if (strategyExecutor.HasActivePosition && _portfolioState.IsInvested(instrumentId))
                {
                    strategyExecutor.IncrementBarsHeld();

                    if (strategyExecutor.Definition.CombineWithTimeExit &&
                        strategyExecutor.BarsHeld >= strategyExecutor.Definition.MaxBars)
                    {
                        _orderRouter.LiquidateInstrument(
                            instrumentId, OrderPurpose.TimeExit, strategyExecutor.ExecutorIdentifier);
                        continue;
                    }
                }

                if (_killSwitchManager.IsKillSwitchActivated) continue;

                SignalDirection signalDirection = strategyExecutor.Strategy.EvaluateSignal(marketBar);

                if (signalDirection == SignalDirection.Flat) continue;

                // Bloqueamos entrada si ya hay posición o hay órdenes pending.
                if (_portfolioState.IsInvested(instrumentId)) continue;
                if (_orderRouter.HasOpenOrders(instrumentId)) continue;

                decimal quantityMagnitude = _positionSizer.CalculateQuantity(strategyExecutor, marketBar.Close);
                if (quantityMagnitude == 0m) continue;
                if (!_positionSizer.IsValidNotional(instrumentId, quantityMagnitude, marketBar.Close)) continue;

                // El sizer devuelve magnitud (positiva). El signo se aplica acá según la dirección.
                decimal signedQuantity = signalDirection == SignalDirection.Long
                    ? quantityMagnitude
                    : -quantityMagnitude;

                strategyExecutor.EntryOrderHandle = _orderRouter.SubmitMarketOrder(
                    instrumentId, signedQuantity, OrderPurpose.Entry, strategyExecutor.ExecutorIdentifier);
            }
        }
    }
}
