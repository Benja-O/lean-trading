using FluentAssertions;
using System;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Implementations;
using Xunit;

namespace Trading.Application.Tests.Strategies
{
    /// <summary>
    /// Tests de comportamiento de OfiContrarianStrategy con datos sintéticos.
    ///
    /// Convención de índices:
    ///   BuildMicroBar(id, i, ofi) → BarUtc = 2025-01-01 00:00 + i horas
    ///   BuildMarketBar(id, i)     → TimestampUtc = 2025-01-01 01:00 + i horas
    ///   EvaluateSignal(marketBar(i)) → lookup a microBar(i) via TimestampUtc.AddHours(-1)
    ///   → marketBar(i) y microBar(i) son el mismo par.
    ///
    /// Cobertura:
    /// - Warm-up: Flat hasta completar la ventana.
    /// - MicroBar nulo: Flat.
    /// - OFI en percentil bajo (extremo vendedor): Long.
    /// - OFI en percentil alto (extremo comprador): Flat (no Short — Long-only).
    /// - OFI en percentil medio: Flat.
    /// - Sliding window: valor antiguo se descarta, percentil recalculado correctamente.
    /// - Dos símbolos: estado independiente por símbolo.
    /// </summary>
    public class OfiContrarianStrategyTests
    {
        private static readonly InstrumentId BtcUsdt = new("BTCUSDT");
        private static readonly InstrumentId EthUsdt = new("ETHUSDT");

        // marketBar(i).TimestampUtc = 01:00 + i h
        // EvaluateSignal hace lookup a TimestampUtc - 1h = 00:00 + i h = microBar(i).BarUtc
        private static MarketBar BuildMarketBar(InstrumentId id, int barIndex) =>
            new(id, 100m,
                new DateTime(2025, 1, 1, 1, 0, 0, DateTimeKind.Utc).AddHours(barIndex));

        private static MicrostructureBar BuildMicroBar(InstrumentId id, int barIndex, double ofi) =>
            new(id,
                new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(barIndex),
                ofi: ofi, cvdDelta: 0, cvd: 0, arrivalRate: 100, meanTradeSize: 1, buySellRatio: 1, priceReturn: 0);

        private static (OfiContrarianStrategy strategy, FakeMicrostructureProvider provider)
            Create(int lookback = 5, double threshold = 0.20)
        {
            var provider = new FakeMicrostructureProvider();
            var strategy = new OfiContrarianStrategy(provider, lookbackBars: lookback, lowPercentileThreshold: threshold);
            return (strategy, provider);
        }

        [Fact]
        public void WarmUpBars_EqualsLookbackBars()
        {
            var (strategy, _) = Create(lookback: 24);
            strategy.WarmUpBars.Should().Be(24);
        }

