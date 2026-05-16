using System;
using System.Collections.Generic;
using FluentAssertions;
using Trading.Application.Regimes;
using Trading.Domain.Abstractions.Regimes;
using Xunit;

namespace Trading.Application.Tests.Regimes
{
    /// <summary>
    /// Tests de StrategyRegimeCompatibility.
    ///
    /// Cobertura:
    /// - AllowedRegimes null → compatible con todos los regímenes.
    /// - AllowedRegimes vacío → compatible con todos los regímenes.
    /// - AllowedRegimes contiene etiqueta → true para esa etiqueta.
    /// - AllowedRegimes no contiene etiqueta → false.
    /// - RegimeLabel.Unknown → siempre true (fail-safe).
    /// - Constructor lanza si ExecutorIdentifier es nulo o vacío.
    /// </summary>
    public class StrategyRegimeCompatibilityTests
    {
        private static StrategyRegimeCompatibility BuildWith(IReadOnlySet<RegimeLabel>? allowed) =>
            new StrategyRegimeCompatibility("EmaCross_BTCUSDT_1h", allowed);

        [Fact]
        public void IsCompatibleWith_SiAllowedRegimesEsNull_DevuelveSiempreTrue()
        {
            var compatibility = BuildWith(null);

            compatibility.IsCompatibleWith(RegimeLabel.Trend).Should().BeTrue();
            compatibility.IsCompatibleWith(RegimeLabel.HighVolatility).Should().BeTrue();
            compatibility.IsCompatibleWith(RegimeLabel.Squeeze).Should().BeTrue();
            compatibility.IsCompatibleWith(RegimeLabel.MeanReverting).Should().BeTrue();
        }

        [Fact]
        public void IsCompatibleWith_SiAllowedRegimesEstaVacio_DevuelveSiempreTrue()
        {
            var compatibility = BuildWith(new HashSet<RegimeLabel>());

            compatibility.IsCompatibleWith(RegimeLabel.HighVolatility).Should().BeTrue();
        }

        [Fact]
        public void IsCompatibleWith_SiAllowedRegimesContieneEtiqueta_DevuelveTrue()
        {
            var compatibility = BuildWith(new HashSet<RegimeLabel> { RegimeLabel.Trend, RegimeLabel.Squeeze });

            compatibility.IsCompatibleWith(RegimeLabel.Trend).Should().BeTrue();
            compatibility.IsCompatibleWith(RegimeLabel.Squeeze).Should().BeTrue();
        }

        [Fact]
        public void IsCompatibleWith_SiAllowedRegimesNoContieneEtiqueta_DevuelveFalse()
        {
            var compatibility = BuildWith(new HashSet<RegimeLabel> { RegimeLabel.Trend });

            compatibility.IsCompatibleWith(RegimeLabel.HighVolatility).Should().BeFalse();
            compatibility.IsCompatibleWith(RegimeLabel.MeanReverting).Should().BeFalse();
            compatibility.IsCompatibleWith(RegimeLabel.Squeeze).Should().BeFalse();
        }

        [Fact]
        public void IsCompatibleWith_Unknown_SiempreDevuelveTrue()
        {
            var compatibility = BuildWith(new HashSet<RegimeLabel> { RegimeLabel.Trend });

            // Aunque la estrategia solo acepta Trend, Unknown siempre pasa (fail-safe).
            compatibility.IsCompatibleWith(RegimeLabel.Unknown).Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_LanzaSiExecutorIdentifierEsNuloOVacio(string invalidIdentifier)
        {
            Action act = () => new StrategyRegimeCompatibility(invalidIdentifier!, null);

            act.Should().Throw<ArgumentException>();
        }
    }
}
