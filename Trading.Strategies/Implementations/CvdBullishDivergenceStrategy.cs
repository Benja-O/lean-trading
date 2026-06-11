using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// CVD Bullish Divergence Strategy (Hito E).
    ///
    /// Hipótesis: cuando el precio cierra un nuevo mínimo de N barras pero el CVD acumulativo
    /// no confirma ese mínimo (CVD permanece por encima de su mínimo del lookback), hay presión
    /// compradora oculta — los agresores compradores absorben la venta visible → señal Long.
    ///
    /// Condición de señal (Long únicamente):
    ///   close[t] &lt; min(close[t-N..t-1])        — nuevo mínimo estricto de precio
    ///   cvd[t]   &gt; min(cvd[t-N..t-1])          — CVD no confirma el nuevo mínimo
    ///
    /// Parámetros nominales (M4 2021-2024): lookbackBars=24, MaxBarsInPosition=6 (en strategies.json).
    /// M4 resultado: BTC +1.744 / ETH +0.521 / SOL +0.947 — 3/3 activos superan Sharpe ≥ 0.5.
    ///
    /// Acceso a datos microestructurales: IMicrostructureProvider via constructor (ADR-037).
    /// Timeframe: 1h — coincide 1:1 con la granularidad de MicrostructureBar.
    ///
    /// Alineación de timestamps: MarketBar.TimestampUtc = EndTime de la barra 1h (ej. 01:00 UTC
    /// para la barra 00:00–01:00). MicrostructureBar.BarUtc = inicio de la misma barra (00:00 UTC).
    /// Lookup: GetBar(id, marketBar.TimestampUtc.AddHours(-1)).
    /// </summary>
    public sealed class CvdBullishDivergenceStrategy : IStrategy
    {
        private readonly IMicrostructureProvider _microstructureProvider;
        private readonly int _lookbackBars;

        private readonly Dictionary<string, SymbolState> _stateBySymbol = new();

        /// <param name="microstructureProvider">
        /// Proveedor de features microestructurales. Las estrategias que lo usan deben
        /// manejar null del proveedor gracefully: si GetBar devuelve null se retorna Flat
        /// sin excepción.
        /// </param>
        /// <param name="lookbackBars">
        /// Número de barras históricas sobre las que se detectan los extremos de precio y CVD.
        /// La señal se emite cuando la barra actual rompe el mínimo del lookback mientras el CVD
        /// permanece por encima de su propio mínimo. Parámetro nominal M4: 24 (= 1 día en 1h).
        /// Rango válido: [2, ∞).
        /// </param>
        public CvdBullishDivergenceStrategy(IMicrostructureProvider microstructureProvider, int lookbackBars = 24)
        {
            _microstructureProvider = microstructureProvider ?? throw new ArgumentNullException(nameof(microstructureProvider));

            if (lookbackBars < 2)
                throw new ArgumentOutOfRangeException(nameof(lookbackBars), lookbackBars,
                    "lookbackBars debe ser al menos 2.");

            _lookbackBars = lookbackBars;
        }

        /// <summary>
        /// El lookback define cuántas barras históricas son necesarias para que la ventana
        /// esté completa y la estrategia pueda emitir señales.
        /// </summary>
        public int WarmUpBars => _lookbackBars;

        public SignalDirection EvaluateSignal(MarketBar marketBar)
        {
            // Alineación EndTime (MarketBar) → StartTime (MicrostructureBar):
            // QC consolidador para 1h: EndTime = hora de cierre (ej. 01:00 UTC).
            // CSV MicrostructureBar: BarUtc = hora de apertura (ej. 00:00 UTC).
            var microstructureBarTime = marketBar.TimestampUtc.AddHours(-1);
            var microstructureBar = _microstructureProvider.GetBar(marketBar.InstrumentId, microstructureBarTime);

            if (microstructureBar is null)
                return SignalDirection.Flat;

            string ticker = marketBar.InstrumentId.Ticker;
            if (!_stateBySymbol.TryGetValue(ticker, out var state))
            {
                state = new SymbolState(_lookbackBars);
                _stateBySymbol[ticker] = state;
            }

            // Warm-up: llenar la ventana antes de evaluar señales
            if (!state.IsReady)
            {
                state.Push(marketBar.Close, microstructureBar.Cvd);
                return SignalDirection.Flat;
            }

            decimal currentClose = marketBar.Close;
            double  currentCvd   = microstructureBar.Cvd;

            // Divergencia alcista: nuevo mínimo de precio que el CVD no confirma
            bool bullishDivergence = currentClose < state.PriceMin()
                                  && currentCvd   > state.CvdMin();

            // Slide de ventana: el estado actual pasa a ser parte del historial
            state.Push(currentClose, currentCvd);

            return bullishDivergence ? SignalDirection.Long : SignalDirection.Flat;
        }

        // ── Estado interno por símbolo ──────────────────────────────────────────

        private sealed class SymbolState
        {
            private readonly int _capacity;
            private readonly Queue<decimal> _priceWindow = new();
            private readonly Queue<double>  _cvdWindow   = new();

            public SymbolState(int capacity) => _capacity = capacity;

            /// <summary>True cuando la ventana contiene exactamente lookbackBars entradas.</summary>
            public bool IsReady => _priceWindow.Count >= _capacity;

            /// <summary>
            /// Agrega la barra actual al historial y descarta la más antigua si la ventana
            /// ya alcanzó su capacidad.
            /// </summary>
            public void Push(decimal close, double cvd)
            {
                _priceWindow.Enqueue(close);
                _cvdWindow.Enqueue(cvd);

                if (_priceWindow.Count > _capacity) _priceWindow.Dequeue();
                if (_cvdWindow.Count  > _capacity) _cvdWindow.Dequeue();
            }

            /// <summary>Mínimo de precio dentro de la ventana actual.</summary>
            public decimal PriceMin() => _priceWindow.Min();

            /// <summary>Mínimo de CVD dentro de la ventana actual.</summary>
            public double CvdMin() => _cvdWindow.Min();
        }
    }
}
