using System;
using Trading.Domain.Abstractions;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Acción de mitigación de riesgo que liquida toda la cartera mediante IOrderRouter.
    /// Una sola implementación de IRiskAction por ahora; arquitectura preparada para
    /// variantes futuras (liquidación parcial, reducción de leverage, etc.).
    /// </summary>
    public sealed class LiquidateAllRiskAction : IRiskAction
    {
        private readonly IOrderRouter _orderRouter;

        public LiquidateAllRiskAction(IOrderRouter orderRouter)
        {
            _orderRouter = orderRouter ?? throw new ArgumentNullException(nameof(orderRouter));
        }

        public void Execute()
        {
            _orderRouter.LiquidateAll();
        }
    }
}
