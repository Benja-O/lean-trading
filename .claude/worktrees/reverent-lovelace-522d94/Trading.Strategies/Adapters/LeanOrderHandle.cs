using QuantConnect.Orders;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Envuelve un QuantConnect.Orders.OrderTicket para exponerlo como IOrderHandle al dominio.
    ///
    /// IMPORTANTE: el parámetro 'cancellationReason' del método Cancel NO se propaga a
    /// OrderTicket.Cancel() de Lean. Razón: Lean sobreescribe el Tag del ticket con el reason,
    /// rompiendo la resolución del OrderRegistry. La trazabilidad del motivo se mantiene
    /// vía logs explícitos en OrderLifecycleService antes de invocar Cancel.
    /// </summary>
    public sealed class LeanOrderHandle : IOrderHandle
    {
        private readonly OrderTicket _orderTicket;

        public LeanOrderHandle(OrderTicket orderTicket)
        {
            _orderTicket = orderTicket;
        }

        public void Cancel(string cancellationReason)
        {
            // 'cancellationReason' deliberadamente NO se pasa a Lean.
            // Ver comentario de la clase para justificación.
            _orderTicket?.Cancel();
        }

        /// <summary>
        /// Acceso interno al OrderTicket subyacente. Sólo para uso del adaptador Lean,
        /// nunca expuesto al dominio.
        /// </summary>
        internal OrderTicket UnderlyingTicket => _orderTicket;
    }
}
