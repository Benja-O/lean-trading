using FluentAssertions;
using System;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Implementations;
using Xunit;

namespace Trading.Application.Tests.Strategies
{
    public class IntradayMomentumStrategyTests
    {
        private static readonly InstrumentId EthUsdt = new("ETHUSDT");
        private static readonly InstrumentId BnbUsdt = new("BNBUSDT");

        private static MarketBar Bar(InstrumentId id, decimal open, decimal close, DateTime timestampUtc) =>
            new(id, open, open + 1m, open - 1m, close, 1000m, timestampUtc);

        private static DateTime Day0Bar(int dayOffset = 0) =>
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(dayOffset);

        private static DateTime BarAt(int hour, int minute) =>
            new DateTime(2025, 1, 1, hour, minute, 0, DateTimeKind.Utc);

        [Fact]
        public void EvaluateSignal_BarNotAtMidnight_ReturnsFlat()
        {
            var strategy = new IntradayMomentumStrategy();

            // 00:30 — segunda barra del día
            strategy.EvaluateSignal(Bar(EthUsdt, 2000m, 2100m, BarAt(0, 30))).Should().Be(SignalDirection.Flat);
            // 06:00 — mitad de la mañana
            strategy.EvaluateSignal(Bar(EthUsdt, 2000m, 2100m, BarAt(6, 0))).Should().Be(SignalDirection.Flat);
            // 23:30 — última barra del día
            strategy.EvaluateSignal(Bar(EthUsdt, 2000m, 2100m, BarAt(23, 30))).Should().Be(SignalDirection.Flat);
        }

        [Fact]
        public void EvaluateSignal_FirstBarOfDayCloseAboveOpen_ReturnsLong()
        {
            var strategy = new IntradayMomentumStrategy();

            var signal = strategy.EvaluateSignal(Bar(EthUsdt, open: 2000m, close: 2050m, Day0Bar()));

            signal.Should().Be(SignalDirection.Long,
                because: "Close > Open en la barra de 00:00 UTC indica momentum alcista.");
        }

        [Fact]
        public void EvaluateSignal_FirstBarOfDayCloseBelowOpen_ReturnsShort()
        {
            var strategy = new IntradayMomentumStrategy();

            var signal = strategy.EvaluateSignal(Bar(EthUsdt, open: 2000m, close: 1950m, Day0Bar()));

            signal.Should().Be(SignalDirection.Short,
                because: "Close < Open en la barra de 00:00 UTC indica momentum bajista.");
        }

        [Fact]
        public void EvaluateSignal_FirstBarOfDayCloseEqualsOpen_ReturnsFlat()
        {
            var strategy = new IntradayMomentumStrategy();

            var signal = strategy.EvaluateSignal(Bar(EthUsdt, open: 2000m, close: 2000m, Day0Bar()));

            signal.Should().Be(SignalDirection.Flat,
                because: "Sin retorno en la barra de apertura no hay señal.");
        }

        [Fact]
        public void EvaluateSignal_MultipleDays_EmitsSignalEachDay()
        {
            var strategy = new IntradayMomentumStrategy();

            var day1 = strategy.EvaluateSignal(Bar(EthUsdt, 2000m, 2050m, Day0Bar(0)));
            var day2 = strategy.EvaluateSignal(Bar(EthUsdt, 2100m, 2050m, Day0Bar(1)));

            day1.Should().Be(SignalDirection.Long);
            day2.Should().Be(SignalDirection.Short);
        }

        [Fact]
        public void EvaluateSignal_MultipleSymbols_EachEvaluatedIndependently()
        {
            var strategy = new IntradayMomentumStrategy();

            var eth = strategy.EvaluateSignal(Bar(EthUsdt, 2000m, 2100m, Day0Bar()));
            var bnb = strategy.EvaluateSignal(Bar(BnbUsdt, 300m, 280m, Day0Bar()));

            eth.Should().Be(SignalDirection.Long);
            bnb.Should().Be(SignalDirection.Short);
        }
    }
}
