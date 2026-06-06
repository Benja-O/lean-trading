using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Tests.Fakes
{
    public class FakeStrategy : IStrategy
    {
        public int WarmUpBars => 0;
        public SignalDirection EvaluateSignal(MarketBar marketBar) => SignalDirection.Flat;
    }
}
