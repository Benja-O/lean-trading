using System;
using QuantConnect.Algorithm;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Reloj basado en QCAlgorithm.Time. En backtest devuelve tiempo simulado;
    /// en live devuelve tiempo real del exchange.
    /// </summary>
    public sealed class LeanClock : IClock
    {
        private readonly QCAlgorithm _algorithm;

        public LeanClock(QCAlgorithm algorithm)
        {
            _algorithm = algorithm;
        }

        public DateTime UtcNow => _algorithm.Time;
    }
}
