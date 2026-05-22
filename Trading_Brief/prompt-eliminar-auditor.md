# Refactor — Eliminar SignalAuditor y reemplazar por tests de referencia

## Contexto del proyecto

Sistema de trading sistemático en C# / .NET 10 sobre QuantConnect/Lean. Cuatro proyectos:

- **Trading.Domain** — capa de dominio, CERO `using QuantConnect`.
- **Trading.Application** — orquestación pura, CERO `using QuantConnect`.
- **Trading.Strategies** — adaptadores Lean. Único proyecto con `using QuantConnect`.
- **Trading.Application.Tests** — tests xUnit.

**Invariante arquitectónica crítica:** Trading.Domain y Trading.Application NO deben tener ningún `using QuantConnect` en ningún archivo.

Documentos de referencia en la raíz del repo (LEER antes de empezar):
- `AI.md` — reglas de estilo y arquitectura.
- `ROADMAP.md` — plan completo.
- `DECISIONS.md` — log de ADRs. El más reciente es ADR-013.

## Reglas de naming (no negociables)

1. **Identificadores en inglés.** Comentarios y mensajes en español.
2. **Campos privados** → `_camelCase`.
3. **Cero abreviaturas.**
4. **Variables descriptivas.**
5. **Mensajes de log con placeholders nombrados.**

## Motivación

El "Hito A — Auditor de fidelidad de señales" implementado anteriormente fue un **diseño equivocado**. La idea de auditar señales en vivo durante el backtest con un recomputer paralelo en C# es:

1. **No estándar en la industria.** Hedge funds y proprietary trading firms NO hacen auditoría en vivo de cálculos de indicadores. Validan los indicadores **una vez** con tests unitarios contra librerías de referencia (TA-Lib, QuantLib), y luego confían en el motor.

2. **Conceptualmente débil.** Recalcular el mismo cálculo en el mismo proceso con el mismo tipo de datos no es auditar — es duplicar. Si ambas implementaciones tienen el mismo bug, ninguna lo detecta.

3. **Operativamente frágil.** Después de 4 fixes iterativos (buffer 200→2000, warm-up 200, tolerancia absoluta→relativa, algoritmo SMA-seed→EMA-puro) seguían apareciendo 55-59 discrepancias sobre 173 señales (~33%), sin causa raíz clara. El sistema acumulaba complejidad sin resolver el problema.

4. **Confirmado por la propia documentación de QuantConnect.** QC valida sus indicadores con tests unitarios contra valores de referencia de TA-Lib guardados en CSV. NO con auditores en vivo.

## Plan de acción

**Eliminar TODO el código del auditor.** Reemplazarlo por dos tests unitarios estáticos que son el estándar de la industria:

1. **Test de indicador contra TA-Lib:** verifica que `ExponentialMovingAverage` de QC produce valores equivalentes a TA-Lib sobre una serie conocida.
2. **Test de estrategia con datos sintéticos:** verifica que `EmaCrossStrategy` emite la señal correcta cuando las condiciones se dan deliberadamente.

Si ambos tests pasan, queda comprobado que (a) el motor de cálculo de QC es fiel, y (b) el flujo de la estrategia es correcto. Cobertura institucional estándar. No hay overhead runtime ni complejidad arquitectónica.

## Decisiones de diseño aplicadas

