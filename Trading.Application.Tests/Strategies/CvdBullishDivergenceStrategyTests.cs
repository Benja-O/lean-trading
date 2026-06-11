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
    /// Tests de comportamiento de CvdBullishDivergenceStrategy con datos sintéticos.
    ///
    /// Cobertura:
    /// - Durante warm-up (ventana incompleta): siempre Flat.
    /// - Divergencia alcista confirmada: precio nuevo mínimo, CVD no lo confirma → Long.
    /// - Sin divergencia alcista: CVD confirma el nuevo mínimo → Flat.
    /// - Precio igual al mínimo (sin ruptura estricta): Flat.
    /// - Precio dentro del rango (sin nuevo mínimo): Flat.
    /// - MicrostructureBar null: Flat sin excepción (ADR-037).
    /// - Estado independiente por símbolo.
    /// - La estrategia NUNCA emite Short.
    ///
    /// Convención de timestamps:
    ///   MarketBar.TimestampUtc  = EndTime de la barra 1h (ej. 01:00 UTC para barra 00:00–01:00).
    ///   MicrostructureBar.BarUtc = StartTime de la misma barra (00:00 UTC).
    ///   La estrategia hace GetBar(id, marketBar.TimestampUtc.AddHours(-1)).
    ///   → En los tests: MicroTime(i) = MarketTime(i).AddHours(-1).
    /// </summary>
    public class CvdBullishDivergenceStrategyTests
    {
        private static readonly InstrumentId BtcUsdt = new("BTCUSDT");
        private static readonly InstrumentId EthUsdt = new("ETHUSDT");
        private const int LookbackBars = 5;
        private static readonly DateTime Epoch = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // EndTime de la barra i (lo que QC pone en TradeBar.EndTime y por ende en MarketBar.TimestampUtc)
        private static DateTime MarketTime(int barIndex) => Epoch.AddHours(barIndex + 1);

        // StartTime de la misma barra (BarUtc en el CSV de features)
        private static DateTime MicroTime(int barIndex) => Epoch.AddHours(barIndex);

        private static MarketBar BuildMarketBar(InstrumentId id, int barIndex, decimal close) =>
            new MarketBar(id, close, MarketTime(barIndex));

        private static MarketBar BuildMarketBar(int barIndex, decimal close) =>
            BuildMarketBar(BtcUsdt, barIndex, close);

        private static MicrostructureBar BuildMicroBar(InstrumentId id, int barIndex, double cvd) =>
            new MicrostructureBar(id, MicroTime(barIndex),
                ofi: 0, cvdDelta: 0, cvd: cvd,
                arrivalRate: 100, meanTradeSize: 0.1, buySellRatio: 1.0, priceReturn: 0);

        private static MicrostructureBar BuildMicroBar(int barIndex, double cvd) =>
            BuildMicroBar(BtcUsdt, barIndex, cvd);

        /// <summary>
        /// Warm-up con 5 barras: close=100 (constante), CVD=[70, 65, 60, 55, 50] (decreciente).
        /// Resultado: priceMin=100, cvdMin=50.
        /// </summary>
        private static (CvdBullishDivergenceStrategy Strategy, FakeMicrostructureProvider Provider) BuildWarmedUp()
        {
            var provider = new FakeMicrostructureProvider();
            var strategy = new CvdBullishDivergenceStrategy(provider, LookbackBars);

            for (int barIndex = 0; barIndex < LookbackBars; barIndex++)
            {
                double cvd = 70.0 - barIndex * 5.0;  // 70, 65, 60, 55, 50
                provider.Add(BuildMicroBar(barIndex, cvd));
                strategy.EvaluateSignal(BuildMarketBar(barIndex, close: 100m));
            }
            return (strategy, provider);
        }

        // ── Warm-up ────────────────────────────────────────────────────────────

        [Fact]
        public void EvaluateSignal_DuranteWarmUp_SiempreDevuelveFlat()
        {
            var provider = new FakeMicrostructureProvider();
            var strategy = new CvdBullishDivergenceStrategy(provider, LookbackBars);

            for (int barIndex = 0; barIndex < LookbackBars; barIndex++)
            {
                provider.Add(BuildMicroBar(barIndex, cvd: 100.0 - barIndex * 10));
                var signal = strategy.EvaluateSignal(BuildMarketBar(barIndex, close: 100m));

                signal.Should().Be(SignalDirection.Flat,
                    because: $"En la barra {barIndex} la ventana aún no está completa (necesita {LookbackBars} barras).");
            }
        }

        // ── Señal Long ─────────────────────────────────────────────────────────

        [Fact]
        public void EvaluateSignal_DivergenciaAlcista_DevuelveLong()
        {
            // Warm-up: priceMin=100, cvdMin=50
            var (strategy, provider) = BuildWarmedUp();
            int nextBar = LookbackBars;

            // Precio rompe mínimo estrictamente (99 < 100) y CVD no lo confirma (55 > 50)
            provider.Add(BuildMicroBar(nextBar, cvd: 55.0));
            var signal = strategy.EvaluateSignal(BuildMarketBar(nextBar, close: 99m));

            signal.Should().Be(SignalDirection.Long,
                because: "Nuevo mínimo de precio con CVD por encima de su mínimo = divergencia alcista.");
        }

        [Fact]
        public void EvaluateSignal_DivergenciaAlcista_CvdJustoEncimadelMinimo_DevuelveLong()
        {
            // Warm-up: cvdMin=50
            var (strategy, provider) = BuildWarmedUp();
            int nextBar = LookbackBars;

            // CVD apenas por encima del mínimo (50.001 > 50): la condición es estricta >
            provider.Add(BuildMicroBar(nextBar, cvd: 50.001));
            var signal = strategy.EvaluateSignal(BuildMarketBar(nextBar, close: 99m));

            signal.Should().Be(SignalDirection.Long,
                because: "cvdMin=50 y currentCvd=50.001: 50.001 > 50 satisface la condición estricta.");
        }

        // ── Sin señal ──────────────────────────────────────────────────────────

        [Fact]
        public void EvaluateSignal_NuevoMinimoPrecioConCvdConfirmando_DevuelveFlat()
        {
            // Warm-up: priceMin=100, cvdMin=50
            var (strategy, provider) = BuildWarmedUp();
            int nextBar = LookbackBars;

            // Precio rompe mínimo (99 < 100) pero CVD también cae a nuevo mínimo (45 < 50)
            provider.Add(BuildMicroBar(nextBar, cvd: 45.0));
            var signal = strategy.EvaluateSignal(BuildMarketBar(nextBar, close: 99m));

            signal.Should().Be(SignalDirection.Flat,
                because: "CVD confirma el nuevo mínimo: no hay divergencia alcista.");
        }

        [Fact]
        public void EvaluateSignal_PrecioIgualAlMinimo_NoEsRupturaEstricta_DevuelveFlat()
        {
            // Warm-up: priceMin=100
            var (strategy, provider) = BuildWarmedUp();
            int nextBar = LookbackBars;

            // Precio == priceMin (igualar no es romper estrictamente)
            provider.Add(BuildMicroBar(nextBar, cvd: 55.0));
            var signal = strategy.EvaluateSignal(BuildMarketBar(nextBar, close: 100m));

            signal.Should().Be(SignalDirection.Flat,
                because: "close == priceMin: se requiere close < priceMin para ser ruptura estricta.");
        }

        [Fact]
        public void EvaluateSignal_PrecioDentroDelRango_DevuelveFlat()
        {
            // Warm-up: priceMin=100
            var (strategy, provider) = BuildWarmedUp();
            int nextBar = LookbackBars;

            // Precio por encima del mínimo: sin señal posible
            provider.Add(BuildMicroBar(nextBar, cvd: 45.0));
            var signal = strategy.EvaluateSignal(BuildMarketBar(nextBar, close: 105m));

            signal.Should().Be(SignalDirection.Flat,
                because: "El precio no rompió el mínimo de la ventana.");
        }

        [Fact]
        public void EvaluateSignal_NuncaEmiteShort()
        {
            // La estrategia es Long-only: ninguna combinación de datos debe producir Short.
            var provider = new FakeMicrostructureProvider();
            var strategy = new CvdBullishDivergenceStrategy(provider, LookbackBars);

            // Alimentar 100 barras con movimiento de precio arbitrario
            for (int barIndex = 0; barIndex < 100; barIndex++)
            {
                double cvd   = Math.Sin(barIndex * 0.3) * 100;
                decimal close = 100m + (decimal)(Math.Cos(barIndex * 0.2) * 30);
                provider.Add(BuildMicroBar(barIndex, cvd));
                var signal = strategy.EvaluateSignal(BuildMarketBar(barIndex, close));

                signal.Should().NotBe(SignalDirection.Short,
                    because: "CvdBullishDivergenceStrategy es Long-only; nunca emite Short.");
            }
        }

        // ── Null handling ──────────────────────────────────────────────────────

        [Fact]
        public void EvaluateSignal_MicroBarNull_DevuelveFlatSinExcepcion()
        {
            var provider = new FakeMicrostructureProvider();
            var strategy = new CvdBullishDivergenceStrategy(provider, LookbackBars);

            // Provider sin datos: GetBar devuelve null siempre
            var act = () => strategy.EvaluateSignal(BuildMarketBar(barIndex: 0, close: 100m));

            act.Should().NotThrow(because: "Ausencia de datos microestructurales no debe lanzar excepción.");
            act().Should().Be(SignalDirection.Flat,
                because: "Sin datos microestructurales la estrategia degrada a Flat (ADR-037).");
        }

        // ── Estado por símbolo ─────────────────────────────────────────────────

        [Fact]
        public void EvaluateSignal_DosSimbolosEnParalelo_MantienenEstadoIndependiente()
        {
            var provider = new FakeMicrostructureProvider();
            var strategy = new CvdBullishDivergenceStrategy(provider, LookbackBars);

            // Warm-up para BTC y ETH en paralelo con precios distintos
            for (int barIndex = 0; barIndex < LookbackBars; barIndex++)
            {
                provider.Add(BuildMicroBar(BtcUsdt, barIndex, cvd: 70.0 - barIndex * 5));
                provider.Add(BuildMicroBar(EthUsdt, barIndex, cvd: 200.0 - barIndex * 5));
                strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, barIndex, close: 30_000m));
                strategy.EvaluateSignal(BuildMarketBar(EthUsdt, barIndex, close: 2_000m));
            }

            int nextBar = LookbackBars;

            // BTC: nuevo mínimo (29999 < 30000) con CVD divergente (55 > 50) → Long
            provider.Add(BuildMicroBar(BtcUsdt, nextBar, cvd: 55.0));
            var btcSignal = strategy.EvaluateSignal(BuildMarketBar(BtcUsdt, nextBar, close: 29_999m));

            // ETH: precio dentro del rango (2000 == 2000, no < 2000) → Flat
            provider.Add(BuildMicroBar(EthUsdt, nextBar, cvd: 190.0));
            var ethSignal = strategy.EvaluateSignal(BuildMarketBar(EthUsdt, nextBar, close: 2_000m));

            btcSignal.Should().Be(SignalDirection.Long,
                because: "BTC hizo nuevo mínimo con CVD divergente (estado BTC independiente de ETH).");
            ethSignal.Should().Be(SignalDirection.Flat,
                because: "ETH no hizo nuevo mínimo (estado ETH independiente de BTC).");
        }

        // ── Ventana deslizante ─────────────────────────────────────────────────

        [Fact]
        public void EvaluateSignal_DespuesDeVentanaCompleta_VentanaDeslizaCorrectamente()
        {
            // Verificar que la ventana descarta la barra más antigua correctamente.
            // Warm-up con close=100, CVD decreciente [70..50] → priceMin=100, cvdMin=50
            var (strategy, provider) = BuildWarmedUp();

            // Barra 5: close=99, cvd=45 → Long (99 < 100, 45 < 50 → NO divergencia, debería ser Flat)
            // Espera: Flat porque cvd=45 < cvdMin=50 (CVD confirma el nuevo mínimo)
            int bar5 = LookbackBars;
            provider.Add(BuildMicroBar(bar5, cvd: 45.0));
            var signal5 = strategy.EvaluateSignal(BuildMarketBar(bar5, close: 99m));
            signal5.Should().Be(SignalDirection.Flat, because: "CVD confirma el mínimo: no hay divergencia.");

            // Ahora la ventana contiene barras 1..5: priceMin=99 (barra 5), cvdMin=45 (barra 5)
            // Barra 6: close=98 (nuevo mínimo < 99), cvd=50 (50 > 45 → divergencia alcista) → Long
            int bar6 = LookbackBars + 1;
            provider.Add(BuildMicroBar(bar6, cvd: 50.0));
            var signal6 = strategy.EvaluateSignal(BuildMarketBar(bar6, close: 98m));
            signal6.Should().Be(SignalDirection.Long,
                because: "Después del slide, 98 < priceMin(ventana actualizada)=99 y 50 > cvdMin=45.");
        }
    }
}
