using System;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Tests.Fakes
{
    public class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public void Advance(TimeSpan amount) => UtcNow += amount;
    }
}
