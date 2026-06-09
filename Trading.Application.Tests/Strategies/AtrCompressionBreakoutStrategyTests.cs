using FluentAssertions;
using System;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Implementations;
using Xunit;

namespace Trading.Application.Tests.Strategies
{
    /// <summary>
    /// Tests de comportamiento de AtrCompressionBreakoutStrategy con datos sintéticos.
    ///
    /// Diseño: el M4 validó la hipótesis estadísticamente; estos tests validan
    /// que la implementación C# produce la señal correcta cuando se dan las condiciones.
    ///
    /// Cobertura:
    /// - Antes del warm-up (114 barras): siempre Flat.
    /// - En compresión ATR + rompimiento alcista: Long.
    /// - En compresión ATR + rompimiento bajista: Short.
    /// - Fuera de compresión (ATR alto): Flat aunque haya rompimiento.
    /// - Dentro del rango (sin rompimiento): Flat aunque haya compresión.
    /// - Multi-símbolo: estado independiente por instrumento.
    /// </summary>
    public class AtrCompressionBreakoutStrategyTests
    {
        private static readonly InstrumentId BtcUsdt = new("BTCUSDT");
        private static readonly InstrumentId EthUsdt = new("ETHUSDT");
        private static readonly DateTime T0 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Construye una barra OHLCV con el cierre dado y un spread H/L simétrico fijo.
        /// </summary>
        private static MarketBar BuildBar(
            InstrumentId id, decimal close, int barIndex, decimal spread = 2m) =>
            new(id, close, close + spread, close - spread, close, 0m, T0.AddHours(barIndex));

        /// <summary>
        /// Alimenta la estrategia con `count` barras de rango lateral (close constante, spread fijo).
        /// Retorna el índice de barra siguiente.
        /// </summary>
        private static int WarmUp(AtrCompressionBreakoutStrategy strategy, InstrumentId id,
            int count, decimal close = 100m, int startIndex = 0, decimal spread = 1m)
        {
            for (int i = 0; i < count; i++)
                strategy.EvaluateSignal(BuildBar(id, close, startIndex + i, spread));
            return startIndex + count;
        }

        /// <summary>
        /// Crea un estado de compresión real usando dos fases:
        /// 1. spread alto (10) → AtrHistory se llena con ATR ≈ 20 (alta volatilidad).
        /// 2. spread bajo (0.1) → ATR decae a ≈ 0.7, muy por debajo del P20 del historial.
        ///
        /// Tras esta llamada: ATR actual ≈ 0.7 &lt; P20 ≈ 2.2 → compresión activa.
        /// PriceHistory: últimas 10 barras con close=<paramref name="close"/>.
        /// </summary>
        private static int WarmUpToCompression(AtrCompressionBreakoutStrategy strategy, InstrumentId id,
            decimal close = 100m, int startIndex = 0)
        {
            int nextBar = WarmUp(strategy, id, 200, close, startIndex, spread: 10m);
            return WarmUp(strategy, id, 50, close, nextBar, spread: 0.1m);
        }

        [Fact]
        public void EvaluateSignal_BeforeWarmUp_AlwaysReturnsFlat()
        {
            var strategy = new AtrCompressionBreakoutStrategy();

            // WarmUpBars = 114; verificamos hasta 113 (sin incluir la barra 114).
            for (int barIndex = 0; barIndex < strategy.WarmUpBars - 1; barIndex++)
            {
                var signal = strategy.EvaluateSignal(BuildBar(BtcUsdt, close: 100m, barIndex));
                signal.Should().Be(SignalDirection.Flat,
                    because: $"En la barra {barIndex} (antes del warm-up completo), la señal debe ser Flat.");
            }
        }

        [Fact]
        public void EvaluateSignal_InCompressionWithBullishBreakout_ReturnsLong()
        {
            var strategy = new AtrCompressionBreakoutStrategy();

            // WarmUpToCompression: fase alta (spread=10 → ATR≈20) + fase baja (spread=0.1 → ATR≈0.7).
            // Resultado: AtrHistory con 50 valores ≈ 20 y 50 valores decrecientes; P20 ≈ 2.2.
            // ATR actual ≈ 0.7 < P20 → compresión activa. PriceHistory: 10 barras con close=100.
            int nextBar = WarmUpToCompression(strategy, BtcUsdt, close: 100m);

            // Rompimiento alcista: close=110 > max(PriceHistory=100).
            var signal = strategy.EvaluateSignal(BuildBar(BtcUsdt, close: 110m, barIndex: nextBar, spread: 0.1m));

            signal.Should().Be(SignalDirection.Long,
                because: "Con ATR comprimido y cierre que supera el máximo reciente, debe emitir Long.");
        }

