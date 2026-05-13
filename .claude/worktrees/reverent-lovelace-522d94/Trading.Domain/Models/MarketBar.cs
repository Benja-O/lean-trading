using System;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Models
{
    /// <summary>
    /// Barra de mercado (OHLC consolidada) en un timeframe dado para un instrumento.
    /// Reemplaza al antiguo MarketData. Estructura del dominio, sin acoplamiento a ningún motor.
    /// 
    /// Por ahora expone solo Close y Time porque es lo único que las estrategias actuales consumen.
    /// Cuando una estrategia requiera Open/High/Low/Volume se agregan acá; el adaptador (MarketBarMapper)
    /// ya los tiene disponibles desde el TradeBar de Lean.
    /// </summary>
    public sealed class MarketBar
    {
        public InstrumentId InstrumentId { get; }
        public decimal Close { get; }
        public DateTime TimestampUtc { get; }

        public MarketBar(InstrumentId instrumentId, decimal close, DateTime timestampUtc)
        {
            InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
            Close = close;
            TimestampUtc = timestampUtc;
        }
    }
}
