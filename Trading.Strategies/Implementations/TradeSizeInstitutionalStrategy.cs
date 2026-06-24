using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// H5 — Trade Size Institutional (Long-only).
    /// Hipótesis: cuando el tamaño medio de trade está en el percentil superior
    /// (flujo institucional) Y el ratio buy/sell es positivo (acumulación neta),
    /// hay acumulación institucional silenciosa → precio sube.
    /// Parámetros nominales M4: size_w=24, pct=0.90, bsr>1.02, hold=8 (3/3 activos).
    /// </summary>
    public sealed class TradeSizeInstitutionalStrategy : IStrategy, ISignalDiagnosticsProvider
    {
        private const int SizeWindow               = 24;
        private const double SizePct               = 0.90;
        private const double BuySellRatioThreshold = 1.02;

        private readonly IMicrostructureProvider? _microstructure;
        private readonly Dictionary<string, Queue<double>> _historyBySymbol = new();

        // Rationale de la última evaluación (ISignalDiagnosticsProvider). Single-threaded:
        // se sobreescribe en cada EvaluateSignal y el host lo lee inmediatamente después.
        private SignalDiagnostics? _lastDiagnostics;

        public int WarmUpBars => SizeWindow + 2;

        public TradeSizeInstitutionalStrategy(IMicrostructureProvider? microstructure = null)
        {
            _microstructure = microstructure;
        }

        public SignalDirection EvaluateSignal(MarketBar marketBar)
        {
            var msBar = _microstructure?.GetBar(marketBar.InstrumentId, marketBar.TimestampUtc);
            if (msBar == null)
            {
                _lastDiagnostics = new SignalDiagnostics(
                    "Sin barra microestructural para el timestamp — Flat.",
                    new List<SignalCondition>());
                return SignalDirection.Flat;
            }

            string ticker = marketBar.InstrumentId.Ticker;
            if (!_historyBySymbol.TryGetValue(ticker, out var history))
            {
                history = new Queue<double>(SizeWindow + 1);
                _historyBySymbol[ticker] = history;
            }

            double meanSize   = msBar.MeanTradeSize;
            double buySellRat = msBar.BuySellRatio;

            bool isWarmedUp = history.Count >= SizeWindow;
            double pct = isWarmedUp ? PercentileRank(history, meanSize) : 0.0;
            // Valor P90 absoluto de la ventana, solo para auditar el log ("mean_trade_size ≥ P90=Y").
            // La DECISIÓN sigue siendo por rango percentil (pct >= SizePct), no por este umbral.
            double p90Value = isWarmedUp ? PercentileValue(history, SizePct) : 0.0;

            if (history.Count >= SizeWindow)
                history.Dequeue();
            history.Enqueue(meanSize);

            if (!isWarmedUp)
            {
                _lastDiagnostics = new SignalDiagnostics(
                    $"Warmup: {history.Count}/{SizeWindow} mean_trade_size — Flat.",
                    new List<SignalCondition>());
                return SignalDirection.Flat;
            }

            bool sizeInTopPct = pct >= SizePct;
            bool bsrAbove = buySellRat > BuySellRatioThreshold;
            bool fired = sizeInTopPct && bsrAbove;

            _lastDiagnostics = new SignalDiagnostics(
                fired
                    ? "TradeSizeInstitutional Long: mean_trade_size en P90+ ∧ buy_sell_ratio>1.02."
                    : "TradeSizeInstitutional Flat: no se cumple (mean_trade_size en P90+) ∧ (bsr>1.02).",
                new List<SignalCondition>
                {
                    new("MeanTradeSizeGeP90", meanSize, ">=", p90Value, sizeInTopPct),
                    new("BuySellRatioAboveThreshold", buySellRat, ">", BuySellRatioThreshold, bsrAbove),
                });

            return fired ? SignalDirection.Long : SignalDirection.Flat;
        }

        public SignalDiagnostics? DescribeLastEvaluation() => _lastDiagnostics;

        private static double PercentileRank(Queue<double> history, double value)
        {
            int below = 0;
            foreach (var v in history)
                if (v < value) below++;
            return (double)below / history.Count;
        }

        /// <summary>
        /// Valor en el cuantil q de la ventana (interpolación lineal sobre los datos ordenados,
        /// método 'linear' de numpy). Solo para enriquecer el log de auditoría con un umbral
        /// absoluto legible; no participa de la decisión de señal.
        /// </summary>
        private static double PercentileValue(Queue<double> history, double q)
        {
            var sorted = new List<double>(history);
            sorted.Sort();
            int n = sorted.Count;
            if (n == 0) return 0.0;
            if (n == 1) return sorted[0];

            double rank = q * (n - 1);
            int lo = (int)rank;
            double frac = rank - lo;
            if (lo + 1 >= n) return sorted[n - 1];
            return sorted[lo] + frac * (sorted[lo + 1] - sorted[lo]);
        }
    }
}
