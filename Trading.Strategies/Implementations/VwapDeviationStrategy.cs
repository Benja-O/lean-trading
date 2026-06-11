using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// H1 — VWAP Deviation (Long-only).
    /// Hipótesis: cuando el precio cae más de DevThr por debajo del VWAP de las últimas
    /// VwapWindow barras, hay una desviación temporal que revierte al equilibrio.
    /// Parámetros nominales M4: vwap_w=24, dev_thr=0.015, hold=4 (2/3 activos).
    /// OHLCV-only: no requiere IMicrostructureProvider.
    /// </summary>
    public sealed class VwapDeviationStrategy : IStrategy
    {
        private const int    VwapWindow = 24;    // barras en el rolling VWAP (incluye barra actual)
        private const double DevThr     = 0.015; // desviación mínima por debajo del VWAP para señal

        // Queues almacenan los N-1 = 23 valores previos; el bar actual completa las N=24 barras del VWAP
        private readonly Dictionary<string, Queue<double>> _pvBySymbol = new();
        private readonly Dictionary<string, Queue<double>> _vBySymbol  = new();

        public int WarmUpBars => VwapWindow + 2;

        public SignalDirection EvaluateSignal(MarketBar marketBar)
        {
            string ticker = marketBar.InstrumentId.Ticker;
            if (!_pvBySymbol.TryGetValue(ticker, out var pvQueue))
            {
                pvQueue = new Queue<double>(VwapWindow);
                _pvBySymbol[ticker] = pvQueue;
            }
            if (!_vBySymbol.TryGetValue(ticker, out var vQueue))
            {
                vQueue = new Queue<double>(VwapWindow);
                _vBySymbol[ticker] = vQueue;
            }

            double close  = (double)marketBar.Close;
            double volume = (double)marketBar.Volume;
            double pv     = close * volume;

            // El VWAP de N barras incluye la barra actual; la queue mantiene N-1 previos
            bool isWarmedUp = pvQueue.Count >= VwapWindow - 1;

            if (!isWarmedUp)
            {
                pvQueue.Enqueue(pv);
                vQueue.Enqueue(volume);
                return SignalDirection.Flat;
            }

            // Suma sobre N-1 previos + barra actual = N barras
            double sumPV = pv, sumV = volume;
            foreach (var v in pvQueue) sumPV += v;
            foreach (var v in vQueue) sumV  += v;

            // Mantener la queue en N-1 barras previas (deslizar ventana)
            pvQueue.Dequeue();
            vQueue.Dequeue();
            pvQueue.Enqueue(pv);
            vQueue.Enqueue(volume);

            if (sumV <= 0) return SignalDirection.Flat;

            double vwap = sumPV / sumV;
            if (vwap <= 0) return SignalDirection.Flat;

            if ((close - vwap) / vwap < -DevThr)
                return SignalDirection.Long;

            return SignalDirection.Flat;
        }
    }
}
