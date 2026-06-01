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
        private DateTime? _lastOrderSubmittedUtc;
        private DateTime? _lastOrderFilledUtc;
        private DateTime? _lastRiskBreachUtc;
        private string? _lastRiskBreachReason;
        private bool _killSwitchActive;

        public HealthHeartbeatTracker(IDomainEventBus eventBus, IClock clock, ITradingLogger logger)
        {
            _clock = clock;
            _processStartedUtc = clock.UtcNow;

            eventBus.Subscribe<BarProcessedEvent>(OnBarProcessed);
            eventBus.Subscribe<OrderSubmittedEvent>(OnOrderSubmitted);
            eventBus.Subscribe<OrderFilledEvent>(OnOrderFilled);
            eventBus.Subscribe<RiskLimitBreachedEvent>(OnRiskLimitBreached);
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
                        : (double?)null);
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
