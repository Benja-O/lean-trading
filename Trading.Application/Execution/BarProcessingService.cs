using System;
using System.Collections.Generic;
using Trading.Application.Regimes;
using Trading.Application.Risk;
using Trading.Application.Sizing;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Execution
{
    /// <summary>
    /// Procesa una MarketBar consolidada: gestiona time exits, evalúa señales y dispara entradas.
    ///
    /// Reemplazó al ConsolidatedBarHandler. Sin acoplamiento a QuantConnect: opera sobre
    /// IPortfolioState (consultas de invested), IOrderRouter (envío y cancelación) y MarketBar.
    /// Emite OrderSubmittedEvent al bus por cada orden enviada al router.
    /// </summary>
    public class BarProcessingService
    {
        private readonly IPortfolioState _portfolioState;
        private readonly IOrderRouter _orderRouter;
        private readonly RiskOrchestrator _riskOrchestrator;
        private readonly PositionSizer _positionSizer;
        private readonly ITradingLogger _logger;
        private readonly IDomainEventBus _eventBus;
        private readonly IClock _clock;
        private readonly MarketRegimeRegistry _regimeRegistry;
        private readonly IReadOnlyDictionary<string, StrategyRegimeCompatibility> _strategyCompatibilities;
        private readonly IStrategyHealthMonitor _strategyHealthMonitor;

        public BarProcessingService(
            IPortfolioState portfolioState,
            IOrderRouter orderRouter,
            RiskOrchestrator riskOrchestrator,
            PositionSizer positionSizer,
            ITradingLogger logger,
            IDomainEventBus eventBus,
            IClock clock,
            MarketRegimeRegistry regimeRegistry,
            IReadOnlyDictionary<string, StrategyRegimeCompatibility> strategyCompatibilities,
            IStrategyHealthMonitor strategyHealthMonitor)
        {
            _portfolioState = portfolioState;
            _orderRouter = orderRouter;
            _riskOrchestrator = riskOrchestrator;
            _positionSizer = positionSizer;
            _logger = logger;
            _eventBus = eventBus;
            _clock = clock;
            _regimeRegistry = regimeRegistry ?? throw new ArgumentNullException(nameof(regimeRegistry));
            _strategyCompatibilities = strategyCompatibilities ?? throw new ArgumentNullException(nameof(strategyCompatibilities));
            _strategyHealthMonitor = strategyHealthMonitor ?? throw new ArgumentNullException(nameof(strategyHealthMonitor));
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

                        _eventBus.Publish(new OrderSubmittedEvent(
                            TimestampUtc: _clock.UtcNow,
                            ExecutorIdentifier: strategyExecutor.ExecutorIdentifier,
                            InstrumentId: instrumentId,
                            Purpose: OrderPurpose.TimeExit,
                            Quantity: 0m,
                            LimitPrice: null,
                            StopPrice: null,
                            ClientTag: string.Empty));

                        _eventBus.Publish(new BarProcessedEvent(
                            _clock.UtcNow, marketBar.TimestampUtc, instrumentId));

                        continue;
                    }
                }

                if (_riskOrchestrator.IsKillSwitchActivated) continue;

                if (_strategyHealthMonitor.IsExcluded(strategyExecutor.ExecutorIdentifier))
                {
                    continue;
                }

                SignalDirection signalDirection = strategyExecutor.Strategy.EvaluateSignal(marketBar);

                if (signalDirection == SignalDirection.Flat) continue;

                // ===== Filtro de régimen pre-orden =====
                // Si la estrategia declara CompatibleRegimes y el régimen actual del instrumento no está
                // en esa lista, la señal se descarta. Régimen Unknown nunca filtra (fail-safe).
                if (_strategyCompatibilities.TryGetValue(strategyExecutor.ExecutorIdentifier, out var compatibility))
                {
                    var currentRegime = _regimeRegistry.GetLastClassification(instrumentId);
                    if (!compatibility.IsCompatibleWith(currentRegime.Label))
                    {
                        _logger.Debug(
                            "BarProcessingService: señal {Direction} descartada para {ExecutorIdentifier}. " +
                            "Régimen actual de {InstrumentId} es {CurrentRegime}, no está en CompatibleRegimes de la estrategia.",
                            signalDirection, strategyExecutor.ExecutorIdentifier, instrumentId, currentRegime.Label);
                        continue;
                    }
                }
                // Si no hay compatibility registrada para este executor, no filtramos (fail-safe).

                // Bloqueamos entrada si ya hay posición o hay órdenes pending.
                if (_portfolioState.IsInvested(instrumentId)) continue;
                if (_orderRouter.HasOpenOrders(instrumentId)) continue;

                var sizingResult = _positionSizer.CalculateQuantity(strategyExecutor, marketBar.Close);
                if (sizingResult.IsFailure)
                {
                    _logger.Debug(
                        "BarProcessingService: cálculo de cantidad falló para {ExecutorIdentifier}. Motivo: {FailureReason}. Detalle: {FailureDescription}.",
                        strategyExecutor.ExecutorIdentifier, sizingResult.FailureReason, sizingResult.FailureDescription);
                    continue;
                }
                decimal quantityMagnitude = sizingResult.Value;

                var notionalResult = _positionSizer.ValidateNotional(instrumentId, quantityMagnitude, marketBar.Close);
                if (notionalResult.IsFailure)
                {
                    _logger.Debug(
                        "BarProcessingService: notional inválido para {ExecutorIdentifier}. Motivo: {FailureReason}. Detalle: {FailureDescription}.",
                        strategyExecutor.ExecutorIdentifier, notionalResult.FailureReason, notionalResult.FailureDescription);
                    continue;
                }

                // El sizer devuelve magnitud (positiva). El signo se aplica acá según la dirección.
                decimal signedQuantity = signalDirection == SignalDirection.Long
                    ? quantityMagnitude
                    : -quantityMagnitude;

                strategyExecutor.EntryOrderHandle = _orderRouter.SubmitMarketOrder(
                    instrumentId, signedQuantity, OrderPurpose.Entry, strategyExecutor.ExecutorIdentifier);

                _eventBus.Publish(new OrderSubmittedEvent(
                    TimestampUtc: _clock.UtcNow,
                    ExecutorIdentifier: strategyExecutor.ExecutorIdentifier,
                    InstrumentId: instrumentId,
                    Purpose: OrderPurpose.Entry,
                    Quantity: signedQuantity,
                    LimitPrice: null,
                    StopPrice: null,
                    ClientTag: string.Empty));

                _eventBus.Publish(new BarProcessedEvent(
                    _clock.UtcNow, marketBar.TimestampUtc, instrumentId));
            }
        }
    }
}