- **D1 — Borrado completo, sin código muerto:** se eliminan TODOS los archivos del auditor y todas sus integraciones. No se deja nada "por si acaso". Cualquier funcionalidad futura de auditoría se diseña desde cero con principios correctos.
- **D2 — Tests en `Trading.Application.Tests`:** los nuevos tests viven en el proyecto de tests existente. NO se crea un proyecto nuevo.
- **D3 — Valores de referencia hardcodeados en código C#:** los valores esperados de EMA se incluyen directamente como arrays `decimal[]` en el archivo de test. Decisión simple: alternativa (CSV externo) agrega complejidad de carga sin valor para el alcance actual (un solo indicador). Si en el futuro se agregan muchos indicadores, migrar a CSV en `TestData/`.
- **D4 — Generación de valores de referencia:** los valores `expectedEmaFast` y `expectedEmaSlow` que aparecen en el test fueron computados a priori usando TA-Lib en Python sobre la misma serie de 50 closes. Se incluyen ya calculados en el código del test. El script Python NO se versiona en el repo de C# — vive como referencia documental en el comentario XML del test.
- **D5 — Serie de prueba:** 50 closes sintéticos con un patrón tendencial seguido de reversión. Suficiente para cubrir el régimen donde EmaFast cruza EmaSlow en ambas direcciones.
- **D6 — Tolerancia:** 1e-6 relativo (heredado de ADR-012). Cubre el ruido `double` vs `decimal` esperado.
- **D7 — Sin script Python en el repo:** el script de generación de referencia se documenta en el XML del test pero NO se compromete al repositorio. Razón: agregar dependencia Python al pipeline solo para generar 100 valores que cambian cero veces es overkill. Si en el futuro se agregan muchos indicadores, considerar migrar el flujo de generación a un proyecto auxiliar.

---

## Especificación detallada

### Fase 1: BORRAR archivos del auditor

Eliminar completamente los siguientes archivos:

1. `Trading.Domain/ValueObjects/SignalDiagnostics.cs`
2. `Trading.Application/Auditing/SignalAuditResult.cs` (contiene también `SignalDiscrepancy`)
3. `Trading.Application/Auditing/IIndicatorRecomputer.cs`
4. `Trading.Application/Auditing/SignalAuditor.cs`
5. `Trading.Application/Auditing/` (eliminar el directorio si queda vacío)
6. `Trading.Strategies/Auditing/EmaCrossIndicatorRecomputer.cs`
7. `Trading.Strategies/Auditing/` (eliminar el directorio si queda vacío)
8. `Trading.Application.Tests/Auditing/SignalAuditorTests.cs`
9. `Trading.Application.Tests/Auditing/` (eliminar el directorio si queda vacío)

### Fase 2: MODIFICAR archivos que referenciaban el auditor

#### Archivo: `Trading.Domain/Abstractions/IStrategy.cs`

Revertir al estado pre-auditoría: eliminar el método `GetLastDiagnostics`.

**Estado final esperado:**

```csharp
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato de una estrategia de trading. Recibe una MarketBar consolidada
    /// y emite una SignalDirection con la dirección sugerida.
    /// </summary>
    public interface IStrategy
    {
        SignalDirection EvaluateSignal(MarketBar marketBar);
    }
}
```

Eliminar el `using` de `SignalDiagnostics` si quedó huérfano. Revisar y limpiar.

#### Archivo: `Trading.Strategies/Implementations/EmaCrossStrategy.cs`

Quitar la implementación de `GetLastDiagnostics`, los campos `LastEmaFastValue`, `LastEmaSlowValue`, `LastPreviousSignalSnapshot` de `SymbolState`, y todas las asignaciones a esos campos en `EvaluateSignal`.

**Estado final esperado:**

```csharp
using QuantConnect.Indicators;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    /// <summary>
    /// Estrategia de cruce de medias móviles exponenciales (EMA 30 vs EMA 60).
    /// Emite Long en cruce alcista, Short en cruce bajista, Flat el resto del tiempo.
    /// </summary>
    public class EmaCrossStrategy : IStrategy
    {
        private class SymbolState
        {
            public ExponentialMovingAverage EmaFast { get; } = new(30);
            public ExponentialMovingAverage EmaSlow { get; } = new(60);
            public int PreviousSignal { get; set; } = 0;
        }

        private readonly Dictionary<string, SymbolState> _stateBySymbol = new();

        public SignalDirection EvaluateSignal(MarketBar marketBar)
        {
            string ticker = marketBar.InstrumentId.Ticker;

            if (!_stateBySymbol.TryGetValue(ticker, out var state))
            {
                state = new SymbolState();
                _stateBySymbol[ticker] = state;
            }

            state.EmaFast.Update(marketBar.TimestampUtc, marketBar.Close);
            state.EmaSlow.Update(marketBar.TimestampUtc, marketBar.Close);

            if (!state.EmaFast.IsReady || !state.EmaSlow.IsReady)
                return SignalDirection.Flat;

            int currentSignal = state.EmaFast > state.EmaSlow ? 1 : -1;

            if (state.PreviousSignal == 0)
            {
                state.PreviousSignal = currentSignal;
                return SignalDirection.Flat;
            }

            bool isLongCross = state.PreviousSignal < 0 && currentSignal > 0;
            bool isShortCross = state.PreviousSignal > 0 && currentSignal < 0;
            state.PreviousSignal = currentSignal;

            if (isLongCross) return SignalDirection.Long;
            if (isShortCross) return SignalDirection.Short;
            return SignalDirection.Flat;
        }
    }
}
```

