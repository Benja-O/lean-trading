using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Monitor de riesgo por pérdidas consecutivas. El counter se incrementa cuando el caller
    /// invoca RegisterLoss(). El monitor dispara kill switch cuando el counter alcanza el límite.
    ///
    /// El caller (OrderLifecycleService o quien corresponda) es responsable de invocar
    /// RegisterLoss/RegisterWin cuando un trade se cierra. El monitor no consume eventos
    /// de fills directamente — depende de quien sabe interpretar P&amp;L.
    /// </summary>
    public sealed class ConsecutiveLossesMonitor : IRiskMonitor
    {
        private readonly int _maximumConsecutiveLosses;
        private int _consecutiveLossesCounter;
        private bool _shouldTrigger;
        private string _triggerDescription = string.Empty;

        public string MonitorName => "ConsecutiveLossesMonitor";

        public ConsecutiveLossesMonitor(int maximumConsecutiveLosses)
        {
            _maximumConsecutiveLosses = maximumConsecutiveLosses;
        }

        public void RegisterLoss()
        {
            _consecutiveLossesCounter++;
            if (_consecutiveLossesCounter >= _maximumConsecutiveLosses)
            {
                _shouldTrigger = true;
                _triggerDescription = $"{_maximumConsecutiveLosses} pérdidas consecutivas alcanzadas.";
            }
        }

        public void RegisterWin()
        {
            _consecutiveLossesCounter = 0;
        }

        public RiskAssessment Evaluate()
        {
            if (_shouldTrigger)
            {
                return RiskAssessment.Trigger(
                    RiskLimitBreachReason.ConsecutiveLossesExceeded,
                    _triggerDescription);
            }
            return RiskAssessment.Pass();
        }

        public void Reset()
        {
            _consecutiveLossesCounter = 0;
            _shouldTrigger = false;
            _triggerDescription = string.Empty;
        }
    }
}
