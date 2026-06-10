using System;
using Trading.Domain.Abstractions;
using Trading.Strategies.Implementations;

namespace Trading.Strategies.Infrastructure
{
    public static class StrategyFactory
    {
        /// <summary>
        /// Crea una estrategia por nombre. Las estrategias que implementan IMicrostructureAwareStrategy
        /// reciben el proveedor de features microestructurales; las demás lo ignoran.
        ///
        /// El parámetro microstructureProvider puede ser null para estrategias que no lo requieren
        /// (EmaCrossStrategy y similares OHLCV-only). Si una estrategia microestructural recibe null,
        /// debe degradarse a Flat gracefully (nunca lanzar excepción).
        /// </summary>
        public static IStrategy Create(string strategyName, IMicrostructureProvider microstructureProvider = null)
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
