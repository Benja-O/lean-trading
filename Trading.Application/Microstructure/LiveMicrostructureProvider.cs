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
        /// Computa las features para una barra 1h recién cerrada y las almacena para lookup inmediato.
        /// Debe llamarse desde el DataConsolidated handler, ANTES de BarProcessingService.ProcessBar(),
        /// para que GetBar() ya tenga el resultado cuando la estrategia lo consulte.
        /// </summary>
        /// <param name="instrumentId">Instrumento.</param>
        /// <param name="barUtc">Timestamp de inicio de la barra (floor a la hora, UTC).</param>
        /// <param name="bucket">Ticks acumulados durante esa barra.</param>
        /// <returns>El MicrostructureBar recién computado, o null si el bucket está vacío.</returns>
        public MicrostructureBar? ComputeAndAdd(InstrumentId instrumentId, DateTime barUtc, AggTradeBucket bucket)
        {
            if (instrumentId is null) throw new ArgumentNullException(nameof(instrumentId));
            if (bucket is null)       throw new ArgumentNullException(nameof(bucket));

            if (!bucket.HasData)
            {
                _logger.Warning(
                    "LiveMicrostructureProvider: bucket vacío para {Ticker} {BarUtc:yyyy-MM-dd HH:mm} UTC — sin aggTrades en esta barra.",
                    instrumentId.Ticker, barUtc);
                return null;
            }

            double cvdSeed = _cvdRunning.TryGetValue(instrumentId, out var running) ? running : 0.0;
            var (bar, newCvd) = MicrostructureFeatureComputer.Compute(instrumentId, barUtc, bucket, cvdSeed);

            _cvdRunning[instrumentId] = newCvd;

            if (!_live.TryGetValue(instrumentId, out var byTime))
            {
                byTime = new Dictionary<DateTime, MicrostructureBar>();
                _live[instrumentId] = byTime;
            }

            var key = DateTime.SpecifyKind(barUtc, DateTimeKind.Utc);
            byTime[key] = bar;

            return bar;
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

        /// <inheritdoc/>
        public bool HasDataFor(InstrumentId instrumentId) =>
            _live.ContainsKey(instrumentId) || _historical.HasDataFor(instrumentId);
    }
}
