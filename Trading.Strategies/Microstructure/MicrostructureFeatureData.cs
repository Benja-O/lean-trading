using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using QuantConnect;
using QuantConnect.Data;

namespace Trading.Strategies.Microstructure
{
    /// <summary>
    /// Custom data source de Lean que streamea barras de microestructura (OHLCV + las 7 features
    /// derivadas de aggTrades) por el MISMO camino en backtest y en live (ADR-053). Reemplaza el
    /// cómputo en vivo desde el WebSocket (LiveAggTradeAccumulator), cuyo transporte ADR-049 probó
    /// irreparable, y unifica el camino de datos para cerrar la paridad backtest/live.
    ///
    /// Fuente:
    ///   - Backtest → CSV histórico de research ({ticker}_1h_features.csv).
    ///   - Live     → CSV del store del Recorder ({ticker}_1h_live.csv); el Recorder es el escritor
    ///                único (ADR-048) y lo materializa por REST polling de aggTrades (ADR-049).
    ///
    /// Los dos CSV tienen distinto orden de columnas y formato de timestamp, así que <see cref="Reader"/>
    /// ramifica por isLiveMode (patrón canónico de Lean, ver CustomDataBitcoinAlgorithm). El timestamp
    /// de la fila es el INICIO de la barra (floor de la hora — convención de ambos productores:
    /// download_aggtrades.py usa dt.floor("1h"), el Recorder usa FloorToWindow). EndTime = inicio + 1h
    /// para que el motor live entregue el dato recién al cierre de la barra.
    ///
    /// Se registra a Resolution.Minute (no Hour): el custom data live se pollea cada Min(resolución,
    /// 30min); a Hour serían 30 min de latencia. A Minute el poll baja a ~1 min y, como el dato es
    /// sparse (una fila/hora) con IsSparseData()=true, el frontier-dedup por EndTime de Lean emite cada
    /// barra una sola vez.
    /// </summary>
    public class MicrostructureFeatureData : BaseData
    {
        // Columnas del CSV de research (backtest), índices 0-based — ver MicrostructureRegistry/ParseLine:
        // 0:bar 1:open 2:high 3:low 4:close 5:volume 6:buy_volume 7:sell_volume 8:trade_count
        // 9:mean_trade_size 10:ofi 11:buy_sell_ratio 12:cvd_delta 13:arrival_rate 14:price_return 15:cvd
        private const int ResearchColumns = 16;

        // Columnas del CSV del store (live), índices 0-based — ver PersistentMicrostructureStore.Header:
        // 0:bar_utc 1:ofi 2:cvd_delta 3:cvd 4:arrival_rate 5:mean_trade_size 6:buy_sell_ratio
        // 7:price_return 8:open 9:high 10:low 11:close 12:volume
        private const int StoreColumns = 13;

        // OHLCV → decimal (precios/volumen, AI.md). Features → double (paridad con MicrostructureBar del dominio).
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public double Ofi { get; set; }
        public double CvdDelta { get; set; }
        public double Cvd { get; set; }
        public double ArrivalRate { get; set; }
        public double MeanTradeSize { get; set; }
        public double BuySellRatio { get; set; }
        public double PriceReturn { get; set; }

        /// <summary>EndTime distinto de Time: la barra abarca [Time, Time+1h). Ver remarks de la clase.</summary>
        public override DateTime EndTime { get; set; }

        public MicrostructureFeatureData() { }

        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            string ticker = config.Symbol.Value;
            string directory = isLiveMode ? LiveStoreDirectory() : BacktestFeaturesDirectory();
            string fileName  = isLiveMode ? $"{ticker}_1h_live.csv" : $"{ticker}_1h_features.csv";
            return new SubscriptionDataSource(
                Path.Combine(directory, fileName), SubscriptionTransportMedium.LocalFile, FileFormat.Csv);
        }

        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;

