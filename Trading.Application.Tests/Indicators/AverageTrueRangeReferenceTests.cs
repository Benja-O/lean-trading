using FluentAssertions;
using QuantConnect;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System;
using Xunit;

namespace Trading.Application.Tests.Indicators
{
    /// <summary>
    /// Tests de referencia para QuantConnect's AverageTrueRange (Wilder's smoothing).
    ///
    /// Verifica que el indicador se comporta según la fórmula canónica de Wilder:
    ///   - Seed: SMA simple de los primeros `period` True Range.
    ///   - Smoothing: ATR[i] = (ATR[i-1] * (period-1) + TR[i]) / period
    ///
    /// Los valores de los tests de propiedades se computan analíticamente sobre series
    /// sintéticas simples para evitar dependencias externas en tiempo de compilación.
    /// </summary>
    public class AverageTrueRangeReferenceTests
    {
        private const int Period = 14;
        private static readonly DateTime T0 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static TradeBar Bar(int index, decimal high, decimal low, decimal close) =>
            new(T0.AddHours(index), Symbol.Empty, close, high, low, close, 0m);

        [Fact]
        public void AverageTrueRange_IsReadyAfterPeriodUpdates()
        {
            var atr = new AverageTrueRange(Period);

            for (int i = 0; i < Period - 1; i++)
            {
                atr.Update(Bar(i, 102m, 98m, 100m));
                atr.IsReady.Should().BeFalse(because: $"En la barra {i}, ATR(14) aún no tiene suficientes datos.");
            }

            atr.Update(Bar(Period - 1, 102m, 98m, 100m));
            atr.IsReady.Should().BeTrue(because: "Después de 14 actualizaciones, ATR(14) debe estar listo.");
        }

        [Fact]
        public void AverageTrueRange_ConstantTrueRange_ConvergesToTrValue()
        {
            // Serie sintética: H=102, C=100, L=98 constante.
            // TR[i>=1] = max(H-L=4, |H-PrevC|=2, |L-PrevC|=2) = 4 para todos.
            // Seed (barra 14): ATR = mean(4 * 14) = 4.0
            // Barras siguientes: ATR = (4 * 13 + 4) / 14 = 4.0 (estable).
            var atr = new AverageTrueRange(Period);

            for (int i = 0; i < 30; i++)
                atr.Update(Bar(i, 102m, 98m, 100m));

            atr.IsReady.Should().BeTrue();
            decimal atrValue = (decimal)atr.Current.Value;
            atrValue.Should().BeApproximately(4.0m, precision: 0.0001m,
                because: "Con TR constante de 4, ATR(14) debe converger a 4.0.");
        }

        [Fact]
        public void AverageTrueRange_AfterSpike_RespondsWithWilderFormula()
        {
            // 14 barras de TR=4 → ATR seed = 4.0.
            // Bar 15: H=114, C=100, L=98 con PrevC=100.
            //   TR = max(H-L=16, |H-PrevC|=14, |L-PrevC|=2) = 16
            //   ATR = (4.0 * 13 + 16) / 14 = 68/14 ≈ 4.8571428571
            var atr = new AverageTrueRange(Period);

            for (int i = 0; i < Period; i++)
                atr.Update(Bar(i, 102m, 98m, 100m));

            atr.Update(Bar(Period, 114m, 98m, 100m));

            decimal expected = 68m / 14m; // 4.857142857...
            decimal actual = (decimal)atr.Current.Value;
            decimal relativeError = Math.Abs(expected - actual) / expected;

            relativeError.Should().BeLessThan(0.000001m,
                because: $"Tras un spike de TR=16 después de ATR=4.0, la fórmula de Wilder debe dar ~{expected:F6}. " +
                         $"Valor actual: {actual:F6}.");
        }

        [Fact]
        public void AverageTrueRange_IsAlwaysPositiveWhenReady()
        {
            var atr = new AverageTrueRange(Period);

            for (int i = 0; i < 50; i++)
            {
                decimal close = 100m + i * 0.5m;
                atr.Update(Bar(i, close + 3m, close - 3m, close));

                if (atr.IsReady)
                    ((decimal)atr.Current.Value).Should().BeGreaterThan(0m,
                        because: $"ATR siempre debe ser positivo (barra {i}).");
            }
        }
    }
}
