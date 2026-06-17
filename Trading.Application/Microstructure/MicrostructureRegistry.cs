using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Microstructure
{
    /// <summary>
    /// Carga features microestructurales desde CSV (generado por Research/download_aggtrades.py)
    /// y sirve lookups O(1) por (InstrumentId, DateTime).
    ///
    /// Ciclo de vida: cargado una vez en Initialize(), inmutable después.
    /// Formato CSV esperado (con header):
    ///   bar,ofi,cvd_delta,cvd,arrival_rate,mean_trade_size,buy_sell_ratio,price_return
    ///   2021-01-01 00:00:00+00:00,0.123,...
    /// </summary>
    public sealed class MicrostructureRegistry : IMicrostructureProvider
    {
        private readonly Dictionary<InstrumentId, Dictionary<DateTime, MicrostructureBar>> _data = new();
        private readonly ITradingLogger _logger;

        public MicrostructureRegistry(ITradingLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Carga el CSV de un instrumento. Puede llamarse múltiples veces para distintos instrumentos.
        /// Si el archivo no existe, loguea Warning y continúa (el proveedor devuelve null para ese instrumento).
        /// </summary>
        public void Load(InstrumentId instrumentId, string csvPath)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));
            if (string.IsNullOrWhiteSpace(csvPath)) throw new ArgumentException("csvPath no puede estar vacío.", nameof(csvPath));

            if (!File.Exists(csvPath))
            {
                _logger.Warning(
                    "MicrostructureRegistry: archivo CSV no encontrado para {Ticker} en '{Path}'. " +
                    "Las estrategias que requieran features microestructurales recibirán null.",
                    instrumentId.Ticker, csvPath);
                return;
            }

            var index = new Dictionary<DateTime, MicrostructureBar>();
            int linesRead = 0;
            int parseErrors = 0;

            using var reader = new StreamReader(csvPath);
            string? header = reader.ReadLine(); // saltar header
            if (header is null)
            {
                _logger.Warning("MicrostructureRegistry: CSV vacío para {Ticker}.", instrumentId.Ticker);
                return;
            }

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                linesRead++;
                var bar = ParseLine(line, instrumentId, linesRead + 1);
                if (bar is null) { parseErrors++; continue; }

                // Normalizar a UTC sin offset para lookup consistente
                var key = DateTime.SpecifyKind(bar.BarUtc, DateTimeKind.Utc);
                index[key] = bar;
            }

            if (index.Count == 0)
            {
                _logger.Warning("MicrostructureRegistry: CSV sin barras para {Ticker} (solo header o todo inválido).", instrumentId.Ticker);
                return;
            }

            _data[instrumentId] = index;
            _logger.Info(
                "MicrostructureRegistry: {Ticker} cargado — {Bars} barras 1h, {Errors} errores de parseo.",
                instrumentId.Ticker, index.Count, parseErrors);
        }

        public MicrostructureBar? GetBar(InstrumentId instrumentId, DateTime barUtc)
        {
            if (!_data.TryGetValue(instrumentId, out var index)) return null;
            var key = DateTime.SpecifyKind(barUtc, DateTimeKind.Utc);
            return index.TryGetValue(key, out var bar) ? bar : null;
        }

        public bool HasDataFor(InstrumentId instrumentId) => _data.ContainsKey(instrumentId);

        /// <summary>
        /// Devuelve el CVD acumulado de la última barra histórica del instrumento.
        /// Usado para sembrar el CVD running del LiveMicrostructureProvider al arrancar en live.
        /// Retorna 0.0 si no hay datos cargados.
        /// </summary>
        public double GetLastCvd(InstrumentId instrumentId)
        {
            if (instrumentId is null) return 0.0;
            if (!_data.TryGetValue(instrumentId, out var index) || index.Count == 0) return 0.0;

            MicrostructureBar? latest = null;
            foreach (var bar in index.Values)
            {
                if (latest is null || bar.BarUtc > latest.BarUtc)
                    latest = bar;
            }
            return latest?.Cvd ?? 0.0;
        }

        // ── Parsing ──────────────────────────────────────────────────────────────

        // Columnas del CSV (índices 0-based):
        // 0:bar  1:open  2:high  3:low  4:close  5:volume
        // 6:buy_volume  7:sell_volume  8:trade_count  9:mean_trade_size
        // 10:ofi  11:buy_sell_ratio  12:cvd_delta  13:arrival_rate  14:price_return  15:cvd
        private MicrostructureBar? ParseLine(string line, InstrumentId instrumentId, int lineNumber)
        {
            var parts = line.Split(',');
            if (parts.Length < 16)
            {
                _logger.Warning(
                    "MicrostructureRegistry [{Ticker}] línea {Line}: columnas insuficientes ({Count}), esperadas 16.",
                    instrumentId.Ticker, lineNumber, parts.Length);
                return null;
            }

            try
            {
                // El timestamp del Parquet/CSV tiene formato: "2021-01-01 00:00:00+00:00"
                var barUtc = DateTimeOffset.Parse(parts[0].Trim(), CultureInfo.InvariantCulture).UtcDateTime;

                double ofi           = ParseDouble(parts[10]);
                double cvdDelta      = ParseDouble(parts[12]);
                double cvd           = ParseDouble(parts[15]);
                double arrivalRate   = ParseDouble(parts[13]);
                double meanTradeSize = ParseDouble(parts[9]);
                double buySellRatio  = ParseDouble(parts[11]);
                double priceReturn   = ParseDouble(parts[14]);

                return new MicrostructureBar(
                    instrumentId, barUtc,
                    ofi, cvdDelta, cvd,
                    arrivalRate, meanTradeSize, buySellRatio, priceReturn);
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "MicrostructureRegistry [{Ticker}] línea {Line}: error de parseo — {Message}",
                    instrumentId.Ticker, lineNumber, ex.Message);
                return null;
            }
        }

        private static double ParseDouble(string value)
        {
            var trimmed = value.Trim();
            if (trimmed == "" || trimmed.Equals("nan", StringComparison.OrdinalIgnoreCase))
                return double.NaN;
            return double.Parse(trimmed, CultureInfo.InvariantCulture);
        }
    }
}
