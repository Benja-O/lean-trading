using System;
using Trading.Domain.Events;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Bus de eventos de dominio. Síncrono, sin librerías externas.
    ///
    /// Modelo de publicación-suscripción:
    /// - Suscriptores registran callbacks por tipo de evento vía Subscribe&lt;TEvent&gt;.
    /// - Publicadores llaman Publish&lt;TEvent&gt;; el bus invoca a TODOS los suscriptores de
    ///   ese tipo en orden de suscripción, en el mismo thread, antes de retornar.
    /// - Si un suscriptor lanza excepción, el bus la captura y loguea; continúa con los siguientes.
    ///   Una métrica fallida no debe interrumpir el flujo de trading.
    /// </summary>
    public interface IDomainEventBus
    {
        /// <summary>
        /// Registra un callback que se ejecutará cada vez que se publique un evento del tipo TEvent.
        /// </summary>
        void Subscribe<TEvent>(Action<TEvent> subscriberCallback) where TEvent : IDomainEvent;

        /// <summary>
        /// Publica un evento. Invoca a todos los suscriptores registrados para TEvent, en orden,
        /// en el mismo thread. Retorna cuando todos los suscriptores terminaron (o fallaron).
        /// </summary>
        void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
    }
}
