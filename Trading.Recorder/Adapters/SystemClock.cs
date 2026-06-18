using System;
using Trading.Domain.Abstractions;

namespace Trading.Recorder.Adapters
{
    internal sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
