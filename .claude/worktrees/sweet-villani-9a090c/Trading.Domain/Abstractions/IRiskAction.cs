namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato de la acción ejecutada cuando se activa el kill switch.
    ///
    /// Hoy hay una sola implementación: LiquidateAllRiskAction (delega a IOrderRouter.LiquidateAll).
    /// En el futuro podría haber acciones más sutiles (cerrar solo cierto símbolo, reducir leverage, etc.).
    /// </summary>
    public interface IRiskAction
    {
        /// <summary>
        /// Ejecuta la acción de mitigación. Idempotente: invocarla múltiples veces no debe
        /// producir efectos adicionales después de la primera (la lógica de "ya se ejecutó"
        /// vive en el orchestrator vía el flag IsKillSwitchActivated).
        /// </summary>
        void Execute();
    }
}
