using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// H10 — Selling Climax (Long-only).
    /// Hipótesis: cuando el número de trades (ArrivalRate) es extremo (P95 de las últimas
    /// ArrivalRateWindow barras) Y el precio cae más de PriceReturnThreshold en la misma barra,
    /// es un clímax de venta: los vendedores se agotan y el precio rebota.
    /// Parámetros nominales M4: window=48, pct=0.95, ret<-0.008, hold=4.
    /// </summary>
    public sealed class SellingClimaxStrategy : IStrategy
    {
        private const int ArrivalRateWindow     = 48;
        private const double ArrivalRatePct     = 0.95;
        private const double PriceReturnThr     = -0.008;

        private readonly IMicrostructureProvider _microstructure;
        private readonly Dictionary<string, Queue<double>> _historyBySymbol = new();

        public int WarmUpBars => ArrivalRateWindow + 2;

        public SellingClimaxStrategy(IMicrostructureProvider microstructure)
        {
            _microstructure = microstructure;
        }

        public SignalDirection EvaluateSignal(MarketBar marketBar)
        {
            var msBar = _microstructure?.GetBar(marketBar.InstrumentId, marketBar.TimestampUtc);
            if (msBar == null) return SignalDirection.Flat;

            string ticker = marketBar.InstrumentId.Ticker;
            if (!_historyBySymbol.TryGetValue(ticker, out var history))
            {
                history = new Queue<double>(ArrivalRateWindow + 1);
                _historyBySymbol[ticker] = history;
            }

            double arrival = msBar.ArrivalRate;
            double priceReturn = msBar.PriceReturn;

            bool isWarmedUp = history.Count >= ArrivalRateWindow;
            double pct = isWarmedUp ? PercentileRank(history, arrival) : 0.0;

            if (history.Count >= ArrivalRateWindow)
                history.Dequeue();
            history.Enqueue(arrival);

            if (!isWarmedUp) return SignalDirection.Flat;

            if (pct >= ArrivalRatePct && priceReturn < PriceReturnThr)
                return SignalDirection.Long;

            return SignalDirection.Flat;
        }

        private static double PercentileRank(Queue<double> history, double value)
        {
            int below = 0;
            foreach (var v in history)
                if (v < value) below++;
            return (double)below / history.Count;
        }
    }
}
