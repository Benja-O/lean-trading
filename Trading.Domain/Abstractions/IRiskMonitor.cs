namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato de un monitor de riesgo. Cada implementación chequea UNA condición específica
    /// (drawdown, pérdidas consecutivas, régimen de mercado incompatible, etc.) y reporta
    /// si fue violada.
    ///
    /// El monitor NO ejecuta acciones — solo emite veredictos. La acción (liquidar, marcar
    /// kill switch, publicar evento) la hace el RiskOrchestrator.
    ///
    /// Cada monitor mantiene su propio estado interno (counters, históricos, timestamps).
    /// El orchestrator es agnóstico al estado de cada uno.
    /// </summary>
    public interface IRiskMonitor
    {
        /// <summary>Identificador legible para logs y diagnóstico.</summary>
        string MonitorName { get; }

        /// <summary>
        /// Evalúa las condiciones actuales y devuelve el veredicto.
        /// Se invoca por el orchestrator en cada ciclo de chequeo (típicamente cada barra).
        /// </summary>
        RiskAssessment Evaluate();

        /// <summary>
        /// Resetea cualquier estado acumulado. Lo invoca el orchestrator al finalizar
        /// un período de cooling-off. Monitors sin estado pueden implementar como no-op.
        /// </summary>
        void Reset();
    }
}
