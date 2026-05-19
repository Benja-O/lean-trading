using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Regimes
{
    /// <summary>
    /// Parsea archivos CSV de Binance Klines (formato oficial de https://data.binance.vision/),
    /// incluyendo los zip mensuales (ej. <c>2020-01.zip</c> con un único CSV dentro).
    ///
    /// Formato esperado (12 columnas, sin header en archivos oficiales recientes; el parser
    /// detecta y descarta header si existe):
    /// <list type="number">
    ///   <item>OpenTime (epoch ms, UTC)</item>
    ///   <item>Open (decimal)</item>
    ///   <item>High</item>
    ///   <item>Low</item>
    ///   <item>Close</item>
    ///   <item>Volume</item>
    ///   <item>CloseTime (epoch ms)</item>
    ///   <item>QuoteAssetVolume</item>
    ///   <item>NumberOfTrades</item>
    ///   <item>TakerBuyBaseAssetVolume</item>
    ///   <item>TakerBuyQuoteAssetVolume</item>
    ///   <item>Ignore</item>
    /// </list>
    /// El parser usa la columna <see cref="OpenTime"/> como timestamp del bar (convertido a
    /// <see cref="DateTime"/> UTC).
    /// </summary>
    public sealed class BinanceKlinesParser
    {
        private const int ExpectedColumnCount = 12;

        private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public InstrumentId InstrumentId { get; }

        public BinanceKlinesParser(InstrumentId instrumentId)
        {
            InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
        }

        /// <summary>
        /// Parsea TODOS los archivos zip del directorio (orden alfabético del nombre)
        /// y devuelve las barras dentro del rango [startUtc, endUtc] (inclusive ambos extremos).
        /// </summary>
        public IReadOnlyList<MarketBar> ParseDirectory(string directoryPath, DateTime startUtc, DateTime endUtc)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)) throw new ArgumentException("Directorio vacío.", nameof(directoryPath));
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"No se encontró el directorio '{directoryPath}'.");
            if (endUtc < startUtc)
                throw new ArgumentException("endUtc debe ser >= startUtc.", nameof(endUtc));

            var zipPaths = Directory.GetFiles(directoryPath, "*.zip").OrderBy(p => p, StringComparer.Ordinal).ToList();
            if (zipPaths.Count == 0)
                throw new InvalidOperationException($"No se encontraron archivos .zip en '{directoryPath}'.");

            var result = new List<MarketBar>(capacity: 16384);
            foreach (var zipPath in zipPaths)
                result.AddRange(ParseZipFile(zipPath, startUtc, endUtc));

            return result;
        }

        public IReadOnlyList<MarketBar> ParseZipFile(string zipPath, DateTime startUtc, DateTime endUtc)
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException($"No se encontró el archivo '{zipPath}'.", zipPath);

            using var fileStream = File.OpenRead(zipPath);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var result = new List<MarketBar>();
            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0) continue;
                using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                result.AddRange(ParseTextReader(reader, startUtc, endUtc));
            }
            return result;
        }

        public IReadOnlyList<MarketBar> ParseTextReader(TextReader reader, DateTime startUtc, DateTime endUtc)
        {
            if (reader is null) throw new ArgumentNullException(nameof(reader));
            var bars = new List<MarketBar>();
            string? line;
            bool firstLine = true;
            int lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (firstLine)
                {
                    firstLine = false;
                    if (IsHeader(line)) continue;
                }

                var bar = ParseRow(line, lineNumber);
                if (bar.TimestampUtc < startUtc || bar.TimestampUtc > endUtc) continue;
                bars.Add(bar);
            }
            return bars;
        }

        private static bool IsHeader(string line)
        {
            // El header oficial empieza con "open_time" o similar (texto no numérico).
            // Heurística: si la primera columna no parsea como long, asumimos header.
            int firstSeparator = line.IndexOf(',');
            string firstColumn = firstSeparator < 0 ? line : line.Substring(0, firstSeparator);
            return !long.TryParse(firstColumn.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        private MarketBar ParseRow(string line, int lineNumber)
        {
            var columns = line.Split(',');
            if (columns.Length != ExpectedColumnCount)
                throw new FormatException(
                    $"Línea {lineNumber}: se esperaban {ExpectedColumnCount} columnas, se encontraron {columns.Length}. " +
                    "Verificar formato de Binance Klines.");

            long openTimeMs = ParseLong(columns[0], lineNumber, "OpenTime");
            decimal open = ParseDecimal(columns[1], lineNumber, "Open");
            decimal high = ParseDecimal(columns[2], lineNumber, "High");
            decimal low = ParseDecimal(columns[3], lineNumber, "Low");
            decimal close = ParseDecimal(columns[4], lineNumber, "Close");
            decimal volume = ParseDecimal(columns[5], lineNumber, "Volume");

            if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                throw new FormatException(
                    $"Línea {lineNumber}: precios deben ser estrictamente positivos " +
                    $"(open={open}, high={high}, low={low}, close={close}).");
            if (volume < 0)
                throw new FormatException($"Línea {lineNumber}: volumen no puede ser negativo (vol={volume}).");

            var timestampUtc = UnixEpoch.AddMilliseconds(openTimeMs);
            return new MarketBar(InstrumentId, open, high, low, close, volume, timestampUtc);
        }

        private static long ParseLong(string raw, int lineNumber, string fieldName)
        {
            if (!long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
                throw new FormatException(
                    $"Línea {lineNumber}: campo '{fieldName}' inválido (valor crudo: '{raw}').");
            return value;
        }

        private static decimal ParseDecimal(string raw, int lineNumber, string fieldName)
        {
            if (!decimal.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value))
                throw new FormatException(
                    $"Línea {lineNumber}: campo '{fieldName}' inválido (valor crudo: '{raw}').");
            return value;
        }
    }
}
