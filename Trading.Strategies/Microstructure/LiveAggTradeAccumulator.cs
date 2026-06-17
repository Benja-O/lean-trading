using System;
using System.Collections.Generic;
using QuantConnect;
using QuantConnect.Data.Market;
using Trading.Application.Microstructure;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Microstructure
{
    /// <summary>
    /// Adaptador Lean-aware que recibe ticks de QuantConnect y los acumula en AggTradeBuckets de 1h,
    /// listos para que MicrostructureFeatureComputer los compute cuando cierra la barra.
    ///
    /// Clasificación de lado: lee tick.SaleCondition ("BUY"/"SELL") codificado por EmitTradeTick
    /// a partir del campo "m" (is_buyer_maker) del WebSocket aggTrade de Binance.
    ///
    /// Thread-safety: no es thread-safe. QC procesa ticks en el hilo del algoritmo (OnData),
    /// por lo que no se necesita sincronización adicional.
    /// </summary>
    public sealed class LiveAggTradeAccumulator
    {
        // instrument → (bucket_start_utc → bucket)
        private readonly Dictionary<InstrumentId, Dictionary<DateTime, AggTradeBucket>> _buckets = new();

        /// <summary>
        /// Procesa un tick de QC y lo acumula en el bucket 1h correspondiente.
        /// Solo procesa TickType.Trade; los ticks de Quote se ignoran.
        /// </summary>
        public void OnTick(InstrumentId instrumentId, Tick tick)
        {
            if (tick.TickType != TickType.Trade) return;
            if (tick.Quantity <= 0) return;

            // is_buyer_maker=true → "SELL" (seller agresivo), is_buyer_maker=false → "BUY"
            bool isBuyerMaker = string.Equals(tick.SaleCondition, "SELL", StringComparison.Ordinal);

            var bucketTime = FloorToHour(tick.Time.ToUniversalTime());

            if (!_buckets.TryGetValue(instrumentId, out var byTime))
            {
                byTime = new Dictionary<DateTime, AggTradeBucket>();
                _buckets[instrumentId] = byTime;
            }

            if (!byTime.TryGetValue(bucketTime, out var bucket))
            {
                bucket = new AggTradeBucket();
                byTime[bucketTime] = bucket;
            }

            bucket.Add(tick.Value, tick.Quantity, isBuyerMaker);
        }

        /// <summary>
        /// Retira y devuelve el bucket para el instrumento y barra indicados.
        /// Retorna null si no hay datos acumulados (feed sin ticks para esa hora).
        /// Llamar desde DataConsolidated handler ANTES de BarProcessingService.ProcessBar().
        /// </summary>
        public AggTradeBucket? TakeBucket(InstrumentId instrumentId, DateTime barUtc)
        {
            if (!_buckets.TryGetValue(instrumentId, out var byTime)) return null;

            var key = DateTime.SpecifyKind(barUtc, DateTimeKind.Utc);
            if (!byTime.TryGetValue(key, out var bucket)) return null;

            byTime.Remove(key);

            // Limpiar buckets obsoletos (> 2h en el pasado) para no acumular memoria.
            PurgeOldBuckets(byTime, barUtc);

            return bucket;
        }

        private static void PurgeOldBuckets(Dictionary<DateTime, AggTradeBucket> byTime, DateTime referenceUtc)
        {
            var cutoff = referenceUtc.AddHours(-2);
            var toRemove = new List<DateTime>(2);
            foreach (var key in byTime.Keys)
                if (key < cutoff) toRemove.Add(key);
            foreach (var key in toRemove)
                byTime.Remove(key);
        }

        private static DateTime FloorToHour(DateTime utc) =>
            new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
    }
}
