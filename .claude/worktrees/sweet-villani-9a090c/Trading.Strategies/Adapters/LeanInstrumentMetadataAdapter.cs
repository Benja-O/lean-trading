using QuantConnect.Algorithm;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Adapta QCAlgorithm.Securities[symbol].SymbolProperties al contrato IInstrumentMetadata.
    /// </summary>
    public sealed class LeanInstrumentMetadataAdapter : IInstrumentMetadata
    {
        private readonly QCAlgorithm _algorithm;
        private readonly LeanInstrumentResolver _instrumentResolver;

        public LeanInstrumentMetadataAdapter(QCAlgorithm algorithm, LeanInstrumentResolver instrumentResolver)
        {
            _algorithm = algorithm;
            _instrumentResolver = instrumentResolver;
        }

        public decimal GetLotSize(InstrumentId instrumentId)
        {
            var symbol = _instrumentResolver.Resolve(instrumentId);
            return _algorithm.Securities[symbol].SymbolProperties.LotSize;
        }

        public decimal GetMinimumPriceVariation(InstrumentId instrumentId)
        {
            var symbol = _instrumentResolver.Resolve(instrumentId);
            return _algorithm.Securities[symbol].SymbolProperties.MinimumPriceVariation;
        }

        public decimal? GetMinimumNotional(InstrumentId instrumentId)
        {
            var symbol = _instrumentResolver.Resolve(instrumentId);
            return _algorithm.Securities[symbol].SymbolProperties.MinimumOrderSize;
        }
    }
}
