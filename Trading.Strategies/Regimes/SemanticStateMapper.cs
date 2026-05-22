using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions.Regimes;

namespace Trading.Strategies.Regimes
{
    /// <summary>
    /// Mapea estados crudos del HMM (índices 0..K-1) a <see cref="RegimeLabel"/> semánticos
    /// (Trend, MeanReverting, HighVolatility, Squeeze).
    ///
    /// Diseño deliberadamente simple. El cálculo del mapeo se hace UNA sola vez OFFLINE
    /// durante el entrenamiento (vía <see cref="Build"/>) y se persiste junto al modelo.
    /// En runtime, el mapper se reconstruye desde el diccionario serializado y se usa para
    /// traducir cada estado decodificado por Viterbi.
    ///
    /// Permite que dos estados crudos mapeen a la misma etiqueta: el classifier suma las
    /// probabilidades de esos estados antes de devolver la distribución.
    /// </summary>
    public sealed class SemanticStateMapper
    {
        private readonly IReadOnlyDictionary<int, RegimeLabel> _stateToLabel;

        public IReadOnlyDictionary<int, RegimeLabel> StateToLabel => _stateToLabel;

        public SemanticStateMapper(IReadOnlyDictionary<int, RegimeLabel> stateToLabel)
        {
            if (stateToLabel is null) throw new ArgumentNullException(nameof(stateToLabel));
            if (stateToLabel.Count == 0)
                throw new ArgumentException(
                    "SemanticStateMapper no admite mapeo vacío: el modelo debe tener al menos un estado.",
                    nameof(stateToLabel));
            _stateToLabel = stateToLabel;
        }

        public RegimeLabel MapState(int stateIndex)
        {
            if (!_stateToLabel.TryGetValue(stateIndex, out var label))
                throw new ArgumentOutOfRangeException(
                    nameof(stateIndex),
                    $"El estado {stateIndex} no está mapeado a ningún RegimeLabel. " +
                    "Mapeos conocidos: " + string.Join(", ", _stateToLabel.Keys.OrderBy(k => k)));
            return label;
        }

        /// <summary>
        /// Estadísticas por estado calculadas sobre el training set (Viterbi).
        /// </summary>
        /// <param name="StateIndex">Índice del estado crudo del HMM (0..K-1).</param>
        /// <param name="MeanReturn">Media de retornos cuando el HMM está en este estado (μᵢ).</param>
        /// <param name="StdDevReturn">Desvío estándar de retornos del estado (σᵢ).</param>
        /// <param name="SelfTransitionProbability">Probabilidad de auto-transición (A[i, i]) = ρᵢ.</param>
        public readonly record struct StateStatistics(
            int StateIndex,
            double MeanReturn,
            double StdDevReturn,
            double SelfTransitionProbability);

        /// <summary>
        /// Construye un SemanticStateMapper a partir de las estadísticas por estado.
        ///
        /// Reglas adaptativas según el número de estados K:
        /// - K=2: estado de σ mayor → HighVolatility candidato; el otro evalúa Trend/Squeeze/MeanReverting.
        /// - K=3: tercios — última posición ordenada por σ → HighVolatility, primera posición (si ρ > 0.7) → Squeeze.
        /// - K>=4: cuartiles tradicionales — top cuartil HighVolatility, bottom cuartil (con ρ > 0.7) Squeeze.
        ///
        /// Reglas comunes a todos los K:
        /// - Estados no clasificados como HighVolatility ni Squeeze evalúan |μ| > 0.001 y ρ > 0.6 → Trend.
        /// - Resto → MeanReverting.
        /// - Caso degenerado (ningún estado HighVolatility ni Trend): forzar HighVolatility al de σ máxima.
        ///
        /// La adaptación por K es necesaria porque Math.Ceiling(K * 0.75) con K=3 produce 3, lo cual deja la
        /// condición "positionInSorted >= 3" insatisfacible en un array de 3 elementos (ver ADR-024).
        /// </summary>
        public static SemanticStateMapper Build(IReadOnlyList<StateStatistics> statistics)
        {
            if (statistics is null) throw new ArgumentNullException(nameof(statistics));
            if (statistics.Count == 0)
                throw new ArgumentException("Se requieren estadísticas para al menos un estado.", nameof(statistics));

            var sortedBySigma = statistics
                .Select((stat, index) => (stat, index))
                .OrderBy(tuple => tuple.stat.StdDevReturn)
                .ToList();

            int stateCount = sortedBySigma.Count;

            // ADAPTATIVO A K:
            //   K=2: el estado de σ mayor es HighVolatility candidato, el otro evalúa Trend/MeanReverting/Squeeze por su μ y ρ.
            //   K=3: tercios — el último tercio es HighVolatility, el primer tercio (si ρ > 0.7) es Squeeze.
            //   K>=4: cuartiles tradicionales — top cuartil HighVolatility, bottom cuartil (con ρ > 0.7) Squeeze.
            int topThreshold;
            int bottomThreshold;
            if (stateCount == 2)
            {
                topThreshold = 1;
                bottomThreshold = 1;
            }
            else if (stateCount == 3)
            {
                topThreshold = 2;
                bottomThreshold = 1;
            }
            else
            {
                topThreshold = (int)Math.Ceiling(stateCount * 0.75);
                bottomThreshold = (int)Math.Floor(stateCount * 0.25);
            }

            var highVolatilityStateIndices = new HashSet<int>();
            var squeezeStateIndices = new HashSet<int>();

            for (int positionInSorted = 0; positionInSorted < stateCount; positionInSorted++)
            {
                var (stat, _) = sortedBySigma[positionInSorted];
                bool isTopBracket = positionInSorted >= topThreshold;
                bool isBottomBracket = positionInSorted < bottomThreshold;

                if (isTopBracket)
                    highVolatilityStateIndices.Add(stat.StateIndex);
                else if (isBottomBracket && stat.SelfTransitionProbability > 0.7)
                    squeezeStateIndices.Add(stat.StateIndex);
            }

            var mapping = new Dictionary<int, RegimeLabel>();
            bool anyTrend = false;
            foreach (var stat in statistics)
            {
                if (highVolatilityStateIndices.Contains(stat.StateIndex))
                {
                    mapping[stat.StateIndex] = RegimeLabel.HighVolatility;
                }
                else if (squeezeStateIndices.Contains(stat.StateIndex))
                {
                    mapping[stat.StateIndex] = RegimeLabel.Squeeze;
                }
                else if (Math.Abs(stat.MeanReturn) > 0.001 && stat.SelfTransitionProbability > 0.6)
                {
                    mapping[stat.StateIndex] = RegimeLabel.Trend;
                    anyTrend = true;
                }
                else
                {
                    mapping[stat.StateIndex] = RegimeLabel.MeanReverting;
                }
            }

            // Caso degenerado: si no quedó ningún estado HighVolatility ni Trend, forzar al menos uno.
            bool anyHighVol = highVolatilityStateIndices.Count > 0;
            if (!anyTrend && !anyHighVol)
            {
                var maxSigmaState = statistics.OrderByDescending(stat => stat.StdDevReturn).First();
                mapping[maxSigmaState.StateIndex] = RegimeLabel.HighVolatility;
            }

            return new SemanticStateMapper(mapping);
        }
    }
}
