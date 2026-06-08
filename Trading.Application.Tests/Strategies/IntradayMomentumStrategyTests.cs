using FluentAssertions;
using System;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Implementations;
using Xunit;

namespace Trading.Application.Tests.Strategies
{
    /// <summary>
    /// Tests de IntradayMomentumStrategy.
    ///
    /// Lógica en dos pasos:
    ///   Bar_0 (00:00 UTC): registra dirección, siempre retorna Flat.
    ///   Bar_47 (23:30 UTC): emite la dirección registrada si es del mismo día.
    ///   Todas las demás barras: Flat.
    /// </summary>
    public class IntradayMomentumStrategyTests
    {
        private static readonly InstrumentId EthUsdt = new("ETHUSDT");
        private static readonly InstrumentId BnbUsdt = new("BNBUSDT");

        private static MarketBar Bar(InstrumentId id, decimal open, decimal close, int dayOffset, int hour, int minute) =>
            new(id, open, open + 1m, open - 1m, close, 1000m,
                new DateTime(2025, 1, 1, hour, minute, 0, DateTimeKind.Utc).AddDays(dayOffset));

        private static MarketBar Bar0(InstrumentId id, decimal open, decimal close, int dayOffset = 0) =>
            Bar(id, open, close, dayOffset, hour: 0, minute: 0);

        private static MarketBar Bar47(InstrumentId id, int dayOffset = 0) =>
            Bar(id, 2000m, 2010m, dayOffset, hour: 23, minute: 30);

        private static MarketBar MidBar(InstrumentId id, int hour = 12, int dayOffset = 0) =>
            Bar(id, 2000m, 2010m, dayOffset, hour, minute: 0);

        [Fact]
        public void Bar0_AlwaysReturnsFlat_RegardlessOfDirection()
        {
            var strategy = new IntradayMomentumStrategy();

            strategy.EvaluateSignal(Bar0(EthUsdt, open: 2000m, close: 2100m)).Should().Be(SignalDirection.Flat,
                because: "Bar_0 registra la dirección pero no emite señal todavía.");
        }

        [Fact]
        public void Bar47_WithoutPriorBar0_ReturnsFlat()
        {
            var strategy = new IntradayMomentumStrategy();

            strategy.EvaluateSignal(Bar47(EthUsdt)).Should().Be(SignalDirection.Flat,
                because: "Sin bar_0 registrada, bar_47 no tiene señal que emitir.");
        }

        [Fact]
        public void Bar0Bullish_ThenBar47_ReturnsLong()
        {
            var strategy = new IntradayMomentumStrategy();

            strategy.EvaluateSignal(Bar0(EthUsdt, open: 2000m, close: 2050m));
            var signal = strategy.EvaluateSignal(Bar47(EthUsdt));

            signal.Should().Be(SignalDirection.Long,
                because: "Bar_0 subió → se espera que bar_47 también suba.");
        }

        [Fact]
        public void Bar0Bearish_ThenBar47_ReturnsShort()
        {
            var strategy = new IntradayMomentumStrategy();

            strategy.EvaluateSignal(Bar0(EthUsdt, open: 2000m, close: 1950m));
            var signal = strategy.EvaluateSignal(Bar47(EthUsdt));

            signal.Should().Be(SignalDirection.Short,
                because: "Bar_0 bajó → se espera que bar_47 también baje.");
        }

        [Fact]
        public void Bar0Flat_ThenBar47_ReturnsFlat()
        {
            var strategy = new IntradayMomentumStrategy();

            strategy.EvaluateSignal(Bar0(EthUsdt, open: 2000m, close: 2000m));
            var signal = strategy.EvaluateSignal(Bar47(EthUsdt));

            signal.Should().Be(SignalDirection.Flat,
                because: "Sin retorno en bar_0 no hay señal.");
        }

        [Fact]
        public void Bar0Day1_ThenBar47Day2_ReturnsFlat()
        {
            var strategy = new IntradayMomentumStrategy();

            strategy.EvaluateSignal(Bar0(EthUsdt, open: 2000m, close: 2100m, dayOffset: 0));
            var signal = strategy.EvaluateSignal(Bar47(EthUsdt, dayOffset: 1));

            signal.Should().Be(SignalDirection.Flat,
                because: "La señal de bar_0 del día anterior no vale para bar_47 de otro día.");
        }

        [Fact]
        public void MidDayBars_AlwaysReturnFlat()
        {
            var strategy = new IntradayMomentumStrategy();

            strategy.EvaluateSignal(Bar0(EthUsdt, open: 2000m, close: 2100m));

            strategy.EvaluateSignal(MidBar(EthUsdt, hour: 1)).Should().Be(SignalDirection.Flat);
            strategy.EvaluateSignal(MidBar(EthUsdt, hour: 6)).Should().Be(SignalDirection.Flat);
            strategy.EvaluateSignal(MidBar(EthUsdt, hour: 12)).Should().Be(SignalDirection.Flat);
            strategy.EvaluateSignal(MidBar(EthUsdt, hour: 18)).Should().Be(SignalDirection.Flat);
        }

        [Fact]
        public void MultipleSymbols_MaintainIndependentState()
        {
            var strategy = new IntradayMomentumStrategy();

            strategy.EvaluateSignal(Bar0(EthUsdt, open: 2000m, close: 2100m));
            strategy.EvaluateSignal(Bar0(BnbUsdt, open: 300m,  close: 280m));

            strategy.EvaluateSignal(Bar47(EthUsdt)).Should().Be(SignalDirection.Long);
            strategy.EvaluateSignal(Bar47(BnbUsdt)).Should().Be(SignalDirection.Short);
        }
    }
}
