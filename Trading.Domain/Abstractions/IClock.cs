using System;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Reloj del sistema. Abstracción sobre QCAlgorithm.Time (que en backtest es tiempo simulado,
    /// en live es tiempo real). Permite testear lógica temporal (cooling-off period del kill switch)
    /// con relojes controlables en tests, sin necesidad de Thread.Sleep ni de un QCAlgorithm real.
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
