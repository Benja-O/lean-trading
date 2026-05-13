namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato de la acción ejecutada cuando se activa el kill switch.
    ///
    /// Hoy hay una sola implementación: LiquidateAllRiskAction.
    /// En el futuro podría haber acciones más sutiles (cerrar solo cierto símbolo,
    /// reducir leverage, etc.).
    /// </summary>
    public interface IRiskAction
    {
        /// <summary>
        /// Ejecuta la acción de mitigación. La idempotencia se garantiza en el orchestrator
        /// vía el flag IsKillSwitchActivated — esta interfaz no requiere ser idempotente.
        /// </summary>
        void Execute();
    }
}
