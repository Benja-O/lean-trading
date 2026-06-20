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
    /// Persiste cada barra cerrada en un CSV por (símbolo, timeframe) con nombre
    /// {ticker}_{timeframe}_live.csv dentro de un directorio configurable. El Recorder
    /// es el único escritor; Lean solo lee en Initialize().
    ///
    /// Diseño de almacenamiento:
    ///   - Una fila = una barra cerrada para un símbolo en el timeframe dado.
    ///   - Rolling window configurable: TrimOlderThan() reescribe el archivo
    ///     eliminando filas antiguas. Incluso con timeframes sub-horarios (5m = 288 filas/día)
    ///     el almacenamiento es de kilobytes por activo.
    ///   - Append-only en operación normal; reescritura solo en trim.
    ///
    /// Thread safety: no thread-safe. Una instancia por timeframe; cada instancia opera
    /// en el hilo de su dueño (Recorder o Lean, nunca ambos a la vez).
    /// </summary>
    public sealed class PersistentMicrostructureStore
    {
        private const string Header = "bar_utc,ofi,cvd_delta,cvd,arrival_rate,mean_trade_size,buy_sell_ratio,price_return";

        private readonly string _directory;
        private readonly string _timeframe;

        public PersistentMicrostructureStore(string directory, string timeframe)
        {
            _directory = directory  ?? throw new ArgumentNullException(nameof(directory));
            _timeframe = timeframe  ?? throw new ArgumentNullException(nameof(timeframe));
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
        public DateTime? GetLastBarUtc(InstrumentId instrumentId) =>
            GetLastBar(instrumentId)?.BarUtc;

        /// <summary>
        /// Retorna la última barra almacenada (incluye Cvd para sembrar el acumulado), o null si
        /// el archivo no existe / está vacío. No depende del reloj (lee la última línea de datos).
        /// </summary>
        public MicrostructureBar? GetLastBar(InstrumentId instrumentId)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));

            var file = FilePath(instrumentId);
            if (!File.Exists(file)) return null;

            string? lastDataLine = null;
            foreach (var line in File.ReadLines(file))
            {
                if (!string.IsNullOrWhiteSpace(line) &&
                    !line.StartsWith("bar_utc", StringComparison.Ordinal))
                    lastDataLine = line;
            }

            return lastDataLine is null ? null : ParseLine(instrumentId, lastDataLine);
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
            Path.Combine(_directory, $"{instrumentId.Ticker}_{_timeframe}_live.csv");

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
