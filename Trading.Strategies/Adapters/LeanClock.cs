using System;
using QuantConnect.Algorithm;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Reloj basado en QCAlgorithm.UtcTime. Siempre devuelve UTC real:
    /// en backtest es el tiempo simulado UTC; en live es el tiempo UTC del exchange.
    /// NOTA: _algorithm.Time devuelve la hora local del timezone del algoritmo (EDT en
    /// este deployment = UTC-4), lo que produce un offset de 4h contra DateTime.UtcNow
    /// y rompe el watchdog de staleness que compara ambas referencias.
    /// </summary>
    public sealed class LeanClock : IClock
    {
        private readonly QCAlgorithm _algorithm;

        public LeanClock(QCAlgorithm algorithm)
        {
            _algorithm = algorithm;
        }

        // _algorithm.UtcTime vale el epoch de QC (1997-12-31T19:00:00) durante Initialize(),
        // antes de que el motor inicialice su reloj interno. Fallback a DateTime.UtcNow
        // para que ProcessStartedUtc y los primeros logs del JSONL tengan timestamp real.
        private static readonly DateTime _qcEpochThreshold = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow
        {
            get
            {
                var t = _algorithm.UtcTime;
                return t >= _qcEpochThreshold ? t : DateTime.UtcNow;
            }
        }
    }
}
