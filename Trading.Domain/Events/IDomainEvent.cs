using System;

namespace Trading.Domain.Events
{
    /// <summary>
    /// Marker interface para eventos del dominio. Todos los eventos publicados al DomainEventBus
    /// implementan esta interfaz. Expone solo TimestampUtc, único campo común a todos los eventos
    /// (cuándo ocurrió la transición que el evento representa).
    /// </summary>
    public interface IDomainEvent
    {
        DateTime TimestampUtc { get; }
    }
}
