namespace Trading.Domain.ValueObjects
{
    /// <summary>
    /// Propósito de una orden dentro del ciclo de vida de una operación.
    /// Reemplaza los strings ENTRY/SL/TP/TIME que se usaban en los tags.
    /// </summary>
    public enum OrderPurpose
    {
        Entry,
        StopLoss,
        TakeProfit,
        TimeExit
    }
}
