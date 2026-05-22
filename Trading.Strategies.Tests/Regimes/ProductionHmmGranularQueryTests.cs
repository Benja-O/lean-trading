// Consulta granular al modelo HMM de producción para la ventana 3 de DEUDA-1.
// Evidencia durable de la validación cruzada (Fase 4). Reutilizable en Hito G
// para consultas granulares al modelo sobre períodos históricos específicos.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Regimes;
using Xunit;
using Xunit.Abstractions;

namespace Trading.Strategies.Tests.Regimes
{
    public class ProductionHmmGranularQueryTests
    {
        private readonly ITestOutputHelper _output;

        public ProductionHmmGranularQueryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void QueryGranularProductionHmm_ForDeuda1Window3()
        {
            // === Rutas ===
            string modelPath = Path.Combine(
                AppContext.BaseDirectory, "models", "regime", "BTCUSDT-perp-binance.hmm.json");

            const string KlinesDir =
                @"F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\4h\BTCUSDT";

            string outputMdPath =
                @"F:\DesarrolloTrading\QuantConnect\Lean\briefs\DEUDA_1_ventana3_granular.md";

            // Rango de reporte (lo que se incluye en la tabla de salida)
            var reportStart = new DateTime(2025, 1, 22, 0, 0, 0, DateTimeKind.Utc);
            var reportEnd   = new DateTime(2025, 2, 15, 23, 59, 59, DateTimeKind.Utc);

            // Warm-up: cargar desde 2024-12-01 para dar >= 100 barras 4h de precalentamiento
            var warmUpStart = new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc);

            // === Cargar modelo ===
            _output.WriteLine($"Modelo: {modelPath}");
            Assert.True(File.Exists(modelPath), $"No se encontró el modelo en: {modelPath}");
            var classifier = AccordHmmClassifierFactory.Load(modelPath);
            _output.WriteLine($"Instrumento: {classifier.Instrument}");

            // === Cargar barras históricas (meses que cubren warm-up + ventana) ===
            var parser = new BinanceKlinesParser(new InstrumentId("BTCUSDT"));

            // Meses necesarios: dic-2024 (warm-up), ene-2025, feb-2025
            string[] zipFiles = ["2024-12.zip", "2025-01.zip", "2025-02.zip"];

            var allBars = new List<Trading.Domain.Models.MarketBar>();
            foreach (var zipName in zipFiles)
            {
                string zipPath = Path.Combine(KlinesDir, zipName);
                Assert.True(File.Exists(zipPath), $"No se encontró el archivo: {zipPath}");
                var bars = parser.ParseZipFile(zipPath, warmUpStart, reportEnd);
                allBars.AddRange(bars);
            }

            allBars = [.. allBars.OrderBy(b => b.TimestampUtc)];
            _output.WriteLine($"Barras cargadas (dic-2024 → feb-2025): {allBars.Count}");
            _output.WriteLine(
                $"Rango: {allBars.First().TimestampUtc:yyyy-MM-dd HH:mm} → " +
                $"{allBars.Last().TimestampUtc:yyyy-MM-dd HH:mm}");

            // === Clasificar barra a barra (warm-up sucede naturalmente) ===
            var inRange = new List<(DateTime ts, RegimeLabel label, IReadOnlyDictionary<RegimeLabel, double> probs)>();

            foreach (var bar in allBars)
            {
                var classification = classifier.Classify(bar);

                if (bar.TimestampUtc >= reportStart && bar.TimestampUtc <= reportEnd)
                {
                    inRange.Add((classification.ClassifiedAtUtc, classification.Label, classification.Probabilities));
                }
            }

            _output.WriteLine($"\nClasificaciones en ventana de reporte: {inRange.Count}");

            // === Verificación de cordura (Paso 4 del brief) ===
            Assert.True(inRange.Count >= 100,
                $"Esperadas ≥100 barras en el rango, encontradas {inRange.Count}. Revisar warm-up.");

            bool hasRealLabels = inRange.Any(c => c.label != RegimeLabel.Unknown);
            Assert.True(hasRealLabels,
                "Todas las clasificaciones son Unknown — el warm-up no se completó. " +
                "Verificar que el archivo histórico tiene suficientes barras antes del 2025-01-22.");

            var crashBars = inRange
                .Where(c => c.ts >= new DateTime(2025, 2, 2, 0, 0, 0, DateTimeKind.Utc)
                         && c.ts <= new DateTime(2025, 2, 4, 23, 59, 59, DateTimeKind.Utc))
                .ToList();
            Assert.True(crashBars.All(c => c.label != RegimeLabel.Unknown),
                "Las barras del crash (2025-02-02→04) aún están en Unknown — insuficiente warm-up.");

            // === Tabla cronológica al output del test ===
            _output.WriteLine("\n=== TABLA CRONOLÓGICA (4h) ===");
            _output.WriteLine(
                "| Timestamp UTC          | Etiqueta     | Trend  | Squeeze | HighVol | MeanRev |");
            _output.WriteLine(
                "|------------------------|--------------|--------|---------|---------|---------|");

            foreach (var (ts, label, probs) in inRange)
                _output.WriteLine(FormatRow(ts, label, probs));

            // === Resumen ===
            var labelCounts = inRange
                .GroupBy(c => c.label)
                .OrderByDescending(g => g.Count())
                .ToList();

