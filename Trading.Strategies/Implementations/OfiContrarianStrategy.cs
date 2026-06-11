using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// OFI Contrarian Strategy — Long-only, compra cuando vendedores se agotan.
    ///
    /// Señal: cuando el OFI del bar actual está en el percentil inferior de su distribución
    /// reciente (presión vendedora extrema), el precio tiende a rebotar en las próximas horas.
    /// Esto captura mean reversion post-exhaustion-of-sellers, validado en M4 IS 2021-2024:
    /// BTC +0.87, ETH +1.48, SOL +1.37 (window=24, threshold=0.85, hold=8h).
    ///
    /// Long-only: el lado Short (vender cuando compradores dominan) no tiene edge estable
    /// en mercados cripto estructuralmente alcistas — detectado en análisis direccional M4.
    /// </summary>
    public sealed class OfiContrarianStrategy : IStrategy
    {
        private readonly IMicrostructureProvider _microstructureProvider;
        private readonly int _lookbackBars;
        private readonly double _lowPercentileThreshold;

        private readonly Dictionary<string, Queue<double>> _ofiWindowBySymbol = new();

        public int WarmUpBars => _lookbackBars;

        public OfiContrarianStrategy(
            IMicrostructureProvider microstructureProvider,
            int lookbackBars = 24,
            double lowPercentileThreshold = 0.15)
        {
            _microstructureProvider = microstructureProvider
                ?? throw new ArgumentNullException(nameof(microstructureProvider));
            _lookbackBars = lookbackBars;
            _lowPercentileThreshold = lowPercentileThreshold;
        }

        public SignalDirection EvaluateSignal(MarketBar marketBar)
        {
            var microBar = _microstructureProvider.GetBar(
                marketBar.InstrumentId,
                marketBar.TimestampUtc.AddHours(-1));

            if (microBar == null) return SignalDirection.Flat;

            var ticker = marketBar.InstrumentId.Ticker;
            if (!_ofiWindowBySymbol.TryGetValue(ticker, out var window))
            {
                window = new Queue<double>();
                _ofiWindowBySymbol[ticker] = window;
            }

            window.Enqueue(microBar.Ofi);
            if (window.Count > _lookbackBars)
                window.Dequeue();

            if (window.Count < _lookbackBars)
                return SignalDirection.Flat;

            var arr = window.ToArray();
            var currentOfi = arr[^1];
            var history    = arr[..^1];   // previous lookback-1 values

            if (history.Length == 0) return SignalDirection.Flat;

            var lessThanCount = history.Count(x => x < currentOfi);
            var percentile    = lessThanCount / (double)history.Length;

            return percentile < _lowPercentileThreshold ? SignalDirection.Long : SignalDirection.Flat;
        }
    }
}
