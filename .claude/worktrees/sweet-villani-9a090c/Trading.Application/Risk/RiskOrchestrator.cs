using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Coordinador de los monitors de riesgo y la acción de mitigación.
    ///
    /// Cada ciclo (típicamente una vez por barra) el caller invoca EvaluateAllMonitors().
    /// El orchestrator:
    /// 1. Si el kill switch ya está activo: chequea el cooling-off. Si expiró, desactiva
    ///    el kill switch y resetea todos los monitors.
    /// 2. Si el kill switch NO está activo: itera los monitors, recoge el primer veredicto
    ///    Trigger (si lo hay) y activa el kill switch.
    ///
    /// Publicación de eventos: emite RiskLimitBreachedEvent al activar el kill switch.
    /// </summary>
    public sealed class RiskOrchestrator
    {
        private readonly IReadOnlyList<IRiskMonitor> _monitors;
        private readonly IRiskAction _riskAction;
        private readonly CoolingOffTracker _coolingOffTracker;
        private readonly IClock _clock;
        private readonly ITradingLogger _logger;
        private readonly IDomainEventBus _eventBus;

        public bool IsKillSwitchActivated { get; private set; }

        public RiskOrchestrator(
            IEnumerable<IRiskMonitor> monitors,
            IRiskAction riskAction,
            CoolingOffTracker coolingOffTracker,
            IClock clock,
            ITradingLogger logger,
            IDomainEventBus eventBus)
        {
            _monitors = (monitors ?? throw new ArgumentNullException(nameof(monitors))).ToList();
            _riskAction = riskAction ?? throw new ArgumentNullException(nameof(riskAction));
            _coolingOffTracker = coolingOffTracker ?? throw new ArgumentNullException(nameof(coolingOffTracker));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// Punto de entrada principal. Llamar una vez por ciclo (típicamente por barra).
        /// </summary>
        public void EvaluateAllMonitors()
        {
            if (IsKillSwitchActivated)
            {
                EvaluateCoolingOffPeriod();
                return;
            }

            foreach (var monitor in _monitors)
            {
                var assessment = monitor.Evaluate();
                if (assessment.ShouldTriggerKillSwitch)
                {
                    ActivateKillSwitch(assessment.Reason, assessment.Description, monitor.MonitorName);
                    return;
                }
            }
        }

        /// <summary>
        /// Activa el kill switch manualmente (no a través de un monitor). Útil para activación
        /// externa o de testing. Razón asociada: Manual.
        /// </summary>
        public void ActivateKillSwitchManually(string description)
        {
            ActivateKillSwitch(RiskLimitBreachReason.Manual, description, "Manual");
        }

        private void ActivateKillSwitch(RiskLimitBreachReason reason, string description, string sourceMonitorName)
        {
            IsKillSwitchActivated = true;
            _coolingOffTracker.StartCoolingOff();

            _eventBus.Publish(new RiskLimitBreachedEvent(
                TimestampUtc: _clock.UtcNow,
                Reason: reason,
                Description: description));

            _riskAction.Execute();

            _logger.Critical(
                "KILL SWITCH ACTIVADO por {SourceMonitor}. Motivo: {Reason}. Detalle: {Description}.",
                sourceMonitorName, reason, description);
        }

        private void EvaluateCoolingOffPeriod()
        {
            if (_coolingOffTracker.HasCoolingOffExpired())
            {
                IsKillSwitchActivated = false;
                _coolingOffTracker.Reset();

                foreach (var monitor in _monitors)
                {
                    monitor.Reset();
                }

                _logger.Info("Cooling-off finalizó. Kill switch desactivado y monitors reseteados.");
            }
        }
    }
}
