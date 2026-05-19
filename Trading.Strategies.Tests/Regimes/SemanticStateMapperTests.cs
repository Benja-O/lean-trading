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
    }
}
