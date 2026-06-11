using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// H2 — Trade Count Spike (Long-only).
    /// Hipótesis: un spike de actividad (ArrivalRate extremo) sin movimiento de precio grande
    /// indica absorción institucional → el precio rebota.
    /// Parámetros nominales M4: spike_w=48, spike_pct=0.95, max_ret=0.005, hold=6 (2/3 activos).
    /// Marginal: BTC Sharpe=-0.669 en la config nominal; continúa a QC IS para validación adicional.
    /// </summary>
    public sealed class TradeCountSpikeStrategy : IStrategy
    {
        private const int    SpikeWindow  = 48;
        private const double SpikePct     = 0.95;
        private const double MaxAbsReturn = 0.005;

        private readonly IMicrostructureProvider _microstructure;
        private readonly Dictionary<string, Queue<double>> _historyBySymbol = new();

        public int WarmUpBars => SpikeWindow + 2;

        public TradeCountSpikeStrategy(IMicrostructureProvider microstructure)
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
                history = new Queue<double>(SpikeWindow + 1);
                _historyBySymbol[ticker] = history;
            }

            double arrival     = msBar.ArrivalRate;
            double priceReturn = msBar.PriceReturn;

            bool isWarmedUp = history.Count >= SpikeWindow;
            double pct = isWarmedUp ? PercentileRank(history, arrival) : 0.0;

            if (history.Count >= SpikeWindow) history.Dequeue();
            history.Enqueue(arrival);

            if (!isWarmedUp) return SignalDirection.Flat;

            if (pct >= SpikePct && System.Math.Abs(priceReturn) < MaxAbsReturn)
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
