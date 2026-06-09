using System;
using Trading.Domain.Abstractions;
using Trading.Strategies.Implementations;

namespace Trading.Strategies.Infrastructure
{
    public static class StrategyFactory
    {
        public static IStrategy Create(string strategyName)
        {
            return strategyName?.ToLower() switch
            {
                "emacrossstrategy" or "emacross" => new EmaCrossStrategy(),

                null or "" => throw new ArgumentNullException(nameof(strategyName),
                    "El nombre de la estrategia no puede ser nulo o vacío."),

                _ => throw new NotSupportedException(
                    $"La estrategia '{strategyName}' no está registrada en StrategyFactory. " +
                    $"Verificá el nombre en strategies.json y registrá la estrategia en el factory.")
            };
        }
    }
}
