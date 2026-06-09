namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Estrategia que expone el valor actual de su ATR interno.
    /// Implementada opcionalmente junto a IStrategy cuando la lógica de exit
    /// requiere precios de SL/TP calculados en función de la volatilidad al momento de la señal.
    /// </summary>
    public interface IAtrProvider
    {
        /// <summary>
        /// Último valor del ATR(14) calculado para el ticker dado.
        /// Retorna 0 si el indicador aún no está calentado.
        /// </summary>
        decimal GetLastAtr(string ticker);
    }
}
