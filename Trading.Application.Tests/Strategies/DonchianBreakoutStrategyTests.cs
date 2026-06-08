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
    /// Los loops de warmup usan strategy.WarmUpBars para ser agnósticos al lookback
    /// configurado en la estrategia — los tests pasan sin cambios cuando cambia LookbackPeriods.
    ///
    /// Cobertura:
    /// - Antes de IsReady (&lt;WarmUpBars barras): siempre Flat.
    /// - Breakout alcista: Long en la barra que supera el máximo de las N anteriores.
    /// - Sin entrada repetida: segunda barra sobre canal → Flat.
    /// - Breakout bajista: Short en la barra que cae bajo el mínimo de las N anteriores.
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

            for (int i = 0; i < strategy.WarmUpBars - 1; i++)
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
            int n = strategy.WarmUpBars;

            for (int i = 0; i < n; i++)
                strategy.EvaluateSignal(FlatBar(i));

            // Barra N+1: cierre 103 > máximo del canal (102) → Long
            var breakoutBar = Bar(BtcUsdt, close: 103m, high: 104m, low: 101m, index: n);
            var signal = strategy.EvaluateSignal(breakoutBar);

            signal.Should().Be(SignalDirection.Long,
                because: "El cierre supera el máximo de las N barras anteriores.");
        }

        [Fact]
        public void EvaluateSignal_SecondBarAboveChannel_ReturnsFlat()
        {
            var strategy = new DonchianBreakoutStrategy();
            int n = strategy.WarmUpBars;

            for (int i = 0; i < n; i++)
                strategy.EvaluateSignal(FlatBar(i));

            // Primera barra de breakout: estado pasa a Long
            strategy.EvaluateSignal(Bar(BtcUsdt, close: 103m, high: 110m, low: 101m, index: n));

            // Segunda barra: sigue en estado Long → no re-emite
            var nextBar = Bar(BtcUsdt, close: 112m, high: 113m, low: 109m, index: n + 1);
            var signal = strategy.EvaluateSignal(nextBar);

            signal.Should().Be(SignalDirection.Flat,
                because: "Ya estamos en estado Long: no se re-emite en barras subsiguientes del mismo breakout.");
        }

        [Fact]
        public void EvaluateSignal_BreakoutBelowChannel_ReturnsShort()
        {
            var strategy = new DonchianBreakoutStrategy();
            int n = strategy.WarmUpBars;

            for (int i = 0; i < n; i++)
                strategy.EvaluateSignal(FlatBar(i));

            // Barra N+1: cierre 97 < mínimo del canal (98) → Short
            var breakoutBar = Bar(BtcUsdt, close: 97m, high: 99m, low: 96m, index: n);
            var signal = strategy.EvaluateSignal(breakoutBar);

            signal.Should().Be(SignalDirection.Short,
                because: "El cierre cae por debajo del mínimo de las N barras anteriores.");
        }

        [Fact]
        public void EvaluateSignal_DirectLongToShort_EmitsShort()
        {
            var strategy = new DonchianBreakoutStrategy();
            int n = strategy.WarmUpBars;

            for (int i = 0; i < n; i++)
                strategy.EvaluateSignal(FlatBar(i));

            // Barra N+1: breakout alcista → estado Long
            // Canal tras esta barra: max = 104 (su high), min = 98 (flat bars)
            strategy.EvaluateSignal(Bar(BtcUsdt, close: 103m, high: 104m, low: 101m, index: n));

            // Barra N+2: colapso directo por debajo del mínimo del canal ajustado
            // Canal = max(barras 2..N+1): max = 104, min = 98 → close=97 < 98 → Short
            var collapseBar = Bar(BtcUsdt, close: 97m, high: 103m, low: 96m, index: n + 1);
            var signal = strategy.EvaluateSignal(collapseBar);

            signal.Should().Be(SignalDirection.Short,
                because: "Una transición directa de estado Long a estado Short emite Short.");
        }

        [Fact]
        public void EvaluateSignal_MultipleSymbols_MaintainIndependentState()
        {
            var strategy = new DonchianBreakoutStrategy();
            int n = strategy.WarmUpBars;

            for (int i = 0; i < n; i++)
            {
                strategy.EvaluateSignal(FlatBar(i, price: 100m));
                strategy.EvaluateSignal(Bar(EthUsdt, 3000m, 3020m, 2980m, i));
            }

            // BTC rompe hacia arriba
            var btcBreakout = Bar(BtcUsdt, close: 105m, high: 106m, low: 103m, index: n);
            var btcSignal = strategy.EvaluateSignal(btcBreakout);

            // ETH sigue lateral
            var ethFlat = Bar(EthUsdt, close: 3010m, high: 3025m, low: 2995m, index: n);
            var ethSignal = strategy.EvaluateSignal(ethFlat);

            btcSignal.Should().Be(SignalDirection.Long,
                because: "BTC rompe el canal hacia arriba.");
            ethSignal.Should().Be(SignalDirection.Flat,
                because: "ETH no rompió su canal — el estado de cada símbolo es independiente.");
        }
    }
}
