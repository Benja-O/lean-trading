using System.Collections.Generic;
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
        // Min notional (quote currency) de los futures Binance USDT-M que operamos. El SPDB de
        // Lean no trae minimum_order_size para cryptofuture (devuelve null) y Data/ está gitignored
        // (no se versiona ni se deploya por git), así que el override vive acá, en la capa adapter
        // (infra), versionado y deployable con el build. Ver ADR-045.
        private static readonly IReadOnlyDictionary<string, decimal> _binanceFuturesMinNotional =
            new Dictionary<string, decimal>
            {
                ["BTCUSDT"] = 100m,
                ["ETHUSDT"] = 20m,
                ["SOLUSDT"] = 5m,
            };

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
            var fromProperties = _algorithm.Securities[symbol].SymbolProperties.MinimumOrderSize;
            if (fromProperties.HasValue)
                return fromProperties;

            // Fallback al override versionado cuando el SPDB no declara min notional (futures Binance).
            return _binanceFuturesMinNotional.TryGetValue(instrumentId.Ticker, out var min)
                ? min
                : (decimal?)null;
        }
    }
}