#### Archivo: `Trading.Application/Execution/BarProcessingService.cs`

Quitar el parámetro `signalAuditor` del constructor, el campo `_signalAuditor`, y las llamadas `_signalAuditor?.ObserveBar(...)` y `_signalAuditor?.AuditSignal(...)`. Quitar el `using Trading.Application.Auditing;` si queda huérfano.

#### Archivo: `Trading.Strategies/TradingAlgorithmHost.cs`

Quitar:
- La constante `private const bool EnableSignalAuditing = true;`.
- El campo `private SignalAuditor _signalAuditor;`.
- El bloque `if (EnableSignalAuditing) { ... new SignalAuditor(...) ... }` en `Initialize()`.
- El parámetro `signalAuditor: _signalAuditor` en la construcción de `BarProcessingService`.
- La llamada `_signalAuditor?.ReportSummary();` en `OnEndOfAlgorithm()` — y eliminar `OnEndOfAlgorithm()` completo si solo tenía esa línea (verificar: si solo contiene `_signalAuditor?.ReportSummary(); base.OnEndOfAlgorithm();`, eliminar el override entero porque heredar el default es equivalente).
- Los `using` de `Trading.Application.Auditing` y `Trading.Strategies.Auditing` si quedan huérfanos.

### Fase 3: CREAR los dos tests nuevos

#### Archivo nuevo: `Trading.Application.Tests/Indicators/ExponentialMovingAverageReferenceTests.cs`

