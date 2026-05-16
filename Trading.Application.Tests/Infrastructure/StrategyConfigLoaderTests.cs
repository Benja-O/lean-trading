using System.IO;
using System.Linq;
using FluentAssertions;
using Trading.Domain.Exceptions;
using Trading.Strategies.Infrastructure;
using Xunit;

namespace Trading.Application.Tests.Infrastructure
{
    /// <summary>
    /// Tests de validación del StrategyConfigLoader para el campo CompatibleRegimes.
    ///
    /// Cobertura:
    /// - Lista vacía explícita → error agregado al cargar.
    /// - Campo ausente → válido; CompatibleRegimes queda null (fail-safe = compatible con todos).
    /// - Lista con valores → válido; CompatibleRegimes tiene los valores esperados.
    /// </summary>
    public class StrategyConfigLoaderTests
    {
        private static string WriteTemp(string json)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, json);
            return path;
        }

        private static void DeleteTemp(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        [Fact]
        public void Load_FallaSiCompatibleRegimesEstaPresenteVacio()
        {
            string json = @"{
  ""Timeframes"": {
    ""1h"": {
      ""Strategies"": [
        {
          ""StrategyName"": ""EmaCrossStrategy"",
          ""Symbol"": ""BTCUSDT"",
          ""StopLossPercentage"": 3.0,
          ""TakeProfitPercentage"": 6.0,
          ""RiskPerTradePercentage"": 2.0,
          ""CompatibleRegimes"": []
        }
      ]
    }
  }
}";
            string tempPath = WriteTemp(json);
            try
            {
                var loader = new StrategyConfigLoader();
                Action act = () => loader.Load(tempPath);

                var exception = act.Should().Throw<StrategyConfigurationException>().Which;
                exception.Errors.Should().ContainSingle(error =>
                    error.Contains("CompatibleRegimes") && error.Contains("lista vacía"));
            }
            finally
            {
                DeleteTemp(tempPath);
            }
        }

        [Fact]
        public void Load_AceptaSiCompatibleRegimesEstaAusente()
        {
            string json = @"{
  ""Timeframes"": {
    ""1h"": {
      ""Strategies"": [
        {
          ""StrategyName"": ""EmaCrossStrategy"",
          ""Symbol"": ""BTCUSDT"",
          ""StopLossPercentage"": 3.0,
          ""TakeProfitPercentage"": 6.0,
          ""RiskPerTradePercentage"": 2.0
        }
      ]
    }
  }
}";
            string tempPath = WriteTemp(json);
            try
            {
                var loader = new StrategyConfigLoader();
                var rootConfig = loader.Load(tempPath);

                var strategyDefinition = rootConfig.Timeframes["1h"].Strategies.Single();
                strategyDefinition.CompatibleRegimes.Should().BeNull();
            }
            finally
            {
                DeleteTemp(tempPath);
            }
        }

        [Fact]
        public void Load_AceptaSiCompatibleRegimesTieneValores()
        {
            string json = @"{
  ""Timeframes"": {
    ""1h"": {
      ""Strategies"": [
        {
          ""StrategyName"": ""EmaCrossStrategy"",
          ""Symbol"": ""BTCUSDT"",
          ""StopLossPercentage"": 3.0,
          ""TakeProfitPercentage"": 6.0,
          ""RiskPerTradePercentage"": 2.0,
          ""CompatibleRegimes"": [""Trend"", ""Squeeze""]
        }
      ]
    }
  }
}";
            string tempPath = WriteTemp(json);
            try
            {
                var loader = new StrategyConfigLoader();
                var rootConfig = loader.Load(tempPath);

                var strategyDefinition = rootConfig.Timeframes["1h"].Strategies.Single();
                strategyDefinition.CompatibleRegimes.Should().HaveCount(2);
                strategyDefinition.CompatibleRegimes.Should().Contain("Trend");
                strategyDefinition.CompatibleRegimes.Should().Contain("Squeeze");
            }
            finally
            {
                DeleteTemp(tempPath);
            }
        }
    }
}
