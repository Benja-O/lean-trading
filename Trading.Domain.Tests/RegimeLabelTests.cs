using System;
using FluentAssertions;
using Trading.Domain.Abstractions.Regimes;
using Xunit;

namespace Trading.Domain.Tests
{
    /// <summary>
    /// Tests de RegimeLabelParser.Parse.
    ///
    /// Cobertura:
    /// - Parsing correcto de todos los valores válidos.
    /// - Falla con string vacío, nulo, inválido.
    /// - Falla con "Unknown" (no aceptado como configuración explícita).
    /// - Respeta case-sensitive (solo PascalCase aceptado).
    /// </summary>
    public class RegimeLabelTests
    {
        [Fact]
        public void Parse_DevuelveTrendCorrectamente()
        {
            var result = RegimeLabelParser.Parse("Trend");

            result.Should().Be(RegimeLabel.Trend);
        }

        [Theory]
        [InlineData("Trend", RegimeLabel.Trend)]
        [InlineData("MeanReverting", RegimeLabel.MeanReverting)]
        [InlineData("HighVolatility", RegimeLabel.HighVolatility)]
        [InlineData("Squeeze", RegimeLabel.Squeeze)]
        public void Parse_DevuelveTodosLosValoresValidos(string input, RegimeLabel expected)
        {
            var result = RegimeLabelParser.Parse(input);

            result.Should().Be(expected);
        }

        [Fact]
        public void Parse_LanzaSiStringEsVacio()
        {
            Action act = () => RegimeLabelParser.Parse(string.Empty);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*nulo ni vacío*");
        }

        [Fact]
        public void Parse_LanzaSiStringEsNulo()
        {
            Action act = () => RegimeLabelParser.Parse(null!);

            act.Should().Throw<ArgumentException>()
                .WithMessage("*nulo ni vacío*");
        }

        [Fact]
        public void Parse_LanzaSiStringEsInvalido()
        {
            Action act = () => RegimeLabelParser.Parse("FooBar");

            act.Should().Throw<ArgumentException>()
                .WithMessage("*FooBar*")
                .WithMessage("*no es un RegimeLabel válido*");
        }

        [Fact]
        public void Parse_LanzaSiStringEsUnknown()
        {
            Action act = () => RegimeLabelParser.Parse("Unknown");

            act.Should().Throw<ArgumentException>()
                .WithMessage("*Unknown*no se acepta*");
        }

        [Fact]
        public void Parse_RespetaCaseSensitive()
        {
            // "trend" (minúsculas) debe fallar: solo acepta "Trend" (PascalCase).
            Action act = () => RegimeLabelParser.Parse("trend");

            act.Should().Throw<ArgumentException>();
        }
    }
}
