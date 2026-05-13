using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trading.Application.Execution;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Execution
{
    public class OrderRegistryTests
    {
        private readonly InstrumentId _btcUsdt = new("BTCUSDT");
        private readonly InstrumentId _ethUsdt = new("ETHUSDT");

        [Fact]
        public void Register_ReturnsTagWithCorrectPrefix()
        {
            var registry = new OrderRegistry();

            string tag = registry.Register(OrderPurpose.Entry, "EmaCross_BTCUSDT_5m", _btcUsdt);

            Assert.StartsWith("ord_", tag);
            Assert.Equal(12, tag.Length);
        }

        [Fact]
        public void Resolve_ReturnsRegistrationWithSameValues()
        {
            var registry = new OrderRegistry();
            string executorIdentifier = "EmaCross_BTCUSDT_5m";

            string tag = registry.Register(OrderPurpose.StopLoss, executorIdentifier, _btcUsdt);
            var registration = registry.Resolve(tag);

            Assert.NotNull(registration);
            Assert.Equal(OrderPurpose.StopLoss, registration.Purpose);
            Assert.Equal(executorIdentifier, registration.ExecutorIdentifier);
            Assert.Equal(_btcUsdt, registration.InstrumentId);
            Assert.Equal(tag, registration.ClientTag);
        }

        [Fact]
        public void Resolve_UnknownTag_ReturnsNull()
        {
            var registry = new OrderRegistry();

            var registration = registry.Resolve("ord_doesnotexist");

            Assert.Null(registration);
        }

        [Fact]
        public void Resolve_NullOrEmpty_ReturnsNull()
        {
            var registry = new OrderRegistry();

            Assert.Null(registry.Resolve(null));
            Assert.Null(registry.Resolve(string.Empty));
        }

        [Fact]
        public void Forget_RemovesRegistration()
        {
            var registry = new OrderRegistry();
            string tag = registry.Register(OrderPurpose.Entry, "exec1", _btcUsdt);

            registry.Forget(tag);

            Assert.Null(registry.Resolve(tag));
            Assert.Equal(0, registry.LiveOrderCount);
        }

        [Fact]
        public void Forget_IsIdempotent()
        {
            var registry = new OrderRegistry();
            string tag = registry.Register(OrderPurpose.Entry, "exec1", _btcUsdt);

            registry.Forget(tag);
            registry.Forget(tag);
            registry.Forget("ord_neverexisted");

            Assert.Equal(0, registry.LiveOrderCount);
        }

        [Fact]
        public void Register_Multiple_ProducesUniqueTags()
        {
            var registry = new OrderRegistry();

            string tag1 = registry.Register(OrderPurpose.Entry, "exec1", _btcUsdt);
            string tag2 = registry.Register(OrderPurpose.Entry, "exec2", _ethUsdt);
            string tag3 = registry.Register(OrderPurpose.StopLoss, "exec1", _btcUsdt);

            Assert.NotEqual(tag1, tag2);
            Assert.NotEqual(tag1, tag3);
            Assert.NotEqual(tag2, tag3);
            Assert.Equal(3, registry.LiveOrderCount);
        }

        [Fact]
        public void LiveOrderCount_ReflectsRegistersAndForgets()
        {
            var registry = new OrderRegistry();

            string tag1 = registry.Register(OrderPurpose.Entry, "exec1", _btcUsdt);
            string tag2 = registry.Register(OrderPurpose.StopLoss, "exec1", _btcUsdt);
            Assert.Equal(2, registry.LiveOrderCount);

            registry.Forget(tag1);
            Assert.Equal(1, registry.LiveOrderCount);

            registry.Forget(tag2);
            Assert.Equal(0, registry.LiveOrderCount);
        }

        [Fact]
        public async Task Register_Concurrent_AllSucceedWithoutCollision()
        {
            var registry = new OrderRegistry();
            var generatedTags = new ConcurrentBag<string>();
            const int concurrentRegistrations = 1000;

            var tasks = new Task[concurrentRegistrations];
            for (int taskIndex = 0; taskIndex < concurrentRegistrations; taskIndex++)
            {
                int localIndex = taskIndex;
                tasks[taskIndex] = Task.Run(() =>
                {
                    string tag = registry.Register(
                        OrderPurpose.Entry, $"exec_{localIndex}", _btcUsdt);
                    generatedTags.Add(tag);
                });
            }

            await Task.WhenAll(tasks);

            Assert.Equal(concurrentRegistrations, generatedTags.Count);
            var uniqueTags = new HashSet<string>(generatedTags);
            Assert.Equal(concurrentRegistrations, uniqueTags.Count);
            Assert.Equal(concurrentRegistrations, registry.LiveOrderCount);
        }
    }
}
