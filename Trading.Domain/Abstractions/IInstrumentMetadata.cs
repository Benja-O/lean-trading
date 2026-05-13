using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Metadatos de un instrumento que afectan el cálculo de tamaño y precio.
    /// Provienen del exchange/venue. La implementación Lean los obtiene de SymbolProperties.
    /// 
    /// El sizer y el rounder de precios consumen esta abstracción para no depender de Securities[].
    /// </summary>
    public interface IInstrumentMetadata
    {
        /// <summary>
        /// Tamaño mínimo de lote del instrumento (granularidad de cantidad).
        /// Equivale a Lean's SymbolProperties.LotSize.
        /// </summary>
        decimal GetLotSize(InstrumentId instrumentId);

        /// <summary>
        /// Variación mínima de precio (tick size).
        /// Equivale a Lean's SymbolProperties.MinimumPriceVariation.
        /// </summary>
        decimal GetMinimumPriceVariation(InstrumentId instrumentId);

        /// <summary>
        /// Notional mínimo aceptado por el exchange para una orden.
        /// Equivale a Lean's SymbolProperties.MinimumOrderSize. Puede ser null si el exchange no lo declara.
        /// </summary>
        decimal? GetMinimumNotional(InstrumentId instrumentId);
    }
}
