using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Trading.Recorder;
using Trading.Recorder.Feed;

namespace Trading.Recorder.Tests.Feed
{
    /// <summary>
    /// Tests del adapter REST de aggTrades (<see cref="BinanceAggTradeRestFeed"/>) con API y
    /// cursor store fakes (sin red ni disco). Verifica: cold start, drenaje multipágina por
    /// fromId, idempotencia y backoff ante rate-limit.
    /// </summary>
    public class BinanceAggTradeRestFeedTests
    {
        private const string Symbol = "BTCUSDT";
        private const long TimeBase = 1_700_000_000_000L;

        private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay =
            (_, _) => Task.CompletedTask;

        // El aggId se recupera en los asserts vía tradeTimeMs (= TimeBase + aggId), porque
        // el TradeHandler no recibe el aggId.
        private static IReadOnlyList<AggTrade> Page(params long[] ids) =>
            ids.Select(id => new AggTrade(id, 100m + id, 1m, id % 2 == 0, TimeBase + id)).ToList();

        private static BinanceAggTradeRestFeed MakeFeed(
            IAggTradesApi api,
            IAggTradeCursorStore cursorStore,
            List<long> received,
            int limit = BinanceAggTradeRestFeed.MaxLimit,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            TradeHandler handler = (_, _, _, _, tradeTimeMs) => received.Add(tradeTimeMs - TimeBase);
            return new BinanceAggTradeRestFeed(
                new[] { Symbol },
                handler,
                api,
                cursorStore,
                pollInterval: TimeSpan.Zero,
                limit: limit,
                delay: delay ?? NoDelay);
        }

        [Fact]
        public async Task InitializeCursor_sin_cursor_previo_hace_cold_start_desde_el_presente()
        {
            var store = new FakeCursorStore();
            var api   = new QueuedApi(() => Page(1, 2, 3)); // cold start: fromId null
            var received = new List<long>();
            var feed  = MakeFeed(api, store, received);

            await feed.InitializeCursorAsync(Symbol, CancellationToken.None);

            received.Should().Equal(1, 2, 3);
            store.Load(Symbol).Should().Be(3);
            api.FromIds.Should().Equal(new long?[] { null });
        }

        [Fact]
        public async Task Drain_pagina_llena_seguida_de_parcial_drena_todo_sin_gaps()
        {
            var store = new FakeCursorStore((Symbol, 10));
            var api = new QueuedApi(
                () => Page(11, 12),  // fromId 11, página llena (limit 2) → sigue drenando
                () => Page(13));     // fromId 13, parcial → alcanza el presente
            var received = new List<long>();
            var feed = MakeFeed(api, store, received, limit: 2);

            await feed.InitializeCursorAsync(Symbol, CancellationToken.None);
            await feed.DrainSymbolAsync(Symbol, CancellationToken.None);

            received.Should().Equal(11, 12, 13);
            store.Load(Symbol).Should().Be(13);
            api.FromIds.Should().Equal(new long?[] { 11, 13 }); // fromId avanza correctamente
        }

        [Fact]
        public async Task Drain_descarta_trades_con_aggId_menor_o_igual_al_cursor()
        {
            var store = new FakeCursorStore((Symbol, 100));
            // La página incluye 100 (<= cursor, solapado) que debe descartarse.
            var api = new QueuedApi(() => Page(100, 101, 102));
            var received = new List<long>();
            var feed = MakeFeed(api, store, received);

            await feed.InitializeCursorAsync(Symbol, CancellationToken.None);
            await feed.DrainSymbolAsync(Symbol, CancellationToken.None);

            received.Should().Equal(101, 102);
            store.Load(Symbol).Should().Be(102);
        }

        [Fact]
        public async Task Drain_ante_rate_limit_hace_backoff_y_reintenta_el_mismo_fromId()
        {
            var store = new FakeCursorStore((Symbol, 5));
            var api = new QueuedApi(
                () => throw new BinanceRateLimitException(TimeSpan.FromSeconds(2)),
                () => Page(6));
            var received = new List<long>();
            var backoffs = new List<TimeSpan>();
            Func<TimeSpan, CancellationToken, Task> recordingDelay = (timeSpan, _) =>
            {
                backoffs.Add(timeSpan);
                return Task.CompletedTask;
            };
            var feed = MakeFeed(api, store, received, delay: recordingDelay);

            await feed.InitializeCursorAsync(Symbol, CancellationToken.None);
            await feed.DrainSymbolAsync(Symbol, CancellationToken.None);

            received.Should().Equal(6);
            store.Load(Symbol).Should().Be(6);
            backoffs.Should().Contain(TimeSpan.FromSeconds(2));      // respetó el Retry-After
            api.FromIds.Should().Equal(new long?[] { 6, 6 });        // reintentó el mismo fromId
        }

        // ── fakes ──────────────────────────────────────────────────────────────

        private sealed class FakeCursorStore : IAggTradeCursorStore
        {
            private readonly Dictionary<string, long> _map = new(StringComparer.OrdinalIgnoreCase);

            public FakeCursorStore(params (string symbol, long aggId)[] seed)
            {
                foreach (var (symbol, aggId) in seed)
                    _map[symbol] = aggId;
            }

            public long? Load(string symbol) => _map.TryGetValue(symbol, out var value) ? value : null;
            public void Save(string symbol, long lastAggregateTradeId) => _map[symbol] = lastAggregateTradeId;
        }

        private sealed class QueuedApi : IAggTradesApi
        {
            private readonly Queue<Func<IReadOnlyList<AggTrade>>> _responses;

            public List<long?> FromIds { get; } = new();

            public QueuedApi(params Func<IReadOnlyList<AggTrade>>[] responses) =>
                _responses = new Queue<Func<IReadOnlyList<AggTrade>>>(responses);

            public Task<IReadOnlyList<AggTrade>> GetAggTradesAsync(
                string symbol, long? fromId, int limit, CancellationToken cancellationToken)
            {
                FromIds.Add(fromId);
                Func<IReadOnlyList<AggTrade>> next = _responses.Count > 0
                    ? _responses.Dequeue()
                    : () => Array.Empty<AggTrade>();
                return Task.FromResult(next());
            }
        }
    }
}
