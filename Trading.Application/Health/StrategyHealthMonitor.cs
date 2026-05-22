using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Health
{
    /// <summary>
    /// Monitor de salud por estrategia. Consume OrderFilledEvent del bus, mantiene métricas
    /// rolling por ExecutorIdentifier y evalúa los umbrales U1-U4 de POLICY sección 3.1.
    /// Al disparar: liquida posición abierta (si existe), marca la estrategia como degradada
    /// y publica RiskLimitBreachedEvent con StrategyDegradation.
    ///
    /// NO implementa IRiskMonitor: la degradación de una estrategia NO activa el kill switch
    /// global. Ver ADR-023.
    /// </summary>
    public sealed class StrategyHealthMonitor : IStrategyHealthMonitor
    {
        private readonly StrategyHealthThresholds _thresholds;
        private readonly IClock _clock;
        private readonly IOrderRouter _orderRouter;
        private readonly ITradingLogger _logger;
        private readonly IDomainEventBus _eventBus;

        private readonly object _lock = new();

        private readonly Dictionary<string, OpenPosition?> _openPositions = new();
        private readonly Dictionary<string, LinkedList<ClosedTrade>> _closedTrades = new();
        private readonly Dictionary<string, decimal> _equity = new();
        private readonly Dictionary<string, decimal> _ath = new();
        private readonly Dictionary<string, LinkedList<DailyEquityPoint>> _dailyEquity = new();
        private readonly Dictionary<string, DateOnly?> _lastEvaluatedDay = new();
        private readonly Dictionary<string, int> _u2SustainedDaysCounter = new();
        private readonly Dictionary<string, int> _u3SustainedTradesCounter = new();
        private readonly Dictionary<string, int> _u4SustainedTradesCounter = new();
        private readonly Dictionary<string, bool> _degraded = new();
        private readonly Dictionary<string, bool> _rollingThresholdsArmed = new();
        private readonly Dictionary<string, int> _totalClosedTrades = new();

        public StrategyHealthMonitor(
            StrategyHealthThresholds thresholds,
            IClock clock,
            IOrderRouter orderRouter,
            ITradingLogger logger,
            IDomainEventBus eventBus)
        {
            _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _orderRouter = orderRouter ?? throw new ArgumentNullException(nameof(orderRouter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            eventBus.Subscribe<OrderFilledEvent>(OnOrderFilled);
        }

        public bool IsExcluded(string executorIdentifier)
        {
            if (string.IsNullOrEmpty(executorIdentifier)) return false;
            lock (_lock)
            {
                return _degraded.TryGetValue(executorIdentifier, out var d) && d;
            }
        }

        private void OnOrderFilled(OrderFilledEvent e)
        {
            lock (_lock)
            {
                var id = e.ExecutorIdentifier;

                if (_degraded.TryGetValue(id, out var isDegraded) && isDegraded) return;

                EnsureBuckets(id);

                switch (e.Purpose)
                {
                    case OrderPurpose.Entry:
                        if (_openPositions[id] is not null)
                        {
                            throw new InvalidOperationException(
                                $"OPS-2 invariante violado: Entry de '{id}' con posición ya abierta.");
                        }
                        _openPositions[id] = new OpenPosition(
                            EntryPrice: e.FillPrice,
                            EntryQuantity: e.FillQuantity,
                            InstrumentId: e.InstrumentId,
                            EntryTimeUtc: e.TimestampUtc);
                        return;

                    case OrderPurpose.StopLoss:
                    case OrderPurpose.TakeProfit:
                    case OrderPurpose.TimeExit:
                        ProcessTradeClose(id, e);
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"OPS-2 invariante violado: OrderPurpose '{e.Purpose}' no manejado.");
                }
            }
        }

        private void ProcessTradeClose(string id, OrderFilledEvent e)
        {
            var open = _openPositions[id]
                ?? throw new InvalidOperationException(
                    $"OPS-2 invariante violado: cierre de '{id}' sin posición previa.");

            var directionSign = Math.Sign(open.EntryQuantity);
            if (directionSign == 0)
                throw new InvalidOperationException(
                    $"OPS-2 invariante violado: EntryQuantity de '{id}' es cero.");

            var realizedPnl = (e.FillPrice - open.EntryPrice) * Math.Abs(open.EntryQuantity) * directionSign;

            _openPositions[id] = null;

            var closed = new ClosedTrade(realizedPnl, e.TimestampUtc);
            _closedTrades[id].AddLast(closed);
            if (_closedTrades[id].Count > _thresholds.RollingWindowTrades)
                _closedTrades[id].RemoveFirst();

            _equity[id] += realizedPnl;
            if (_equity[id] > _ath[id]) _ath[id] = _equity[id];

            _totalClosedTrades[id]++;

            var currentDay = DateOnly.FromDateTime(_clock.UtcNow);
            if (_lastEvaluatedDay[id] is null || currentDay > _lastEvaluatedDay[id]!.Value)
            {
                _dailyEquity[id].AddLast(new DailyEquityPoint(currentDay, _equity[id]));
                if (_dailyEquity[id].Count > _thresholds.RollingWindowDays)
                    _dailyEquity[id].RemoveFirst();

                var (u2Triggered, u2Desc) = EvaluateU2(id);
                _lastEvaluatedDay[id] = currentDay;

                if (u2Triggered)
                {
                    TriggerDegradation(id, u2Desc);
                    return;
                }
            }

            var (u1Triggered, u1Desc) = EvaluateU1(id);
            if (u1Triggered)
            {
                TriggerDegradation(id, u1Desc);
                return;
            }

            if (!_rollingThresholdsArmed[id] &&
                _totalClosedTrades[id] >= _thresholds.MinimumTradesToArmRollingThresholds)
            {
                _rollingThresholdsArmed[id] = true;
                _logger.Info(
                    "U3 y U4 armados para '{ExecutorIdentifier}' tras alcanzar {Trades} trades acumulados.",
                    id, _totalClosedTrades[id]);
            }

            if (_rollingThresholdsArmed[id] &&
                _closedTrades[id].Count >= _thresholds.RollingWindowTrades)
            {
                var (u3Triggered, u3Desc) = EvaluateU3(id);
                if (u3Triggered)
                {
                    TriggerDegradation(id, u3Desc);
                    return;
                }

                var (u4Triggered, u4Desc) = EvaluateU4(id);
                if (u4Triggered)
                {
                    TriggerDegradation(id, u4Desc);
                    return;
                }
            }
        }

        private (bool triggered, string description) EvaluateU1(string id)
        {
            if (_ath[id] <= 0m) return (false, string.Empty);
            var dd = (_ath[id] - _equity[id]) / _ath[id];
            if (dd > _thresholds.AbsoluteDrawdownFromAthFraction)
                return (true, $"U1: DD absoluto {dd:P2} > umbral {_thresholds.AbsoluteDrawdownFromAthFraction:P2}");
            return (false, string.Empty);
        }

        private (bool triggered, string description) EvaluateU2(string id)
        {
            var dailySeries = _dailyEquity[id];
            if (dailySeries.Count < 2) return (false, string.Empty);
            var maxInWindow = dailySeries.Max(p => p.EquityAtEndOfDay);
            var lastInWindow = dailySeries.Last!.Value.EquityAtEndOfDay;
            if (maxInWindow <= 0m) { _u2SustainedDaysCounter[id] = 0; return (false, string.Empty); }
            var ddRolling = (maxInWindow - lastInWindow) / maxInWindow;
            if (ddRolling > _thresholds.RollingDrawdownThirtyDaysFraction)
            {
                _u2SustainedDaysCounter[id]++;
                if (_u2SustainedDaysCounter[id] >= _thresholds.RollingDrawdownSustainedDays)
                    return (true, $"U2: DD rolling 30d {ddRolling:P2} sostenido {_u2SustainedDaysCounter[id]} días.");
            }
            else
            {
                _u2SustainedDaysCounter[id] = 0;
            }
            return (false, string.Empty);
        }

        private (bool triggered, string description) EvaluateU3(string id)
        {
            var trades = _closedTrades[id];
            if (trades.Count < _thresholds.RollingWindowTrades) return (false, string.Empty);
            decimal grossProfit = 0m, grossLoss = 0m;
            foreach (var t in trades)
            {
                if (t.RealizedPnl > 0) grossProfit += t.RealizedPnl;
                else if (t.RealizedPnl < 0) grossLoss += -t.RealizedPnl;
            }
            if (grossLoss == 0m)
            {
                _u3SustainedTradesCounter[id] = 0;
                return (false, string.Empty);
            }
            var pf = grossProfit / grossLoss;
            if (pf < _thresholds.RollingProfitFactorThreshold)
            {
                _u3SustainedTradesCounter[id]++;
                if (_u3SustainedTradesCounter[id] >= _thresholds.RollingProfitFactorSustainedTrades)
                    return (true, $"U3: PF rolling {pf:F2} sostenido {_u3SustainedTradesCounter[id]} trades.");
            }
            else
            {
                _u3SustainedTradesCounter[id] = 0;
            }
            return (false, string.Empty);
        }

        private (bool triggered, string description) EvaluateU4(string id)
        {
            var trades = _closedTrades[id];
            if (trades.Count < _thresholds.RollingWindowTrades) return (false, string.Empty);
            var wins = trades.Where(t => t.RealizedPnl > 0).ToList();
            var losses = trades.Where(t => t.RealizedPnl < 0).ToList();
            var total = (decimal)trades.Count;
            var winRate = wins.Count / total;
            var lossRate = losses.Count / total;
            var avgWin = wins.Count > 0 ? wins.Sum(t => t.RealizedPnl) / wins.Count : 0m;
            var avgLoss = losses.Count > 0 ? -losses.Sum(t => t.RealizedPnl) / losses.Count : 0m;
            var expectancy = (winRate * avgWin) - (lossRate * avgLoss);
            if (expectancy < _thresholds.RollingExpectancyThreshold)
            {
                _u4SustainedTradesCounter[id]++;
                if (_u4SustainedTradesCounter[id] >= _thresholds.RollingExpectancySustainedTrades)
                    return (true, $"U4: expectancy rolling {expectancy:F2} sostenido {_u4SustainedTradesCounter[id]} trades.");
            }
            else
            {
                _u4SustainedTradesCounter[id] = 0;
            }
            return (false, string.Empty);
        }

        private void TriggerDegradation(string id, string description)
        {
            _degraded[id] = true;

            var open = _openPositions[id];
            if (open is not null)
            {
                _orderRouter.LiquidateInstrument(open.InstrumentId, OrderPurpose.TimeExit, id);
                _openPositions[id] = null;
                _logger.Critical(
                    "OPS-2 disparó degradación de '{ExecutorIdentifier}'. {Description} Liquidando posición.",
                    id, description);
            }
            else
            {
                _logger.Critical(
                    "OPS-2 disparó degradación de '{ExecutorIdentifier}' sin posición abierta. {Description}",
                    id, description);
                _logger.Info(
                    "'{ExecutorIdentifier}' degradada sin posición abierta a liquidar.",
                    id);
            }

            _eventBus.Publish(new RiskLimitBreachedEvent(
                TimestampUtc: _clock.UtcNow,
                Reason: RiskLimitBreachReason.StrategyDegradation,
                Description: $"{id}: {description}"));
        }

        private void EnsureBuckets(string id)
        {
            if (_openPositions.ContainsKey(id)) return;
            _openPositions[id] = null;
            _closedTrades[id] = new LinkedList<ClosedTrade>();
            _equity[id] = 0m;
            _ath[id] = 0m;
            _dailyEquity[id] = new LinkedList<DailyEquityPoint>();
            _lastEvaluatedDay[id] = null;
            _u2SustainedDaysCounter[id] = 0;
            _u3SustainedTradesCounter[id] = 0;
            _u4SustainedTradesCounter[id] = 0;
            _degraded[id] = false;
            _rollingThresholdsArmed[id] = false;
            _totalClosedTrades[id] = 0;
        }

        private sealed record OpenPosition(
            decimal EntryPrice,
            decimal EntryQuantity,
            InstrumentId InstrumentId,
            DateTime EntryTimeUtc);

        private sealed record ClosedTrade(
            decimal RealizedPnl,
            DateTime ClosedAtUtc);

        private sealed record DailyEquityPoint(
            DateOnly Date,
            decimal EquityAtEndOfDay);
    }
}
