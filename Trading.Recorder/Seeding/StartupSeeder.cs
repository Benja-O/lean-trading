using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Trading.Application.Microstructure;
using Trading.Domain.ValueObjects;
using Trading.Recorder.Feed;

namespace Trading.Recorder.Seeding
{
    /// <summary>
    /// Rellena el hueco de historia desde <c>data.binance.vision</c> al arrancar el recorder,
    /// y deja el cursor del feed REST en el último <c>aggId</c> sembrado — el "puente" que hace
    /// que el feed en vivo drene desde justo después del histórico, sin gaps y con CVD continuo.
    ///
    /// Por cada (símbolo, timeframe):
    ///   - Store vacío  → siembra los últimos <c>seedDays</c> días (deploy en frío: arranca con
    ///     historia inmediata, sin esperar días).
    ///   - Store con datos → siembra desde el día siguiente a la última barra hasta T-1 (Vision
    ///     publica hasta ayer). No re-siembra días ya presentes (el store es append-only, sin
    ///     dedup), evitando barras duplicadas.
    ///   - Store al día → no siembra; el cursor REST existente resume el vivo.
    ///
    /// El puente del cursor se aplica una vez por símbolo (todos los timeframes comparten el mismo
    /// stream de trades, así que el aggId es el mismo). Solo se adelanta el cursor, nunca se
    /// retrocede.
    /// </summary>
    public sealed class StartupSeeder
    {
        private readonly BinanceVisionSeeder _seeder;
        private readonly IAggTradeCursorStore _cursorStore;

        public StartupSeeder(BinanceVisionSeeder seeder, IAggTradeCursorStore cursorStore)
        {
            _seeder      = seeder      ?? throw new ArgumentNullException(nameof(seeder));
            _cursorStore = cursorStore ?? throw new ArgumentNullException(nameof(cursorStore));
        }

        /// <param name="streams">Pares (símbolo, timeframe) a sembrar.</param>
        /// <param name="storeByTimeframe">Store por timeframe (donde se escriben las barras).</param>
        /// <param name="today">Fecha UTC actual; Vision tiene datos hasta <c>today − 1</c>.</param>
        /// <param name="seedDays">Días a sembrar cuando el store está vacío.</param>
        public async Task SeedGapsAsync(
            IReadOnlyList<(string Symbol, string Timeframe)> streams,
            IReadOnlyDictionary<string, PersistentMicrostructureStore> storeByTimeframe,
            DateOnly today,
            int seedDays,
            CancellationToken cancellationToken = default)
        {
            if (streams is null)          throw new ArgumentNullException(nameof(streams));
            if (storeByTimeframe is null) throw new ArgumentNullException(nameof(storeByTimeframe));

            var lastAggIdBySymbol = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var (symbol, timeframe) in streams)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var store        = storeByTimeframe[timeframe];
                var instrumentId = new InstrumentId(symbol);
                var lastBar      = store.GetLastBar(instrumentId);

                DateOnly fromDate;
                double cvdSeed;
                if (lastBar is null)
                {
                    fromDate = today.AddDays(-seedDays);
                    cvdSeed  = 0.0;
                }
                else
                {
                    // No re-sembrar el día de la última barra (el store es append-only sin dedup).
                    fromDate = DateOnly.FromDateTime(lastBar.BarUtc).AddDays(1);
                    cvdSeed  = lastBar.Cvd;
                }

                if (fromDate >= today)
                {
                    Console.WriteLine($"[Recorder][Seed] {symbol}/{timeframe}: store al dia, sin siembra.");
                    continue;
                }

                Console.WriteLine($"[Recorder][Seed] {symbol}/{timeframe}: sembrando Vision {fromDate} -> {today.AddDays(-1)} (cvdSeed={cvdSeed:F4})...");
                var seedResult = await _seeder.SeedAsync(instrumentId, timeframe, store, fromDate, today, cvdSeed, cancellationToken);

                if (seedResult.LastAggregateTradeId is long maxId &&
                    (!lastAggIdBySymbol.TryGetValue(symbol, out var existing) || maxId > existing))
                {
                    lastAggIdBySymbol[symbol] = maxId;
                }
            }

            // Puente: adelantar el cursor REST al último aggId sembrado (nunca retroceder).
            foreach (var (symbol, aggId) in lastAggIdBySymbol)
            {
                var existing = _cursorStore.Load(symbol);
                if (existing is null || aggId > existing.Value)
                {
                    _cursorStore.Save(symbol, aggId);
                    Console.WriteLine($"[Recorder][Seed] {symbol}: cursor REST puenteado a aggId={aggId} (el feed drena de ahi al presente).");
                }
            }
        }
    }
}
