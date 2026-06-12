using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// H5 — Trade Size Institutional (Long-only).
    /// Hipótesis: cuando el tamaño medio de trade está en el percentil superior
    /// (flujo institucional) Y el ratio buy/sell es positivo (acumulación neta),
    /// hay acumulación institucional silenciosa → precio sube.
    /// Parámetros nominales M4: size_w=24, pct=0.90, bsr>1.02, hold=8 (3/3 activos).
    /// </summary>
    public sealed class TradeSizeInstitutionalStrategy : IStrategy
    {
        private const int SizeWindow               = 24;
        private const double SizePct               = 0.90;
        private const double BuySellRatioThreshold = 1.02;

        private readonly IMicrostructureProvider? _microstructure;
        private readonly Dictionary<string, Queue<double>> _historyBySymbol = new();

        public int WarmUpBars => SizeWindow + 2;

        public TradeSizeInstitutionalStrategy(IMicrostructureProvider? microstructure = null)
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
                history = new Queue<double>(SizeWindow + 1);
                _historyBySymbol[ticker] = history;
            }

            double meanSize   = msBar.MeanTradeSize;
            double buySellRat = msBar.BuySellRatio;

            bool isWarmedUp = history.Count >= SizeWindow;
            double pct = isWarmedUp ? PercentileRank(history, meanSize) : 0.0;

            if (history.Count >= SizeWindow)
                history.Dequeue();
            history.Enqueue(meanSize);

            if (!isWarmedUp) return SignalDirection.Flat;

            if (pct >= SizePct && buySellRat > BuySellRatioThreshold)
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
