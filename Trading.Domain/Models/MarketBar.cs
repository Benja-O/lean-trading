using System;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Models
{
    /// <summary>
    /// Barra de mercado consolidada (OHLCV) en un timeframe dado para un instrumento.
    /// Estructura del dominio, sin acoplamiento a ningún motor.
    /// </summary>
    public sealed class MarketBar
    {
        public InstrumentId InstrumentId { get; }
        public decimal Open { get; }
        public decimal High { get; }
        public decimal Low { get; }
        public decimal Close { get; }
        public decimal Volume { get; }
        public DateTime TimestampUtc { get; }

        /// <summary>
        /// Constructor primario con OHLCV completo. Es el que deben usar los productores
        /// de barras (adaptadores de motor, datos sintéticos para tests, parsers de archivos históricos).
        /// </summary>
        public MarketBar(
            InstrumentId instrumentId,
            decimal open,
            decimal high,
            decimal low,
            decimal close,
            decimal volume,
            DateTime timestampUtc)
        {
            InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            TimestampUtc = timestampUtc;
        }

        /// <summary>
        /// Constructor legado: cuando una barra solo expone close (por ejemplo, código antiguo
        /// que aún no migró a OHLCV). Inicializa Open/High/Low con el mismo close y Volume en 0.
        /// </summary>
        /// <remarks>
        /// Marcado [Obsolete] como guía de migración, no como error: el sistema sigue funcionando.
        /// Cuando se elimine, debe ser después de verificar que todos los productores pasaron a OHLCV.
        /// </remarks>
        [Obsolete("Usar el constructor con OHLCV completo. Este constructor existe para compatibilidad temporal hasta que todos los productores de barras pasen a OHLCV.")]
        public MarketBar(InstrumentId instrumentId, decimal close, DateTime timestampUtc)
            : this(instrumentId, close, close, close, close, 0m, timestampUtc)
        {
        }
    }
}
