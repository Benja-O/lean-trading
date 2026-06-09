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
        /// Alimenta la estrategia con `count` barras de rango lateral (close constante, spread bajo)
        /// para llenar los indicadores sin disparar señales. Retorna el índice de barra siguiente.
        /// </summary>
        private static int WarmUp(AtrCompressionBreakoutStrategy strategy, InstrumentId id,
            int count, decimal close = 100m, int startIndex = 0, decimal spread = 1m)
        {
            for (int i = 0; i < count; i++)
                strategy.EvaluateSignal(BuildBar(id, close, startIndex + i, spread));
            return startIndex + count;
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

            // Fase 1: 200 barras de lateral con spread=1 → ATR ≈ 1 (bajo).
            // Estas barras llenan el historial ATR con valores bajos → P20 ≈ 1.
            // También llenan el historial de precios con close=100.
            int nextBar = WarmUp(strategy, BtcUsdt, 200, close: 100m, spread: 1m);

            // Fase 2: disparar un rompimiento alcista MIENTRAS el ATR sigue bajo.
            // El cierre de 110 > max(PriceHistory=100) con spread=1 → ATR ≈ 1 < P20 del historial bajo.
            var breakoutBar = BuildBar(BtcUsdt, close: 110m, barIndex: nextBar, spread: 1m);
            var signal = strategy.EvaluateSignal(breakoutBar);

            signal.Should().Be(SignalDirection.Long,
                because: "Con ATR comprimido y cierre que supera el máximo reciente, debe emitir Long.");
        }

        [Fact]
        public void EvaluateSignal_InCompressionWithBearishBreakout_ReturnsShort()
        {
            var strategy = new AtrCompressionBreakoutStrategy();

            int nextBar = WarmUp(strategy, BtcUsdt, 200, close: 100m, spread: 1m);

            // Rompimiento bajista: cierre de 90 < min(PriceHistory=100).
            var breakoutBar = BuildBar(BtcUsdt, close: 90m, barIndex: nextBar, spread: 1m);
            var signal = strategy.EvaluateSignal(breakoutBar);

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

            // BTC: warm-up con spread bajo.
            int btcBar = WarmUp(strategy, BtcUsdt, 200, close: 100m, spread: 1m);

            // ETH: warm-up con spread ALTO → ATR alto → no compresión.
            int ethBar = WarmUp(strategy, EthUsdt, 200, close: 3000m, spread: 100m);

            // BTC: rompimiento alcista claro en compresión → Long.
            var btcSignal = strategy.EvaluateSignal(BuildBar(BtcUsdt, 120m, btcBar, spread: 1m));
            btcSignal.Should().Be(SignalDirection.Long,
                because: "BTC con ATR comprimido y breakout debe emitir Long.");

            // ETH: rompimiento de precio pero ATR alto → Flat.
            var ethSignal = strategy.EvaluateSignal(BuildBar(EthUsdt, 4000m, ethBar, spread: 100m));
            ethSignal.Should().Be(SignalDirection.Flat,
                because: "ETH con ATR alto no debe emitir señal, incluso con rompimiento de precio.");
        }
    }
}
