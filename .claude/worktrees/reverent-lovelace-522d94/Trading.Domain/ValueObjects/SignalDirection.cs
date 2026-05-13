namespace Trading.Domain.ValueObjects
{
    /// <summary>
    /// Dirección de una señal emitida por una estrategia.
    /// Reemplaza el retorno bool de IStrategy.EvaluateSignal: permite distinguir Long de Short
    /// y representa "sin señal" de forma explícita (Flat) sin recurrir a nullable.
    /// </summary>
    public enum SignalDirection
    {
        /// <summary>Sin señal de acción.</summary>
        Flat,

        /// <summary>Señal de entrada long (compra).</summary>
        Long,

        /// <summary>Señal de entrada short (venta en corto).</summary>
        Short
    }
}
