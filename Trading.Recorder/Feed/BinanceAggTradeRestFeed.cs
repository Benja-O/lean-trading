using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Trading.Recorder.Feed
{
    /// <summary>
    /// Feed de aggTrades por polling REST de Binance Futures (<c>/fapi/v1/aggTrades</c>),
    /// alternativa al stream WebSocket cuando fstream no entrega push (ver ADR-049).
    ///
    /// Por cada símbolo mantiene un cursor <c>fromId</c> persistido y pide páginas de hasta
    /// <c>limit</c> (máx 1000) trades. Garantías:
    ///   - <b>Sin gaps:</b> al pedir por fromId se obtienen TODOS los trades desde el último visto.
    ///   - <b>Drenaje en picos:</b> si una página viene llena, repollea de inmediato hasta alcanzar
    ///     el presente (página &lt; limit), así no se atrasa con alta volatilidad.
    ///   - <b>Idempotencia:</b> descarta trades con aggId &lt;= cursor.
    ///   - <b>Rate-limit:</b> ante <see cref="BinanceRateLimitException"/> (429/418) hace backoff
    ///     según Retry-After; nunca martillea tras un 429 (evita el ban escalado a 418).
    ///   - <b>Gap grande:</b> si el backlog tras un downtime largo excede <c>maxDrainPages</c>, salta
    ///     al presente y lo loguea — NO fabrica dato; la historia faltante se re-siembra con
    ///     <c>BinanceVisionSeeder</c>.
    ///
    /// El dato es idéntico en contenido al del stream WS; solo cambia el transporte y la latencia,
    /// irrelevante para barras cerradas de 1h.
    ///
    /// Thread safety: no thread-safe. RunAsync corre en el hilo del caller e invoca onTrade
    /// sincrónicamente desde ese hilo.
    /// </summary>
    public sealed class BinanceAggTradeRestFeed : ITradeFeed
    {
        /// <summary>Máximo de trades por página soportado por el endpoint.</summary>
        public const int MaxLimit = 1000;

        /// <summary>
        /// Tope de páginas por símbolo en un mismo catch-up. A 1000 trades/página son ~200k trades;
        /// superarlo implica un downtime largo cuyo backlog conviene re-sembrar, no drenar por REST.
        /// </summary>
        private const int DefaultMaxDrainPages = 200;

        private readonly IReadOnlyList<string> _symbols;
        private readonly TradeHandler _onTrade;
        private readonly IAggTradesApi _api;
        private readonly IAggTradeCursorStore _cursorStore;
        private readonly TimeSpan _pollInterval;
        private readonly int _limit;
        private readonly int _maxDrainPages;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;

        private readonly Dictionary<string, long> _lastAggregateTradeId =
            new(StringComparer.OrdinalIgnoreCase);

        public BinanceAggTradeRestFeed(
            IReadOnlyList<string> symbols,
            TradeHandler onTrade,
            IAggTradesApi aggTradesApi,
            IAggTradeCursorStore cursorStore,
            TimeSpan pollInterval,
            int limit = MaxLimit,
            int maxDrainPages = DefaultMaxDrainPages,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            _symbols       = symbols      ?? throw new ArgumentNullException(nameof(symbols));
            _onTrade       = onTrade      ?? throw new ArgumentNullException(nameof(onTrade));
            _api           = aggTradesApi ?? throw new ArgumentNullException(nameof(aggTradesApi));
            _cursorStore   = cursorStore  ?? throw new ArgumentNullException(nameof(cursorStore));
            _pollInterval  = pollInterval;
            _limit         = limit > 0 && limit <= MaxLimit ? limit : MaxLimit;
            _maxDrainPages = maxDrainPages > 0 ? maxDrainPages : DefaultMaxDrainPages;
            _delay         = delay ?? Task.Delay;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine(
                $"[Recorder][REST] Feed iniciado — {_symbols.Count} simbolo(s), poll cada {_pollInterval.TotalSeconds:F0}s, limit={_limit}.");

            foreach (var symbol in _symbols)
                await InitializeCursorAsync(symbol, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var symbol in _symbols)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await DrainSymbolAsync(symbol, cancellationToken);
                }

                try { await _delay(_pollInterval, cancellationToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        // ── privado ──────────────────────────────────────────────────────────

        /// <summary>Carga el cursor persistido; si no hay, hace cold start desde el presente.</summary>
        internal async Task InitializeCursorAsync(string symbol, CancellationToken cancellationToken)
        {
            var persisted = _cursorStore.Load(symbol);
            if (persisted.HasValue)
            {
                _lastAggregateTradeId[symbol] = persisted.Value;
                Console.WriteLine($"[Recorder][REST] {symbol}: cursor cargado aggId={persisted.Value}.");
                return;
            }

            var latest = await FetchWithBackoffAsync(symbol, fromId: null, cancellationToken);
            if (latest.Count == 0)
            {
                Console.Error.WriteLine($"[Recorder][REST] {symbol}: cold start sin trades; reintento en el proximo ciclo.");
                return;
            }

            long maxId = ProcessAndTrack(symbol, latest, cursor: long.MinValue);
            _lastAggregateTradeId[symbol] = maxId;
            _cursorStore.Save(symbol, maxId);
            Console.WriteLine($"[Recorder][REST] {symbol}: cold start desde aggId={maxId} ({latest.Count} trades).");
        }

        internal async Task DrainSymbolAsync(string symbol, CancellationToken cancellationToken)
        {
            if (!_lastAggregateTradeId.TryGetValue(symbol, out long cursor))
            {
                // Cold start falló antes (sin trades); reintentar.
                await InitializeCursorAsync(symbol, cancellationToken);
                return;
            }

            int pages = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var trades = await FetchWithBackoffAsync(symbol, cursor + 1, cancellationToken);
                if (trades.Count == 0)
                    break;

                cursor = ProcessAndTrack(symbol, trades, cursor);
                _lastAggregateTradeId[symbol] = cursor;
                _cursorStore.Save(symbol, cursor);

                pages++;
                if (trades.Count < _limit)
                    break; // alcanzado el presente

                if (pages >= _maxDrainPages)
                {
                    Console.Error.WriteLine(
                        $"[Recorder][REST] {symbol}: gap grande — {pages} paginas sin alcanzar el presente. " +
                        "Se salta el backlog restante para no fabricar dato; re-sembrar con BinanceVisionSeeder si se necesita la historia completa.");
                    await JumpToPresentAsync(symbol, cancellationToken);
                    break;
                }
            }
        }

        /// <summary>Reposiciona el cursor al presente tras detectar un gap demasiado grande.</summary>
        private async Task JumpToPresentAsync(string symbol, CancellationToken cancellationToken)
        {
            var latest = await FetchWithBackoffAsync(symbol, fromId: null, cancellationToken);
            if (latest.Count == 0) return;

            long maxId = long.MinValue;
            foreach (var trade in latest)
                if (trade.AggregateTradeId > maxId) maxId = trade.AggregateTradeId;

            _lastAggregateTradeId[symbol] = maxId;
            _cursorStore.Save(symbol, maxId);
            Console.WriteLine($"[Recorder][REST] {symbol}: cursor reposicionado al presente aggId={maxId} (hay un hueco previo).");
        }

        /// <summary>
        /// Invoca onTrade por cada trade con aggId &gt; <paramref name="cursor"/> (idempotencia)
        /// y devuelve el mayor aggId visto (o el cursor original si no hubo trades nuevos).
        /// </summary>
        private long ProcessAndTrack(string symbol, IReadOnlyList<AggTrade> trades, long cursor)
        {
            // Avanzar el cursor al último trade recibido incondicionalmente: Binance devuelve
            // aggTrades en orden ascendente, así que si todos tienen aggId <= cursor (eventual
            // consistency edge-case) el cursor igualmente avanza y evitamos un bucle infinito.
            long maxId = trades.Count > 0
                ? Math.Max(cursor, trades[trades.Count - 1].AggregateTradeId)
                : cursor;

            foreach (var trade in trades)
            {
                if (trade.AggregateTradeId <= cursor)
                    continue;

                _onTrade(symbol, trade.Price, trade.Quantity, trade.IsBuyerMaker, trade.TradeTimeMs);
            }
            return maxId;
        }

        private async Task<IReadOnlyList<AggTrade>> FetchWithBackoffAsync(
            string symbol, long? fromId, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await _api.GetAggTradesAsync(symbol, fromId, _limit, cancellationToken);
                }
                catch (BinanceRateLimitException ex)
                {
                    Console.Error.WriteLine(
                        $"[Recorder][REST] {symbol}: rate limit (429/418) — backoff {ex.RetryAfter.TotalSeconds:F0}s.");
                    await _delay(ex.RetryAfter, cancellationToken);
                }
            }
        }
    }
}