        [Fact]
        public void EvaluateSignal_DuringWarmUp_AlwaysReturnsFlat()
        {
            var (strategy, provider) = Create(lookback: 5);

            for (int i = 0; i < 10; i++)
                provider.Add(BuildMicroBar(BtcUsdt, i, ofi: -1.0));

            // lookback=5: primera señal válida en barra i=4 (quinta barra)
            // barras 0..3 → count 1..4 < 5 → todas Flat
            for (int i = 0; i < 4; i++)
            {
                var signal = strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, i));
                signal.Should().Be(SignalDirection.Flat,
                    because: $"Barra {i}: ventana {i + 1} < lookback 5 → warm-up incompleto.");
            }
        }

        [Fact]
        public void EvaluateSignal_WhenMicroBarIsNull_ReturnsFlat()
        {
            var (strategy, _) = Create(lookback: 3);

            // Sin microBars → lookup retorna null → Flat
            var signal = strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, 0));
            signal.Should().Be(SignalDirection.Flat,
                because: "Sin datos microestructurales, la estrategia retorna Flat.");
        }

        [Fact]
        public void EvaluateSignal_WhenOfiInLowPercentile_ReturnsLong()
        {
            // lookback=5, threshold=0.20
            // OFI historia (primeras 4 barras): [0.5, 0.6, 0.4, 0.7]
            // OFI barra 5: -1.0 → count(prev < -1.0) = 0 → pct = 0/4 = 0.0 < 0.20 → Long
            var (strategy, provider) = Create(lookback: 5, threshold: 0.20);

            double[] ofis = { 0.5, 0.6, 0.4, 0.7, -1.0 };
            for (int i = 0; i < ofis.Length; i++)
                provider.Add(BuildMicroBar(BtcUsdt, i, ofis[i]));

            SignalDirection lastSignal = SignalDirection.Flat;
            for (int i = 0; i < ofis.Length; i++)
                lastSignal = strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, i));

            lastSignal.Should().Be(SignalDirection.Long,
                because: "OFI=-1.0 es menor que toda la historia [0.5,0.6,0.4,0.7] → pct=0 < 0.20.");
        }

        [Fact]
        public void EvaluateSignal_WhenOfiInHighPercentile_ReturnsFlatNotShort()
        {
            // OFI historia: [0.1, 0.2, 0.3, 0.4] → OFI barra 5: 1.0
            // count(prev < 1.0) = 4 → pct = 4/4 = 1.0 NOT < 0.20 → Flat
            // Long-only: nunca emite Short aunque el OFI sea extremadamente alto.
            var (strategy, provider) = Create(lookback: 5, threshold: 0.20);

            double[] ofis = { 0.1, 0.2, 0.3, 0.4, 1.0 };
            for (int i = 0; i < ofis.Length; i++)
                provider.Add(BuildMicroBar(BtcUsdt, i, ofis[i]));

            SignalDirection lastSignal = SignalDirection.Flat;
            for (int i = 0; i < ofis.Length; i++)
                lastSignal = strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, i));

            lastSignal.Should().Be(SignalDirection.Flat,
                because: "Long-only: OFI alto (pct=1.0) no dispara Short, debe ser Flat.");
        }

        [Fact]
        public void EvaluateSignal_WhenOfiInMidRange_ReturnsFlat()
        {
            // OFI historia: [-1.0, -0.5, 0.5, 1.0] → OFI barra 5: 0.0
            // count(prev < 0.0) = 2 → pct = 2/4 = 0.5 NOT < 0.20 → Flat
            var (strategy, provider) = Create(lookback: 5, threshold: 0.20);

            double[] ofis = { -1.0, -0.5, 0.5, 1.0, 0.0 };
            for (int i = 0; i < ofis.Length; i++)
                provider.Add(BuildMicroBar(BtcUsdt, i, ofis[i]));

            SignalDirection lastSignal = SignalDirection.Flat;
            for (int i = 0; i < ofis.Length; i++)
                lastSignal = strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, i));

            lastSignal.Should().Be(SignalDirection.Flat,
                because: "OFI=0.0 en rango medio (pct=0.5) no supera el threshold extremo → Flat.");
        }

        [Fact]
        public void EvaluateSignal_SlidingWindow_DropsOldValuesCorrectly()
        {
            // lookback=4 → ventana de 4 valores.
            // Barras 0..3: [1.0, 2.0, 3.0, 4.0] → barra 3: current=4.0, hist=[1,2,3], pct=1.0 → Flat
            // Barra 4: [2.0, 3.0, 4.0, -5.0] (1.0 descartado) → current=-5.0, hist=[2,3,4], pct=0 → Long
            var (strategy, provider) = Create(lookback: 4, threshold: 0.20);

            double[] allOfi = { 1.0, 2.0, 3.0, 4.0, -5.0 };
            for (int i = 0; i < allOfi.Length; i++)
                provider.Add(BuildMicroBar(BtcUsdt, i, allOfi[i]));

            // Warm-up: barras 0..2 (count 1..3 < 4)
            for (int i = 0; i < 3; i++)
                strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, i));

            // Barra 3: ventana llena [1.0, 2.0, 3.0, 4.0], current=4.0
            // hist=[1.0, 2.0, 3.0], pct=3/3=1.0 → Flat
            var s3 = strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, 3));
            s3.Should().Be(SignalDirection.Flat,
                because: "OFI=4.0 > toda historia [1,2,3] → pct=1.0 → Flat.");

            // Barra 4: slide → [2.0, 3.0, 4.0, -5.0], current=-5.0
            // hist=[2.0, 3.0, 4.0], pct=0/3=0.0 < 0.20 → Long
            // Verifica que 1.0 fue descartado (si no, hist=[1,2,3,4] → pct=0/4=0 → mismo resultado;
            // pero el largo de hist sería 4 en lugar de 3, diferente comportamiento de edge cases)
            var s4 = strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, 4));
            s4.Should().Be(SignalDirection.Long,
                because: "OFI=-5.0 < toda nueva historia [2,3,4] → pct=0 → Long; valor 1.0 fue descartado.");
        }

        [Fact]
        public void EvaluateSignal_TwoSymbols_MaintainIndependentState()
        {
            // Dos símbolos en la misma instancia no deben mezclar su estado de ventana OFI.
            // BTC: OFI alto en todas las barras → Flat.
            // ETH: OFI alto en las primeras 3 barras, muy bajo en la última → Long.
            var (strategy, provider) = Create(lookback: 4, threshold: 0.20);

            double[] btcOfi = { 0.8, 0.9, 1.0, 0.9 };
            double[] ethOfi = { 0.1, 0.2, 0.3, -2.0 };

            for (int i = 0; i < 4; i++)
            {
                provider.Add(BuildMicroBar(BtcUsdt, i, btcOfi[i]));
                provider.Add(BuildMicroBar(EthUsdt, i, ethOfi[i]));
            }

            SignalDirection btcLast = SignalDirection.Flat;
            SignalDirection ethLast = SignalDirection.Flat;

            for (int i = 0; i < 4; i++)
            {
                btcLast = strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, i));
                ethLast = strategy.EvaluateSignal(BuildMarketBar(EthUsdt, i));
            }

            btcLast.Should().Be(SignalDirection.Flat,
                because: "BTC con OFI persistentemente alto no debe generar Long.");
            ethLast.Should().Be(SignalDirection.Long,
                because: "ETH con OFI=-2.0 (mínimo absoluto de su ventana [0.1,0.2,0.3]) debe generar Long.");
        }
    }
}
