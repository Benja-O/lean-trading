using System;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Sizing
{
    /// <summary>
    /// Implementación concreta de IPriceRounder. Redondea precios al tick size del instrumento.
    /// Vive en Application porque es lógica genérica que opera sobre la abstracción IInstrumentMetadata.
    /// </summary>
    public sealed class PriceRounder : IPriceRounder
    {
        private readonly IInstrumentMetadata _instrumentMetadata;

        public PriceRounder(IInstrumentMetadata instrumentMetadata)
        {
            _instrumentMetadata = instrumentMetadata ?? throw new ArgumentNullException(nameof(instrumentMetadata));
        }

        public decimal Round(InstrumentId instrumentId, decimal price)
        {
            decimal priceStep = _instrumentMetadata.GetMinimumPriceVariation(instrumentId);
            return priceStep == 0m ? price : Math.Round(price / priceStep) * priceStep;
        }
    }
}