            _output.WriteLine("\n=== RESUMEN DISTRIBUCIÓN ===");
            foreach (var g in labelCounts)
            {
                double pct = 100.0 * g.Count() / inRange.Count;
                _output.WriteLine($"  {g.Key}: {g.Count()} barras ({pct:F1}%)");
            }

            // === Foco crash 2025-02-02 → 2025-02-04 ===
            _output.WriteLine($"\n=== FOCO CRASH 2025-02-02 → 2025-02-04 ({crashBars.Count} barras 4h) ===");
            foreach (var (ts, label, probs) in crashBars)
                _output.WriteLine($"  {ts:yyyy-MM-dd HH:mm} UTC → {label}");

            // === Escribir Markdown ===
            WriteMarkdownReport(outputMdPath, inRange, crashBars, reportStart, reportEnd);
            _output.WriteLine($"\nReporte escrito en: {outputMdPath}");

            // Test siempre pasa — su valor es el output, no la aserción.
            Assert.True(true);
        }

        // ==================== helpers ====================

        private static string FormatRow(
            DateTime ts,
            RegimeLabel label,
            IReadOnlyDictionary<RegimeLabel, double> probs)
        {
            string Get(RegimeLabel l) =>
                probs.TryGetValue(l, out var v) ? v.ToString("F3") : "—";

            return string.Format(
                "| {0,-22} | {1,-12} | {2,-6} | {3,-7} | {4,-7} | {5,-7} |",
                ts.ToString("yyyy-MM-dd HH:mm"),
                label,
                Get(RegimeLabel.Trend),
                Get(RegimeLabel.Squeeze),
                Get(RegimeLabel.HighVolatility),
                Get(RegimeLabel.MeanReverting));
        }

        private static void WriteMarkdownReport(
            string outputPath,
            List<(DateTime ts, RegimeLabel label, IReadOnlyDictionary<RegimeLabel, double> probs)> inRange,
            List<(DateTime ts, RegimeLabel label, IReadOnlyDictionary<RegimeLabel, double> probs)> crashBars,
            DateTime reportStart,
            DateTime reportEnd)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DEUDA-1 — Desglose granular del HMM en ventana 3");
            sb.AppendLine();
            sb.AppendLine($"**Modelo:** BTCUSDT-perp-binance.hmm.json (K=4)");
            sb.AppendLine($"**Rango analizado:** {reportStart:yyyy-MM-dd} → {reportEnd:yyyy-MM-dd}");
            sb.AppendLine($"**Barras 4h en rango:** {inRange.Count}");
            sb.AppendLine();
            sb.AppendLine("> Consulta directa al modelo serializado de producción.");
            sb.AppendLine("> Cada barra 4h es clasificada por `AccordHmmClassifier.Classify(bar)`.");
            sb.AppendLine("> El modelo usa warm-up desde 2024-12-01 (≥186 barras de precalentamiento).");
            sb.AppendLine();

            // Resumen distribución
            sb.AppendLine("## Distribución de etiquetas en el período");
            sb.AppendLine();
            sb.AppendLine("| Etiqueta | Barras 4h | % |");
            sb.AppendLine("|---|---|---|");
            var counts = inRange.GroupBy(c => c.label).OrderByDescending(g => g.Count());
            foreach (var g in counts)
            {
                double pct = 100.0 * g.Count() / inRange.Count;
                sb.AppendLine($"| {g.Key} | {g.Count()} | {pct:F1}% |");
            }
            sb.AppendLine();

            // Foco crash
            sb.AppendLine("## Foco: crash del 2025-02-02 al 2025-02-04");
            sb.AppendLine();
            sb.AppendLine($"**Barras 4h en el crash:** {crashBars.Count}");
            sb.AppendLine();
            sb.AppendLine("| Timestamp UTC | Etiqueta | Trend | Squeeze | HighVol | MeanRev |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var (ts, label, probs) in crashBars)
            {
                string Get(RegimeLabel l) =>
                    probs.TryGetValue(l, out var v) ? v.ToString("F3") : "—";
                sb.AppendLine(string.Format(
                    "| {0} | **{1}** | {2} | {3} | {4} | {5} |",
                    ts.ToString("yyyy-MM-dd HH:mm"),
                    label,
                    Get(RegimeLabel.Trend),
                    Get(RegimeLabel.Squeeze),
                    Get(RegimeLabel.HighVolatility),
                    Get(RegimeLabel.MeanReverting)));
            }
            sb.AppendLine();

            // Tabla completa
            sb.AppendLine("## Tabla cronológica completa (4h)");
            sb.AppendLine();
            sb.AppendLine("| Timestamp UTC | Etiqueta | Trend | Squeeze | HighVol | MeanRev |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var (ts, label, probs) in inRange)
            {
                string Get(RegimeLabel l) =>
                    probs.TryGetValue(l, out var v) ? v.ToString("F3") : "—";
                sb.AppendLine(string.Format(
                    "| {0} | {1} | {2} | {3} | {4} | {5} |",
                    ts.ToString("yyyy-MM-dd HH:mm"),
                    label,
                    Get(RegimeLabel.Trend),
                    Get(RegimeLabel.Squeeze),
                    Get(RegimeLabel.HighVolatility),
                    Get(RegimeLabel.MeanReverting)));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
