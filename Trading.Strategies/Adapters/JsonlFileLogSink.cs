using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Persiste cada evento de log como línea JSON en logs/trading-{fecha}.jsonl.
    /// Rotación diaria automática. Retención configurable (default 30 días).
    /// Thread-safe: lock interno en Write.
    /// Traga excepciones de IO para no romper el flujo de trading.
    /// </summary>
    public sealed class JsonlFileLogSink : IStructuredLogSink, IDisposable
    {
        private readonly IClock _clock;
        private readonly string _logsDirectory;
        private readonly int _retentionDays;
        private readonly object _lock = new();

        private StreamWriter? _writer;
        private DateTime _currentFileDate;
        private Exception? _lastWriteFailure;

        public JsonlFileLogSink(
            IClock clock,
            string baseDirectoryPath,
            int retentionDays = 30)
        {
            _clock = clock;
            _logsDirectory = Path.Combine(baseDirectoryPath, "logs");
            _retentionDays = retentionDays;

            Directory.CreateDirectory(_logsDirectory);

            // Rotación y retención usan wall clock real, NO el clock simulado del backtest.
            // En backtest, el clock simulado avanza cientos de días simulados disparando rotaciones
            // espurias y retenciones que eliminan los propios logs del run. El campo "timestamp"
            // de cada evento sí usa _clock.UtcNow para correlacionar con órdenes/barras (ver Write).
            var wallClockToday = DateTime.UtcNow.Date;
            OpenWriterForDate(wallClockToday);
            DeleteOldFiles(wallClockToday);
        }

        public JsonlFileLogSink(IClock clock)
            : this(clock, AppContext.BaseDirectory)
        {
        }

        public void Write(
            LogLevel level,
            string messageTemplate,
            IReadOnlyList<KeyValuePair<string, object?>> properties,
            Exception? exception)
        {
            lock (_lock)
            {
                try
                {
                    // Timestamp del evento: clock del sistema (simulado en backtest, real en live).
                    // Es lo que el operador necesita para correlacionar logs con órdenes y barras.
                    var now = _clock.UtcNow;

                    // Decisión de rotación: wall clock real, independiente del clock simulado.
                    var wallClockToday = DateTime.UtcNow.Date;
                    if (wallClockToday != _currentFileDate)
                    {
                        RotateFile(wallClockToday);
                    }

                    var args = PropertiesToArgs(properties);
                    string renderedMessage = LogTemplateRenderer.Render(messageTemplate, args);

                    var entry = BuildEntry(now, level, messageTemplate, renderedMessage, properties, exception);
                    string json = JsonSerializer.Serialize(entry);

                    _writer!.WriteLine(json);
                    _writer.Flush();
                }
                catch (Exception ex)
                {
                    _lastWriteFailure = ex;
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                try
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                    _writer = null;
                }
                catch
                {
                    // tragar — dispose no puede lanzar
                }
            }
        }

        private void OpenWriterForDate(DateTime date)
        {
            string fileName = $"trading-{date:yyyy-MM-dd}.jsonl";
            string filePath = Path.Combine(_logsDirectory, fileName);

            var fileStream = new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);

            _writer = new StreamWriter(fileStream, Encoding.UTF8, leaveOpen: false);
            _currentFileDate = date;
        }

        private void RotateFile(DateTime newDate)
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
            catch { }

            OpenWriterForDate(newDate);
            DeleteOldFiles(newDate);
        }

        private void DeleteOldFiles(DateTime today)
        {
            try
            {
                var cutoff = today.AddDays(-_retentionDays);
                var files = Directory.GetFiles(_logsDirectory, "trading-*.jsonl");

                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    // name = "trading-yyyy-MM-dd"
                    if (name.Length < 8) continue;
                    string datePart = name.Substring("trading-".Length);

                    if (DateTime.TryParseExact(
                            datePart,
                            "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out DateTime fileDate))
                    {
                        if (fileDate < cutoff)
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                    // Si el parseo falla, saltear sin error
                }
            }
            catch { }
        }

        private static object[] PropertiesToArgs(IReadOnlyList<KeyValuePair<string, object?>> properties)
        {
            var args = new object[properties.Count];
            for (int i = 0; i < properties.Count; i++)
                args[i] = properties[i].Value ?? "(null)";
            return args;
        }

        private static object BuildEntry(
            DateTime timestamp,
            LogLevel level,
            string messageTemplate,
            string renderedMessage,
            IReadOnlyList<KeyValuePair<string, object?>> properties,
            Exception? exception)
        {
            var props = new Dictionary<string, object?>();
            foreach (var kv in properties)
                props[kv.Key] = kv.Value;

            object? exceptionObj = null;
            if (exception != null)
            {
                exceptionObj = new
                {
                    Type = exception.GetType().FullName,
                    exception.Message,
                    StackTrace = exception.StackTrace
                };
            }

            return new
            {
                timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                level = level.ToString(),
                messageTemplate,
                renderedMessage,
                properties = props,
                exception = exceptionObj
            };
        }
    }
}
