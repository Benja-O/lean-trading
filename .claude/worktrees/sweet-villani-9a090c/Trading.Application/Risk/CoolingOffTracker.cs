using System;
using Trading.Domain.Abstractions;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Componente que rastrea el período de cooling-off tras una activación del kill switch.
    ///
    /// NO implementa IRiskMonitor porque su rol es inverso: señala cuándo el kill switch
    /// debe DESACTIVARSE, no cuándo activarse. El RiskOrchestrator lo consulta cada ciclo.
    ///
    /// Comportamiento:
    /// - StartCoolingOff(): registra el timestamp de inicio.
    /// - HasCoolingOffExpired(): devuelve true si transcurrió el período configurado.
    /// </summary>
    public sealed class CoolingOffTracker
    {
        private readonly IClock _clock;
        private readonly TimeSpan _coolingOffPeriod;
        private DateTime _coolingOffStartedUtc;
        private bool _isInCoolingOff;

        public CoolingOffTracker(IClock clock, TimeSpan coolingOffPeriod)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _coolingOffPeriod = coolingOffPeriod;
        }

        public void StartCoolingOff()
        {
            _coolingOffStartedUtc = _clock.UtcNow;
            _isInCoolingOff = true;
        }

        public bool HasCoolingOffExpired()
        {
            if (!_isInCoolingOff) return false;
            return _clock.UtcNow - _coolingOffStartedUtc >= _coolingOffPeriod;
        }

        public void Reset()
        {
            _isInCoolingOff = false;
        }
    }
}
