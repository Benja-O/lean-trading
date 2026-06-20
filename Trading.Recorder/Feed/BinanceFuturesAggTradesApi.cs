using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Trading.Recorder.Feed
{
    /// <summary>
    /// Implementación REST de <see cref="IAggTradesApi"/> contra Binance USDT-M Futures
    /// (<c>/fapi/v1/aggTrades</c>). Endpoint público de market data: no requiere API key.
    /// Traduce 429/418 a <see cref="BinanceRateLimitException"/> con el Retry-After del header.
    /// </summary>
    public sealed class BinanceFuturesAggTradesApi : IAggTradesApi
    {
        private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(60);

        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public BinanceFuturesAggTradesApi(HttpClient httpClient, string baseUrl = "https://fapi.binance.com")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _baseUrl    = (baseUrl ?? throw new ArgumentNullException(nameof(baseUrl))).TrimEnd('/');
        }

        public async Task<IReadOnlyList<AggTrade>> GetAggTradesAsync(
            string symbol, long? fromId, int limit, CancellationToken cancellationToken)
        {
            string url = $"{_baseUrl}/fapi/v1/aggTrades?symbol={symbol}&limit={limit.ToString(CultureInfo.InvariantCulture)}";
            if (fromId.HasValue)
                url += $"&fromId={fromId.Value.ToString(CultureInfo.InvariantCulture)}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);

            // 429 = Too Many Requests; 418 = IP ban (escalado por ignorar 429s).
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 418)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? DefaultRetryAfter;
                throw new BinanceRateLimitException(retryAfter);
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return Parse(stream);
        }

        private static IReadOnlyList<AggTrade> Parse(Stream stream)
        {
            using var document = JsonDocument.Parse(stream);

            var trades = new List<AggTrade>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                long aggregateTradeId = element.GetProperty("a").GetInt64();
                decimal price    = decimal.Parse(element.GetProperty("p").GetString()!, CultureInfo.InvariantCulture);
                decimal quantity = decimal.Parse(element.GetProperty("q").GetString()!, CultureInfo.InvariantCulture);
                bool isBuyerMaker = element.GetProperty("m").GetBoolean();
                long tradeTimeMs  = element.GetProperty("T").GetInt64();

                trades.Add(new AggTrade(aggregateTradeId, price, quantity, isBuyerMaker, tradeTimeMs));
            }
            return trades;
        }
    }
}
