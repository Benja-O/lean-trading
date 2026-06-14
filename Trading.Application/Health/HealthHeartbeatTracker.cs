using System;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Health
{
    /// <summary>
    /// Mantiene en memoria el estado de salud del sistema suscribiéndose a eventos de dominio.
    /// Thread-safe: lock interno en todas las actualizaciones y en Snapshot().
    /// </summary>
    public sealed class HealthHeartbeatTracker
    {
        private readonly IClock _clock;
        private readonly object _lock = new();

        private readonly DateTime _processStartedUtc;
        private DateTime? _lastBarProcessedUtc;
        private DateTime? _lastBarTimestampUtc;
        private DateTime? _lastDataReceivedUtc;
        private DateTime? _lastOrderSubmittedUtc;
        private DateTime? _lastOrderFilledUtc;
        private DateTime? _lastRiskBreachUtc;
        private string? _lastRiskBreachReason;
        private bool _killSwitchActive;
        private string? _sinkLastWriteFailureMessage;

        public HealthHeartbeatTracker(IDomainEventBus eventBus, IClock clock, ITradingLogger logger)
        {
            _clock = clock;
            _processStartedUtc = clock.UtcNow;

            eventBus.Subscribe<BarProcessedEvent>(OnBarProcessed);
            eventBus.Subscribe<OrderSubmittedEvent>(OnOrderSubmitted);
            eventBus.Subscribe<OrderFilledEvent>(OnOrderFilled);
            eventBus.Subscribe<RiskLimitBreachedEvent>(OnRiskLimitBreached);
        }

        /// <summary>
        /// Registra el último fallo de IO del sink JSONL. Llamado periódicamente por el host
        /// cuando JsonlFileLogSink.LastWriteFailure es no nulo. El mensaje se incluye en el
        /// próximo snapshot para que aparezca en heartbeat.json.
        /// </summary>
        public void RecordSinkWriteFailure(string? message)
        {
            lock (_lock)
            {
                _sinkLastWriteFailureMessage = message;
            }
        }

        public HealthSnapshot Snapshot()
        {
            lock (_lock)
            {
                var now = _clock.UtcNow;
                return new HealthSnapshot(
                    CurrentUtc: now,
                    ProcessStartedUtc: _processStartedUtc,
                    LastBarProcessedUtc: _lastBarProcessedUtc,
                    LastBarTimestampUtc: _lastBarTimestampUtc,
                    LastOrderSubmittedUtc: _lastOrderSubmittedUtc,
                    LastOrderFilledUtc: _lastOrderFilledUtc,
                    LastRiskBreachUtc: _lastRiskBreachUtc,
                    LastRiskBreachReason: _lastRiskBreachReason,
                    KillSwitchActive: _killSwitchActive,
                    BarStalenessSeconds: _lastBarProcessedUtc.HasValue
                        ? (now - _lastBarProcessedUtc.Value).TotalSeconds
                        : (double?)null,
                    LastDataReceivedUtc: _lastDataReceivedUtc,
                    DataFeedStalenessSeconds: _lastDataReceivedUtc.HasValue
                        ? (now - _lastDataReceivedUtc.Value).TotalSeconds
                        : (double?)null,
                    SinkLastWriteFailureMessage: _sinkLastWriteFailureMessage);
            }
        }

        /// <summary>
        /// Re-baselina el tracking al iniciar el modo live (post-warmup).
        /// Las barras del warmup usan algorithm time (histórico), por lo que
        /// _lastBarProcessedUtc y _lastDataReceivedUtc quedan desfasados respecto
        /// al wall clock real. Llamar con DateTime.UtcNow desde OnWarmupFinished()
        /// para que el primer tick del auto-restart timer (que mira el feed) y el
        /// ping gate (que mira las barras de estrategia) vean staleness ~ segundos.
        /// </summary>
        public void MarkLiveModeStart(DateTime wallClockNow)
        {
            lock (_lock)
            {
                _lastBarProcessedUtc = wallClockNow;
                _lastDataReceivedUtc = wallClockNow;
            }
        }

        /// <summary>
        /// Marca que el feed entregó datos de minuto (liveness del WebSocket).
        /// Llamado desde OnData en cada slice live (~cada 60s). Independiente de la
        /// cadencia de barras de estrategia: el auto-restart usa esta marca para
        /// detectar un socket congelado sin importar el timeframe (1h, 4h, etc.).
        /// wallClockNow debe ser DateTime.UtcNow (wall clock real, no IClock simulado).
        /// </summary>
        public void MarkDataFeedAlive(DateTime wallClockNow)
        {
            lock (_lock)
            {
                _lastDataReceivedUtc = wallClockNow;
            }
        }

        private void OnBarProcessed(BarProcessedEvent e)
        {
            lock (_lock)
            {
                _lastBarProcessedUtc = e.TimestampUtc;
                _lastBarTimestampUtc = e.BarTimestampUtc;
            }
        }

        private void OnOrderSubmitted(OrderSubmittedEvent e)
        {
            lock (_lock)
            {
                _lastOrderSubmittedUtc = e.TimestampUtc;
            }
        }

        private void OnOrderFilled(OrderFilledEvent e)
        {
            lock (_lock)
            {
                _lastOrderFilledUtc = e.TimestampUtc;
            }
        }

        private void OnRiskLimitBreached(RiskLimitBreachedEvent e)
        {
            lock (_lock)
            {
                _lastRiskBreachUtc = e.TimestampUtc;
                _lastRiskBreachReason = e.Reason.ToString();
                _killSwitchActive = true;
            }
        }
    }
}
