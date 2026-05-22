using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Trading.Domain.Abstractions.Regimes;
using Trading.Strategies.Regimes;
using Xunit;

namespace Trading.Strategies.Tests.Regimes
{
    /// <summary>
    /// Tests del mapeo determinista estado → RegimeLabel. Las reglas (en orden) son:
    /// 1. σᵢ en cuartil superior → HighVolatility.
    /// 2. σᵢ en cuartil inferior y ρᵢ > 0.7 → Squeeze.
    /// 3. |μᵢ| > 0.001 y ρᵢ > 0.6 → Trend.
    /// 4. resto → MeanReverting.
    /// </summary>
    public class SemanticStateMapperTests
    {
        [Fact]
        public void Build_EstadoConSigmaEnCuartilSuperior_MapeaAHighVolatility()
        {
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0, StdDevReturn: 0.001, SelfTransitionProbability: 0.8),
                new(1, MeanReturn: 0.0, StdDevReturn: 0.005, SelfTransitionProbability: 0.5),
                new(2, MeanReturn: 0.0, StdDevReturn: 0.010, SelfTransitionProbability: 0.5),
                new(3, MeanReturn: 0.0, StdDevReturn: 0.050, SelfTransitionProbability: 0.5)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            mapper.MapState(3).Should().Be(RegimeLabel.HighVolatility);
        }

