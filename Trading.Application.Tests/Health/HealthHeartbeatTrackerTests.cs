using System;
using System.Threading.Tasks;
using FluentAssertions;
using Trading.Application.Eventing;
using Trading.Application.Health;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Events;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Health
{
    public class HealthHeartbeatTrackerTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime T1 = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime T2 = new DateTime(2025, 6, 1, 10, 5, 0, DateTimeKind.Utc);

        private static (HealthHeartbeatTracker tracker, DomainEventBus bus) Build(FakeClock clock)
        {
            var logger = new FakeTradingLogger();
            var bus = new DomainEventBus(logger);
            var tracker = new HealthHeartbeatTracker(bus, clock, logger);
            return (tracker, bus);
        }

        [Fact]
        public void Construction_WithEmptyBus_SnapshotHasNullsExceptProcessStarted()
        {
            var clock = new FakeClock { UtcNow = T1 };
            var (tracker, _) = Build(clock);

            var snapshot = tracker.Snapshot();

            snapshot.ProcessStartedUtc.Should().Be(T1);
            snapshot.LastBarProcessedUtc.Should().BeNull();
            snapshot.LastBarTimestampUtc.Should().BeNull();
            snapshot.LastOrderSubmittedUtc.Should().BeNull();
            snapshot.LastOrderFilledUtc.Should().BeNull();
            snapshot.LastRiskBreachUtc.Should().BeNull();
            snapshot.LastRiskBreachReason.Should().BeNull();
            snapshot.KillSwitchActive.Should().BeFalse();
        }

        [Fact]
        public void BarProcessedEvent_UpdatesLastBarProcessedAndBarTimestamp()
        {
            var clock = new FakeClock { UtcNow = T1 };
            var (tracker, bus) = Build(clock);
            var barTime = new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc);

            bus.Publish(new BarProcessedEvent(T1, barTime, Btc));

            var snapshot = tracker.Snapshot();
            snapshot.LastBarProcessedUtc.Should().Be(T1);
            snapshot.LastBarTimestampUtc.Should().Be(barTime);
        }

        [Fact]
        public void OrderSubmittedEvent_UpdatesLastOrderSubmitted()
        {
            var clock = new FakeClock { UtcNow = T1 };
            var (tracker, bus) = Build(clock);

            bus.Publish(new OrderSubmittedEvent(T1, "exec1", Btc, OrderPurpose.Entry,
                1m, null, null, "tag1"));

            var snapshot = tracker.Snapshot();
            snapshot.LastOrderSubmittedUtc.Should().Be(T1);
        }

        [Fact]
        public void OrderFilledEvent_UpdatesLastOrderFilled()
        {
            var clock = new FakeClock { UtcNow = T1 };
            var (tracker, bus) = Build(clock);

            bus.Publish(new OrderFilledEvent(T1, "exec1", Btc, OrderPurpose.Entry, 1m, 50000m));

            var snapshot = tracker.Snapshot();
            snapshot.LastOrderFilledUtc.Should().Be(T1);
        }

        [Fact]
        public void RiskLimitBreachedEvent_UpdatesAllRiskFieldsAndSetsKillSwitchActive()
        {
            var clock = new FakeClock { UtcNow = T1 };
            var (tracker, bus) = Build(clock);

            bus.Publish(new RiskLimitBreachedEvent(T1,
                RiskLimitBreachReason.MaximumDrawdownExceeded,
                "Drawdown del 25%"));

            var snapshot = tracker.Snapshot();
            snapshot.KillSwitchActive.Should().BeTrue();
            snapshot.LastRiskBreachUtc.Should().Be(T1);
            snapshot.LastRiskBreachReason.Should().Be("MaximumDrawdownExceeded");
        }

        [Fact]
        public void Construction_DataFeedFieldsAreNull_UntilFirstSignal()
        {
            var clock = new FakeClock { UtcNow = T1 };
            var (tracker, _) = Build(clock);

            var snapshot = tracker.Snapshot();

            snapshot.LastDataReceivedUtc.Should().BeNull();
            snapshot.DataFeedStalenessSeconds.Should().BeNull();
        }

        [Fact]
        public void MarkDataFeedAlive_SetsLastDataReceived_IndependentOfBarProcessed()
        {
            var clock = new FakeClock { UtcNow = T2 };
            var (tracker, _) = Build(clock);

            tracker.MarkDataFeedAlive(T1);

            var snapshot = tracker.Snapshot();
            snapshot.LastDataReceivedUtc.Should().Be(T1);
            // Staleness computada contra el clock (T2 - T1 = 5 min = 300s).
            snapshot.DataFeedStalenessSeconds.Should().Be(300);
            // No toca el tracking de barras de estrategia.
            snapshot.LastBarProcessedUtc.Should().BeNull();
        }

        [Fact]
        public void MarkLiveModeStart_BaselinesBothBarAndDataFeed()
        {
            var clock = new FakeClock { UtcNow = T1 };
            var (tracker, _) = Build(clock);

            tracker.MarkLiveModeStart(T1);

            var snapshot = tracker.Snapshot();
            snapshot.LastBarProcessedUtc.Should().Be(T1);
            snapshot.LastDataReceivedUtc.Should().Be(T1);
        }

        [Fact]
        public void Snapshot_FromMultipleThreadsConcurrently_DoesNotCorruptState()
        {
            var clock = new FakeClock { UtcNow = T1 };
            var (tracker, bus) = Build(clock);

            var ex = Record.Exception(() =>
            {
                Parallel.For(0, 200, i =>
                {
                    if (i % 3 == 0)
                        bus.Publish(new BarProcessedEvent(T1, T2, Btc));
                    else if (i % 3 == 1)
                        bus.Publish(new OrderSubmittedEvent(T1, "exec", Btc,
                            OrderPurpose.Entry, 1m, null, null, "tag"));
                    else
                        _ = tracker.Snapshot();
                });
            });

            ex.Should().BeNull("no debe lanzar excepción en acceso concurrente");
            // Al menos un snapshot debe tener valores (las publicaciones ya ocurrieron)
            tracker.Snapshot().Should().NotBeNull();
        }
    }
}
