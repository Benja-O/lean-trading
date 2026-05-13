using System;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Gestor del kill switch del sistema. Monitorea drawdown y pérdidas consecutivas;
    /// al cruzar umbrales, desactiva el sistema y liquida todo.
    ///
    /// Esta clase NO conoce QuantConnect: opera exclusivamente sobre IPortfolioState, IOrderRouter,
    /// IClock, ITradingLogger e IDomainEventBus.
    ///
    /// NOTA INSTITUCIONAL: en una arquitectura más madura, la detección y la acción de liquidar
    /// estarían separadas (un componente detecta y emite evento, otro decide acción).
    /// Aquí se mantienen juntas por simplicidad inicial. Próxima fase: separar.
    /// </summary>
    public class KillSwitchManager
    {
        private readonly IPortfolioState _portfolioState;
        private readonly IOrderRouter _orderRouter;
        private readonly IClock _clock;
        private readonly ITradingLogger _logger;
        private readonly IDomainEventBus _eventBus;

        private readonly decimal _maximumDrawdownFraction;
        private readonly int _maximumConsecutiveLosses;
        private readonly TimeSpan _coolingOffPeriod;

        public bool IsKillSwitchActivated { get; private set; }

        private int _consecutiveLossesCounter;
        private decimal _maximumPortfolioValue;
        private DateTime _killSwitchTimestampUtc;

        public KillSwitchManager(
            IPortfolioState portfolioState,
            IOrderRouter orderRouter,
            IClock clock,
            ITradingLogger logger,
            IDomainEventBus eventBus,
            decimal maximumDrawdownFraction = 0.25m,
            int maximumConsecutiveLosses = 8,
            TimeSpan? coolingOffPeriod = null)
        {
            _portfolioState = portfolioState ?? throw new ArgumentNullException(nameof(portfolioState));
            _orderRouter = orderRouter ?? throw new ArgumentNullException(nameof(orderRouter));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            _maximumDrawdownFraction = maximumDrawdownFraction;
            _maximumConsecutiveLosses = maximumConsecutiveLosses;
            _coolingOffPeriod = coolingOffPeriod ?? TimeSpan.FromDays(1);

            IsKillSwitchActivated = false;
            _consecutiveLossesCounter = 0;
            _maximumPortfolioValue = 0m;
        }

        public void InitializePortfolioValue()
        {
            _maximumPortfolioValue = _portfolioState.TotalPortfolioValue;
        }

        public void CheckDrawdownKillSwitch()
        {
            decimal currentPortfolioValue = _portfolioState.TotalPortfolioValue;

            if (currentPortfolioValue > _maximumPortfolioValue)
            {
                _maximumPortfolioValue = currentPortfolioValue;
            }

            if (_maximumPortfolioValue == 0m) return; // evita división por cero antes de InitializePortfolioValue

            decimal currentDrawdown = (_maximumPortfolioValue - currentPortfolioValue) / _maximumPortfolioValue;

            if (currentDrawdown >= _maximumDrawdownFraction)
            {
                ActivateKillSwitch(
                    RiskLimitBreachReason.MaximumDrawdownExceeded,
                    $"Drawdown actual {currentDrawdown:P2} >= límite {_maximumDrawdownFraction:P2}");
            }
        }

        public void RegisterLoss()
        {
            _consecutiveLossesCounter++;
            if (_consecutiveLossesCounter >= _maximumConsecutiveLosses)
            {
                ActivateKillSwitch(
                    RiskLimitBreachReason.ConsecutiveLossesExceeded,
                    $"{_maximumConsecutiveLosses} pérdidas consecutivas alcanzadas.");
            }
        }

        public void ResetLossCounter()
        {
            _consecutiveLossesCounter = 0;
        }

        /// <summary>
        /// Activa el kill switch con motivo tipado. Emite RiskLimitBreachedEvent y liquida toda la cartera.
        /// </summary>
        public void ActivateKillSwitch(RiskLimitBreachReason reason, string description)
        {
            IsKillSwitchActivated = true;
            _killSwitchTimestampUtc = _clock.UtcNow;

            // Publicar evento ANTES de liquidar: si un suscriptor falla, igual queremos cumplir
            // con la liquidación. El bus aísla excepciones de suscriptores.
            _eventBus.Publish(new RiskLimitBreachedEvent(
                TimestampUtc: _clock.UtcNow,
                Reason: reason,
                Description: description));

            _orderRouter.LiquidateAll();
            _logger.Critical(
                "KILL SWITCH ACTIVADO. Motivo: {Reason}. Detalle: {Description}.",
                reason, description);
        }

        /// <summary>
        /// Overload de compatibilidad para callers que solo tengan un string libre. Usa Manual como motivo.
        /// </summary>
        public void ActivateKillSwitch(string description)
        {
            ActivateKillSwitch(RiskLimitBreachReason.Manual, description);
        }

        public void EvaluateCoolingOffPeriod()
        {
            if (!IsKillSwitchActivated) return;

            if (_clock.UtcNow - _killSwitchTimestampUtc >= _coolingOffPeriod)
            {
                IsKillSwitchActivated = false;
                _consecutiveLossesCounter = 0;
                _maximumPortfolioValue = _portfolioState.TotalPortfolioValue;
                _logger.Info("Cooling-off period finalizado. Sistema reanudado.");
            }
        }
    }
}