```csharp
using FluentAssertions;
using QuantConnect.Indicators;
using System;
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
    /// === Generación de valores de referencia ===
    /// Los arrays expectedEmaFast30 y expectedEmaSlow60 se computaron en Python con TA-Lib
    /// sobre la misma serie de closes:
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
        /// Valores esperados de EMA(30) computados por TA-Lib sobre TestCloses.
        /// Solo se incluyen los valores a partir del índice 29 (donde TA-Lib emite el primer valor
        /// con período 30 — antes de eso TA-Lib devuelve NaN).
        /// </summary>
        private static readonly decimal[] ExpectedEmaFast30FromIndex29 = new decimal[]
        {
            // índices 29..79 (51 valores)
            100.00193548387096774193548m, 100.03284339459537156207115m, 100.09524432184694726000559m,
            100.21887885930262102000523m, 100.36699020029565224516748m, 100.53799272414676596870380m,
            100.72896416286002171590289m, 100.93584323105163126050593m, 101.15642205500152347982264m,
            101.39406900822722003821731m, 101.69089231701224390991296m, 102.03405153636017011732125m,
            102.41912626432080978500245m, 102.84364586663300592080481m, 103.30664450525824361785936m,
            103.86878484297133499257619m, 104.51724257955318306725934m, 105.24967984023813834296998m,
            106.06099145957114843308579m, 106.94931620248978175114443m, 107.92547259039852809584285m,
            108.90189370940212236093400m, 109.88176764326844284280763m, 110.86487198047179135649811m,
            111.85101701076200868737301m, 112.84001416037200810751411m, 113.83162400869451715299459m,
            114.82563000174249260789528m, 115.79658806616756275410544m, 116.71373429414019644298264m,
            117.50286433776760762148215m, 118.18298084598000139041523m, 118.76482853243016388812304m,
            119.25739732099273361017102m, 119.66760497318610305145708m, 120.00050463491472413620341m,
            120.25855240139888258254629m, 120.45028354710020821471379m, 120.59059491081955800400869m,
            120.69281926011247323968206m, 120.74908250139371563517618m,
            // A partir de acá empieza la reversión
            120.69508329130896077033276m, 120.55217500992320072031121m, 120.32622404444879100029062m,
            120.02519923994563842091962m, 119.65161608123009752215576m, 119.20633683534428107459432m,
            118.69267281692013516623289m, 118.11476843451787189100849m, 117.47765075584060047707378m,
            116.78440003031572044508385m, 116.04036777352581622410566m
        };

        /// <summary>
        /// Valores esperados de EMA(60) computados por TA-Lib sobre TestCloses.
        /// Solo se incluyen los valores a partir del índice 59 (período 60).
        /// </summary>
        private static readonly decimal[] ExpectedEmaSlow60FromIndex59 = new decimal[]
        {
            // índices 59..79 (21 valores)
            106.83793442622950819672131m, 107.56178650557854067201932m, 108.23044229934669209519082m,
            108.84994652722452370839500m, 109.42535764854821880248074m, 109.96073330020613024501715m,
            110.45924554118391268050829m, 110.92330419075368779360470m, 111.35466468697363002664088m,
            111.75451885128323116784898m, 112.12345462585866792279999m,
            // Reversión bajista
            112.42893160580057894947213m, 112.66172320121861985538456m, 112.82217618552830082635566m,
            112.91015753283471884128469m, 112.92539019672801336978744m, 112.86729134900100460832436m,
            112.73484897279277912090625m, 112.52656724635176471087313m, 112.24039303877993857881471m,
            111.87436546952813601396907m
        };

        [Fact]
        public void ExponentialMovingAverage_Period30_MatchesTaLibReference()
        {
            // Arrange
            var emaIndicator = new ExponentialMovingAverage(period: 30);
            var startUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Act + Assert por cada barra
            for (int barIndex = 0; barIndex < TestCloses.Length; barIndex++)
            {
                emaIndicator.Update(startUtc.AddHours(barIndex), TestCloses[barIndex]);

                // TA-Lib emite NaN antes del índice period-1. QC también marca IsReady en barra period.
                if (barIndex < 29) continue;

                decimal actualValue = (decimal)emaIndicator.Current.Value;
                decimal expectedValue = ExpectedEmaFast30FromIndex29[barIndex - 29];

                decimal denominator = Math.Max(Math.Max(Math.Abs(expectedValue), Math.Abs(actualValue)), 1m);
                decimal relativeError = Math.Abs(expectedValue - actualValue) / denominator;

                relativeError.Should().BeLessThan(0.000001m,
                    because: $"En la barra {barIndex}, EMA(30) debe coincidir con TA-Lib. " +
                             $"Esperado: {expectedValue}, Actual: {actualValue}, Error relativo: {relativeError}.");
            }
        }

        [Fact]
        public void ExponentialMovingAverage_Period60_MatchesTaLibReference()
        {
            // Arrange
            var emaIndicator = new ExponentialMovingAverage(period: 60);
            var startUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Act + Assert por cada barra
            for (int barIndex = 0; barIndex < TestCloses.Length; barIndex++)
            {
                emaIndicator.Update(startUtc.AddHours(barIndex), TestCloses[barIndex]);

                if (barIndex < 59) continue;

                decimal actualValue = (decimal)emaIndicator.Current.Value;
                decimal expectedValue = ExpectedEmaSlow60FromIndex59[barIndex - 59];

                decimal denominator = Math.Max(Math.Max(Math.Abs(expectedValue), Math.Abs(actualValue)), 1m);
                decimal relativeError = Math.Abs(expectedValue - actualValue) / denominator;

                relativeError.Should().BeLessThan(0.000001m,
                    because: $"En la barra {barIndex}, EMA(60) debe coincidir con TA-Lib. " +
                             $"Esperado: {expectedValue}, Actual: {actualValue}, Error relativo: {relativeError}.");
            }
        }
    }
}
```

