using System.Collections.Generic;

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

        /// <summary>
        /// Lista de regímenes de mercado compatibles con esta estrategia (declarados como strings
        /// en el JSON, ej. ["Trend", "Squeeze"]).
        ///
        /// Semántica:
        /// - null/ausente del JSON: la estrategia es compatible con todos los regímenes (fail-safe).
        /// - Lista vacía []: inválido; el loader debe rechazarlo (ver StrategyConfigLoader).
        /// - Lista con valores: solo se emiten señales cuando el clasificador reporta un régimen
        ///   incluido en esta lista.
        ///
        /// La conversión de strings a el enum RegimeLabel ocurre en una capa superior cuando esa
        /// abstracción exista (Paso 2 de Hito B). Aquí se mantiene como string para evitar acoplar
        /// el DTO de configuración a una abstracción que aún no existe.
        ///
        /// El tipo es List&lt;string&gt; concreto (no IReadOnlyList&lt;string&gt;) siguiendo el patrón
        /// establecido del repo para DTOs de configuración deserializados con Newtonsoft.Json
        /// (RootConfig.Timeframes es Dictionary concreto por la misma razón).
        /// </summary>
        public List<string>? CompatibleRegimes { get; set; }
    }
}
