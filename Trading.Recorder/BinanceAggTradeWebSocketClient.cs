using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Trading.Recorder
{
    /// <summary>
    /// Suscripción al stream público combinado de aggTrades de Binance Futures.
    ///
    /// URL: wss://fstream.binance.com/stream?streams=btcusdt@aggTrade/ethusdt@aggTrade/...
    ///
    /// Reconexión automática con backoff exponencial (1s → 2s → 4s … → 60s máx).
    /// No requiere API key: usa únicamente el stream público de mercado.
    ///
    /// Thread safety: RunAsync corre en el hilo del caller y llama a OnTrade
    /// sincrónicamente desde ese mismo hilo.
    /// </summary>
    public sealed class BinanceAggTradeWebSocketClient
    {
        /// <summary>Delegado invocado por cada aggTrade recibido.</summary>
        public delegate void TradeHandler(string symbol, decimal price, decimal qty, bool isBuyerMaker, long tradeTimeMs);

        private readonly IReadOnlyList<string> _streamNames; // p.ej. ["btcusdt@aggTrade", ...]
        private readonly TradeHandler _onTrade;
        private readonly string _baseUrl;

        // Inyectable en tests: reemplaza ClientWebSocket con una implementación fake.
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

        /// <summary>
        /// Conecta y recibe mensajes indefinidamente hasta que se cancela el token.
        /// Reconecta automáticamente ante errores.
        /// </summary>
        public async Task RunAsync(CancellationToken ct)
        {
            string streamsParam = string.Join("/", _streamNames);
            var uri = new Uri($"{_baseUrl}/stream?streams={streamsParam}");

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

        // ── privado ──────────────────────────────────────────────────────────

        private async Task ConnectAndReceiveAsync(Uri uri, CancellationToken ct)
        {
            IWebSocketAdapter ws = _wsFactory != null
                ? _wsFactory()
                : new SystemWebSocketAdapter();

            await using (ws)
            {
                await ws.ConnectAsync(uri, ct);
                Console.WriteLine("[Recorder] WebSocket conectado.");

                using var ms     = new MemoryStream();
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

        private void ParseAndDispatch(ReadOnlySpan<byte> data)
        {
            try
            {
                var reader = new Utf8JsonReader(data);
                using var doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;

                if (!root.TryGetProperty("data", out var payload)) return;
                if (!payload.TryGetProperty("e", out var ev) || ev.GetString() != "aggTrade") return;

                string symbol = payload.GetProperty("s").GetString()!;
                decimal price = decimal.Parse(payload.GetProperty("p").GetString()!, CultureInfo.InvariantCulture);
                decimal qty   = decimal.Parse(payload.GetProperty("q").GetString()!, CultureInfo.InvariantCulture);
                bool isBuyerMaker = payload.GetProperty("m").GetBoolean();
                long tradeTimeMs  = payload.GetProperty("T").GetInt64();

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
        WebSocketState State { get; }
    }

    internal sealed class SystemWebSocketAdapter : IWebSocketAdapter
    {
        private readonly ClientWebSocket _ws = new();

        public WebSocketState State => _ws.State;

        public Task ConnectAsync(Uri uri, CancellationToken ct) =>
            _ws.ConnectAsync(uri, ct);

        public async Task<WebSocketReceiveResult> ReceiveAsync(byte[] buffer, CancellationToken ct) =>
            await _ws.ReceiveAsync(buffer, ct);

        public async ValueTask DisposeAsync()
        {
            if (_ws.State == WebSocketState.Open)
            {
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None); }
                catch { /* ignorar errores al cerrar */ }
            }
            _ws.Dispose();
        }
    }
}
