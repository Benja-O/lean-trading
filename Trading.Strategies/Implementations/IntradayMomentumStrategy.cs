using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// Momentum intradía: la primera barra del día UTC predice la última.
    ///
    /// La señal se emite en la barra de 23:30-00:00 UTC (bar_47) basándose en
    /// la dirección de la barra de 00:00-00:30 UTC (bar_0) del mismo día.
    /// Hold: 1 barra (30 minutos) — la posición se cierra al finalizar bar_47.
    ///
    /// Diseño en dos pasos:
    ///   1. Bar_0 (00:00 UTC): registrar dirección, retornar Flat.
    ///   2. Bar_47 (23:30 UTC): emitir la dirección registrada si es del mismo día.
    ///
    /// Referencia: Shen, Urquhart & Wang — "Bitcoin intraday time-series momentum"
    /// (Financial Review 2022). Coeficiente 0.968, t-stat 4.38, datos 2013-2020.
    /// M4 validado en ETH (Sharpe +0.645) y BNB (Sharpe +0.691), 2020-2025.
    /// BTC excluido: el efecto se arbitró post-2021 con la entrada institucional.
    /// </summary>
    public sealed class IntradayMomentumStrategy : IStrategy
    {
        private sealed class SymbolState
        {
            public SignalDirection Bar0Direction { get; set; } = SignalDirection.Flat;
            public DateTime Bar0Date { get; set; } = DateTime.MinValue;
        }

        private readonly Dictionary<string, SymbolState> _stateBySymbol = new();

        public int WarmUpBars => 1;

        public SignalDirection EvaluateSignal(MarketBar bar)
        {
            var state = GetOrCreate(bar.InstrumentId.Ticker);
            var ts = bar.TimestampUtc;

            if (ts.Hour == 0 && ts.Minute == 0)
            {
                state.Bar0Date      = ts.Date;
                state.Bar0Direction = bar.Close > bar.Open ? SignalDirection.Long
                                    : bar.Close < bar.Open ? SignalDirection.Short
                                    : SignalDirection.Flat;
                return SignalDirection.Flat;
            }

            if (ts.Hour == 23 && ts.Minute == 30
                && state.Bar0Date == ts.Date
                && state.Bar0Direction != SignalDirection.Flat)
            {
                return state.Bar0Direction;
            }

            return SignalDirection.Flat;
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
