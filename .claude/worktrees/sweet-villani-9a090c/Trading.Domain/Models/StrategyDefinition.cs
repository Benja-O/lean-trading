namespace Trading.Domain.Models
{
    /// <summary>
    /// DTO de configuración deserializado desde strategies.json.
    /// 
    /// IMPORTANTE - Convención de unidades:
    /// Los campos *Percentage están en formato PORCENTAJE
    /// (ej. 3.0 representa 3%, NO 0.03).
    /// 
    /// Para uso en lógica de dominio, NO acceder directamente a estos campos.
    /// Usar RiskParameters.FromPercentages(...) que convierte a fracciones validadas
    /// y verifica invariantes institucionales.
    /// 
    /// Esta clase es un DTO de transporte; refleja la estructura del JSON sin agregar lógica.
    /// La validación ocurre en el value object RiskParameters y en StrategyConfigLoader.
    /// </summary>
    public class StrategyDefinition
    {
        public string StrategyName { get; set; }
        public string Symbol { get; set; }

        /// <summary>Porcentaje de stop loss. Ejemplo: 3.0 representa 3%.</summary>
        public decimal StopLossPercentage { get; set; }

        /// <summary>Porcentaje de take profit. Ejemplo: 6.0 representa 6%.</summary>
        public decimal TakeProfitPercentage { get; set; }

        /// <summary>
        /// Porcentaje del portfolio arriesgado por trade. Ejemplo: 2.0 representa 2%.
        /// 
        /// Nullable a propósito: permite distinguir "campo ausente del JSON" de "campo presente con valor 0".
        /// Ambos casos son inválidos, pero el StrategyConfigLoader emite mensajes distintos para
        /// facilitar el diagnóstico al operador.
        /// </summary>
        public decimal? RiskPerTradePercentage { get; set; }

        public bool CombineWithTimeExit { get; set; }
        public int MaxBars { get; set; }
    }
}
