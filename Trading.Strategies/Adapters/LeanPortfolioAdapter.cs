using QuantConnect.Algorithm;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Adapta QCAlgorithm.Portfolio al contrato IPortfolioState del dominio.
    /// </summary>
    public sealed class LeanPortfolioAdapter : IPortfolioState
    {
        private readonly QCAlgorithm _algorithm;
        private readonly LeanInstrumentResolver _instrumentResolver;

        public LeanPortfolioAdapter(QCAlgorithm algorithm, LeanInstrumentResolver instrumentResolver)
        {
            _algorithm = algorithm;
            _instrumentResolver = instrumentResolver;
        }

        public decimal TotalPortfolioValue => _algorithm.Portfolio.TotalPortfolioValue;

        public bool IsInvested(InstrumentId instrumentId)
        {
            var symbol = _instrumentResolver.Resolve(instrumentId);
            return _algorithm.Portfolio[symbol].Invested;
        }

        public decimal GetPositionQuantity(InstrumentId instrumentId)
        {
            var symbol = _instrumentResolver.Resolve(instrumentId);
            return _algorithm.Portfolio[symbol].Quantity;
        }
    }
}