        [Fact]
        public void Build_EstadoConSigmaCuartilInferiorYAltaPersistencia_MapeaASqueeze()
        {
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0, StdDevReturn: 0.001, SelfTransitionProbability: 0.8),
                new(1, MeanReturn: 0.0, StdDevReturn: 0.005, SelfTransitionProbability: 0.5),
                new(2, MeanReturn: 0.0, StdDevReturn: 0.010, SelfTransitionProbability: 0.5),
                new(3, MeanReturn: 0.0, StdDevReturn: 0.050, SelfTransitionProbability: 0.5)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            mapper.MapState(0).Should().Be(RegimeLabel.Squeeze);
        }

        [Fact]
        public void Build_EstadoConMediaSignificativaYAltaPersistencia_MapeaATrend()
        {
            // 4 estados: state 1 tiene μ=0.002 y ρ=0.7 → debe ser Trend (no entra en cuartil superior ni inferior).
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0,   StdDevReturn: 0.001, SelfTransitionProbability: 0.8),
                new(1, MeanReturn: 0.002, StdDevReturn: 0.005, SelfTransitionProbability: 0.7),
                new(2, MeanReturn: 0.0,   StdDevReturn: 0.010, SelfTransitionProbability: 0.5),
                new(3, MeanReturn: 0.0,   StdDevReturn: 0.050, SelfTransitionProbability: 0.5)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            mapper.MapState(1).Should().Be(RegimeLabel.Trend);
        }

        [Fact]
        public void Build_EstadoConMediaCercaDeCeroYPersistenciaMedia_MapeaAMeanReverting()
        {
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0,    StdDevReturn: 0.001, SelfTransitionProbability: 0.8),
                new(1, MeanReturn: 0.0001, StdDevReturn: 0.005, SelfTransitionProbability: 0.5),
                new(2, MeanReturn: 0.0,    StdDevReturn: 0.010, SelfTransitionProbability: 0.5),
                new(3, MeanReturn: 0.0,    StdDevReturn: 0.050, SelfTransitionProbability: 0.5)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            mapper.MapState(1).Should().Be(RegimeLabel.MeanReverting);
            mapper.MapState(2).Should().Be(RegimeLabel.MeanReverting);
        }

        [Fact]
        public void Build_CasoDegeneradoConTodosLosEstadosSimilares_FuerzaAlMenosUnNoMeanReverting()
        {
            // Todos con σ similar y baja persistencia: sin la regla degenerada, todos quedarían MeanReverting.
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0, StdDevReturn: 0.0050, SelfTransitionProbability: 0.30),
                new(1, MeanReturn: 0.0, StdDevReturn: 0.0051, SelfTransitionProbability: 0.31),
                new(2, MeanReturn: 0.0, StdDevReturn: 0.0052, SelfTransitionProbability: 0.32)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            var labels = mapper.StateToLabel.Values.ToHashSet();
            labels.Should().Contain(label => label != RegimeLabel.MeanReverting,
                because: "el caso degenerado debe forzar al menos un estado a HighVolatility (o Trend) para que el sistema no quede sin regímenes de alta señal");
        }

        // ---- Tests adaptativos a K (ADR-024) ----

        [Fact]
        public void Build_ConKIgualA2_AsignaHighVolatilityAlEstadoDeSigmaMayor()
        {
            // K=2: el de σ mayor debe quedar HighVolatility.
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0, StdDevReturn: 0.003, SelfTransitionProbability: 0.5),
                new(1, MeanReturn: 0.0, StdDevReturn: 0.020, SelfTransitionProbability: 0.5)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            mapper.MapState(1).Should().Be(RegimeLabel.HighVolatility,
                because: "con K=2 el estado de σ mayor (estado 1) debe mapearse a HighVolatility");
        }

        [Fact]
        public void Build_ConKIgualA3_AsignaHighVolatilityAlUltimoTercio()
        {
            // K=3: este era el caso del bug (Ceiling(3*0.75)=3 → nadie era HighVolatility con la lógica antigua).
            // Con el fix, el estado de σ máxima (posición 2 en sorted) debe ser HighVolatility.
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0015, StdDevReturn: 0.005, SelfTransitionProbability: 0.7),
                new(1, MeanReturn: 0.0,    StdDevReturn: 0.003, SelfTransitionProbability: 0.8),
                new(2, MeanReturn: 0.0,    StdDevReturn: 0.025, SelfTransitionProbability: 0.5)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            mapper.MapState(2).Should().Be(RegimeLabel.HighVolatility,
                because: "con K=3 el estado de σ mayor (estado 2) debe mapearse a HighVolatility (era el bug del cuartil)");
        }

        [Fact]
        public void Build_ConKIgualA3_AsignaSqueezeAlPrimerTercioSiCumpleRho()
        {
            // K=3: el estado de σ mínima (posición 0 sorted) con ρ > 0.7 debe ser Squeeze.
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0,    StdDevReturn: 0.001, SelfTransitionProbability: 0.85),
                new(1, MeanReturn: 0.0015, StdDevReturn: 0.005, SelfTransitionProbability: 0.65),
                new(2, MeanReturn: 0.0,    StdDevReturn: 0.020, SelfTransitionProbability: 0.50)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            mapper.MapState(0).Should().Be(RegimeLabel.Squeeze,
                because: "con K=3, el estado de σ mínima (estado 0) con ρ=0.85 debe mapearse a Squeeze (primer tercio)");
            mapper.MapState(2).Should().Be(RegimeLabel.HighVolatility,
                because: "con K=3, el estado de σ máxima (estado 2) debe mapearse a HighVolatility (último tercio)");
        }

        [Fact]
        public void Build_ConKIgualA4_AplicaCuartilesTradicionales()
        {
            // K=4: cuartiles estándar. topThreshold = Ceiling(4*0.75) = 3 → posición 3 es HighVolatility.
            //       bottomThreshold = Floor(4*0.25) = 1 → posición 0 es bottom; si ρ > 0.7, Squeeze.
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0, StdDevReturn: 0.001, SelfTransitionProbability: 0.80),
                new(1, MeanReturn: 0.0, StdDevReturn: 0.005, SelfTransitionProbability: 0.50),
                new(2, MeanReturn: 0.0, StdDevReturn: 0.010, SelfTransitionProbability: 0.50),
                new(3, MeanReturn: 0.0, StdDevReturn: 0.050, SelfTransitionProbability: 0.50)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            mapper.MapState(3).Should().Be(RegimeLabel.HighVolatility,
                because: "con K=4, el estado de σ máxima (estado 3, posición 3) está en el cuartil superior");
            mapper.MapState(0).Should().Be(RegimeLabel.Squeeze,
                because: "con K=4, el estado de σ mínima (estado 0, posición 0) con ρ=0.80 está en el cuartil inferior y cumple Squeeze");
        }

        [Fact]
        public void Build_ConKIgualA5_AplicaCuartilesTradicionales()
        {
            // K=5: topThreshold = Ceiling(5*0.75) = 4 → posición 4 es HighVolatility.
            //       bottomThreshold = Floor(5*0.25) = 1 → posición 0 es bottom.
            var statistics = new List<SemanticStateMapper.StateStatistics>
            {
                new(0, MeanReturn: 0.0, StdDevReturn: 0.001, SelfTransitionProbability: 0.82),
                new(1, MeanReturn: 0.0, StdDevReturn: 0.004, SelfTransitionProbability: 0.50),
                new(2, MeanReturn: 0.0, StdDevReturn: 0.007, SelfTransitionProbability: 0.50),
                new(3, MeanReturn: 0.0, StdDevReturn: 0.012, SelfTransitionProbability: 0.50),
                new(4, MeanReturn: 0.0, StdDevReturn: 0.060, SelfTransitionProbability: 0.50)
            };

            var mapper = SemanticStateMapper.Build(statistics);

            mapper.MapState(4).Should().Be(RegimeLabel.HighVolatility,
                because: "con K=5, el estado de σ máxima (estado 4, posición 4) está en el cuartil superior");
            mapper.MapState(0).Should().Be(RegimeLabel.Squeeze,
                because: "con K=5, el estado de σ mínima (estado 0, posición 0) con ρ=0.82 está en el cuartil inferior y cumple Squeeze");
        }
    }
}
