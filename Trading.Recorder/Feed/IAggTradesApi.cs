using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Trading.Recorder.Feed
{
    /// <summary>
    /// Acceso al endpoint REST de aggTrades de Binance Futures. Abstraído para poder testear
    /// <see cref="BinanceAggTradeRestFeed"/> sin red.
    /// </summary>
    public interface IAggTradesApi
    {
        /// <summary>
        /// Devuelve hasta <paramref name="limit"/> aggTrades en orden ascendente por aggId.
        /// Si <paramref name="fromId"/> es null devuelve los más recientes (cold start); si tiene
        /// valor, los trades a partir de ese id (inclusive).
        /// </summary>
        /// <exception cref="BinanceRateLimitException">Si Binance responde 429 o 418.</exception>
        Task<IReadOnlyList<AggTrade>> GetAggTradesAsync(
            string symbol, long? fromId, int limit, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Binance respondió 429 (Too Many Requests) o 418 (IP ban). <see cref="RetryAfter"/> indica
    /// cuánto esperar antes de reintentar (del header <c>Retry-After</c>, o un default si falta).
    /// Respetar este backoff es lo que evita que un 429 escale a un ban 418.
    /// </summary>
    public sealed class BinanceRateLimitException : Exception
    {
        public TimeSpan RetryAfter { get; }

        public BinanceRateLimitException(TimeSpan retryAfter)
            : base($"Binance rate limit (429/418); reintentar tras {retryAfter.TotalSeconds:F0}s.")
        {
            RetryAfter = retryAfter;
        }
    }
}
