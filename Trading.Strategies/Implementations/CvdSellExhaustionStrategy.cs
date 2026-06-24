using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// H3 — CVD Sell Exhaustion (Long-only).
    /// Hipótesis: cuando el precio toca el mínimo de las últimas Lookback barras Y el CVD delta
    /// es positivo (compradores netos), los vendedores se agotaron → rebote inminente.
    /// Parámetros nominales M4: lookback=48, cvd_thr=0.0 (equivale a CvdDelta > 0), hold=6.
    /// Marginal: ETH Sharpe=-0.580 y conteo bajo de trades en todos los activos.
    /// </summary>
    public sealed class CvdSellExhaustionStrategy : IStrategy, ISignalDiagnosticsProvider
    {
        private const int Lookback = 48;

        private readonly IMicrostructureProvider? _microstructure;
        // Queue almacena N-1 = 47 closes previos; incluye barra actual para rolling min de 48 barras
        private readonly Dictionary<string, Queue<double>> _closeHistoryBySymbol = new();

        // Rationale de la última evaluación (ISignalDiagnosticsProvider). Single-threaded:
        // se sobreescribe en cada EvaluateSignal y el host lo lee inmediatamente después.
        private SignalDiagnostics? _lastDiagnostics;

        public int WarmUpBars => Lookback + 2;

        public CvdSellExhaustionStrategy(IMicrostructureProvider? microstructure = null)
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
            if (!_closeHistoryBySymbol.TryGetValue(ticker, out var history))
            {
                history = new Queue<double>(Lookback);
                _closeHistoryBySymbol[ticker] = history;
            }

            double close    = (double)marketBar.Close;
            double cvdDelta = msBar.CvdDelta;

            // La cola mantiene N-1 = 47 previos; el mínimo rolling de N=48 incluye la barra actual
            bool isWarmedUp = history.Count >= Lookback - 1;

            if (!isWarmedUp)
            {
                _lastDiagnostics = new SignalDiagnostics(
                    $"Warmup: {history.Count + 1}/{Lookback} closes — Flat.",
                    new List<SignalCondition>());
                history.Enqueue(close);
                return SignalDirection.Flat;
            }

            // close ≤ mínimo de los 47 previos → nuevo mínimo de 48 barras
            double min47 = MinimumOf(history);
            bool isNBarMin = IsMinimum(history, close);

            history.Dequeue();
            history.Enqueue(close);

            // cvd_thr = 0.0 → condición: cvdDelta > 0 (equivalente a cvd_delta/volume > 0 con vol > 0)
            bool cvdPositive = cvdDelta > 0.0;
            bool fired = isNBarMin && cvdPositive;

            _lastDiagnostics = new SignalDiagnostics(
                fired
                    ? "CvdSellExhaustion Long: close es mínimo de 48 ∧ cvd_delta>0."
                    : "CvdSellExhaustion Flat: no se cumple (close mínimo de 48) ∧ (cvd_delta>0).",
                new List<SignalCondition>
                {
                    new("CloseIsMin48", close, "<=", min47, isNBarMin),
                    new("CvdDeltaPositive", cvdDelta, ">", 0.0, cvdPositive),
                });

            return fired ? SignalDirection.Long : SignalDirection.Flat;
        }

        public SignalDiagnostics? DescribeLastEvaluation() => _lastDiagnostics;

        private static bool IsMinimum(Queue<double> history, double value)
        {
            foreach (var v in history)
                if (v < value) return false;
            return true;
        }

        private static double MinimumOf(Queue<double> history)
        {
            double min = double.PositiveInfinity;
            foreach (var v in history)
                if (v < min) min = v;
            return min;
        }
    }
}
