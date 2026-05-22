namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato del monitor de salud por estrategia. La implementación mantiene métricas
    /// rolling por estrategia y, ante degradación, excluye a esa estrategia del flujo de
    /// generación de señales. Consultado por BarProcessingService como guard pre-orden.
    ///
    /// NO es un IRiskMonitor: la degradación de una estrategia NO activa el kill switch
    /// global. Ver ADR-023.
    /// </summary>
    public interface IStrategyHealthMonitor
    {
        /// <summary>
        /// Indica si la estrategia identificada por executorIdentifier está excluida
        /// (degradada). Si retorna true, BarProcessingService descarta señales de esa
        /// estrategia hasta reinicio manual del proceso.
        ///
        /// Para identificadores desconocidos retorna false (estrategia nueva o sin
        /// historia: no excluida).
        /// </summary>
        bool IsExcluded(string executorIdentifier);
    }
}
