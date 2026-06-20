using System.Threading;
using System.Threading.Tasks;

namespace Trading.Recorder
{
    /// <summary>
    /// Callback invocado por un <see cref="ITradeFeed"/> por cada aggTrade recibido.
    /// Firma estable del recorder: NO cambia entre adapters (WebSocket o REST).
    /// </summary>
    public delegate void TradeHandler(string symbol, decimal price, decimal qty, bool isBuyerMaker, long tradeTimeMs);

    /// <summary>
    /// Fuente de aggTrades del recorder. Abstrae el transporte (WebSocket vs REST polling)
    /// detrás de un único contrato: el caller llama <see cref="RunAsync"/> y recibe los trades
    /// vía el <see cref="TradeHandler"/> inyectado al construir el adapter.
    ///
    /// Implementaciones:
    ///   - <c>BinanceAggTradeWebSocketClient</c>: stream WS (válido en redes donde fstream entrega push).
    ///   - <c>BinanceAggTradeRestFeed</c>: polling REST por fromId (default en producción — el WS de
    ///     Binance Futures no entrega push a las redes del proyecto; ver ADR-049).
    /// </summary>
    public interface ITradeFeed
    {
        /// <summary>
        /// Corre el feed hasta que se cancela el token. Invoca el <see cref="TradeHandler"/>
        /// sincrónicamente por cada trade, en orden cronológico por símbolo.
        /// </summary>
        Task RunAsync(CancellationToken cancellationToken);
    }
}
