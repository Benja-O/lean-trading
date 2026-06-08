using FluentAssertions;
using System;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Implementations;
using Xunit;

namespace Trading.Application.Tests.Strategies
{
    /// <summary>
    /// Tests de comportamiento de DonchianBreakoutStrategy con datos sintéticos.
    ///
    /// Cobertura:
    /// - Antes de IsReady (&lt;20 barras): siempre Flat.
    /// - Breakout alcista: Long en la barra que supera el máximo de las 20 anteriores.
    /// - Sin entrada repetida: segunda barra sobre canal → Flat.
    /// - Breakout bajista: Short en la barra que cae bajo el mínimo de las 20 anteriores.
    /// - Transición directa Long→Short.
    /// - Multi-símbolo: estado independiente por ticker.
    /// </summary>
    public class DonchianBreakoutStrategyTests
    {
        private static readonly InstrumentId BtcUsdt = new("BTCUSDT");
        private static readonly InstrumentId EthUsdt = new("ETHUSDT");
        private static readonly DateTime BaseTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static MarketBar Bar(InstrumentId id, decimal close, decimal high, decimal low, int index) =>
            new(id, close, high, low, close, 1000m, BaseTime.AddHours(index * 4));

        private static MarketBar FlatBar(int index, decimal price = 100m) =>
            Bar(BtcUsdt, price, price + 2m, price - 2m, index);

        [Fact]
        public void EvaluateSignal_BeforeReady_AlwaysReturnsFlat()
        {
            var strategy = new DonchianBreakoutStrategy();

            for (int i = 0; i < 19; i++)
            {
                var signal = strategy.EvaluateSignal(FlatBar(i));
                signal.Should().Be(SignalDirection.Flat,
                    because: $"Con solo {i + 1} barras la ventana no está completa todavía.");
            }
        }

        [Fact]
        public void EvaluateSignal_BreakoutAboveChannel_ReturnsLong()
        {
            var strategy = new DonchianBreakoutStrategy();

            // 20 barras laterales: canal [98, 102]
            for (int i = 0; i < 20; i++)
                strategy.EvaluateSignal(FlatBar(i));

            // Barra 21: cierre 103 > máximo del canal (102) → Long
            var breakoutBar = Bar(BtcUsdt, close: 103m, high: 104m, low: 101m, index: 20);
            var signal = strategy.EvaluateSignal(breakoutBar);

            signal.Should().Be(SignalDirection.Long,
                because: "El cierre supera el máximo de las 20 barras anteriores.");
        }

        [Fact]
        public void EvaluateSignal_SecondBarAboveChannel_ReturnsFlat()
        {
            var strategy = new DonchianBreakoutStrategy();

            for (int i = 0; i < 20; i++)
                strategy.EvaluateSignal(FlatBar(i));

            // Barra 21: breakout → Long
            strategy.EvaluateSignal(Bar(BtcUsdt, close: 103m, high: 110m, low: 101m, index: 20));

            // Barra 22: sigue sobre el canal ajustado (que ahora incluye high=110) → cierre 107 < 110 → Flat
            // Pero si el cierre también supera el nuevo canal, igual no debe emitir porque estamos en estado Long
            var nextBar = Bar(BtcUsdt, close: 112m, high: 113m, low: 109m, index: 21);
            var signal = strategy.EvaluateSignal(nextBar);

            signal.Should().Be(SignalDirection.Flat,
                because: "Ya estamos en estado Long: no se re-emite en barras subsiguientes del mismo breakout.");
        }

        [Fact]
        public void EvaluateSignal_BreakoutBelowChannel_ReturnsShort()
        {
            var strategy = new DonchianBreakoutStrategy();

            for (int i = 0; i < 20; i++)
                strategy.EvaluateSignal(FlatBar(i));

            // Barra 21: cierre 97 < mínimo del canal (98) → Short
            var breakoutBar = Bar(BtcUsdt, close: 97m, high: 99m, low: 96m, index: 20);
            var signal = strategy.EvaluateSignal(breakoutBar);

            signal.Should().Be(SignalDirection.Short,
                because: "El cierre cae por debajo del mínimo de las 20 barras anteriores.");
        }

        [Fact]
        public void EvaluateSignal_DirectLongToShort_EmitsShort()
        {
            var strategy = new DonchianBreakoutStrategy();

            for (int i = 0; i < 20; i++)
                strategy.EvaluateSignal(FlatBar(i));

            // Barra 21: breakout alcista
            strategy.EvaluateSignal(Bar(BtcUsdt, close: 103m, high: 104m, low: 101m, index: 20));

            // Barra 22: el canal se ajusta, pero simulamos caída extrema por debajo del canal ajustado
            // Canal tras barra 21: max(barras 2-21) = 104, min(barras 2-21) = 98
            // Barra 22 close = 97 < 98 → transición Long→Short
            var collapseBar = Bar(BtcUsdt, close: 97m, high: 103m, low: 96m, index: 21);
            var signal = strategy.EvaluateSignal(collapseBar);

            signal.Should().Be(SignalDirection.Short,
                because: "Una transición directa de estado Long a estado Short emite Short.");
        }

        [Fact]
        public void EvaluateSignal_MultipleSymbols_MaintainIndependentState()
        {
            var strategy = new DonchianBreakoutStrategy();

            // BTC y ETH en paralelo, ambos laterales
            for (int i = 0; i < 20; i++)
            {
                strategy.EvaluateSignal(FlatBar(i, price: 100m));
                strategy.EvaluateSignal(Bar(EthUsdt, 3000m, 3020m, 2980m, i));
            }

            // BTC rompe hacia arriba
            var btcBreakout = Bar(BtcUsdt, close: 105m, high: 106m, low: 103m, index: 20);
            var btcSignal = strategy.EvaluateSignal(btcBreakout);

            // ETH sigue lateral (no rompe nada)
            var ethFlat = Bar(EthUsdt, close: 3010m, high: 3025m, low: 2995m, index: 20);
            var ethSignal = strategy.EvaluateSignal(ethFlat);

            btcSignal.Should().Be(SignalDirection.Long,
                because: "BTC rompe el canal hacia arriba.");
            ethSignal.Should().Be(SignalDirection.Flat,
                because: "ETH no rompió su canal — el estado de cada símbolo es independiente.");
        }
    }
}
