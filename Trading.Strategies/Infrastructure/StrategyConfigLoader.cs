using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Trading.Domain.Abstractions;
using Trading.Domain.Exceptions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Infrastructure
{
    /// <summary>
    /// Carga la configuración de estrategias desde JSON y la valida agregadamente.
    /// 
    /// Política institucional: el sistema NO arranca si alguna StrategyDefinition tiene
    /// parámetros de riesgo inválidos o ausentes. Reúne TODOS los errores antes de fallar.
    /// 
    /// Vive en Trading.Strategies (no en Application) porque depende de Newtonsoft.Json y
    /// de System.IO; esas dependencias son infraestructura concreta y no deben contaminar Application.
    /// </summary>
    public class StrategyConfigLoader : IStrategyConfigLoader
    {
        public RootConfig Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"No se encontró el archivo de configuración en {path}");

            string json = File.ReadAllText(path);
            var rootConfiguration = JsonConvert.DeserializeObject<RootConfig>(json);

            ValidateOrFail(rootConfiguration, path);

            return rootConfiguration;
        }

        private static void ValidateOrFail(RootConfig rootConfiguration, string sourcePath)
        {
            if (rootConfiguration == null)
            {
                throw new StrategyConfigurationException(new[]
                {
                    $"El archivo {sourcePath} se deserializó a null. Verificar formato JSON."
                });
            }

            if (rootConfiguration.Timeframes == null || rootConfiguration.Timeframes.Count == 0)
            {
                throw new StrategyConfigurationException(new[]
                {
                    $"El archivo {sourcePath} no contiene Timeframes."
                });
            }

            var collectedErrors = new List<string>();

            foreach (var timeframeNode in rootConfiguration.Timeframes)
            {
                string timeframeKey = timeframeNode.Key;
                var timeframeContent = timeframeNode.Value;

                if (timeframeContent?.Strategies == null) continue;

                foreach (var strategyDefinition in timeframeContent.Strategies)
                {
                    string strategyContext =
                        $"[Timeframe={timeframeKey}, Strategy={strategyDefinition.StrategyName}, Symbol={strategyDefinition.Symbol}]";

                    // Verificación de presencia ANTES de intentar construir RiskParameters.
                    // Permite diferenciar "campo ausente del JSON" de "campo presente pero inválido".
                    if (!strategyDefinition.RiskPerTradePercentage.HasValue)
                    {
                        collectedErrors.Add(
                            $"{strategyContext}{System.Environment.NewLine}" +
                            "El campo 'RiskPerTradePercentage' es obligatorio en strategies.json y no fue encontrado. " +
                            "Agregar el campo con un valor estrictamente positivo (ejemplo: 2.0 para 2%).");
                        continue;
                    }

                    try
                    {
                        RiskParameters.FromPercentages(
                            stopLossPercentage: strategyDefinition.StopLossPercentage,
                            takeProfitPercentage: strategyDefinition.TakeProfitPercentage,
                            riskPerTradePercentage: strategyDefinition.RiskPerTradePercentage.Value);
                    }
                    catch (InvalidRiskParametersException invalidRiskException)
                    {
                        collectedErrors.Add(
                            $"{strategyContext}{System.Environment.NewLine}{invalidRiskException.Message}");
                    }

                    // Validación de CompatibleRegimes: null/ausente es válido (fail-safe = compatible con todos).
                    // Lista vacía es inválido — indicaría una decisión confusa que el operador debe explicitar.
                    if (strategyDefinition.CompatibleRegimes != null &&
                        strategyDefinition.CompatibleRegimes.Count == 0)
                    {
                        collectedErrors.Add(
                            $"{strategyContext}{System.Environment.NewLine}" +
                            "El campo 'CompatibleRegimes' está presente pero es una lista vacía. " +
                            "Si querés que la estrategia opere en todos los regímenes, omití el campo (no lo incluyas como []). " +
                            "Si querés desactivar la estrategia, removela del JSON.");
                    }
                }
            }

            if (collectedErrors.Count > 0)
            {
                throw new StrategyConfigurationException(collectedErrors);
            }
        }
    }
}
