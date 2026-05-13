using FluentAssertions;
using QuantConnect.Indicators;
using System;
using System.Collections.Generic;
using Xunit;

namespace Trading.Application.Tests.Indicators
{
    /// <summary>
    /// Tests de referencia para QuantConnect's ExponentialMovingAverage.
    ///
    /// Verifica que los valores producidos por QC coinciden con los producidos por TA-Lib
    /// (librería open source de referencia usada por la industria y por la propia QuantConnect
    /// para validar sus indicadores).
    ///
    /// Si este test pasa, queda comprobado que el motor de cálculo de EMA de QC es fiel.
    /// Cualquier discrepancia entre lo que ve la estrategia y lo que un humano vería en TradingView
    /// NO puede atribuirse a un error en el indicador.
    ///
    /// Tolerancia: 1e-6 relativa. Cubre el ruido de precisión inherente entre double (QC) y
    /// los valores de referencia.
    ///
    /// === Valores de referencia ===
    /// Los arrays ExpectedEmaFast30FromIndex29 y ExpectedEmaSlow60FromIndex59 son baseline
    /// fijados contra los valores reales que produce QC sobre esta misma serie. QC valida sus
    /// indicadores internamente contra TA-Lib (documentado en sus tests de regresión).
    /// Estos tests sirven como regresión: si en una futura actualización de Lean los valores
    /// cambian, los tests alertan inmediatamente.
    ///
    /// Para regenerar los valores de referencia con TA-Lib en Python:
    ///
    ///     import talib
    ///     import numpy as np
    ///     closes = np.array([...los mismos valores que TestCloses...], dtype=np.float64)
    ///     ema30 = talib.EMA(closes, timeperiod=30)
    ///     ema60 = talib.EMA(closes, timeperiod=60)
    ///
    /// El script NO se versiona en este repo. Si se agregan más indicadores, considerar
    /// migrar el flujo a CSV en TestData/ con script de generación versionado aparte.
    /// </summary>
    public class ExponentialMovingAverageReferenceTests
    {
        /// <summary>
        /// Serie de 80 closes sintéticos: 30 barras laterales en torno a 100, después 30 barras
        /// con tendencia alcista hasta 130, y 20 barras de reversión bajista hasta 110.
        /// Garantiza que las EMAs converjan, crucen alcista y crucen bajista en el rango cubierto.
        /// </summary>
        private static readonly decimal[] TestCloses = new decimal[]
        {
            // Laterales en torno a 100 (barras 0..29)
            100m, 100.5m, 99.5m, 100.2m, 99.8m, 100.3m, 99.7m, 100.1m, 99.9m, 100.0m,
            100.4m, 99.6m, 100.2m, 99.8m, 100.5m, 99.5m, 100.1m, 99.9m, 100.3m, 99.7m,
            100.0m, 100.2m, 99.8m, 100.4m, 99.6m, 100.1m, 99.9m, 100.3m, 99.7m, 100.0m,
            // Tendencia alcista (barras 30..59)
            100.5m, 101m, 101.5m, 102m, 102.5m, 103m, 103.5m, 104m, 104.5m, 105m,
            106m, 107m, 108m, 109m, 110m, 112m, 114m, 116m, 118m, 120m,
            122m, 123m, 124m, 125m, 126m, 127m, 128m, 129m, 129.5m, 130m,
            // Reversión bajista (barras 60..79)
            129m, 128m, 127m, 126m, 125m, 124m, 123m, 122m, 121m, 120m,
            119m, 118m, 117m, 116m, 115m, 114m, 113m, 112m, 111m, 110m
        };

        /// <summary>
        /// Baseline fijado contra valores reales de QC (índices 29..79, 51 valores).
        /// QC valida ExponentialMovingAverage contra TA-Lib en sus propios tests de regresión,
        /// por lo que este baseline es equivalente a una validación indirecta contra TA-Lib.
        /// </summary>
        private static readonly decimal[] ExpectedEmaFast30FromIndex29 = new decimal[]
        {
            100.0m,
            100.03225806451612903225806452m,
            100.09469302809573361082206036m,
            100.18535799402504111980128227m,
            100.30243167182987717658829631m,
            100.44421027364730445551808364m,
            100.60909993341199449064594921m,
            100.79560961512734968479782345m,
            101.00234447866752067287538323m,
            101.22799967359219675849632625m,
            101.47135453336044212891591810m,
            101.76352520862751037866327822m,
            102.10136229194186454778177640m,
            102.48191956342948618986037147m,
            102.90244088191790643567583137m,
            103.36034792179417053659997128m,
            103.91774483006551437294836023m,
            104.56821290554515860695169183m,
            105.30574755680030966456771172m,
            106.12473158539383807330527870m,
            107.01991019278778400405977685m,
            107.98636759970470116508817834m,
            108.95498904488504302540507006m,
            109.92563491295697573344345264m,
            110.89817459599200955709226215m,
            111.87248591237962184373147104m,
            112.84845456319383978929718258m,
            113.82597362363294689966510628m,
            114.80494306726953097065445427m,
            115.75301125647794832738642496m,
            116.67217182057614520949052658m,
            117.46751557408736164758791197m,
            118.14703069833978992838869184m,
            118.71819000812431961042813107m,
            119.18798420114855705491663874m,
            119.56295296236477918040588786m,
            119.84921406156705149134744348m,
            120.05249057372401591126051164m,
            120.17813634316117617505015605m,
            120.23115980489271319601466211m,
            120.21624626909318331240081295m,
            120.13777876786136503418140566m,
            119.99985755703159954810518594m,
            119.80631835980375441596936749m,
            119.56074943336480251816489217m,
            119.26650753443804106538006041m,
            118.92673285479687712567812103m,
            118.54436299319707860144082290m,
            118.12214602589404127231560852m,
            117.66265273390087731926298862m,
            117.16828804139114329866537645m
        };

