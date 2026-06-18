using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Trading.Recorder
{
    /// <summary>
    /// Configuración del grabador derivada de strategies.json.
    /// Extrae los pares (símbolo, timeframe) únicos que deben grabarse.
    /// </summary>
    public sealed class RecorderConfig
    {
        public string StorageDirectory { get; }
        public int RetentionDays { get; }

        /// <summary>Pares únicos (símbolo Binance en mayúsculas, timeframe string) a grabar.</summary>
        public IReadOnlyList<(string Symbol, string Timeframe)> Streams { get; }

        private RecorderConfig(string storageDirectory, int retentionDays, IReadOnlyList<(string, string)> streams)
        {
            StorageDirectory = storageDirectory;
            RetentionDays    = retentionDays;
            Streams          = streams;
        }

        /// <summary>
        /// Carga la configuración leyendo strategies.json e infiriendo los streams a suscribir.
        /// </summary>
        /// <param name="strategiesJsonPath">Ruta al strategies.json del sistema.</param>
        /// <param name="storageDirectory">Directorio donde el grabador escribe los CSV.</param>
        /// <param name="retentionDays">Días de retención del rolling window (default: 7).</param>
        public static RecorderConfig FromStrategiesJson(
            string strategiesJsonPath,
            string storageDirectory,
            int retentionDays = 7)
        {
            if (!File.Exists(strategiesJsonPath))
                throw new FileNotFoundException($"strategies.json no encontrado: {strategiesJsonPath}");

            string json = File.ReadAllText(strategiesJsonPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var seen    = new HashSet<(string, string)>();
            var streams = new List<(string, string)>();

            if (root.TryGetProperty("Timeframes", out var timeframes))
            {
                foreach (var tfEntry in timeframes.EnumerateObject())
                {
                    string timeframe = tfEntry.Name;
                    if (!tfEntry.Value.TryGetProperty("Strategies", out var strategies)) continue;

                    foreach (var strategy in strategies.EnumerateArray())
                    {
                        if (!strategy.TryGetProperty("Symbol", out var symProp)) continue;
                        string symbol = symProp.GetString()!.ToUpperInvariant();
                        var key = (symbol, timeframe);
                        if (seen.Add(key))
                            streams.Add(key);
                    }
                }
            }

            if (streams.Count == 0)
                throw new InvalidOperationException(
                    "strategies.json no tiene estrategias con Symbol definido. El grabador no tiene nada que suscribir.");

            return new RecorderConfig(storageDirectory, retentionDays, streams);
        }

        /// <summary>Convierte un string de timeframe (p.ej. "1h", "5m") a TimeSpan.</summary>
        public static TimeSpan ParseTimeframe(string timeframe)
        {
            if (string.IsNullOrEmpty(timeframe))
                throw new ArgumentException("timeframe no puede ser nulo o vacío.", nameof(timeframe));

            char unit  = timeframe[^1];
            string num = timeframe[..^1];

            if (!int.TryParse(num, out int value) || value <= 0)
                throw new ArgumentException($"Valor de timeframe inválido: '{timeframe}'", nameof(timeframe));

            return unit switch
            {
                'm' => TimeSpan.FromMinutes(value),
                'h' => TimeSpan.FromHours(value),
                'd' => TimeSpan.FromDays(value),
                _ => throw new ArgumentException($"Unidad de timeframe desconocida '{unit}' en '{timeframe}'", nameof(timeframe))
            };
        }
    }
}
