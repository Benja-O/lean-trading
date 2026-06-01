using System;

namespace Trading.Application.Health
{
    /// <summary>
    /// Determina si las barras procesadas son suficientemente recientes para justificar un ping
    /// al dead-man's switch externo (Healthchecks.io).
    ///
    /// Umbral configurable: si la última barra procesada supera el threshold (wall-clock),
    /// el gate devuelve false y el caller debe suprimir el ping para que el check caiga a DOWN.
    ///
    /// Decisión de diseño — null/warmup:
    ///   Retorna true cuando aún no se procesó ninguna barra (warmup). El proceso está vivo
    ///   y funcionando normalmente; el feed de datos de historia todavía no terminó. En live
    ///   el warm-up dura segundos, no minutos, por lo que no genera falsos positivos de UP.
    ///
    /// El caller debe pasar DateTime.UtcNow (real wall-clock), no IClock.UtcNow, para
    /// mantener la separación wall-clock vs. clock simulado del backtest (ADR-021).
    /// </summary>
    public sealed class BarStalenessGate
    {
        private readonly HealthHeartbeatTracker _tracker;
        private readonly TimeSpan _threshold;

        public BarStalenessGate(HealthHeartbeatTracker tracker, TimeSpan threshold)
        {
            _tracker = tracker;
            _threshold = threshold;
        }

        public bool IsFresh(DateTime wallClockUtcNow)
        {
            var snapshot = _tracker.Snapshot();
            if (!snapshot.LastBarProcessedUtc.HasValue)
                return true;
            return wallClockUtcNow - snapshot.LastBarProcessedUtc.Value < _threshold;
        }
    }
}
