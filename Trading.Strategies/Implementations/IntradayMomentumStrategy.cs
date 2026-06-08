using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// Momentum intradía: la primera barra del día UTC predice la última.
    ///
    /// Señal: Long si la barra de 00:00-00:30 UTC cierra arriba (close > open);
    /// Short si cierra abajo. Todas las demás barras del día retornan Flat.
    ///
    /// Referencia: Shen, Urquhart & Wang — "Bitcoin intraday time-series momentum"
    /// (Financial Review 2022). Coeficiente 0.968, t-stat 4.38, datos 2013-2020.
    /// Validado con M4 en ETH (Sharpe +0.645) y BNB (Sharpe +0.691), período 2020-2025.
    /// BTC excluido: el efecto se arbitró post-2021 con la entrada institucional.
    ///
    /// Sin estado por símbolo — la lógica depende únicamente del timestamp y OHLCV
    /// de la barra actual.
    /// </summary>
    public sealed class IntradayMomentumStrategy : IStrategy
    {
        public int WarmUpBars => 1;

        public SignalDirection EvaluateSignal(MarketBar bar)
        {
            if (bar.TimestampUtc.Hour != 0 || bar.TimestampUtc.Minute != 0)
                return SignalDirection.Flat;

            if (bar.Close > bar.Open) return SignalDirection.Long;
            if (bar.Close < bar.Open) return SignalDirection.Short;
            return SignalDirection.Flat;
        }
    }
}
