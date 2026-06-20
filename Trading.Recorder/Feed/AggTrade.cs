namespace Trading.Recorder.Feed
{
    /// <summary>
    /// Aggregate trade de Binance Futures tal como lo devuelve el endpoint REST
    /// <c>/fapi/v1/aggTrades</c>. Equivalente en contenido al frame del stream WebSocket
    /// (<c>a</c>=aggId, <c>p</c>=price, <c>q</c>=quantity, <c>m</c>=isBuyerMaker, <c>T</c>=time).
    /// </summary>
    public readonly record struct AggTrade(
        long AggregateTradeId,
        decimal Price,
        decimal Quantity,
        bool IsBuyerMaker,
        long TradeTimeMs);
}
