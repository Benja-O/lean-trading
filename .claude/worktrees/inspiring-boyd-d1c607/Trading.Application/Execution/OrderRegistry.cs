using System;
using System.Collections.Generic;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Execution
{
    public sealed class OrderRegistration
    {
        public string ClientTag { get; }
        public OrderPurpose Purpose { get; }
        public string ExecutorIdentifier { get; }
        public InstrumentId InstrumentId { get; }

        public OrderRegistration(
            string clientTag,
            OrderPurpose purpose,
            string executorIdentifier,
            InstrumentId instrumentId)
        {
            ClientTag = clientTag;
            Purpose = purpose;
            ExecutorIdentifier = executorIdentifier;
            InstrumentId = instrumentId;
        }
    }

    public class OrderRegistry
    {
        private const string TagPrefix = "ord_";
        private const int OpaqueIdentifierLength = 8;

        private readonly Dictionary<string, OrderRegistration> _registrationsByTag = new();
        private readonly object _synchronizationLock = new();

        public string Register(OrderPurpose purpose, string executorIdentifier, InstrumentId instrumentId)
        {
            if (string.IsNullOrEmpty(executorIdentifier))
                throw new ArgumentException("executorIdentifier no puede ser nulo o vacío.", nameof(executorIdentifier));
            if (instrumentId == null)
                throw new ArgumentNullException(nameof(instrumentId));

            lock (_synchronizationLock)
            {
                string clientTag = GenerateUniqueTag();
                var registration = new OrderRegistration(clientTag, purpose, executorIdentifier, instrumentId);
                _registrationsByTag[clientTag] = registration;
                return clientTag;
            }
        }

        public OrderRegistration Resolve(string clientTag)
        {
            if (string.IsNullOrEmpty(clientTag)) return null;

            lock (_synchronizationLock)
            {
                return _registrationsByTag.TryGetValue(clientTag, out var registration)
                    ? registration
                    : null;
            }
        }

        public void Forget(string clientTag)
        {
            if (string.IsNullOrEmpty(clientTag)) return;

            lock (_synchronizationLock)
            {
                _registrationsByTag.Remove(clientTag);
            }
        }

        public int LiveOrderCount
        {
            get
            {
                lock (_synchronizationLock)
                {
                    return _registrationsByTag.Count;
                }
            }
        }

        private string GenerateUniqueTag()
        {
            // Bajo lock (caller). Loop defensivo contra colisión de GUID (probabilidad astronómica).
            while (true)
            {
                string opaqueIdentifier = Guid.NewGuid().ToString("N").Substring(0, OpaqueIdentifierLength);
                string candidateTag = TagPrefix + opaqueIdentifier;
                if (!_registrationsByTag.ContainsKey(candidateTag))
                {
                    return candidateTag;
                }
            }
        }
    }
}
