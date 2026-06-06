using QuantConnect.Indicators;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// Estrategia de cruce de medias móviles exponenciales (EMA 30 vs EMA 60).
    /// Emite Long en cruce alcista, Short en cruce bajista, Flat el resto del tiempo.
    /// </summary>
    public class EmaCrossStrategy : IStrategy
    {
        private class SymbolState
        {
            public ExponentialMovingAverage EmaFast { get; } = new(30);
            public ExponentialMovingAverage EmaSlow { get; } = new(60);
            public int PreviousSignal { get; set; } = 0;
        }

        private readonly Dictionary<string, SymbolState> _stateBySymbol = new();

        // EMA lenta: 60 períodos. Es el indicador más lento de esta estrategia.
        public int WarmUpBars => 60;

        public SignalDirection EvaluateSignal(MarketBar marketBar)
        {
            string ticker = marketBar.InstrumentId.Ticker;

            if (!_stateBySymbol.TryGetValue(ticker, out var state))
            {
                state = new SymbolState();
                _stateBySymbol[ticker] = state;
            }

            state.EmaFast.Update(marketBar.TimestampUtc, marketBar.Close);
            state.EmaSlow.Update(marketBar.TimestampUtc, marketBar.Close);

            if (!state.EmaFast.IsReady || !state.EmaSlow.IsReady)
                return SignalDirection.Flat;

            int currentSignal = state.EmaFast > state.EmaSlow ? 1 : -1;

            if (state.PreviousSignal == 0)
            {
                state.PreviousSignal = currentSignal;
                return SignalDirection.Flat;
            }

            bool isLongCross = state.PreviousSignal < 0 && currentSignal > 0;
            bool isShortCross = state.PreviousSignal > 0 && currentSignal < 0;
            state.PreviousSignal = currentSignal;

            if (isLongCross) return SignalDirection.Long;
            if (isShortCross) return SignalDirection.Short;
            return SignalDirection.Flat;
        }
    }
}
