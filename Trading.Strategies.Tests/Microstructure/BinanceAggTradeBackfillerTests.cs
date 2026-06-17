using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Trading.Strategies.Microstructure;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Strategies.Tests.Microstructure
{
    /// <summary>
    /// Tests del BinanceAggTradeBackfiller usando un fetcher inyectado (sin HttpClient real).
    ///
    /// Cada test construye respuestas JSON que replican el formato del endpoint
    /// /fapi/v1/aggTrades de Binance Futures y verifica que las features computadas
    /// son correctas y consistentes con MicrostructureFeatureComputer (paridad).
    /// </summary>
    public class BinanceAggTradeBackfillerTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime Hour0 = new(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Backfill_una_hora_sin_paginacion_computa_features_correctamente()
        {
            // Arrange: 2 buys, 1 sell en la hora
            // buy_vol=1.5, sell_vol=0.5, total=2.0, count=3
            // ofi=(1.5-0.5)/2=0.5, cvd_delta=1.0, arrival_rate=3, mean=2/3≈0.6667
            // buy_sell_ratio=1.5/0.5=3.0, price_return=(101-100)/100=0.01
            string response = AggTradeJson(new[]
            {
                (id: 1L, price: "100.0", qty: "1.0", ts: ToMs(Hour0, 0),    isMaker: false), // buy
                (id: 2L, price: "101.0", qty: "0.5", ts: ToMs(Hour0, 600),  isMaker: true),  // sell
                (id: 3L, price: "101.0", qty: "0.5", ts: ToMs(Hour0, 1200), isMaker: false), // buy
            });

            var backfiller = MakeBackfiller(new Dictionary<string, string>
            {
                [UrlFor("BTCUSDT", Hour0)] = response,
            });

            var bars = backfiller.Backfill(Btc, "BTCUSDT", Hour0, Hour0.AddHours(1), cvdRunning: 0.0);

            bars.Should().HaveCount(1);
            var bar = bars[0];
            bar.BarUtc.Should().Be(Hour0);
            bar.Ofi.Should().BeApproximately(0.5, 1e-9);
            bar.CvdDelta.Should().BeApproximately(1.0, 1e-9);
            bar.Cvd.Should().BeApproximately(1.0, 1e-9);
            bar.ArrivalRate.Should().BeApproximately(3, 1e-9);
            bar.MeanTradeSize.Should().BeApproximately(2.0 / 3.0, 1e-9);
            bar.BuySellRatio.Should().BeApproximately(3.0, 1e-9);
            bar.PriceReturn.Should().BeApproximately(0.01, 1e-12);
        }

        [Fact]
        public void Backfill_multiples_horas_acumula_cvd_correctamente()
        {
            // Hora 0: cvd_delta=+2 → Cvd=2
            // Hora 1: cvd_delta=-1 → Cvd=1
            var h1 = Hour0.AddHours(1);
            var responses = new Dictionary<string, string>
            {
                [UrlFor("BTCUSDT", Hour0)] = AggTradeJson(new[]
                {
                    (id: 1L, price: "100", qty: "2.0", ts: ToMs(Hour0, 0), isMaker: false), // buy 2
                }),
                [UrlFor("BTCUSDT", h1)] = AggTradeJson(new[]
                {
                    (id: 2L, price: "100", qty: "1.0", ts: ToMs(h1, 0), isMaker: true),     // sell 1
                }),
            };

            var bars = MakeBackfiller(responses).Backfill(Btc, "BTCUSDT", Hour0, Hour0.AddHours(2), cvdRunning: 0.0);

            bars.Should().HaveCount(2);
            bars[0].Cvd.Should().BeApproximately(2.0, 1e-9);
            bars[1].Cvd.Should().BeApproximately(1.0, 1e-9);
        }

        [Fact]
        public void Backfill_hora_vacia_se_omite_del_resultado()
        {
            // Hora 0: respuesta vacía → no se agrega barra
            // Hora 1: tiene datos
            var h1 = Hour0.AddHours(1);
            var responses = new Dictionary<string, string>
            {
                [UrlFor("BTCUSDT", Hour0)] = "[]",
                [UrlFor("BTCUSDT", h1)] = AggTradeJson(new[]
                {
                    (id: 1L, price: "100", qty: "1.0", ts: ToMs(h1, 0), isMaker: false),
                }),
            };

            var bars = MakeBackfiller(responses).Backfill(Btc, "BTCUSDT", Hour0, Hour0.AddHours(2), cvdRunning: 0.0);

            bars.Should().HaveCount(1);
            bars[0].BarUtc.Should().Be(h1);
        }

        [Fact]
        public void Backfill_pagina_cuando_primera_respuesta_tiene_1000_registros()
        {
            // Página 1: 1000 trades todos en Hour0, todos buys, qty=1.0 c/u
            // Página 2 (fromId=1001): 1 trade más, buy qty=0.5
            // Total buy_vol=1000.5, sell_vol=0, count=1001
            var page1 = Build1000Trades(startId: 1, price: "100", qty: "1.0", hourStart: Hour0, isMaker: false);
            var page2 = AggTradeJson(new[]
            {
                (id: 1001L, price: "100", qty: "0.5", ts: ToMs(Hour0, 59 * 60), isMaker: false),
            });

            var responses = new Dictionary<string, string>
            {
                [UrlFor("BTCUSDT", Hour0)] = page1,
                [$"https://fapi.binance.com/fapi/v1/aggTrades?symbol=BTCUSDT&fromId=1001&limit=1000"] = page2,
            };

            var bars = MakeBackfiller(responses).Backfill(Btc, "BTCUSDT", Hour0, Hour0.AddHours(1), cvdRunning: 0.0);

            bars.Should().HaveCount(1);
            bars[0].ArrivalRate.Should().BeApproximately(1001, 1e-9);
            bars[0].CvdDelta.Should().BeApproximately(1000.5, 1e-6);
        }

        [Fact]
        public void Backfill_error_de_api_no_lanza_excepcion_y_retorna_parcial()
        {
            // Primera hora OK, segunda hora lanza excepción
            var responses = new Dictionary<string, string>
            {
                [UrlFor("BTCUSDT", Hour0)] = AggTradeJson(new[]
                {
                    (id: 1L, price: "100", qty: "1.0", ts: ToMs(Hour0, 0), isMaker: false),
                }),
            };
            // La segunda hora no está en el dict → fetcher lanzará KeyNotFoundException
            var h1 = Hour0.AddHours(1);

            BinanceAggTradeBackfiller backfiller = MakeBackfiller(responses);
            var act = () => backfiller.Backfill(Btc, "BTCUSDT", Hour0, Hour0.AddHours(2), cvdRunning: 0.0);

            // No debe lanzar; retorna la barra de Hora0 y omite Hora1
            var bars = act.Should().NotThrow().Subject;
            bars.Should().HaveCount(1);
            bars[0].BarUtc.Should().Be(Hour0);
        }

        [Fact]
        public void Backfill_cvd_running_inicial_se_usa_como_semilla()
        {
            var responses = new Dictionary<string, string>
            {
                [UrlFor("BTCUSDT", Hour0)] = AggTradeJson(new[]
                {
                    (id: 1L, price: "100", qty: "1.0", ts: ToMs(Hour0, 0), isMaker: false), // buy 1
                }),
            };

            var bars = MakeBackfiller(responses).Backfill(Btc, "BTCUSDT", Hour0, Hour0.AddHours(1), cvdRunning: 500.0);

            bars.Should().HaveCount(1);
            bars[0].Cvd.Should().BeApproximately(501.0, 1e-9); // 500 seed + 1 delta
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static BinanceAggTradeBackfiller MakeBackfiller(Dictionary<string, string> responses) =>
            new(url => responses[url], "https://fapi.binance.com");

        private static string UrlFor(string symbol, DateTime hourStart)
        {
            long startMs = ToUnixMs(hourStart);
            long endMs   = ToUnixMs(hourStart.AddHours(1));
            return $"https://fapi.binance.com/fapi/v1/aggTrades?symbol={symbol}&startTime={startMs}&endTime={endMs - 1}&limit=1000";
        }

        private static string AggTradeJson(IEnumerable<(long id, string price, string qty, long ts, bool isMaker)> trades)
        {
            var items = new List<string>();
            foreach (var (id, price, qty, ts, isMaker) in trades)
                items.Add($"{{\"a\":{id},\"p\":\"{price}\",\"q\":\"{qty}\",\"T\":{ts},\"m\":{(isMaker ? "true" : "false")}}}");
            return $"[{string.Join(",", items)}]";
        }

        private static string Build1000Trades(long startId, string price, string qty, DateTime hourStart, bool isMaker)
        {
            var items = new List<string>(1000);
            for (int i = 0; i < 1000; i++)
            {
                long ts = ToUnixMs(hourStart) + i * 100; // cada 100ms
                items.Add($"{{\"a\":{startId + i},\"p\":\"{price}\",\"q\":\"{qty}\",\"T\":{ts},\"m\":{(isMaker ? "true" : "false")}}}");
            }
            return $"[{string.Join(",", items)}]";
        }

        private static long ToMs(DateTime hourStart, int offsetSeconds) =>
            ToUnixMs(hourStart.AddSeconds(offsetSeconds));

        private static long ToUnixMs(DateTime utc) =>
            (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;
    }
}
