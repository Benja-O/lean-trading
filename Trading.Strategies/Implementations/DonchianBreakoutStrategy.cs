using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// Breakout de canal de Donchian (20 períodos).
    ///
    /// Señal: Long cuando el cierre supera el máximo de las 20 barras anteriores;
    /// Short cuando cae por debajo del mínimo. Se emite únicamente en la barra de
    /// ruptura — las barras subsiguientes dentro del mismo estado no generan nueva señal.
    ///
    /// Diseño lookahead-free: el canal se calcula sobre las N barras YA cerradas
    /// antes de la barra actual (la barra actual entra a la ventana DESPUÉS de evaluar).
    ///
    /// Referencia: Li et al. (arXiv 2512.02227, 2025) — breakout de rango con
    /// señales condicionadas por régimen. Usar con CompatibleRegimes: ["Trend"].
    /// </summary>
    public sealed class DonchianBreakoutStrategy : IStrategy
    {
        private const int LookbackPeriods = 20;

        private sealed class SymbolState
        {
            private readonly Queue<(decimal High, decimal Low)> _window = new(LookbackPeriods + 1);
            private SignalDirection _previousState = SignalDirection.Flat;

            public bool IsReady => _window.Count >= LookbackPeriods;

            public SignalDirection Evaluate(MarketBar bar)
            {
                SignalDirection result = SignalDirection.Flat;

                if (IsReady)
                {
                    decimal channelHigh = decimal.MinValue;
                    decimal channelLow  = decimal.MaxValue;
                    foreach (var (high, low) in _window)
                    {
                        if (high > channelHigh) channelHigh = high;
                        if (low  < channelLow)  channelLow  = low;
                    }

                    SignalDirection currentState = SignalDirection.Flat;
                    if (bar.Close > channelHigh)     currentState = SignalDirection.Long;
                    else if (bar.Close < channelLow) currentState = SignalDirection.Short;

                    bool isNewBreakout =
                        currentState != SignalDirection.Flat &&
                        currentState != _previousState;

                    if (isNewBreakout) result = currentState;
                    _previousState = currentState;
                }

                _window.Enqueue((bar.High, bar.Low));
                if (_window.Count > LookbackPeriods) _window.Dequeue();

                return result;
            }
        }

        private readonly Dictionary<string, SymbolState> _stateBySymbol = new();

        public int WarmUpBars => LookbackPeriods;

        public SignalDirection EvaluateSignal(MarketBar bar)
        {
            string ticker = bar.InstrumentId.Ticker;
            if (!_stateBySymbol.TryGetValue(ticker, out var state))
            {
                state = new SymbolState();
                _stateBySymbol[ticker] = state;
            }
            return state.Evaluate(bar);
        }
    }
}
