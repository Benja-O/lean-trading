namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Motivos por los cuales el PositionSizer puede rechazar el cálculo de una cantidad.
    /// El enum permite que el caller decida según el motivo, y que el agregador de métricas
    /// categorice rechazos sin parsear texto.
    /// </summary>
    public enum SizingFailureReason
    {
        /// <summary>Precio recibido es cero o negativo. Dato de mercado inválido.</summary>
        InvalidPrice,

        /// <summary>Cantidad calculada redondeada al lot size resulta en cero (riesgo muy bajo o lot size muy grande).</summary>
        QuantityRoundsToZero,

        /// <summary>Notional resultante (cantidad * precio) está por debajo del mínimo aceptado por el exchange.</summary>
        BelowMinimumNotional
    }
}
