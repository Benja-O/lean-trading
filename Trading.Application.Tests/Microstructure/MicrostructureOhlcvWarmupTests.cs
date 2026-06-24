using System;
using System.Collections.Generic;
using FluentAssertions;
using Trading.Application.Microstructure;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Microstructure
{
    /// <summary>
    /// Cubre el OHLCV agregado a la barra de microestructura para el warmup desde store (HITO-D):
    ///   - AggTradeBucket / MicrostructureFeatureComputer pueblan OHLCV desde los aggTrades.
    ///   - El warmup genérico (replay de barras históricas por EvaluateSignal, igual que hace el host
    ///     desde el store) deja una estrategia lista para señalizar, sin depender de history de precios
    ///     del broker (que en live-tick no existe). El contraste sin replay demuestra el problema que
    ///     el mecanismo resuelve.
    /// </summary>
    public class MicrostructureOhlcvWarmupTests
    {
        private static readonly InstrumentId Btc = new("BTCUSDT");
        private static readonly DateTime BaseTime = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Compute_pobla_OHLCV_desde_el_bucket_de_aggTrades()
        {
            var bucket = new AggTradeBucket();
            bucket.Add(100m, 1m, isBuyerMaker: false); // open
            bucket.Add(110m, 2m, isBuyerMaker: false); // high
            bucket.Add(95m,  1m, isBuyerMaker: true);  // low
            bucket.Add(105m, 3m, isBuyerMaker: false); // close

            var (bar, _) = MicrostructureFeatureComputer.Compute(Btc, BaseTime, bucket, cvdRunning: 0);

            bar.Open.Should().Be(100m);
            bar.High.Should().Be(110m);
            bar.Low.Should().Be(95m);
            bar.Close.Should().Be(105m);
            bar.Volume.Should().Be(7m); // 1+2+1+3
        }

        [Fact]
        public void Bucket_sin_datos_expone_OHLCV_en_cero()
        {
            var bucket = new AggTradeBucket();

            bucket.HasData.Should().BeFalse();
            bucket.HighPrice.Should().Be(0m);
            bucket.LowPrice.Should().Be(0m);
            bucket.Volume.Should().Be(0m);
        }

        [Fact]
        public void Replay_de_barras_historicas_warmea_la_estrategia_para_senalizar()
        {
            // Simula el warmup del host: el provider tiene las features históricas y se reproducen
            // las barras por EvaluateSignal. La estrategia de test necesita Lookback(48) closes.
            var provider = new FakeMicrostructureProvider();
            var strategy = new WarmupSensitiveTestStrategy(provider);

            for (int barIndex = 0; barIndex < 50; barIndex++)
            {
                var barTime = BaseTime.AddHours(barIndex);
                provider.Add(MicrostructureBarAt(barTime, close: 100m, cvdDelta: 5.0));
                strategy.EvaluateSignal(MarketBarAt(barTime, 100m)); // replay; señal descartada
            }

            // Barra nueva: nuevo mínimo de 48 (close 90 < 100) con cvdDelta > 0 → Long.
            var triggerTime = BaseTime.AddHours(50);
            provider.Add(MicrostructureBarAt(triggerTime, close: 90m, cvdDelta: 5.0));

            strategy.EvaluateSignal(MarketBarAt(triggerTime, 90m)).Should().Be(SignalDirection.Long);
        }

        [Fact]
        public void Sin_replay_la_estrategia_sigue_en_warmup_y_retorna_Flat()
        {
            // Contraste: misma barra trigger pero sin warmear → la cola interna está vacía → Flat.
            // Es exactamente el problema (1-2 días ciega tras cada arranque) que el warmup desde store resuelve.
            var provider = new FakeMicrostructureProvider();
            var strategy = new WarmupSensitiveTestStrategy(provider);
            var triggerTime = BaseTime;

            provider.Add(MicrostructureBarAt(triggerTime, close: 90m, cvdDelta: 5.0));

            strategy.EvaluateSignal(MarketBarAt(triggerTime, 90m)).Should().Be(SignalDirection.Flat);
        }

        // ── doble de test ────────────────────────────────────────────────────

        /// <summary>
        /// Estrategia de TEST (no de producción) que replica la dependencia de warmup de una
        /// estrategia microestructural: necesita Lookback closes antes de poder señalizar. Reemplaza
        /// como fixture a CvdSellExhaustionStrategy (rechazada 2026-06-24 por lookahead, ADR-054).
        /// Señaliza Long cuando el close es mínimo de la ventana y cvdDelta &gt; 0 — la misma forma que
        /// hacía falta para ejercitar el replay de warmup.
        /// </summary>
        private sealed class WarmupSensitiveTestStrategy : IStrategy
        {
            private const int Lookback = 48;
            private readonly IMicrostructureProvider _provider;
            private readonly Queue<double> _closes = new();

            public int WarmUpBars => Lookback + 2;

            public WarmupSensitiveTestStrategy(IMicrostructureProvider provider) => _provider = provider;

            public SignalDirection EvaluateSignal(MarketBar marketBar)
            {
                var microstructureBar = _provider?.GetBar(marketBar.InstrumentId, marketBar.TimestampUtc);
                if (microstructureBar == null) return SignalDirection.Flat;

                double close = (double)marketBar.Close;
                if (_closes.Count < Lookback - 1)
                {
                    _closes.Enqueue(close);
                    return SignalDirection.Flat;
                }

                bool isMinimum = true;
                foreach (var previous in _closes)
                    if (previous < close) { isMinimum = false; break; }

                _closes.Dequeue();
                _closes.Enqueue(close);

                return isMinimum && microstructureBar.CvdDelta > 0 ? SignalDirection.Long : SignalDirection.Flat;
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static MicrostructureBar MicrostructureBarAt(DateTime barUtc, decimal close, double cvdDelta) =>
            new(Btc, DateTime.SpecifyKind(barUtc, DateTimeKind.Utc),
                ofi: 0, cvdDelta: cvdDelta, cvd: 0, arrivalRate: 100,
                meanTradeSize: 1.0, buySellRatio: 1.0, priceReturn: 0)
            {
                Open = close, High = close, Low = close, Close = close, Volume = 1m,
            };

        private static MarketBar MarketBarAt(DateTime barUtc, decimal close) =>
            new(Btc, close, close, close, close, volume: 1m, DateTime.SpecifyKind(barUtc, DateTimeKind.Utc));
    }
}
