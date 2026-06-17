using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Microstructure
{
    /// <summary>
    /// Almacén local de features microestructurales computadas en vivo.
    ///
    /// Persiste cada barra 1h cerrada en un CSV por símbolo ({ticker}_live.csv) dentro
    /// de un directorio configurable. En cada reinicio del sistema, carga las últimas
    /// N horas desde disco para cubrir el warmup de estrategias sin depender del CSV
    /// histórico ni de un backfill REST completo.
    ///
    /// Diseño de almacenamiento:
    ///   - Una fila = una barra 1h cerrada para un símbolo.
    ///   - Rolling window de 7 días (máximo): TrimOlderThan() reescribe el archivo
    ///     eliminando filas antiguas. El archivo nunca supera ~170 filas × ~150 bytes ≈ 25 KB.
    ///   - Append-only en operación normal; reescritura solo en trim.
    ///
    /// Thread safety: no thread-safe. El caller (TradingAlgorithmHost) opera
    /// en el hilo del algoritmo QC, que es single-threaded.
    /// </summary>
    public sealed class PersistentMicrostructureStore
    {
        private const string Header = "bar_utc,ofi,cvd_delta,cvd,arrival_rate,mean_trade_size,buy_sell_ratio,price_return";

        private readonly string _directory;

        public PersistentMicrostructureStore(string directory)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            Directory.CreateDirectory(directory);
        }

        /// <summary>
        /// Carga las barras de las últimas <paramref name="hours"/> horas desde disco.
        /// Retorna lista vacía si el archivo no existe o no tiene datos recientes.
        /// </summary>
        public IReadOnlyList<MicrostructureBar> LoadRecent(InstrumentId instrumentId, int hours)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));

            var file = FilePath(instrumentId);
            if (!File.Exists(file)) return Array.Empty<MicrostructureBar>();

            var cutoff = DateTime.UtcNow.AddHours(-hours);
            var result = new List<MicrostructureBar>();

            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("bar_utc", StringComparison.Ordinal))
                    continue;

                var bar = ParseLine(instrumentId, line);
                if (bar != null && bar.BarUtc >= cutoff)
                    result.Add(bar);
            }

            return result;
        }

        /// <summary>
        /// Retorna el BarUtc de la última barra almacenada, o null si el archivo no existe / está vacío.
        /// Usado para calcular el gap a backfillear en cada reinicio.
        /// </summary>
        public DateTime? GetLastBarUtc(InstrumentId instrumentId)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));

            var file = FilePath(instrumentId);
            if (!File.Exists(file)) return null;

            string lastDataLine = null;
            foreach (var line in File.ReadLines(file))
            {
                if (!string.IsNullOrWhiteSpace(line) &&
                    !line.StartsWith("bar_utc", StringComparison.Ordinal))
                    lastDataLine = line;
            }

            return lastDataLine is null ? null : ParseLine(instrumentId, lastDataLine)?.BarUtc;
        }

        /// <summary>
        /// Agrega una barra al archivo. Crea el archivo con header si no existe.
        /// </summary>
        public void Append(MicrostructureBar bar)
        {
            if (bar is null) throw new ArgumentNullException(nameof(bar));

            var file = FilePath(bar.InstrumentId);
            bool isNew = !File.Exists(file);

            using var writer = new StreamWriter(file, append: true);
            if (isNew) writer.WriteLine(Header);
            writer.WriteLine(FormatLine(bar));
        }

        /// <summary>
        /// Elimina filas más antiguas que <paramref name="cutoff"/> reescribiendo el archivo.
        /// Si no hay filas que eliminar, no escribe nada. Si el archivo no existe, no hace nada.
        /// </summary>
        public void TrimOlderThan(InstrumentId instrumentId, DateTime cutoff)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));

            var file = FilePath(instrumentId);
            if (!File.Exists(file)) return;

            var allLines = File.ReadAllLines(file);
            var kept = allLines
                .Where(l => l.StartsWith("bar_utc", StringComparison.Ordinal) || IsOnOrAfterCutoff(l, cutoff))
                .ToArray();

            if (kept.Length < allLines.Length)
                File.WriteAllLines(file, kept);
        }

        private string FilePath(InstrumentId instrumentId) =>
            Path.Combine(_directory, $"{instrumentId.Ticker}_live.csv");

        private static string FormatLine(MicrostructureBar bar) =>
            string.Create(CultureInfo.InvariantCulture, $"{bar.BarUtc:yyyy-MM-ddTHH:mm:ssZ},{bar.Ofi:R},{bar.CvdDelta:R},{bar.Cvd:R},{bar.ArrivalRate:R},{bar.MeanTradeSize:R},{bar.BuySellRatio:R},{bar.PriceReturn:R}");

        private static MicrostructureBar? ParseLine(InstrumentId instrumentId, string line)
        {
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 8) return null;

                return new MicrostructureBar(
                    instrumentId:  instrumentId,
                    barUtc:        DateTime.Parse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    ofi:           ParseDouble(parts[1]),
                    cvdDelta:      ParseDouble(parts[2]),
                    cvd:           ParseDouble(parts[3]),
                    arrivalRate:   ParseDouble(parts[4]),
                    meanTradeSize: ParseDouble(parts[5]),
                    buySellRatio:  ParseDouble(parts[6]),
                    priceReturn:   ParseDouble(parts[7])
                );
            }
            catch
            {
                return null;
            }
        }

        private static double ParseDouble(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;

        private static bool IsOnOrAfterCutoff(string line, DateTime cutoff)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            var comma = line.IndexOf(',');
            if (comma < 0) return false;
            return DateTime.TryParse(
                line.AsSpan(0, comma),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dt) && dt >= cutoff;
        }
    }
}
