using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Eventing
{
    /// <summary>
    /// Implementación síncrona del bus de eventos de dominio.
    ///
    /// Thread-safety: las operaciones se sincronizan vía lock interno. Necesario porque
    /// en live trading callbacks de fills pueden invocar Publish desde threads distintos
    /// del thread principal de evaluación de barras.
    ///
    /// Manejo de fallos en suscriptores: si un callback lanza, se loguea Error con el tipo
    /// de evento y el detalle de la excepción, y se continúa con el siguiente suscriptor.
    /// Esto preserva la propiedad de que el flujo de trading NO se rompe por una métrica mal escrita.
    /// </summary>
    public sealed class DomainEventBus : IDomainEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribersByEventType = new();
        private readonly object _synchronizationLock = new();
        private readonly ITradingLogger _logger;

        public DomainEventBus(ITradingLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Subscribe<TEvent>(Action<TEvent> subscriberCallback) where TEvent : IDomainEvent
        {
            if (subscriberCallback == null) throw new ArgumentNullException(nameof(subscriberCallback));

            lock (_synchronizationLock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribersByEventType.TryGetValue(eventType, out var subscribers))
                {
                    subscribers = new List<Delegate>();
                    _subscribersByEventType[eventType] = subscribers;
                }
                subscribers.Add(subscriberCallback);
            }
        }

        public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
        {
            if (domainEvent == null) throw new ArgumentNullException(nameof(domainEvent));

            // Tomamos snapshot bajo lock y ejecutamos fuera para no retener el lock durante callbacks.
            List<Delegate> subscribersSnapshot;
            lock (_synchronizationLock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribersByEventType.TryGetValue(eventType, out var subscribers) || subscribers.Count == 0)
                {
                    return;
                }
                subscribersSnapshot = new List<Delegate>(subscribers);
            }

            foreach (var subscriberDelegate in subscribersSnapshot)
            {
                try
                {
                    ((Action<TEvent>)subscriberDelegate).Invoke(domainEvent);
                }
                catch (Exception subscriberException)
                {
                    _logger.Error(
                        "DomainEventBus: suscriptor de {EventType} lanzó excepción. Detalle: {Detail}. El bus continúa con los siguientes suscriptores.",
                        typeof(TEvent).Name, subscriberException.ToString());
                }
            }
        }
    }
}
