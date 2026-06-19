using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Trading.Recorder
{
    /// <summary>
    /// Suscripción al stream público de aggTrades de Binance Futures.
    ///
    /// Conecta a wss://fstream.binance.com/ws y envía SUBSCRIBE explícito.
    /// Los mensajes llegan sin wrapper: {"e":"aggTrade","s":"BTCUSDT",...}
    ///
    /// Reconexión automática con backoff exponencial (1s → 2s → 4s … → 60s máx).
    /// No requiere API key: usa el stream público de mercado.
    ///
    /// Thread safety: RunAsync corre en el hilo del caller y llama a OnTrade
    /// sincrónicamente desde ese mismo hilo.
    /// </summary>
    public sealed class BinanceAggTradeWebSocketClient
    {
        public delegate void TradeHandler(string symbol, decimal price, decimal qty, bool isBuyerMaker, long tradeTimeMs);

        private readonly IReadOnlyList<string> _streamNames;
        private readonly TradeHandler _onTrade;
        private readonly string _baseUrl;
        private readonly Func<IWebSocketAdapter>? _wsFactory;

        public BinanceAggTradeWebSocketClient(
            IReadOnlyList<string> streamNames,
            TradeHandler onTrade,
            string baseUrl = "wss://fstream.binance.com",
            Func<IWebSocketAdapter>? wsFactory = null)
        {
            _streamNames = streamNames ?? throw new ArgumentNullException(nameof(streamNames));
            _onTrade     = onTrade     ?? throw new ArgumentNullException(nameof(onTrade));
            _baseUrl     = baseUrl.TrimEnd('/');
            _wsFactory   = wsFactory;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            // /ws con SUBSCRIBE explícito — funciona a través de proxies/firewalls
            // que silencian el combined stream (/stream?streams=...).
            var uri = new Uri($"{_baseUrl}/ws");

            int delaySec = 1;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndReceiveAsync(uri, ct);
                    delaySec = 1;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Recorder] WS error: {ex.Message} — reconectando en {delaySec}s");
                    try { await Task.Delay(TimeSpan.FromSeconds(delaySec), ct); }
                    catch (OperationCanceledException) { break; }
                    delaySec = Math.Min(delaySec * 2, 60);
                }
            }
        }

        private async Task ConnectAndReceiveAsync(Uri uri, CancellationToken ct)
        {
            IWebSocketAdapter ws = _wsFactory != null
                ? _wsFactory()
                : new SystemWebSocketAdapter();

            await using (ws)
            {
                await ws.ConnectAsync(uri, ct);

                var subscribeMsg = BuildSubscribeMessage(_streamNames);
                await ws.SendAsync(Encoding.UTF8.GetBytes(subscribeMsg), WebSocketMessageType.Text, true, ct);
                Console.WriteLine($"[Recorder] WebSocket conectado — suscrito a {_streamNames.Count} stream(s).");

                using var ms = new MemoryStream();
                var buffer = new byte[8192];

                while (!ct.IsCancellationRequested)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(buffer, ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return;
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    ParseAndDispatch(ms.GetBuffer().AsSpan(0, (int)ms.Length));
                }
            }
        }

        private static string BuildSubscribeMessage(IReadOnlyList<string> streams)
        {
            var paramsJson = string.Join(",", streams.Select(s => $"\"{s}\""));
            return $"{{\"method\":\"SUBSCRIBE\",\"params\":[{paramsJson}],\"id\":1}}";
        }

        private void ParseAndDispatch(ReadOnlySpan<byte> data)
        {
            try
            {
                var reader = new Utf8JsonReader(data);
                using var doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;

                // Confirmación de suscripción: {"result":null,"id":1}
                if (root.TryGetProperty("result", out _)) return;

                // aggTrade directo (sin wrapper): {"e":"aggTrade","s":"BTCUSDT",...}
                if (!root.TryGetProperty("e", out var ev) || ev.GetString() != "aggTrade") return;

                string symbol = root.GetProperty("s").GetString()!;
                decimal price = decimal.Parse(root.GetProperty("p").GetString()!, CultureInfo.InvariantCulture);
                decimal qty   = decimal.Parse(root.GetProperty("q").GetString()!, CultureInfo.InvariantCulture);
                bool isBuyerMaker = root.GetProperty("m").GetBoolean();
                long tradeTimeMs  = root.GetProperty("T").GetInt64();

                _onTrade(symbol, price, qty, isBuyerMaker, tradeTimeMs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Recorder] Error parseando mensaje WS: {ex.Message}");
            }
        }
    }

    // ── abstracción de WebSocket para testabilidad ────────────────────────────

    public interface IWebSocketAdapter : IAsyncDisposable
    {
        Task ConnectAsync(Uri uri, CancellationToken ct);
        Task<WebSocketReceiveResult> ReceiveAsync(byte[] buffer, CancellationToken ct);
        Task SendAsync(byte[] buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct);
        WebSocketState State { get; }
    }

    internal sealed class SystemWebSocketAdapter : IWebSocketAdapter
    {
        private readonly ClientWebSocket _ws = new();

        public WebSocketState State => _ws.State;

        public Task ConnectAsync(Uri uri, CancellationToken ct) =>
            _ws.ConnectAsync(uri, ct);

        public Task<WebSocketReceiveResult> ReceiveAsync(byte[] buffer, CancellationToken ct) =>
            _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

        public Task SendAsync(byte[] buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken ct) =>
            _ws.SendAsync(new ArraySegment<byte>(buffer), messageType, endOfMessage, ct);

        public async ValueTask DisposeAsync()
        {
            if (_ws.State == WebSocketState.Open)
            {
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None); }
                catch { }
            }
            _ws.Dispose();
        }
    }
}
