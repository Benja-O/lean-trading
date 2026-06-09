using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// Bollinger Bands %b strategy — señal pura para M4 validation.
    ///
    /// Entrada:
    /// - %b < 0.0 (oversold) por N barras consecutivas de 4h
    /// - ADX(10) > 30 (tendencia confirmada)
    /// - Próxima barra con Close <= SignalPrice × (1 - EntryLimitPercent)
    ///
    /// Salida:
    /// - Responsabilidad de infraestructura (SL/TP/MaxBars en strategies.json)
    /// - EvaluateSignal no señaliza exit; solo devuelve Long/Flat
    ///
    /// Referencia: Connors Research — "Bollinger Bands Trading Strategies That Work"
    /// M4 validation: testear %b en oversold durante N=1,2,4 barras.
    /// </summary>
    public sealed class BollingerBandsStrategy : IStrategy
    {
        private const int BBPeriod = 5;
        private const decimal BBStdDev = 1m;
        private const int ADXPeriod = 10;
        private const decimal ADXThreshold = 30m;
        private const int OversoldBars = 4;
        private const decimal OversoldLevel = 0m;
        private const decimal EntryLimitPercent = 0.10m; // 10%

        private sealed class SymbolState
        {
            public Queue<decimal> Closes { get; } = new();
            public Queue<decimal> Highs { get; } = new();
            public Queue<decimal> Lows { get; } = new();
            public int OversoldCount { get; set; } = 0;
            public decimal SignalPrice { get; set; } = 0;
        }

        private readonly Dictionary<string, SymbolState> _stateBySymbol = new();

        public int WarmUpBars => Math.Max(BBPeriod + ADXPeriod, OversoldBars) + 5;

        public SignalDirection EvaluateSignal(MarketBar bar)
        {
            var state = GetOrCreate(bar.InstrumentId.Ticker);

            UpdateHistorical(state, bar);

            // Necesitamos mínimo BBPeriod + ADXPeriod barras
            if (state.Closes.Count < BBPeriod + ADXPeriod)
                return SignalDirection.Flat;

            var bb = CalculateBollingerBands(state.Closes.ToList());
            var pb = CalculatePercentB(bar.Close, bb);
            var adx = CalculateADX(state.Highs.ToList(), state.Lows.ToList(), state.Closes.ToList());

            // Detectar oversold: %b < 0.0
            if (pb < OversoldLevel && adx > ADXThreshold)
            {
                state.OversoldCount++;
            }
            else
            {
                state.OversoldCount = 0;
            }

            // Oversold confirmado por N barras
            if (state.OversoldCount >= OversoldBars)
            {
                state.SignalPrice = bar.Close;
                state.OversoldCount = 0; // Reset para no re-entrar
            }

            // Entrada: próxima barra que cae EntryLimitPercent del precio de señal
            if (state.SignalPrice > 0 && bar.Close <= state.SignalPrice * (1 - EntryLimitPercent))
            {
                state.SignalPrice = 0;
                return SignalDirection.Long;
            }

            return SignalDirection.Flat;
        }

        private void UpdateHistorical(SymbolState state, MarketBar bar)
        {
            state.Closes.Enqueue(bar.Close);
            state.Highs.Enqueue(bar.High);
            state.Lows.Enqueue(bar.Low);

            // Mantener máximo ~50 barras para no crecer indefinidamente
            int maxHistorical = BBPeriod + ADXPeriod + 20;
            while (state.Closes.Count > maxHistorical)
            {
                state.Closes.Dequeue();
                state.Highs.Dequeue();
                state.Lows.Dequeue();
            }
        }

        private (decimal Upper, decimal Lower, decimal Middle) CalculateBollingerBands(List<decimal> closes)
        {
            if (closes.Count < BBPeriod)
                return (0, 0, 0);

            var recent = closes.TakeLast(BBPeriod).ToList();
            var sma = recent.Average();
            var variance = recent.Sum(x => (x - sma) * (x - sma)) / BBPeriod;
            var stdDev = (decimal)Math.Sqrt((double)variance);

            var upper = sma + BBStdDev * stdDev;
            var lower = sma - BBStdDev * stdDev;

            return (upper, lower, sma);
        }

        private decimal CalculatePercentB(decimal price, (decimal Upper, decimal Lower, decimal Middle) bb)
        {
            if (bb.Upper == bb.Lower)
                return 0m;

            return (price - bb.Lower) / (bb.Upper - bb.Lower);
        }

        private decimal CalculateADX(List<decimal> highs, List<decimal> lows, List<decimal> closes)
        {
            if (highs.Count < ADXPeriod + 1)
                return 0m;

            var recent = highs.TakeLast(ADXPeriod + 1).ToList();
            var recentLows = lows.TakeLast(ADXPeriod + 1).ToList();

            // Simplificación: calcular True Range y DM básico
            decimal sumTR = 0, sumPlusDM = 0, sumMinusDM = 0;

            for (int i = 1; i < recent.Count; i++)
            {
                var tr = Math.Max(recent[i] - recentLows[i],
                            Math.Max(
                                Math.Abs(recent[i] - closes[closes.Count - (ADXPeriod + 1 - i)]),
                                Math.Abs(recentLows[i] - closes[closes.Count - (ADXPeriod + 1 - i)])
                            ));
                sumTR += tr;

                var plusDM = recent[i] > recent[i - 1] ? recent[i] - recent[i - 1] : 0m;
                var minusDM = recentLows[i] < recentLows[i - 1] ? recentLows[i - 1] - recentLows[i] : 0m;

                sumPlusDM += plusDM;
                sumMinusDM += minusDM;
            }

            if (sumTR == 0)
                return 0m;

            var plusDI = (sumPlusDM / sumTR) * 100;
            var minusDI = (sumMinusDM / sumTR) * 100;
            var di = Math.Abs(plusDI - minusDI) / (plusDI + minusDI);

            return di * 100;
        }

        private SymbolState GetOrCreate(string ticker)
        {
            if (!_stateBySymbol.TryGetValue(ticker, out var state))
            {
                state = new SymbolState();
                _stateBySymbol[ticker] = state;
            }
            return state;
        }
    }
}