        [Fact]
        public void EvaluateSignal_InCompressionWithBearishBreakout_ReturnsShort()
        {
            var strategy = new AtrCompressionBreakoutStrategy();

            int nextBar = WarmUpToCompression(strategy, BtcUsdt, close: 100m);

            // Rompimiento bajista: close=90 < min(PriceHistory=100).
            var signal = strategy.EvaluateSignal(BuildBar(BtcUsdt, close: 90m, barIndex: nextBar, spread: 0.1m));

            signal.Should().Be(SignalDirection.Short,
                because: "Con ATR comprimido y cierre por debajo del mínimo reciente, debe emitir Short.");
        }

        [Fact]
        public void EvaluateSignal_HighAtr_ReturnsFlatEvenWithBreakout()
        {
            var strategy = new AtrCompressionBreakoutStrategy();

            // Fase 1: calentar con spread MUY alto (10) → ATR alto en todo el historial.
            // Así el P20 del historial ATR es alto, y el ATR actual no estará en "compresión".
            int nextBar = WarmUp(strategy, BtcUsdt, 200, close: 100m, spread: 10m);

            // Rompimiento de precio claro, pero ATR sigue siendo alto → sin compresión → Flat.
            var breakoutBar = BuildBar(BtcUsdt, close: 150m, barIndex: nextBar, spread: 10m);
            var signal = strategy.EvaluateSignal(breakoutBar);

            signal.Should().Be(SignalDirection.Flat,
                because: "Sin compresión ATR, el rompimiento de precio no debe disparar señal.");
        }

        [Fact]
        public void EvaluateSignal_InCompressionWithinRange_ReturnsFlatWhenNoBreakout()
        {
            var strategy = new AtrCompressionBreakoutStrategy();

            // Calentar con lateral de spread bajo.
            int nextBar = WarmUp(strategy, BtcUsdt, 200, close: 100m, spread: 1m);

            // El cierre se mantiene DENTRO del rango histórico (100 ± 0.5 < rango del warm-up).
            var withinRangeBar = BuildBar(BtcUsdt, close: 100.5m, barIndex: nextBar, spread: 1m);
            var signal = strategy.EvaluateSignal(withinRangeBar);

            signal.Should().Be(SignalDirection.Flat,
                because: "En compresión pero sin rompimiento de rango, no debe emitir señal.");
        }


        [Fact]
        public void EvaluateSignal_MultipleSymbols_KeepsIndependentState()
        {
            var strategy = new AtrCompressionBreakoutStrategy();

            // ETH: warm-up con spread alto → ATR alto ≈ P20 → sin compresión.
            // Se hace primero para garantizar que el estado ETH no interfiere con BTC.
            int ethBar = WarmUp(strategy, EthUsdt, 200, close: 3000m, spread: 100m);

            // BTC: dos fases → compresión activa (ATR ≈ 0.7 < P20 ≈ 2.2).
            int btcBar = WarmUpToCompression(strategy, BtcUsdt, close: 100m);

            // BTC: rompimiento alcista → Long.
            // close=110 produce TR=10.1 con prev_close=100; ATR ≈ 1.36 < P20 ≈ 2.05 → compresión activa.
            var btcSignal = strategy.EvaluateSignal(BuildBar(BtcUsdt, 110m, btcBar, spread: 0.1m));
            btcSignal.Should().Be(SignalDirection.Long,
                because: "BTC con ATR comprimido y breakout debe emitir Long.");

            // ETH: rompimiento de precio pero sin compresión ATR → Flat.
            var ethSignal = strategy.EvaluateSignal(BuildBar(EthUsdt, 4000m, ethBar, spread: 100m));
            ethSignal.Should().Be(SignalDirection.Flat,
                because: "ETH con ATR alto no debe emitir señal, incluso con rompimiento de precio.");
        }
    }
}
