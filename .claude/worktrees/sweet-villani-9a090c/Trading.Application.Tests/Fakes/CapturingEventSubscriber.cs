using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Tests.Fakes
{
    /// <summary>
    /// Suscriptor de tests que captura todos los eventos publicados de un tipo dado.
    /// Se suscribe al bus en el constructor; los tests inspeccionan la propiedad CapturedEvents
    /// para verificar emisiones.
    /// </summary>
    public class CapturingEventSubscriber<TEvent> where TEvent : IDomainEvent
    {
        public List<TEvent> CapturedEvents { get; } = new();

        public CapturingEventSubscriber(IDomainEventBus eventBus)
        {
            eventBus.Subscribe<TEvent>(capturedEvent => CapturedEvents.Add(capturedEvent));
        }
    }
}
