using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Tests.Fakes
{
    public class FakeOrderRouter : IOrderRouter
    {
        public List<LiquidateInstrumentRecord> LiquidateInstrumentCalls { get; } = new();
        public List<SubmittedOrderRecord> SubmittedOrders { get; } = new();

        private readonly Dictionary<string, bool> _hasOpenOrdersByTicker = new();

        public IOrderHandle SubmitMarketOrder(
            InstrumentId instrumentId, decimal quantity, OrderPurpose purpose, string executorIdentifier)
        {
            SubmittedOrders.Add(new SubmittedOrderRecord(
                "Market", instrumentId, quantity, 0m, purpose, executorIdentifier));
            return new FakeOrderHandle();
        }

        public IOrderHandle SubmitStopMarketOrder(
            InstrumentId instrumentId, decimal quantity, decimal stopPrice,
            OrderPurpose purpose, string executorIdentifier)
        {
            SubmittedOrders.Add(new SubmittedOrderRecord(
                "StopMarket", instrumentId, quantity, stopPrice, purpose, executorIdentifier));
            return new FakeOrderHandle();
        }

        public IOrderHandle SubmitLimitOrder(
            InstrumentId instrumentId, decimal quantity, decimal limitPrice,
            OrderPurpose purpose, string executorIdentifier)
        {
            SubmittedOrders.Add(new SubmittedOrderRecord(
                "Limit", instrumentId, quantity, limitPrice, purpose, executorIdentifier));
            return new FakeOrderHandle();
        }

        public void LiquidateInstrument(
            InstrumentId instrumentId, OrderPurpose purpose, string executorIdentifier)
        {
            LiquidateInstrumentCalls.Add(new LiquidateInstrumentRecord(
                instrumentId, purpose, executorIdentifier));
        }

        public bool HasOpenOrders(InstrumentId instrumentId) =>
            _hasOpenOrdersByTicker.TryGetValue(instrumentId.Ticker, out var hasOpen) && hasOpen;

        public void SetHasOpenOrders(InstrumentId instrumentId, bool hasOpen)
        {
            _hasOpenOrdersByTicker[instrumentId.Ticker] = hasOpen;
        }
    }

    public sealed record SubmittedOrderRecord(
        string OrderType,
        InstrumentId InstrumentId,
        decimal Quantity,
        decimal Price,
        OrderPurpose Purpose,
        string ExecutorIdentifier);

    public sealed record LiquidateInstrumentRecord(
        InstrumentId InstrumentId,
        OrderPurpose Purpose,
        string ExecutorIdentifier);

    public sealed class FakeOrderHandle : IOrderHandle
    {
        public int CancelCallCount { get; private set; }
        public string LastCancelReason { get; private set; }

        public void Cancel(string reason)
        {
            CancelCallCount++;
            LastCancelReason = reason;
        }
    }
}
