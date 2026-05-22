using Trading.Domain.Abstractions;

namespace Trading.Application.Health
{
    /// <summary>
    /// Implementación pasiva de IStrategyHealthMonitor. Nunca excluye. Existe para usar
    /// como placeholder durante el wiring de Pieza A (antes de que StrategyHealthMonitor
    /// real esté disponible en Pieza B) y como fallback testeable.
    ///
    /// En el wiring final de Pieza B, este Null se reemplaza por StrategyHealthMonitor real.
    /// </summary>
    public sealed class NullStrategyHealthMonitor : IStrategyHealthMonitor
    {
        public bool IsExcluded(string executorIdentifier) => false;
    }
}
