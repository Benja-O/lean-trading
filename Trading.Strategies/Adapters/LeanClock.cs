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

        public DateTime UtcNow => _algorithm.UtcTime;
    }
}
