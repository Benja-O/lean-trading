using QuantConnect;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// ATR Compression Breakout: entra en la dirección del rompimiento de rango
    /// después de un período de compresión de volatilidad.
    ///
    /// Compresión: ATR(14) &lt; percentil 20 de las últimas 100 lecturas del ATR.
    /// Rompimiento Long:  Close &gt; máximo de los 10 cierres anteriores.
    /// Rompimiento Short: Close &lt; mínimo de los 10 cierres anteriores.
    ///
    /// El exit se delega al motor vía MaxBars (time exit en BarProcessingService).
    ///
    /// Validado en M4 (Binance 4h 2020-2025): 6/9 configs BTC, 5/9 ETH, 7/9 BNB.
    /// Configuración nominal: ATR&lt;P20, lookback=10, hold=3 barras.
    /// </summary>
    public class AtrCompressionBreakoutStrategy : IStrategy, IAtrProvider
    {
        private class SymbolState
        {
            public AverageTrueRange Atr { get; } = new(14);
            // Historial de valores ATR para calcular el percentil de compresión.
            public RollingWindow<decimal> AtrHistory { get; } = new(100);
            // Historial de cierres ANTERIORES a la barra actual; se actualiza DESPUÉS de evaluar la señal.
            public RollingWindow<decimal> PriceHistory { get; } = new(10);
        }

        private const int _compressionPercentile = 20;
        private readonly Dictionary<string, SymbolState> _stateBySymbol = new();

        // ATR necesita 14 barras para IsReady; luego el historial del ATR necesita 100 valores más.
        public int WarmUpBars => 114;

        public SignalDirection EvaluateSignal(MarketBar marketBar)
        {
            string ticker = marketBar.InstrumentId.Ticker;
            if (!_stateBySymbol.TryGetValue(ticker, out var state))
            {
                state = new SymbolState();
                _stateBySymbol[ticker] = state;
            }

            state.Atr.Update(new TradeBar(
                marketBar.TimestampUtc,
                Symbol.Empty,
                marketBar.Open,
                marketBar.High,
                marketBar.Low,
                marketBar.Close,
                0m));

            if (state.Atr.IsReady)
                state.AtrHistory.Add((decimal)state.Atr.Current.Value);

            // Durante el calentamiento, alimentar el historial de precios y retornar Flat.
            if (!state.AtrHistory.IsReady || !state.PriceHistory.IsReady)
            {
                state.PriceHistory.Add(marketBar.Close);
                return SignalDirection.Flat;
            }

            // Compresión: ATR actual < percentil 20 del historial de 100 barras.
            decimal threshold = ComputePercentile(state.AtrHistory, _compressionPercentile);
            if ((decimal)state.Atr.Current.Value >= threshold)
            {
                state.PriceHistory.Add(marketBar.Close);
                return SignalDirection.Flat;
            }

            // Rompimiento: cierre actual vs rango de los N cierres anteriores (PriceHistory
            // aún NO contiene el cierre de esta barra — se actualiza al final del método).
            decimal rangeHigh = Enumerable.Range(0, state.PriceHistory.Size).Max(i => state.PriceHistory[i]);
            decimal rangeLow  = Enumerable.Range(0, state.PriceHistory.Size).Min(i => state.PriceHistory[i]);
            decimal currentClose = marketBar.Close;

            SignalDirection signal = SignalDirection.Flat;
            if (currentClose > rangeHigh)
                signal = SignalDirection.Long;
            else if (currentClose < rangeLow)
                signal = SignalDirection.Short;

            state.PriceHistory.Add(currentClose);
            return signal;
        }

        public decimal GetLastAtr(string ticker)
        {
            if (!_stateBySymbol.TryGetValue(ticker, out var state) || !state.Atr.IsReady)
                return 0m;
            return (decimal)state.Atr.Current.Value;
        }

        private static decimal ComputePercentile(RollingWindow<decimal> window, int percentile)
        {
            var values = Enumerable.Range(0, window.Size)
                .Select(i => window[i])
                .OrderBy(v => v)
                .ToArray();

            // Método nearest-rank.
            double rank = (double)percentile / 100.0 * values.Length;
            int index = Math.Max(0, (int)Math.Ceiling(rank) - 1);
            return values[Math.Min(index, values.Length - 1)];
        }
    }
}
