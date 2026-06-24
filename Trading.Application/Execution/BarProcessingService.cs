using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
        // Opcional: providers de microestructura por timeframe (mismas instancias que usan las
        // estrategias). Si está presente, se loguean las features de la barra que disparó cada señal
        // (observabilidad genérica, sin código por estrategia). El executor trae su Timeframe, que es
        // la clave para resolver el provider correcto en setups multi-timeframe. Null/ausente en
        // deployments sin microestructura: se loguea igual la señal y el OHLC, sin features de flujo.
        private readonly IReadOnlyDictionary<string, IMicrostructureProvider>? _microstructureByTimeframe;

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
            IStrategyHealthMonitor strategyHealthMonitor,
            IReadOnlyDictionary<string, IMicrostructureProvider>? microstructureByTimeframe = null)
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
            _microstructureByTimeframe = microstructureByTimeframe;
        }

        public void ProcessBar(
            MarketBar marketBar,
            IReadOnlyList<StrategyExecutor> strategyExecutors,
            bool isWarmingUp = false)
        {
            // Emitir una vez por barra, independientemente de si se genera señal o si es warm-up.
            // Garantiza que LastBarProcessedUtc se actualice en el heartbeat cada minuto
            // (no solo en submit-order), lo que el watchdog de barras stale necesita.
            _eventBus.Publish(new BarProcessedEvent(_clock.UtcNow, marketBar.TimestampUtc, marketBar.InstrumentId));

            if (isWarmingUp)
            {
                // Durante warm-up: alimentar los indicadores internos de cada estrategia
                // para que estén calentados al inicio del trading real, sin emitir órdenes.
                foreach (var strategyExecutor in strategyExecutors)
                    strategyExecutor.Strategy.EvaluateSignal(marketBar);
                return;
            }

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

                // Auditoría de señal (ADR-052): se loguea ACÁ —tras confirmar señal no-Flat y ANTES
                // de los filtros de régimen/posición/sizing— para que toda señal emitida quede
                // auditable aunque después se descarte. Genérico: features del provider + condición
                // de la estrategia (si expone ISignalDiagnosticsProvider).
                LogSignalEmitted(strategyExecutor, marketBar, signalDirection);

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

                // Modo ATR: capturar precios de SL/TP al momento de la señal.
                // El ATR cambia barra a barra; si esperamos al fill (barra siguiente) perdemos el valor correcto.
                if (strategyExecutor.Definition.StopTakeMode == "Atr" &&
                    strategyExecutor.Strategy is IAtrProvider atrProvider)
                {
                    decimal atr = atrProvider.GetLastAtr(instrumentId.Ticker);
                    if (atr > 0m)
                    {
                        decimal slDist = strategyExecutor.Definition.StopLossAtrMultiplier * atr;
                        decimal tpDist = strategyExecutor.Definition.TakeProfitAtrMultiplier * atr;
                        strategyExecutor.PendingStopLossPrice = signalDirection == SignalDirection.Long
                            ? marketBar.Close - slDist
                            : marketBar.Close + slDist;
                        strategyExecutor.PendingTakeProfitPrice = signalDirection == SignalDirection.Long
                            ? marketBar.Close + tpDist
                            : marketBar.Close - tpDist;
                    }
                }

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
            }
        }

        /// <summary>
        /// Emite un evento estructurado por cada señal no-Flat: identidad del executor, OHLC de la
        /// MarketBar, las features microestructurales de la barra (si hay provider) y la condición
        /// evaluada por la estrategia (si expone ISignalDiagnosticsProvider). Placeholders nombrados
        /// sin format specifiers: la estructura va en las properties del JSONL, no en el render.
        /// </summary>
        private void LogSignalEmitted(
            StrategyExecutor strategyExecutor,
            MarketBar marketBar,
            SignalDirection signalDirection)
        {
            var instrumentId = strategyExecutor.InstrumentId;

            MicrostructureBar? msBar = null;
            if (_microstructureByTimeframe != null &&
                _microstructureByTimeframe.TryGetValue(strategyExecutor.Timeframe, out var provider))
            {
                msBar = provider.GetBar(instrumentId, marketBar.TimestampUtc);
            }

            string conditions = BuildConditionsText(strategyExecutor.Strategy);

            _logger.Info(
                "SignalEmitted estrategia={ExecutorIdentifier} ticker={Ticker} timeframe={Timeframe} " +
                "barUtc={BarUtc} direccion={Direction} " +
                "open={Open} high={High} low={Low} close={Close} volume={Volume} " +
                "ofi={Ofi} cvd={Cvd} cvdDelta={CvdDelta} arrivalRate={ArrivalRate} " +
                "meanTradeSize={MeanTradeSize} buySellRatio={BuySellRatio} priceReturn={PriceReturn} " +
                "tieneMicroestructura={HasMicrostructure} condiciones={Conditions}",
                strategyExecutor.ExecutorIdentifier,
                instrumentId.Ticker,
                strategyExecutor.Timeframe,
                marketBar.TimestampUtc,
                signalDirection,
                marketBar.Open, marketBar.High, marketBar.Low, marketBar.Close, marketBar.Volume,
                msBar?.Ofi, msBar?.Cvd, msBar?.CvdDelta, msBar?.ArrivalRate,
                msBar?.MeanTradeSize, msBar?.BuySellRatio, msBar?.PriceReturn,
                msBar != null, conditions);
        }

        /// <summary>
        /// Resumen legible de la condición que evaluó la estrategia. "(n/d)" si la estrategia no
        /// expone diagnóstico (no implementa ISignalDiagnosticsProvider o aún no evaluó ninguna barra).
        /// </summary>
        private static string BuildConditionsText(IStrategy strategy)
        {
            if (strategy is not ISignalDiagnosticsProvider diagnosticsProvider)
                return "(n/d)";

            var diagnostics = diagnosticsProvider.DescribeLastEvaluation();
            if (diagnostics == null)
                return "(n/d)";

            // InvariantCulture: el log de auditoría debe ser independiente de la cultura de la máquina
            // (evita separador decimal coma en VPS con locale no-US y mantiene los valores parseables).
            var builder = new StringBuilder(diagnostics.Summary);
            foreach (var condition in diagnostics.Conditions)
            {
                builder.Append(" | ")
                    .Append(condition.Name).Append('[')
                    .Append(condition.Value.ToString(CultureInfo.InvariantCulture))
                    .Append(condition.Comparison)
                    .Append(condition.Threshold.ToString(CultureInfo.InvariantCulture))
                    .Append('=').Append(condition.Satisfied).Append(']');
            }
            return builder.ToString();
        }
    }
}