            var parts = line.Split(',');
            return isLiveMode ? ReadStoreRow(config, parts) : ReadResearchRow(config, parts);
        }

        // ── Parsing por formato ──────────────────────────────────────────────────────

        private static BaseData ReadResearchRow(SubscriptionDataConfig config, string[] parts)
        {
            if (parts.Length < ResearchColumns) return null; // header u OHLCV-less → descartar
            try
            {
                var barStart = ParseBarStart(parts[0]);
                return Build(
                    config, barStart,
                    open:          ParseDecimal(parts[1]),
                    high:          ParseDecimal(parts[2]),
                    low:           ParseDecimal(parts[3]),
                    close:         ParseDecimal(parts[4]),
                    volume:        ParseDecimal(parts[5]),
                    meanTradeSize: ParseDouble(parts[9]),
                    ofi:           ParseDouble(parts[10]),
                    buySellRatio:  ParseDouble(parts[11]),
                    cvdDelta:      ParseDouble(parts[12]),
                    arrivalRate:   ParseDouble(parts[13]),
                    priceReturn:   ParseDouble(parts[14]),
                    cvd:           ParseDouble(parts[15]));
            }
            catch { return null; } // header o línea corrupta
        }

        private static BaseData ReadStoreRow(SubscriptionDataConfig config, string[] parts)
        {
            if (parts.Length < StoreColumns) return null; // header o fila vieja sin OHLCV → descartar
            try
            {
                var barStart = ParseBarStart(parts[0]);
                return Build(
                    config, barStart,
                    open:          ParseDecimal(parts[8]),
                    high:          ParseDecimal(parts[9]),
                    low:           ParseDecimal(parts[10]),
                    close:         ParseDecimal(parts[11]),
                    volume:        ParseDecimal(parts[12]),
                    meanTradeSize: ParseDouble(parts[5]),
                    ofi:           ParseDouble(parts[1]),
                    buySellRatio:  ParseDouble(parts[6]),
                    cvdDelta:      ParseDouble(parts[2]),
                    arrivalRate:   ParseDouble(parts[4]),
                    priceReturn:   ParseDouble(parts[7]),
                    cvd:           ParseDouble(parts[3]));
            }
            catch { return null; }
        }

        private static MicrostructureFeatureData Build(
            SubscriptionDataConfig config, DateTime barStart,
            decimal open, decimal high, decimal low, decimal close, decimal volume,
            double ofi, double cvdDelta, double cvd, double arrivalRate,
            double meanTradeSize, double buySellRatio, double priceReturn)
        {
            return new MicrostructureFeatureData
            {
                Symbol  = config.Symbol,
                Time    = barStart,                  // inicio de la barra
                EndTime = barStart.AddHours(1),      // cierre: el motor live entrega aquí
                Value   = close,                     // precio de referencia de BaseData
                Open = open, High = high, Low = low, Close = close, Volume = volume,
                Ofi = ofi, CvdDelta = cvdDelta, Cvd = cvd, ArrivalRate = arrivalRate,
                MeanTradeSize = meanTradeSize, BuySellRatio = buySellRatio, PriceReturn = priceReturn,
            };
        }

        // ── Comportamiento del custom data ─────────────────────────────────────────────

        /// <summary>Una fila por hora: dato sparse, sin fill-forward (no inventar barras inexistentes).</summary>
        public override bool IsSparseData() => true;

        /// <summary>Default si AddData no especifica resolución. Minute para que el poll live sea ~1 min.</summary>
        public override Resolution DefaultResolution() => Resolution.Minute;

        public override List<Resolution> SupportedResolutions() =>
            new() { Resolution.Minute, Resolution.Hour };

        // ── Helpers de parseo ───────────────────────────────────────────────────────

        // Ambos formatos etiquetan la barra por su inicio (floor de la hora). Research: "2021-01-01
        // 00:00:00+00:00". Store: "2026-06-24T15:00:00Z". DateTimeOffset.Parse maneja ambos offsets.
        private static DateTime ParseBarStart(string raw) =>
            DateTimeOffset.Parse(raw.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal).UtcDateTime;

        private static decimal ParseDecimal(string value) =>
            decimal.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0m;

        private static double ParseDouble(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0 || trimmed.Equals("nan", StringComparison.OrdinalIgnoreCase))
                return double.NaN;
            return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
        }

        private static string LiveStoreDirectory() =>
            Environment.GetEnvironmentVariable("MICROSTRUCTURE_STORE_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "microstructure-live");

        private static string BacktestFeaturesDirectory() =>
            Environment.GetEnvironmentVariable("MICROSTRUCTURE_FEATURES_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "microstructure");
    }
}
