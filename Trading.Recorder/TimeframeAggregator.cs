using System;
using Trading.Application.Microstructure;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Recorder
{
    /// <summary>
    /// Acumula aggTrades de un símbolo en ventanas de un timeframe y emite
    /// MicrostructureBars al cerrar cada ventana.
    ///
    /// Diseño:
    ///   - Una instancia por (símbolo, timeframe).
    ///   - Detecta el cierre de ventana comparando el floor del trade entrante
    ///     con el de la ventana abierta.
    ///   - Mantiene cvdRunning entre ventanas; se siembra al construir desde
    ///     el último Cvd persistido en disco.
    ///   - Al cerrar la ventana invoca BarClosed. El caller persiste y actualiza CVD.
    ///
    /// Thread safety: no thread-safe. Llamar siempre desde el hilo receptor del WS.
    /// </summary>
    public sealed class TimeframeAggregator
    {
        private readonly InstrumentId _instrumentId;
        private readonly TimeSpan _windowSize;

        private AggTradeBucket _bucket = new();
        private DateTime _windowStart;
        private bool _initialized;
        private double _cvdRunning;

        /// <summary>Se invoca cuando una ventana se cierra con datos.</summary>
        public event Action<InstrumentId, DateTime, MicrostructureBar>? BarClosed;

        public InstrumentId InstrumentId => _instrumentId;
        public string Timeframe { get; }

        public TimeframeAggregator(InstrumentId instrumentId, string timeframe, double cvdSeed = 0.0)
        {
            _instrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
            Timeframe     = timeframe    ?? throw new ArgumentNullException(nameof(timeframe));
            _windowSize   = RecorderConfig.ParseTimeframe(timeframe);
            _cvdRunning   = cvdSeed;
        }

        /// <summary>
        /// Procesa un aggTrade. Si el trade pertenece a una ventana más nueva, cierra la
        /// ventana anterior (si tiene datos) y abre una nueva.
        /// </summary>
        public void OnTrade(decimal price, decimal qty, bool isBuyerMaker, long tradeTimeMs)
        {
            var tradeUtc    = DateTimeOffset.FromUnixTimeMilliseconds(tradeTimeMs).UtcDateTime;
            var windowStart = FloorToWindow(tradeUtc);

            if (!_initialized)
            {
                _windowStart  = windowStart;
                _initialized  = true;
            }

            if (windowStart > _windowStart)
            {
                // Ventana anterior cerrada — flush si tiene datos.
                if (_bucket.HasData)
                    Flush();

                // Avanzar; si hay huecos entre ventanas (reconexión) se omiten (sin datos).
                _bucket      = new AggTradeBucket();
                _windowStart = windowStart;
            }

            _bucket.Add(price, qty, isBuyerMaker);
        }

        // ── privado ──────────────────────────────────────────────────────────

        private void Flush()
        {
            var (bar, newCvd) = MicrostructureFeatureComputer.Compute(
                _instrumentId, _windowStart, _bucket, _cvdRunning);

            _cvdRunning = newCvd;
            BarClosed?.Invoke(_instrumentId, _windowStart, bar);
        }

        private DateTime FloorToWindow(DateTime utc)
        {
            long ticks  = utc.Ticks;
            long wTicks = _windowSize.Ticks;
            return new DateTime((ticks / wTicks) * wTicks, DateTimeKind.Utc);
        }
    }
}
