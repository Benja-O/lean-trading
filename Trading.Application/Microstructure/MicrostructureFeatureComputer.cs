using System;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Microstructure
{
    /// <summary>
    /// Replica exacta de la función _agg_1h() de Trading.Research/download_aggtrades.py.
    /// Convierte un AggTradeBucket en un MicrostructureBar con las 8 features microestructurales.
    ///
    /// Fórmulas (idénticas al Python):
    ///   ofi            = (buy_vol - sell_vol) / total_vol       → NaN si total_vol = 0
    ///   buy_sell_ratio = buy_vol / sell_vol                     → NaN si sell_vol = 0
    ///   cvd_delta      = buy_vol - sell_vol
    ///   cvd            = cvdRunning + cvd_delta  (acumulativo continuo desde inicio del dataset)
    ///   arrival_rate   = trade_count             (conteo crudo, no rata/seg)
    ///   mean_trade_size = sum(qty) / count
    ///   price_return   = (close - open) / open                  → NaN si open = 0
    /// </summary>
    public static class MicrostructureFeatureComputer
    {
        /// <summary>
        /// Computa features para una barra 1h.
        /// </summary>
        /// <param name="instrumentId">Instrumento.</param>
        /// <param name="barUtc">Timestamp de inicio de la barra (floor a la hora).</param>
        /// <param name="bucket">Datos acumulados de aggTrades para esta barra.</param>
        /// <param name="cvdRunning">CVD acumulado hasta el final de la barra anterior. Se suma cvd_delta de esta barra.</param>
        /// <returns>El MicrostructureBar computado y el nuevo valor de cvdRunning para la próxima barra.</returns>
        public static (MicrostructureBar Bar, double NewCvdRunning) Compute(
            InstrumentId instrumentId,
            DateTime barUtc,
            AggTradeBucket bucket,
            double cvdRunning)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));
            if (bucket is null)       throw new ArgumentNullException(nameof(bucket));

            double totalVolume = bucket.BuyVolume + bucket.SellVolume;

            // Replica Python: vol = bars["volume"].replace(0.0, float("nan"))
            double vol = totalVolume == 0.0 ? double.NaN : totalVolume;
            // Replica Python: sv  = bars["sell_volume"].replace(0.0, float("nan"))
            double sv  = bucket.SellVolume == 0.0 ? double.NaN : bucket.SellVolume;

            double ofi           = double.IsNaN(vol) ? double.NaN : (bucket.BuyVolume - bucket.SellVolume) / vol;
            double buySellRatio  = double.IsNaN(sv)  ? double.NaN : bucket.BuyVolume / sv;
            double cvdDelta      = bucket.BuyVolume - bucket.SellVolume;
            double newCvdRunning = cvdRunning + cvdDelta;
            double arrivalRate   = bucket.TradeCount;
            double priceReturn   = bucket.Open == 0.0 ? double.NaN : (bucket.Close - bucket.Open) / bucket.Open;

            var bar = new MicrostructureBar(
                instrumentId,
                barUtc,
                ofi,
                cvdDelta,
                newCvdRunning,
                arrivalRate,
                bucket.MeanTradeSize,
                buySellRatio,
                priceReturn);

            return (bar, newCvdRunning);
        }
    }
}
