using System.Collections.Generic;
using Trading.Domain.Abstractions;

namespace Trading.Application.Tests.Fakes
{
    /// <summary>
    /// Fake del monitor de salud por estrategia. Por defecto no excluye nada.
    /// Tests pueden agregar identifiers al HashSet ExcludedIdentifiers para forzar
    /// la exclusión de estrategias específicas.
    /// </summary>
    internal sealed class FakeStrategyHealthMonitor : IStrategyHealthMonitor
    {
        public HashSet<string> ExcludedIdentifiers { get; } = new();

        public bool IsExcluded(string executorIdentifier)
            => ExcludedIdentifiers.Contains(executorIdentifier);
    }
}
