using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Tests.Fakes
{
    /// <summary>
    /// Fake controlable de IPortfolioState para tests de dominio.
    /// </summary>
    public class FakePortfolioState : IPortfolioState
    {
        public decimal TotalPortfolioValue { get; set; }

        private readonly HashSet<string> _investedTickers = new();

        public bool IsInvested(InstrumentId instrumentId) =>
            _investedTickers.Contains(instrumentId.Ticker);

        public void SetInvested(InstrumentId instrumentId, bool invested)
        {
            if (invested) _investedTickers.Add(instrumentId.Ticker);
            else _investedTickers.Remove(instrumentId.Ticker);
        }

        private readonly Dictionary<string, decimal> _quantities = new();

        public decimal GetPositionQuantity(InstrumentId instrumentId) =>
            _quantities.TryGetValue(instrumentId.Ticker, out var q) ? q : 0m;

        public void SetPositionQuantity(InstrumentId instrumentId, decimal quantity)
        {
            _quantities[instrumentId.Ticker] = quantity;
            SetInvested(instrumentId, quantity != 0m);
        }
    }
}
