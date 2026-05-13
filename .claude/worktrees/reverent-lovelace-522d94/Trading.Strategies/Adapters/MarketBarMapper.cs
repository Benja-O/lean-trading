using QuantConnect.Data.Market;
using Trading.Domain.Models;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Traduce un QuantConnect.Data.Market.TradeBar a un MarketBar del dominio.
    /// Único punto donde el sistema convierte el modelo de barra de Lean al modelo del dominio.
    /// </summary>
    public static class MarketBarMapper
    {
        public static MarketBar ToMarketBar(TradeBar tradeBar, LeanInstrumentResolver instrumentResolver)
        {
            var instrumentId = instrumentResolver.Resolve(tradeBar.Symbol);
            return new MarketBar(
                instrumentId: instrumentId,
                close: tradeBar.Close,
                timestampUtc: tradeBar.EndTime);
        }
    }
}
