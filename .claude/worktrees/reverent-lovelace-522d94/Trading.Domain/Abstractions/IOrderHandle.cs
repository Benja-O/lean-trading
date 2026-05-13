namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Handle a una orden viva. Reemplaza el uso directo de QuantConnect.Orders.OrderTicket en el dominio.
    /// 
    /// La interfaz es deliberadamente mínima: el dominio sólo necesita poder cancelar.
    /// Cualquier consulta de estado de la orden ocurre vía OrderLifecycleEvent (push), no por query.
    /// </summary>
    public interface IOrderHandle
    {
        void Cancel(string reason);
    }
}