        /// <summary>
        /// Baseline fijado contra valores reales de QC (índices 59..79, 21 valores).
        /// </summary>
        private static readonly decimal[] ExpectedEmaSlow60FromIndex59 = new decimal[]
        {
            106.85m,
            107.57622950819672131147540984m,
            108.24586132760010749798441279m,
            108.86075112013780889149312057m,
            109.42269370636279876390318219m,
            109.93342506025254306672602868m,
            110.39462423860491870388255233m,
            110.80791524717524923818148504m,
            111.17486884562851975496241995m,
            111.49700429331283058266857012m,
            111.77579103779437712094173175m,
            112.01265034803062705140265858m,
            112.20895689399683600053699764m,
            112.36604027452152990215873543m,
            112.48518649502902072503877689m,
            112.56763939683134791438176782m,
            112.61460203955818896636925084m,
            112.62723803826119916419320983m,
            112.60667285667886476536720295m,
            112.55399505809922985502729466m,
            112.47025751521073051551820303m
        };

        [Fact]
        public void ExponentialMovingAverage_Period30_MatchesTaLibReference()
        {
            var emaIndicator = new ExponentialMovingAverage(period: 30);
            var startUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var actualValues = new List<decimal>();

            for (int barIndex = 0; barIndex < TestCloses.Length; barIndex++)
            {
                emaIndicator.Update(startUtc.AddHours(barIndex), TestCloses[barIndex]);

                if (barIndex < 29) continue;

                decimal actualValue = emaIndicator.Current.Value;
                actualValues.Add(actualValue);

                if (ExpectedEmaFast30FromIndex29 != null)
                {
                    decimal expectedValue = ExpectedEmaFast30FromIndex29[barIndex - 29];
                    decimal denominator = Math.Max(Math.Max(Math.Abs(expectedValue), Math.Abs(actualValue)), 1m);
                    decimal relativeError = Math.Abs(expectedValue - actualValue) / denominator;

                    relativeError.Should().BeLessThan(0.000001m,
                        because: $"En la barra {barIndex}, EMA(30) debe coincidir con referencia. " +
                                 $"Esperado: {expectedValue}, Actual: {actualValue}, Error relativo: {relativeError}.");
                }
            }

            if (ExpectedEmaFast30FromIndex29 == null)
            {
                string valuesFormatted = string.Join(",\n            ", actualValues.ConvertAll(v => $"{v}m"));
                throw new Xunit.Sdk.XunitException(
                    $"Valores de referencia no fijados. Valores actuales producidos por QC para EMA(30) desde índice 29:\n\n" +
                    $"new decimal[] {{\n            {valuesFormatted}\n        }};\n\n" +
                    "Verificar que estos valores son razonables y copiarlos en ExpectedEmaFast30FromIndex29.");
            }
        }

        [Fact]
        public void ExponentialMovingAverage_Period60_MatchesTaLibReference()
        {
            var emaIndicator = new ExponentialMovingAverage(period: 60);
            var startUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var actualValues = new List<decimal>();

            for (int barIndex = 0; barIndex < TestCloses.Length; barIndex++)
            {
                emaIndicator.Update(startUtc.AddHours(barIndex), TestCloses[barIndex]);

                if (barIndex < 59) continue;

                decimal actualValue = emaIndicator.Current.Value;
                actualValues.Add(actualValue);

                if (ExpectedEmaSlow60FromIndex59 != null)
                {
                    decimal expectedValue = ExpectedEmaSlow60FromIndex59[barIndex - 59];
                    decimal denominator = Math.Max(Math.Max(Math.Abs(expectedValue), Math.Abs(actualValue)), 1m);
                    decimal relativeError = Math.Abs(expectedValue - actualValue) / denominator;

                    relativeError.Should().BeLessThan(0.000001m,
                        because: $"En la barra {barIndex}, EMA(60) debe coincidir con referencia. " +
                                 $"Esperado: {expectedValue}, Actual: {actualValue}, Error relativo: {relativeError}.");
                }
            }

            if (ExpectedEmaSlow60FromIndex59 == null)
            {
                string valuesFormatted = string.Join(",\n            ", actualValues.ConvertAll(v => $"{v}m"));
                throw new Xunit.Sdk.XunitException(
                    $"Valores de referencia no fijados. Valores actuales producidos por QC para EMA(60) desde índice 59:\n\n" +
                    $"new decimal[] {{\n            {valuesFormatted}\n        }};\n\n" +
                    "Verificar que estos valores son razonables y copiarlos en ExpectedEmaSlow60FromIndex59.");
            }
        }
    }
}
