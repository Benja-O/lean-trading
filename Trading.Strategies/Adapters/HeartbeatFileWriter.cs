using System;
using System.IO;
using System.Text.Json;
using Trading.Application.Health;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Serializa el snapshot de salud del sistema a health/heartbeat.json con escritura atómica
    /// (escribe a .tmp y luego mueve). Si falla, loggea Warning y no propaga excepción.
    /// </summary>
    public sealed class HeartbeatFileWriter
    {
        private readonly HealthHeartbeatTracker _tracker;
        private readonly ITradingLogger _logger;
        private readonly string _targetPath;
        private readonly string _tmpPath;

        private static readonly JsonSerializerOptions IndentedOptions =
            new() { WriteIndented = true };

        public HeartbeatFileWriter(
            HealthHeartbeatTracker tracker,
            IClock clock,
            ITradingLogger logger,
            string baseDirectoryPath)
        {
            _tracker = tracker;
            _logger = logger;

            string healthDir = Path.Combine(baseDirectoryPath, "health");
            Directory.CreateDirectory(healthDir);
            _targetPath = Path.Combine(healthDir, "heartbeat.json");
            _tmpPath = _targetPath + ".tmp";
        }

        public HeartbeatFileWriter(
            HealthHeartbeatTracker tracker,
            IClock clock,
            ITradingLogger logger)
            : this(tracker, clock, logger, AppContext.BaseDirectory)
        {
        }

        public void Flush()
        {
            try
            {
                var snapshot = _tracker.Snapshot();
                string json = JsonSerializer.Serialize(snapshot, IndentedOptions);
                File.WriteAllText(_tmpPath, json);
                File.Move(_tmpPath, _targetPath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "HeartbeatFileWriter: fallo al escribir heartbeat.json. Detalle: {Detail}",
                    ex.Message);
            }
        }
    }
}
