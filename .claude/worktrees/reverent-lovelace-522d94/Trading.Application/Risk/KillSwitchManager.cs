using System;
using Trading.Domain.Abstractions;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Gestor del kill switch del sistema. Monitorea drawdown y pérdidas consecutivas;
    /// al cruzar umbrales, desactiva el sistema y liquida todo.
    /// 
    /// Esta clase NO conoce QuantConnect: opera exclusivamente sobre IPortfolioState, IOrderRouter,
    /// IClock e ITradingLogger. Por eso se puede testear con fakes en milisegundos.
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
            decimal maximumDrawdownFraction = 0.25m,
            int maximumConsecutiveLosses = 8,
            TimeSpan? coolingOffPeriod = null)
        {
            _portfolioState = portfolioState ?? throw new ArgumentNullException(nameof(portfolioState));
            _orderRouter = orderRouter ?? throw new ArgumentNullException(nameof(orderRouter));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
                ActivateKillSwitch($"Drawdown de {currentDrawdown:P}");
            }
        }

        public void ActivateKillSwitch(string reason)
        {
            IsKillSwitchActivated = true;
            _killSwitchTimestampUtc = _clock.UtcNow;
            _orderRouter.LiquidateAll();
            _logger.Critical("Kill switch activado. Reason={Reason}", reason);
        }

        public void RegisterLoss()
        {
            _consecutiveLossesCounter++;
            if (_consecutiveLossesCounter >= _maximumConsecutiveLosses)
            {
                ActivateKillSwitch($"{_maximumConsecutiveLosses} pérdidas consecutivas");
            }
        }

        public void ResetLossCounter()
        {
            _consecutiveLossesCounter = 0;
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
