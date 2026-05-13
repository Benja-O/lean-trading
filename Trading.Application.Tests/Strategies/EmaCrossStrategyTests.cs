using FluentAssertions;
using System;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Implementations;
using Xunit;

namespace Trading.Application.Tests.Strategies
{
    /// <summary>
    /// Tests de comportamiento de EmaCrossStrategy con datos sintéticos.
    ///
    /// Verifica que la estrategia genera las señales correctas cuando las condiciones
    /// matemáticas se dan deliberadamente. Diseño: pasar barras una por una y observar
    /// cuándo se emite cada SignalDirection.
    ///
    /// Cobertura:
    /// - Antes de IsReady: siempre Flat.
    /// - Primer signal después de IsReady: Flat (sin previo para comparar).
    /// - Cruce alcista (EmaFast pasa de debajo a encima de EmaSlow): Long.
    /// - Cruce bajista: Short.
    /// - Barras intermedias sin cruce: Flat.
    /// </summary>
    public class EmaCrossStrategyTests
    {
        private static readonly InstrumentId BtcUsdt = new("BTCUSDT");

        private static MarketBar BuildBar(decimal close, int barIndex)
        {
            return new MarketBar(
                BtcUsdt,
                close,
                new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(barIndex));
        }

        [Fact]
        public void EvaluateSignal_BeforeBothEmasReady_AlwaysReturnsFlat()
        {
            var strategy = new EmaCrossStrategy();

            // EMA(60) necesita 60 updates para IsReady. Antes de eso, siempre Flat.
            for (int barIndex = 0; barIndex < 59; barIndex++)
            {
                var bar = BuildBar(close: 100m, barIndex);
                var signal = strategy.EvaluateSignal(bar);

                signal.Should().Be(SignalDirection.Flat,
                    because: $"En la barra {barIndex}, antes de que EMA(60) esté lista, no puede emitir señal.");
            }
        }

        [Fact]
        public void EvaluateSignal_GeneratesLongOnBullishCross()
        {
            var strategy = new EmaCrossStrategy();

            // Primeras 80 barras laterales en 100 → ambas EMAs convergen a 100.
            for (int barIndex = 0; barIndex < 80; barIndex++)
            {
                strategy.EvaluateSignal(BuildBar(close: 100m, barIndex));
            }

            // A partir de la barra 80, subida fuerte: EmaFast (30) reacciona más rápido que EmaSlow (60).
            // Eventualmente EmaFast supera a EmaSlow → cruce alcista → Long en alguna barra.
            SignalDirection? observedLong = null;

            for (int barIndex = 80; barIndex < 160; barIndex++)
            {
                decimal close = 100m + (barIndex - 80) * 0.5m;
                var signal = strategy.EvaluateSignal(BuildBar(close, barIndex));

                if (signal == SignalDirection.Long)
                {
                    observedLong = signal;
                    break;
                }
            }

            observedLong.Should().Be(SignalDirection.Long,
                because: "Con subida progresiva sostenida, EmaFast(30) debe cruzar por encima de EmaSlow(60) en algún momento.");
        }

        [Fact]
        public void EvaluateSignal_GeneratesShortOnBearishCross()
        {
            var strategy = new EmaCrossStrategy();

            // Primero llevar las dos EMAs a un régimen alcista estable (EmaFast > EmaSlow).
            for (int barIndex = 0; barIndex < 80; barIndex++)
            {
                strategy.EvaluateSignal(BuildBar(close: 100m, barIndex));
            }

            // Subida sostenida para forzar cruce alcista y establecer PreviousSignal = +1.
            for (int barIndex = 80; barIndex < 200; barIndex++)
            {
                decimal close = 100m + (barIndex - 80) * 0.5m;
                strategy.EvaluateSignal(BuildBar(close, barIndex));
            }

            // Ahora bajada sostenida: EmaFast debe terminar cruzando por debajo de EmaSlow.
            SignalDirection? observedShort = null;

            for (int barIndex = 200; barIndex < 400; barIndex++)
            {
                decimal close = 160m - (barIndex - 200) * 0.5m;
                var signal = strategy.EvaluateSignal(BuildBar(close, barIndex));

                if (signal == SignalDirection.Short)
                {
                    observedShort = signal;
                    break;
                }
            }

            observedShort.Should().Be(SignalDirection.Short,
                because: "Con bajada progresiva sostenida tras un régimen alcista, EmaFast debe cruzar por debajo de EmaSlow.");
        }

        [Fact]
        public void EvaluateSignal_MultipleSymbols_KeepsIndependentState()
        {
            // Verifica que el estado por símbolo no se cruza: dos símbolos en paralelo
            // operados sobre la misma instancia de estrategia mantienen EMAs separadas.
            var strategy = new EmaCrossStrategy();
            var ethUsdt = new InstrumentId("ETHUSDT");

            // BTC: lateral en 100. ETH: lateral en 3000.
            for (int barIndex = 0; barIndex < 70; barIndex++)
            {
                strategy.EvaluateSignal(BuildBar(close: 100m, barIndex));

                var ethBar = new MarketBar(ethUsdt, 3000m,
                    new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(barIndex));
                strategy.EvaluateSignal(ethBar);
            }

            var btcFinal = strategy.EvaluateSignal(BuildBar(close: 100m, 70));
            btcFinal.Should().Be(SignalDirection.Flat, because: "En régimen lateral puro no debe haber cruce.");
        }
    }
}
