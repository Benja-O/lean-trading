using System;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Monitor de riesgo por drawdown del portfolio respecto a su máximo histórico.
    ///
    /// Mantiene un high-water mark interno. Cada evaluación calcula el drawdown actual
    /// (max - current) / max. Si supera maximumDrawdownFraction, dispara kill switch.
    ///
    /// El high-water mark se inicializa con el valor del portfolio en la construcción
    /// (se inyecta vía InitializeWithCurrentValue, no por constructor para mantener
    /// el wiring simple).
    /// </summary>
    public sealed class DrawdownMonitor : IRiskMonitor
    {
        private readonly IPortfolioState _portfolioState;
        private readonly decimal _maximumDrawdownFraction;
        private decimal _maximumPortfolioValue;

        public string MonitorName => "DrawdownMonitor";

        /// <param name="portfolioState">Fuente del valor actual del portfolio.</param>
        /// <param name="maximumDrawdownFraction">
        /// Fracción decimal del drawdown máximo tolerado (ej. 0.25m = 25%).
        /// Si el drawdown observado iguala o supera este valor, se dispara kill switch.
        /// </param>
        public DrawdownMonitor(IPortfolioState portfolioState, decimal maximumDrawdownFraction)
        {
            _portfolioState = portfolioState ?? throw new ArgumentNullException(nameof(portfolioState));
            _maximumDrawdownFraction = maximumDrawdownFraction;
        }

        /// <summary>
        /// Inicializa el high-water mark con el valor actual del portfolio.
        /// Llamar una vez tras la construcción, cuando el portfolio ya está poblado con el cash inicial.
        /// </summary>
        public void InitializeWithCurrentValue()
        {
            _maximumPortfolioValue = _portfolioState.TotalPortfolioValue;
        }

        public RiskAssessment Evaluate()
        {
            decimal currentPortfolioValue = _portfolioState.TotalPortfolioValue;

            if (currentPortfolioValue > _maximumPortfolioValue)
            {
                _maximumPortfolioValue = currentPortfolioValue;
            }

            if (_maximumPortfolioValue == 0m)
            {
                return RiskAssessment.Pass();
            }

            decimal currentDrawdown =
                (_maximumPortfolioValue - currentPortfolioValue) / _maximumPortfolioValue;

            if (currentDrawdown >= _maximumDrawdownFraction)
            {
                return RiskAssessment.Trigger(
                    RiskLimitBreachReason.MaximumDrawdownExceeded,
                    $"Drawdown actual {currentDrawdown:P2} >= límite {_maximumDrawdownFraction:P2}");
            }

            return RiskAssessment.Pass();
        }

        public void Reset()
        {
            _maximumPortfolioValue = _portfolioState.TotalPortfolioValue;
        }
    }
}
