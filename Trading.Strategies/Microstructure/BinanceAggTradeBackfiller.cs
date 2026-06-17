using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Trading.Application.Microstructure;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Microstructure
{
    /// <summary>
    /// Backfiller de features microestructurales vía API REST de Binance Futures.
    ///
    /// Resuelve el problema de warmup al reiniciar el sistema: en lugar de depender de un CSV
    /// estático (que envejece) o esperar N horas de WebSocket en vivo, consulta
    /// /fapi/v1/aggTrades para el período de gap y computa las features con exacta paridad
    /// al pipeline en vivo (mismos AggTradeBucket + MicrostructureFeatureComputer).
    ///
    /// Paridad con el CSV histórico: /fapi/v1/aggTrades usa el campo "m" (is_buyer_maker),
    /// idéntico a lo que el WebSocket emite en tiempo real. Los features derivados de
    /// arrival_rate y mean_trade_size usan conteo de aggTrades (no trades individuales),
    /// igual que el Python de referencia (download_aggtrades.py).
    ///
    /// Paginación: el endpoint retorna hasta 1000 registros por request. Para horas con
    /// más de 1000 aggTrades (BTC activo), se pagina con fromId hasta cubrir toda la ventana.
    ///
    /// Manejo de errores: cualquier fallo HTTP se loggea y retorna los bars computados
    /// hasta ese punto (el warmup usará datos parciales + fallback al CSV histórico).
    ///
    /// Thread safety: no thread-safe (se llama desde Initialize(), hilo del algoritmo QC).
    /// </summary>
    public sealed class BinanceAggTradeBackfiller
    {
        private readonly Func<string, string> _fetch;
        private readonly string _baseUrl;

        public BinanceAggTradeBackfiller(HttpClient httpClient, string fapiBaseUrl)
            : this(
                url => httpClient.GetStringAsync(url).GetAwaiter().GetResult(),
                fapiBaseUrl)
        { }

        // Constructor para tests: inyecta el fetcher sin necesidad de HttpClient real.
        public BinanceAggTradeBackfiller(Func<string, string> fetch, string fapiBaseUrl)
        {
            _fetch   = fetch    ?? throw new ArgumentNullException(nameof(fetch));
            _baseUrl = (fapiBaseUrl ?? "https://fapi.binance.com").TrimEnd('/');
        }

        /// <summary>
        /// Computa features microestructurales para cada barra 1h en [fromUtc, toUtc) descargando
        /// aggTrades desde la API REST de Binance Futures.
        /// </summary>
        /// <param name="instrumentId">InstrumentId del símbolo.</param>
        /// <param name="binanceSymbol">Símbolo en formato Binance (ej: "BTCUSDT").</param>
        /// <param name="fromUtc">Inicio del período (inclusive, se redondea hacia abajo a hora).</param>
        /// <param name="toUtc">Fin del período (exclusivo, se redondea hacia abajo a hora).</param>
        /// <param name="cvdRunning">Valor de CVD acumulado al inicio del período.</param>
        /// <returns>Lista de barras computadas en orden cronológico. Puede estar vacía si la API falla.</returns>
        public IReadOnlyList<MicrostructureBar> Backfill(
            InstrumentId instrumentId,
            string binanceSymbol,
            DateTime fromUtc,
            DateTime toUtc,
            double cvdRunning)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));
            if (string.IsNullOrWhiteSpace(binanceSymbol)) throw new ArgumentException("binanceSymbol requerido.", nameof(binanceSymbol));

            var results = new List<MicrostructureBar>();
            var current = FloorToHour(fromUtc);
            var end     = FloorToHour(toUtc);

            while (current < end)
            {
                try
                {
                    var bucket = FetchHourBucket(binanceSymbol, current, current.AddHours(1));

                    if (bucket.HasData)
                    {
                        var (bar, newCvd) = MicrostructureFeatureComputer.Compute(instrumentId, current, bucket, cvdRunning);
                        cvdRunning = newCvd;
                        results.Add(bar);
                    }
                }
                catch (Exception ex)
                {
                    // Fallo parcial: loggear y continuar. Las horas no cubiertas quedan
                    // en blanco; GetBar() caerá al CSV histórico para esas barras.
                    Console.Error.WriteLine(
                        $"[BinanceAggTradeBackfiller] Error al backfillear {binanceSymbol} {current:yyyy-MM-dd HH:mm} UTC: {ex.Message}");
                    // No relanzar: el startup no debe fallar por indisponibilidad de la API.
                }

                current = current.AddHours(1);

                // Rate limiting: Binance Futures limita a 2400 weight/min; aggTrades cuesta
                // 20 weight/request → máximo 120 req/min. 400 ms = 150 req/min → margen seguro.
                System.Threading.Thread.Sleep(400);
            }

            return results;
        }

        private AggTradeBucket FetchHourBucket(string symbol, DateTime windowStart, DateTime windowEnd)
        {
            var bucket  = new AggTradeBucket();
            long startMs = ToUnixMs(windowStart);
            long endMs   = ToUnixMs(windowEnd);
            long? fromId = null;

            while (true)
            {
                string url = fromId.HasValue
                    ? $"{_baseUrl}/fapi/v1/aggTrades?symbol={symbol}&fromId={fromId}&limit=1000"
                    : $"{_baseUrl}/fapi/v1/aggTrades?symbol={symbol}&startTime={startMs}&endTime={endMs - 1}&limit=1000";

                var trades = ParseAggTradeResponse(_fetch(url));
                if (trades.Count == 0) break;

                long? lastIdInWindow = null;
                foreach (var t in trades)
                {
                    if (t.Timestamp < startMs || t.Timestamp >= endMs) continue;
                    bucket.Add((decimal)t.Price, (decimal)t.Quantity, t.IsBuyerMaker);
                    lastIdInWindow = t.AggTradeId;
                }

                // Página completa y había trades dentro de la ventana → puede haber más.
                if (trades.Count < 1000) break;
                if (lastIdInWindow is null) break;
                fromId = lastIdInWindow.Value + 1;
            }

            return bucket;
        }

        private static List<AggTradeRecord> ParseAggTradeResponse(string json)
        {
            var result = new List<AggTradeRecord>();
            using var doc = JsonDocument.Parse(json);

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                result.Add(new AggTradeRecord(
                    AggTradeId:    element.GetProperty("a").GetInt64(),
                    Price:         double.Parse(element.GetProperty("p").GetString()!, CultureInfo.InvariantCulture),
                    Quantity:      double.Parse(element.GetProperty("q").GetString()!, CultureInfo.InvariantCulture),
                    Timestamp:     element.GetProperty("T").GetInt64(),
                    IsBuyerMaker:  element.GetProperty("m").GetBoolean()
                ));
            }

            return result;
        }

        private static DateTime FloorToHour(DateTime utc) =>
            new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);

        private static long ToUnixMs(DateTime utc) =>
            (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;

        private readonly record struct AggTradeRecord(
            long AggTradeId,
            double Price,
            double Quantity,
            long Timestamp,
            bool IsBuyerMaker);
    }
}
