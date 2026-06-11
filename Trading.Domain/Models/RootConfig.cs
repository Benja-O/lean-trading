using System.Collections.Generic;

namespace Trading.Domain.Models
{
    /// <summary>
    /// Raíz de la configuración de strategies.json.
    /// La clave del diccionario es el timeframe ("1m", "5m", "1h", etc.).
    /// </summary>
    public class RootConfig
    {
        public Dictionary<string, TimeframeNode> Timeframes { get; set; } = new();

        /// <summary>
        /// Directorio donde están los CSVs de features microestructurales ({SYMBOL}_1h_features.csv).
        /// Si es null, se usa {AppContext.BaseDirectory}/microstructure como fallback.
        /// </summary>
        public string? MicrostructureDataPath { get; set; }
    }
}