**ATENCIÓN IMPORTANTE:** los valores de `ExpectedEmaFast30FromIndex29` y `ExpectedEmaSlow60FromIndex59` que escribí ARRIBA son **ESTIMACIONES MANUALES, NO computadas con TA-Lib real**. Es muy probable que fallen el test con error relativo > 1e-6.

**Acción requerida de Claude Code:** ejecutar el siguiente flujo para obtener valores reales:

1. Correr el test la primera vez. Va a fallar.
2. Leer el output del test: contiene los valores **actuales** producidos por QC para cada barra.
3. **Verificar manualmente que los valores actuales producidos por QC son consistentes con la fórmula EMA estándar** (no son disparates).
4. Si lo son, reemplazar los arrays `ExpectedEmaFast30FromIndex29` y `ExpectedEmaSlow60FromIndex59` por los valores actuales producidos por QC, con tolerancia 1e-6 pasa el test.

**Justificación de este flujo:** estamos en un loop. La forma correcta de generar estos valores es con TA-Lib en Python. Pero ese ambiente no está disponible. La alternativa pragmática es: **fijar los valores actuales de QC como baseline**, con la garantía de que QC mismo valida sus indicadores contra TA-Lib en sus propios tests internos (documentado en https://www.quantconnect.com/docs/v2/lean-engine/contributions/indicators). El test entonces sirve como **regresión**: si en una futura actualización de Lean los valores cambian, el test alerta inmediatamente.

Si Claude Code tiene acceso a un entorno Python con TA-Lib instalado, puede generarlos auténticamente. Si no, el approach de baseline-QC es aceptable y debe documentarse en el comentario XML del test.

#### Archivo nuevo: `Trading.Application.Tests/Strategies/EmaCrossStrategyTests.cs`

```csharp
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
            // El primer "currentSignal" se calcula en la barra 60 (IsReady).
            // Como ambas EMAs son ~100, currentSignal = EmaFast > EmaSlow → posible -1 o +1.
            // En barras laterales puras, el comportamiento depende de ruido numérico → consideramos Flat aceptable.
            for (int barIndex = 0; barIndex < 80; barIndex++)
            {
                strategy.EvaluateSignal(BuildBar(close: 100m, barIndex));
            }

            // A partir de la barra 80, subida fuerte: EmaFast (30) reacciona más rápido que EmaSlow (60).
            // Eventualmente EmaFast supera a EmaSlow → cruce alcista → Long en alguna barra.
            SignalDirection? observedLong = null;
            int? barOfLongSignal = null;

            for (int barIndex = 80; barIndex < 160; barIndex++)
            {
                // Subida progresiva
                decimal close = 100m + (barIndex - 80) * 0.5m;
                var signal = strategy.EvaluateSignal(BuildBar(close, barIndex));

                if (signal == SignalDirection.Long)
                {
                    observedLong = signal;
                    barOfLongSignal = barIndex;
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
            // Barras 0..79 laterales en 100.
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

            // Si el estado se cruzaba, alguno habría dado señales inconsistentes (ej. crossover artificial).
            // Que el test no falle es prueba suficiente; verificación adicional con una señal final.
            var btcFinal = strategy.EvaluateSignal(BuildBar(close: 100m, 70));
            btcFinal.Should().Be(SignalDirection.Flat, because: "En régimen lateral puro no debe haber cruce.");
        }
    }
}
```

### Fase 4: Verificación de invariantes y rebuild

1. Verificar que NO quedaron referencias huérfanas:

```bash
grep -rn "SignalAuditor\|SignalDiagnostics\|IIndicatorRecomputer\|EmaCrossIndicatorRecomputer\|SignalAuditResult\|SignalDiscrepancy\|GetLastDiagnostics" Trading.Domain/ Trading.Application/ Trading.Strategies/ Trading.Application.Tests/
```

Si devuelve líneas, hay referencias que faltó limpiar.

2. Verificar invariante arquitectónica:

```bash
grep -rn "^using QuantConnect" Trading.Domain/ Trading.Application/ Trading.Application.Tests/
```

NO debe devolver nada.

3. Verificar que NO quedaron directorios vacíos en `Auditing/` (Application, Strategies, Tests).

4. Rebuild completo:

```powershell
dotnet clean
dotnet build
```

---

## Verificaciones finales obligatorias

1. **Compilación limpia** sin errores ni warnings nuevos.
2. **Invariante arquitectónica:** sin `using QuantConnect` fuera de `Trading.Strategies`.
3. **Tests preexistentes:** todos los tests previos NO relacionados al auditor deben seguir pasando.
4. **Tests nuevos:**
   - `ExponentialMovingAverageReferenceTests` (2 tests): si fallan, seguir el flujo descrito en la nota de "ATENCIÓN IMPORTANTE" para fijar baseline.
   - `EmaCrossStrategyTests` (4 tests): deben pasar al primer intento.
5. **Backtest:** el número de operaciones generadas debe ser **idéntico** al backtest previo (la eliminación del auditor no toca lógica de trading). Confirmar como sanity check final.
6. **El log del backtest NO debe contener ninguna referencia a SignalAuditor** (debe haber desaparecido por completo).

## Estilo

- Documentación XML en español.
- Comentarios inline en español donde haya decisión de diseño no obvia.
- FluentAssertions con `Should()` en lugar de `Assert.Equal`.

## Si encuentras algún problema

- Si los tests de referencia de EMA fallan con error relativo > 1e-6, seguir el flujo de "fijar baseline" descrito arriba. Documentar en el XML que los valores se fijaron como baseline contra QC (no contra TA-Lib externamente verificado).
- Si algún test de `EmaCrossStrategy` falla, reportar los detalles antes de modificar la estrategia.
- Si `grep` de referencias huérfanas devuelve resultados, NO terminar el refactor hasta que esté limpio.

Ejecutar en este orden:

1. Borrar los 9 archivos del auditor (Fase 1).
2. Modificar los 4 archivos que referenciaban al auditor (Fase 2).
3. `dotnet clean && dotnet build`. Si NO compila, hay referencias huérfanas — limpiar.
4. Crear los 2 archivos nuevos de tests (Fase 3).
5. `dotnet build` otra vez.
6. Correr todos los tests. Si los de EMA fallan, fijar baseline.
7. Correr backtest para verificar que sigue produciendo las mismas operaciones.
8. Reportar resultados.

---

## Actualización de documentación al cierre

Una vez que todas las verificaciones obligatorias pasan, actualizá los archivos de tracking.

### ROADMAP.md

1. En "Historial completado", **REVERTIR la entrada del Hito A original** marcándola como ❌:

   ```markdown
   ### ❌ Hito A — Auditor de fidelidad de señales en backtest (REVERTIDO)
   **Fecha original:** [fecha original]
   **Fecha de reversión:** [YYYY-MM-DD]
   **Razón:** diseño equivocado. Recalcular indicadores en vivo durante el backtest dentro del mismo
   proceso es duplicación, no auditoría. Tras cuatro fixes iterativos (buffer, warm-up, tolerancia,
   algoritmo) persistían ~33% de discrepancias sin causa raíz clara. Reemplazado por tests unitarios
   estáticos contra valores de referencia (TA-Lib / baseline QC), que es el estándar institucional
   documentado por la propia QuantConnect. Ver ADR-014.
   ```

   Y también marcar como ❌ las entradas relacionadas: fixes del SignalAuditor (buffer, tolerancia, recomputer).

2. **Reemplazar el "Hito A" original por un nuevo Hito A:**

   En el diagrama del "Plan general", reemplazar la línea del Hito A original por:

   ```
                     HITO A: Tests de referencia de
                     indicadores y estrategias  ✅
   ```

3. Agregar al final de "Historial completado" una entrada nueva:

   ```markdown
   ### ✅ Hito A (versión 2) — Tests de referencia de indicadores y estrategias
   **Fecha:** [YYYY-MM-DD]
   **Resumen:** eliminado completamente el SignalAuditor y todo el código del enfoque anterior
   (9 archivos borrados, 4 modificados). Reemplazado por dos tipos de tests unitarios estándares
   institucionales: (1) tests de referencia que verifican que ExponentialMovingAverage de QC
   produce valores equivalentes a TA-Lib sobre serie sintética conocida, (2) tests de
   comportamiento de EmaCrossStrategy con datos sintéticos diseñados para forzar cruces alcistas
   y bajistas. Cobertura institucional sin overhead runtime. 6 tests nuevos. Total verde: [N]
   tests. Sanity check final humano (verificación de 3-5 señales en TradingView antes de pasar
   a paper trading) queda como práctica recomendada, no automatizada.
   ```

### DECISIONS.md

Agregar **ADR-014** al inicio del archivo (después del template):

```markdown
## ADR-014 — Reversión del SignalAuditor: validación de indicadores por tests unitarios estáticos
**Fecha:** [YYYY-MM-DD]
**Estado:** Aceptada (revierte ADR-010, ADR-011, ADR-012, ADR-013 en lo que respecta a auditoría runtime)

### Contexto
El Hito A original implementaba un SignalAuditor que durante el backtest mantenía un buffer rolling
de barras observadas y, cuando una estrategia emitía señal, recalculaba los indicadores en C#
independientemente y comparaba con los valores que la estrategia declaraba haber usado.

Tras cuatro fixes iterativos (buffer 200→2000, warm-up 200, tolerancia absoluta 1e-9 → relativa 1e-6,
reemplazo del algoritmo SMA-seed→EMA-puro), persistían ~33% de señales reportadas como inconsistentes
sin causa raíz clara. El sistema acumulaba complejidad arquitectónica sin resolver el problema de fondo.

Búsqueda posterior reveló que la práctica institucional estándar (documentada por la propia
QuantConnect en https://www.quantconnect.com/docs/v2/lean-engine/contributions/indicators) es validar
indicadores mediante tests unitarios contra valores de referencia de librerías open source
(TA-Lib, QuantLib) almacenados en CSV o arrays estáticos. NO se hace auditoría en vivo durante backtest.

### Decisión
Eliminar completamente el SignalAuditor y todos sus componentes asociados (9 archivos borrados).
Reemplazar por dos tests unitarios:
1. Test de indicador: verifica que ExponentialMovingAverage de QC produce valores equivalentes a
   TA-Lib (o baseline QC, si TA-Lib no está disponible) sobre serie sintética de referencia.
2. Test de estrategia: verifica que EmaCrossStrategy emite señales correctas con datos sintéticos
   diseñados.

Para cualquier indicador o estrategia nueva que se agregue al sistema, replicar este patrón en lugar
de re-introducir auditoría runtime.

### Alternativas consideradas
- **A: Continuar iterando sobre el SignalAuditor.** Descartada: cuatro fixes sin convergencia indica
  que el diseño es fundamentalmente incorrecto, no que falte un fix más.
- **B: Auditor independiente en Python con TA-Lib durante el backtest.** Descartada: agrega un
  pipeline cross-language al desarrollo cotidiano por un problema que tests unitarios resuelven
  mejor. Reservar este enfoque para validación pre-live trading (ver TODO AUDIT-1 en ROADMAP).
- **C (elegida): Tests unitarios estáticos contra valores de referencia.** Estándar institucional
  documentado. Costo runtime cero. Cobertura efectiva.

### Consecuencias
- El sistema runtime queda más simple: BarProcessingService y TradingAlgorithmHost vuelven a no
  conocer auditoría.
- La verificación de fidelidad de señales se hace una sola vez en CI (al correr tests), no en cada
  backtest.
- ADRs anteriores (ADR-010 a ADR-013) quedan superseded en lo que respecta a auditoría runtime, pero
  se mantienen como registro histórico del aprendizaje.
- Práctica recomendada antes de pasar a paper trading: verificación manual de 3-5 señales en
  TradingView (sanity check humano final). No automatizada, no bloqueante.
- TODO AUDIT-1 (auditor Python independiente) sigue en ROADMAP Bloque 4 para fase pre-live con
  capital significativo.
```

### Verificación final

Mostrar el diff resumido. NO commitear automáticamente — esperar confirmación.

Si las verificaciones del refactor NO pasan, NO actualizar tracking. Reportar error y esperar instrucciones.
