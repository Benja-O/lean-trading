using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Tests.Fakes
{
    public class FakeInstrumentMetadata : IInstrumentMetadata
    {
        private readonly Dictionary<string, decimal> _lotSizes = new();
        private readonly Dictionary<string, decimal> _minimumPriceVariations = new();
        private readonly Dictionary<string, decimal?> _minimumNotionals = new();

        public decimal GetLotSize(InstrumentId instrumentId) =>
            _lotSizes.TryGetValue(instrumentId.Ticker, out var lotSize) ? lotSize : 0.001m;

        public decimal GetMinimumPriceVariation(InstrumentId instrumentId) =>
            _minimumPriceVariations.TryGetValue(instrumentId.Ticker, out var step) ? step : 0.01m;

        public decimal? GetMinimumNotional(InstrumentId instrumentId) =>
            _minimumNotionals.TryGetValue(instrumentId.Ticker, out var min) ? min : null;

        public void SetLotSize(InstrumentId instrumentId, decimal lotSize) =>
            _lotSizes[instrumentId.Ticker] = lotSize;

        public void SetMinimumPriceVariation(InstrumentId instrumentId, decimal step) =>
            _minimumPriceVariations[instrumentId.Ticker] = step;

        public void SetMinimumNotional(InstrumentId instrumentId, decimal? minimum) =>
            _minimumNotionals[instrumentId.Ticker] = minimum;
    }
}
