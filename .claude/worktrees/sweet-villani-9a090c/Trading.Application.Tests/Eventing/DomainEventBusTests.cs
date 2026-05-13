using System;
using System.Collections.Generic;
using Trading.Application.Eventing;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Events;
using Xunit;

namespace Trading.Application.Tests.Eventing
{
    /// <summary>
    /// Tests del DomainEventBus. Cubre suscripción, publicación, manejo de fallos en suscriptores,
    /// orden de invocación, y ausencia de suscriptores.
    /// </summary>
    public class DomainEventBusTests
    {
        private readonly FakeTradingLogger _logger = new();

        [Fact]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            var eventBus = new DomainEventBus(_logger);
            var domainEvent = new RiskLimitBreachedEvent(
                DateTime.UtcNow, RiskLimitBreachReason.Manual, "test");

            // No debe lanzar.
            eventBus.Publish(domainEvent);
        }

        [Fact]
        public void Subscribe_ThenPublish_InvokesCallback()
        {
            var eventBus = new DomainEventBus(_logger);
            var capturedEvents = new List<RiskLimitBreachedEvent>();
            eventBus.Subscribe<RiskLimitBreachedEvent>(domainEvent => capturedEvents.Add(domainEvent));

            var publishedEvent = new RiskLimitBreachedEvent(
                DateTime.UtcNow, RiskLimitBreachReason.ConsecutiveLossesExceeded, "test");
            eventBus.Publish(publishedEvent);

            Assert.Single(capturedEvents);
            Assert.Equal(publishedEvent, capturedEvents[0]);
        }

        [Fact]
        public void Publish_InvokesAllSubscribersInOrder()
        {
            var eventBus = new DomainEventBus(_logger);
            var invocationOrder = new List<int>();
            eventBus.Subscribe<RiskLimitBreachedEvent>(_ => invocationOrder.Add(1));
            eventBus.Subscribe<RiskLimitBreachedEvent>(_ => invocationOrder.Add(2));
            eventBus.Subscribe<RiskLimitBreachedEvent>(_ => invocationOrder.Add(3));

            eventBus.Publish(new RiskLimitBreachedEvent(DateTime.UtcNow, RiskLimitBreachReason.Manual, ""));

            Assert.Equal(new[] { 1, 2, 3 }, invocationOrder);
        }

        [Fact]
        public void Publish_WhenSubscriberThrows_LogsErrorAndContinuesWithNextSubscribers()
        {
            var eventBus = new DomainEventBus(_logger);
            var secondSubscriberInvoked = false;
            eventBus.Subscribe<RiskLimitBreachedEvent>(_ => throw new InvalidOperationException("boom"));
            eventBus.Subscribe<RiskLimitBreachedEvent>(_ => secondSubscriberInvoked = true);

            eventBus.Publish(new RiskLimitBreachedEvent(DateTime.UtcNow, RiskLimitBreachReason.Manual, ""));

            Assert.True(secondSubscriberInvoked);
            // El bus debe haber logueado el error del primer suscriptor.
            Assert.NotEmpty(_logger.ErrorEntries);
        }

        [Fact]
        public void Subscribe_DifferentEventTypes_AreIndependent()
        {
            var eventBus = new DomainEventBus(_logger);
            int riskCount = 0;
            int filledCount = 0;
            eventBus.Subscribe<RiskLimitBreachedEvent>(_ => riskCount++);
            eventBus.Subscribe<OrderFilledEvent>(_ => filledCount++);

            eventBus.Publish(new RiskLimitBreachedEvent(DateTime.UtcNow, RiskLimitBreachReason.Manual, ""));

            Assert.Equal(1, riskCount);
            Assert.Equal(0, filledCount);
        }

        [Fact]
        public void Subscribe_WithNullCallback_Throws()
        {
            var eventBus = new DomainEventBus(_logger);
            Assert.Throws<ArgumentNullException>(() =>
                eventBus.Subscribe<RiskLimitBreachedEvent>(null));
        }

        [Fact]
        public void Publish_WithNullEvent_Throws()
        {
            var eventBus = new DomainEventBus(_logger);
            Assert.Throws<ArgumentNullException>(() =>
                eventBus.Publish<RiskLimitBreachedEvent>(null));
        }
    }
}
