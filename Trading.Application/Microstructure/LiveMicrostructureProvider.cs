using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Microstructure
{
    /// <summary>
    /// Proveedor de features microestructurales para trading en vivo.
    ///
    /// Combina dos fuentes de datos:
    ///   1. CSV histórico (MicrostructureRegistry) — cubre el período de warmup y sirve de fallback.
    ///   2. Cómputo en tiempo real (via ComputeAndAdd) — para barras 1h que cierran en vivo.
    ///
    /// GetBar() prioriza el dato en vivo sobre el CSV. Esto garantiza que las estrategias
    /// siempre ven la feature más reciente, y durante warmup reciben los datos del CSV
    /// sin ningún cambio en el contrato de IMicrostructureProvider.
    ///
    /// CVD: el acumulado continuo se siembra desde la última barra del CSV (SeedCvdFromHistory)
    /// para mantener la continuidad del dataset. Es responsabilidad del caller llamar
    /// SeedCvdFromHistory por cada instrumento antes de arrancar el procesamiento de barras en vivo.
    /// </summary>
    public sealed class LiveMicrostructureProvider : IMicrostructureProvider
    {
        private readonly MicrostructureRegistry _historical;
        private readonly ITradingLogger _logger;

        // Barras computadas en vivo, por instrumento y timestamp
        private readonly Dictionary<InstrumentId, Dictionary<DateTime, MicrostructureBar>> _live = new();

        // CVD acumulado por instrumento. Inicializado con el último CVD del CSV.
        private readonly Dictionary<InstrumentId, double> _cvdRunning = new();

        public LiveMicrostructureProvider(MicrostructureRegistry historical, ITradingLogger logger)
        {
            _historical = historical ?? throw new ArgumentNullException(nameof(historical));
            _logger     = logger     ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Siembra el CVD running con el último valor del CSV histórico.
        /// Debe llamarse una vez por instrumento en Initialize(), antes de que lleguen barras en vivo.
        /// </summary>
        public void SeedCvdFromHistory(InstrumentId instrumentId)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));

            double seed = _historical.GetLastCvd(instrumentId);
            _cvdRunning[instrumentId] = seed;
            _logger.Info(
                "LiveMicrostructureProvider: CVD seed para {Ticker} = {Seed:F4} (último bar del CSV histórico).",
                instrumentId.Ticker, seed);
        }

        /// <summary>
        /// Pre-carga una barra ya computada (desde disco o backfill REST) en el dict en vivo.
        /// Actualiza _cvdRunning con el CVD de la barra para que el próximo Compute lo use como seed.
        /// Llamar antes de SetWarmUp(), en orden cronológico para que _cvdRunning quede en el último valor.
        /// </summary>
        public void AddBar(MicrostructureBar bar)
        {
            if (bar is null) throw new ArgumentNullException(nameof(bar));

            if (!_live.TryGetValue(bar.InstrumentId, out var byTime))
            {
                byTime = new Dictionary<DateTime, MicrostructureBar>();
                _live[bar.InstrumentId] = byTime;
            }

            var key = DateTime.SpecifyKind(bar.BarUtc, DateTimeKind.Utc);
            byTime[key] = bar;
            _cvdRunning[bar.InstrumentId] = bar.Cvd;
        }

        /// <summary>
        /// Retorna el CVD acumulado corriente para el instrumento, o 0 si no hay seed.
        /// Usado por BinanceAggTradeBackfiller para continuar el CVD desde el último punto conocido.
        /// </summary>
        public double GetCvdRunning(InstrumentId instrumentId)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));
            return _cvdRunning.TryGetValue(instrumentId, out var cvd) ? cvd : 0.0;
        }

        /// <inheritdoc/>
        public MicrostructureBar? GetBar(InstrumentId instrumentId, DateTime barUtc)
        {
            var key = DateTime.SpecifyKind(barUtc, DateTimeKind.Utc);

            // 1. Dato en vivo (prioridad)
            if (_live.TryGetValue(instrumentId, out var byTime) &&
                byTime.TryGetValue(key, out var liveBar))
                return liveBar;

            // 2. Fallback al CSV histórico (warmup y barras pasadas)
            return _historical.GetBar(instrumentId, barUtc);
        }

        /// <summary>
        /// Devuelve las barras pre-cargadas (vía AddBar desde el store) para un instrumento,
        /// en orden cronológico ascendente. Usado por el warmup de estrategias en Initialize()
        /// para reproducirlas por EvaluateSignal y llenar el estado interno de cada estrategia.
        /// Lista vacía si no hay barras cargadas para el instrumento.
        /// </summary>
        public IReadOnlyList<MicrostructureBar> GetHistoricalBarsSorted(InstrumentId instrumentId)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));
            if (!_live.TryGetValue(instrumentId, out var byTime))
                return Array.Empty<MicrostructureBar>();

            var bars = new List<MicrostructureBar>(byTime.Values);
            bars.Sort((left, right) => left.BarUtc.CompareTo(right.BarUtc));
            return bars;
        }

        /// <inheritdoc/>
        public bool HasDataFor(InstrumentId instrumentId) =>
            _live.ContainsKey(instrumentId) || _historical.HasDataFor(instrumentId);
    }
}
