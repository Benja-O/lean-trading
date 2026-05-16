using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions.Regimes;

namespace Trading.Application.Regimes
{
    /// <summary>
    /// Define qué regímenes son compatibles con una estrategia específica. Construido al boot
    /// a partir de StrategyDefinition.CompatibleRegimes (strings) tras parseo a RegimeLabel.
    ///
    /// Semántica de IsCompatibleWith:
    /// - Si AllowedRegimes es null/vacío: la estrategia es compatible con todos los regímenes
    ///   (fail-safe: ausencia de configuración no filtra nada).
    /// - Si AllowedRegimes tiene valores: la estrategia es compatible solo con esos regímenes.
    /// - RegimeLabel.Unknown siempre devuelve true (si no sabemos el régimen, no filtramos).
    /// </summary>
    public sealed class StrategyRegimeCompatibility
    {
        private readonly IReadOnlySet<RegimeLabel> _allowedRegimes;

        /// <summary>Identificador del executor (StrategyExecutor.ExecutorIdentifier) al que aplica esta compatibilidad.</summary>
        public string ExecutorIdentifier { get; }

        public StrategyRegimeCompatibility(string executorIdentifier, IReadOnlySet<RegimeLabel>? allowedRegimes)
        {
            if (string.IsNullOrWhiteSpace(executorIdentifier))
                throw new ArgumentException("ExecutorIdentifier no puede ser nulo ni vacío.", nameof(executorIdentifier));

            ExecutorIdentifier = executorIdentifier;
            // null y vacío se tratan igual: "compatible con todos". Internamente normalizamos a HashSet vacío.
            _allowedRegimes = allowedRegimes ?? new HashSet<RegimeLabel>();
        }

        /// <summary>True si la estrategia puede operar en el régimen dado.</summary>
        public bool IsCompatibleWith(RegimeLabel regime)
        {
            if (regime == RegimeLabel.Unknown) return true;       // fail-safe
            if (_allowedRegimes.Count == 0) return true;          // no configurado = compatible con todos
            return _allowedRegimes.Contains(regime);
        }
    }
}
