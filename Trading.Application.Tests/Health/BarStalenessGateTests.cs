using System;
using FluentAssertions;
using Trading.Application.Eventing;
using Trading.Application.Health;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Events;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Health
{
    public class BarStalenessGateTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly TimeSpan Threshold = TimeSpan.FromMinutes(10);

        private static (BarStalenessGate gate, DomainEventBus bus, FakeClock clock)
            Build(DateTime startTime)
        {
            var clock = new FakeClock { UtcNow = startTime };
            var logger = new FakeTradingLogger();
            var bus = new DomainEventBus(logger);
            var tracker = new HealthHeartbeatTracker(bus, clock, logger);
            var gate = new BarStalenessGate(tracker, Threshold);
            return (gate, bus, clock);
        }

        private static readonly DateTime T0 = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        // ===== Estado inicial (warmup: ninguna barra procesada) =====

        [Fact]
        public void IsFresh_WhenNoBarProcessed_ReturnsTrueForWarmup()
        {
            var (gate, _, _) = Build(T0);
            gate.IsFresh(T0).Should().BeTrue("en warmup no hay barras aún pero el proceso está vivo");
        }

        [Fact]
        public void IsFresh_WhenNoBarProcessed_ReturnsTrueEvenMuchLater()
        {
            var (gate, _, _) = Build(T0);
            gate.IsFresh(T0.AddHours(1)).Should().BeTrue(
                "sin FirstBar nunca hay staleness; el warmup puede durar variablemente");
        }

        // ===== Operación normal =====

        [Fact]
        public void IsFresh_WhenLastBarJustProcessed_ReturnsTrue()
        {
            var (gate, bus, _) = Build(T0);
            bus.Publish(new BarProcessedEvent(T0, T0, Btc));

            gate.IsFresh(T0).Should().BeTrue();
        }

        [Fact]
        public void IsFresh_WhenLastBarWithinThreshold_ReturnsTrue()
        {
            var (gate, bus, _) = Build(T0);
            bus.Publish(new BarProcessedEvent(T0, T0, Btc));

            gate.IsFresh(T0.AddMinutes(9).AddSeconds(59)).Should().BeTrue(
                "9m59s < umbral de 10 min → fresco");
        }

        // ===== Límite del umbral =====

        [Fact]
        public void IsFresh_WhenLastBarAtExactThreshold_ReturnsFalse()
        {
            var (gate, bus, _) = Build(T0);
            bus.Publish(new BarProcessedEvent(T0, T0, Btc));

            gate.IsFresh(T0.AddMinutes(10)).Should().BeFalse(
                "exactamente en el umbral → stale (comparación estricta <)");
        }

        [Fact]
        public void IsFresh_WhenLastBarBeyondThreshold_ReturnsFalse()
        {
            var (gate, bus, _) = Build(T0);
            bus.Publish(new BarProcessedEvent(T0, T0, Btc));

            gate.IsFresh(T0.AddHours(1)).Should().BeFalse();
        }

        // ===== Recuperación tras stall =====

        [Fact]
        public void IsFresh_AfterStall_ReturnsFreshWhenNewBarArrives()
        {
            var (gate, bus, _) = Build(T0);
            bus.Publish(new BarProcessedEvent(T0, T0, Btc));

            // Stall: 30 minutos sin barras
            gate.IsFresh(T0.AddMinutes(30)).Should().BeFalse();

            // Nueva barra llega
            var resumeTime = T0.AddMinutes(30);
            bus.Publish(new BarProcessedEvent(resumeTime, resumeTime, Btc));

            gate.IsFresh(resumeTime.AddMinutes(1)).Should().BeTrue(
                "tras nueva barra el gate vuelve a fresco");
        }

        // ===== Umbral personalizado =====

        [Fact]
        public void IsFresh_RespectsCustomThreshold()
        {
            var clock = new FakeClock { UtcNow = T0 };
            var logger = new FakeTradingLogger();
            var bus = new DomainEventBus(logger);
            var tracker = new HealthHeartbeatTracker(bus, clock, logger);
            var shortGate = new BarStalenessGate(tracker, TimeSpan.FromMinutes(3));

            bus.Publish(new BarProcessedEvent(T0, T0, Btc));

            shortGate.IsFresh(T0.AddMinutes(2)).Should().BeTrue();
            shortGate.IsFresh(T0.AddMinutes(3)).Should().BeFalse(
                "umbral de 3 min: exactamente 3 min → stale");
        }
    }
}
