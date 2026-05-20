using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Trading.Domain.Abstractions;
using Trading.Strategies.Adapters;
using Trading.Strategies.Tests.Fakes;
using Xunit;

namespace Trading.Strategies.Tests.Adapters
{
    public class JsonlFileLogSinkTests : IDisposable
    {
        private readonly string _testDir;

        public JsonlFileLogSinkTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "JsonlSinkTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { }
        }

        private FakeClock ClockAt(DateTime utc) => new FakeClock { UtcNow = utc };

        private static DateTime SimulatedDay(int year, int month, int day)
            => new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);

        private static IReadOnlyList<KeyValuePair<string, object?>> Props(params (string k, object? v)[] pairs)
            => pairs.Select(p => new KeyValuePair<string, object?>(p.k, p.v)).ToList();

        private string LogsDir => Path.Combine(_testDir, "logs");

        // El archivo del día se nombra con el wall clock real, no con el clock simulado.
        private string TodayFile =>
            Path.Combine(LogsDir, $"trading-{DateTime.UtcNow.Date:yyyy-MM-dd}.jsonl");

        // ===== Tests de formato JSON (siguen usando FakeClock para el timestamp del evento) =====

        [Fact]
        public void Write_SingleEvent_ProducesParseableJsonLine()
        {
            // El clock simulado es 2025-06-01 pero el archivo se nombra con la fecha real de hoy.
            var clock = ClockAt(SimulatedDay(2025, 6, 1));
            using (var sink = new JsonlFileLogSink(clock, _testDir))
            {
                sink.Write(LogLevel.Info, "Hello world", Props(), null);
            }

            var lines = File.ReadAllLines(TodayFile);
            lines.Should().HaveCount(1);
            var doc = JsonDocument.Parse(lines[0]);
            doc.RootElement.GetProperty("level").GetString().Should().Be("Info");
            doc.RootElement.GetProperty("renderedMessage").GetString().Should().Be("Hello world");
            // El timestamp del evento refleja el clock simulado, no el wall clock
            doc.RootElement.GetProperty("timestamp").GetString()
                .Should().StartWith("2025-06-01");
        }

        [Fact]
        public void Write_WithProperties_SerializesAllFields()
        {
            var clock = ClockAt(SimulatedDay(2025, 6, 1));
            using (var sink = new JsonlFileLogSink(clock, _testDir))
            {
                sink.Write(LogLevel.Warning, "Order {OrderId} at {Price}",
                    Props(("OrderId", "ABC123"), ("Price", 50000m)), null);
            }

            var line = File.ReadAllLines(TodayFile).Single();
            var doc = JsonDocument.Parse(line);
            doc.RootElement.GetProperty("messageTemplate").GetString()
                .Should().Be("Order {OrderId} at {Price}");
            doc.RootElement.GetProperty("renderedMessage").GetString()
                .Should().Contain("ABC123");
            doc.RootElement.GetProperty("exception").ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Fact]
        public void Write_WithException_SerializesTypeMessageStackTrace()
        {
            var clock = ClockAt(SimulatedDay(2025, 6, 1));
            var ex = new InvalidOperationException("boom");
            using (var sink = new JsonlFileLogSink(clock, _testDir))
            {
                sink.Write(LogLevel.Error, "Error occurred", Props(), ex);
            }

            var line = File.ReadAllLines(TodayFile).Single();
            var doc = JsonDocument.Parse(line);
            var exObj = doc.RootElement.GetProperty("exception");
            exObj.ValueKind.Should().NotBe(JsonValueKind.Null);
            exObj.GetProperty("Type").GetString().Should().Contain("InvalidOperationException");
            exObj.GetProperty("Message").GetString().Should().Be("boom");
        }

        // ===== Test nuevo: el archivo creado al arranque usa wall clock real =====

        [Fact]
        public void Constructor_CreatesFileForTodaysWallClockDate()
        {
            // El clock simulado está en una fecha distinta (pasada del backtest).
            // El archivo JSONL debe nombrarse según DateTime.UtcNow.Date (wall clock real).
            var clock = ClockAt(SimulatedDay(2025, 1, 1));
            using var sink = new JsonlFileLogSink(clock, _testDir);

            sink.Write(LogLevel.Info, "probe", Props(), null);

            string expectedFileName = $"trading-{DateTime.UtcNow.Date:yyyy-MM-dd}.jsonl";
            File.Exists(Path.Combine(LogsDir, expectedFileName))
                .Should().BeTrue($"el archivo debe nombrarse con el wall clock real: {expectedFileName}");
        }

        // ===== Tests de retención (adaptados a wall clock real) =====

        [Fact]
        public void Constructor_AtStartup_DeletesFilesOlderThanRetention()
        {
            // Crear archivos viejos usando fechas relativas al wall clock real.
            // cutoff = DateTime.UtcNow.Date - 30 días
            Directory.CreateDirectory(LogsDir);
            var today = DateTime.UtcNow.Date;
            var veryOld = today.AddDays(-60);   // definitivamente antes del cutoff
            var borderline = today.AddDays(-29); // reciente — debe conservarse
            string veryOldPath = Path.Combine(LogsDir, $"trading-{veryOld:yyyy-MM-dd}.jsonl");
            string borderlinePath = Path.Combine(LogsDir, $"trading-{borderline:yyyy-MM-dd}.jsonl");
            File.WriteAllText(veryOldPath, "old");
            File.WriteAllText(borderlinePath, "recent");

            // FakeClock en cualquier fecha — ya no importa para el cutoff de retención
            var clock = ClockAt(SimulatedDay(2025, 6, 1));
            using var sink = new JsonlFileLogSink(clock, _testDir, retentionDays: 30);

            File.Exists(veryOldPath).Should().BeFalse("el archivo supera la retención de 30 días de wall clock");
            File.Exists(borderlinePath).Should().BeTrue("el archivo está dentro de la retención");
        }

        // ===== Tests de concurrencia y fallos (sin cambios de lógica) =====

        [Fact]
        public void Write_ConcurrentFromMultipleThreads_ProducesNoCorruptedLines()
        {
            var clock = ClockAt(SimulatedDay(2025, 6, 1));
            int count = 50;
            using (var sink = new JsonlFileLogSink(clock, _testDir))
            {
                Parallel.For(0, count, i =>
                {
                    sink.Write(LogLevel.Info, "Concurrent {Index}", Props(("Index", (object?)i)), null);
                });
            }

            var lines = File.ReadAllLines(TodayFile);
            lines.Should().HaveCount(count);
            foreach (var line in lines)
            {
                var act = () => JsonDocument.Parse(line);
                act.Should().NotThrow($"la línea debe ser JSON válido: {line}");
            }
        }

        [Fact]
        public void Write_WhenFileSystemFails_DoesNotPropagateException()
        {
            var clock = ClockAt(SimulatedDay(2025, 6, 1));
            JsonlFileLogSink? sink = null;
            try
            {
                sink = new JsonlFileLogSink(clock, _testDir);
                sink.Dispose();
                // Escribir después de dispose — debe tragar la excepción
                var act = () => sink.Write(LogLevel.Info, "after dispose", Props(), null);
                act.Should().NotThrow();
            }
            finally
            {
                sink?.Dispose();
            }
        }

        [Fact]
        public void RetentionCleanup_IgnoresFilesWithUnparseableNames()
        {
            Directory.CreateDirectory(LogsDir);
            // Archivo con prefijo trading- pero fecha no parseable — debe quedar intacto
            string unparseable = Path.Combine(LogsDir, "trading-notadate.jsonl");
            File.WriteAllText(unparseable, "data");

            var clock = ClockAt(SimulatedDay(2025, 6, 1));
            using var sink = new JsonlFileLogSink(clock, _testDir, retentionDays: 30);

            File.Exists(unparseable).Should().BeTrue();
        }
    }
}
