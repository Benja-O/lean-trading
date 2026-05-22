# HITO B — Paso 1: Pre-requisitos arquitectónicos (Domain only, sin régimen aún)

> **Brief ejecutable para Claude Code.** Este es el primer paso de tres del Hito B. Aísla cambios estructurales del Domain que el resto del Hito B necesita, sin tocar régimen, sin Accord, sin HMM. Si el Paso 1 sale verde (compila, todos los tests existentes verdes + nuevos verdes), se commitea y se arranca el Paso 2 en una sesión nueva con el repo en estado limpio.

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio de los puntos críticos para este brief:

- **Cero comandos `git` de cualquier tipo.** Lista exhaustiva en `AI.md`. Si Claude Code o la herramienta subyacente ofrece crear rama automática, declinar/desactivar. Trabajar sobre la rama que el usuario tiene checked-out (presumiblemente `master`).
- **No compilar.** El usuario compila.
- **No correr tests.** El usuario corre tests.
- **No `git stash`, `git checkout -b`, ni nada similar bajo ninguna razón.** Si la sesión necesita "aislar trabajo", el usuario decide cómo.

---

## Contexto y motivación del Paso 1

El Hito B implementa clasificación de regímenes de mercado con HMM. Antes de tocar régimen, hay tres cambios al Domain que son pre-requisitos:

1. **`MarketBar` solo expone `Close` y `TimestampUtc` hoy.** El HMM necesita al menos High y Low para calcular features de volatilidad (true range, no solo std de retornos). El propio comentario del archivo dice "Por ahora expone solo Close [...] cuando una estrategia requiera Open/High/Low/Volume se agregan acá; el adaptador (MarketBarMapper) ya los tiene disponibles desde el TradeBar de Lean."

2. **`StrategyDefinition` no tiene campo para declarar compatibilidad de regímenes.** Hito B necesita que cada entrada del `strategies.json` declare qué regímenes son compatibles con la estrategia. El campo es nullable: ausencia = compatible con todos (fail-safe).

3. **`RiskLimitBreachReason` no tiene valor para incompatibilidad de régimen.** Aunque el filtro de régimen NO se implementa vía `RiskOrchestrator` (sino como guard en `BarProcessingService`, decisión tomada tras analizar el código real), igual hay valor en extender el enum para casos futuros de eventos diagnósticos. Se agrega ahora para no tener que tocar el enum en cada paso siguiente.

Aislar estos cambios en el Paso 1 tiene dos beneficios concretos:

- **Blast radius chico.** Si algo sale mal, el diagnóstico es trivial (no hay Accord, no hay HMM, no hay régimen). El sistema debe seguir compilando y todos los tests existentes deben seguir verdes.
- **Compilación verificable.** Al final del Paso 1, el sistema sigue funcionando idéntico a antes (sin filtros nuevos, sin features nuevos). Si el backtest se corre, debe producir resultados idénticos al backtest pre-Paso-1.

---

## Decisiones técnicas aplicadas (no discutir, aplicar)

| Decisión | Valor |
|---|---|
| `MarketBar` ampliada | Agregar `Open`, `High`, `Low`, `Volume` como propiedades de solo lectura. Tipos: `decimal` para precios (consistente con `Close`), `decimal` para volumen (consistente con la convención del sistema de no usar `double` para magnitudes financieras). |
| Compatibilidad con código existente | `MarketBar` actual usa constructor `(InstrumentId, decimal close, DateTime timestampUtc)`. Mantener ese constructor existente funcionando: agregar uno nuevo con OHLCV completo y el viejo invoca al nuevo pasando `close` para todos los campos OHLC y `0m` para volumen. **Marcar el constructor viejo como `[Obsolete]`** con mensaje claro para que en futuras refactorizaciones se migre, pero sin romper código actual. |
| `MarketBarMapper` actualizado | Adaptar el mapper en `Trading.Strategies` para que extraiga OHLCV del `TradeBar` de Lean en lugar de pasar solo `Close`. |
| `CompatibleRegimes` en `StrategyDefinition` | Propiedad `List<string>? CompatibleRegimes` (nullable, lista concreta). Se modela como lista de strings (no de enum `RegimeLabel`) porque el enum vive en una capa que el JSON loader no debería conocer prematuramente. La conversión string→RegimeLabel se hace en Paso 2 cuando exista el enum. Si la lista está vacía (no nula), el loader falla loud. Se usa `List<T>` concreto (no `IReadOnlyList<T>`) porque es el patrón establecido del repo para DTOs de configuración: `RootConfig.Timeframes` ya es `Dictionary<string, TimeframeNode>` concreto, y Newtonsoft.Json deserializa tipos concretos sin ambigüedad. |
| Validación en `StrategyConfigLoader` | Si `CompatibleRegimes` está presente pero es lista vacía → error agregado al `collectedErrors` con mensaje claro. Si está ausente o nula → válido (significa "compatible con todos los regímenes", se resuelve en Paso 2). |
| `RiskLimitBreachReason` extendido | Agregar valor `RegimeIncompatibility`. No se usa todavía, pero queda definido para que Pasos 2 y 3 puedan emitir eventos diagnósticos sin volver a tocar el enum. |
| Nada de régimen aún | No crear `IMarketRegimeClassifier`, ni `RegimeLabel`, ni `MarketRegimeRegistry`, ni nada que tenga "Regime" en el nombre fuera de los dos puntos anteriores. Eso es el Paso 2. |

---

## Archivos a crear y modificar

### Archivos a modificar

```
Trading.Domain/Models/MarketBar.cs
  → Extender con propiedades Open, High, Low, Volume.
  → Agregar constructor completo (InstrumentId, decimal open, decimal high, decimal low, decimal close, decimal volume, DateTime timestampUtc).
  → Mantener constructor viejo (InstrumentId, decimal close, DateTime timestampUtc) marcado [Obsolete] que delega al nuevo.
  → Actualizar XmlDoc.

Trading.Domain/Models/StrategyDefinition.cs
  → Agregar propiedad: public List<string>? CompatibleRegimes { get; set; }
  → XmlDoc explicando semántica: null/ausente = compatible con todos los regímenes (fail-safe).
    Lista vacía = inválido, debe rechazarse en el loader. Lista con valores = compatibilidad explícita.
  → Razón de usar List<T> y no IReadOnlyList<T>: patrón establecido del repo para DTOs de
    configuración (ver RootConfig.Timeframes que es Dictionary concreto). Newtonsoft.Json
    deserializa tipos concretos sin ambigüedad.

Trading.Domain/Events/RiskLimitBreachedEvent.cs
  → Agregar valor RegimeIncompatibility al enum RiskLimitBreachReason.
  → XmlDoc: "Una estrategia intentó emitir una señal en un régimen de mercado declarado incompatible.
     No activa kill switch global; se usa para eventos diagnósticos emitidos por el filtro de régimen en BarProcessingService."

Trading.Strategies/Adapters/MarketBarMapper.cs
  → Actualizar para construir MarketBar con OHLCV completo desde TradeBar de Lean.
  → El TradeBar de Lean expone Open, High, Low, Close, Volume directamente — usar esas propiedades.

Trading.Strategies/Infrastructure/StrategyConfigLoader.cs
  → Agregar validación en ValidateOrFail: si una StrategyDefinition tiene CompatibleRegimes != null
    pero CompatibleRegimes.Count == 0, agregar a collectedErrors:
    "[Timeframe={tf}, Strategy={name}, Symbol={symbol}]
     El campo 'CompatibleRegimes' está presente pero es una lista vacía.
     Si querés que la estrategia opere en todos los regímenes, omití el campo (no lo incluyas como []).
     Si querés desactivar la estrategia, removela del JSON."
```

### Archivos a crear

```
Trading.Domain.Tests/MarketBarTests.cs
  → Tests del constructor nuevo y del [Obsolete] (verifica que delega correctamente).

(opcional, si el patrón del repo lo requiere) Trading.Domain.Tests/StrategyDefinitionTests.cs
  → Tests del DTO: cómo deserializa con CompatibleRegimes ausente, presente con valores, presente vacío.
    Nota: la validación de "lista vacía es error" la testea Trading.Application.Tests o donde
    ya estén los tests existentes del StrategyConfigLoader; verificar el repo y agregar el test
    en el lugar que corresponda según el patrón existente.
```

### Archivos que NO se tocan en este paso

```
strategies.json                              ← NO agregar todavía "CompatibleRegimes" a ninguna entrada.
                                                Eso es Paso 2. El loader debe seguir aceptando el JSON actual.
BarProcessingService.cs                      ← NO modificar. El filtro de régimen es Paso 2.
TradingAlgorithmHost.cs                      ← NO modificar.
RiskOrchestrator.cs                          ← NO modificar.
Cualquier archivo de estrategias concretas   ← NO modificar.
ROADMAP.md / DECISIONS.md                    ← NO modificar en este paso. El Hito B no está completo hasta el Paso 3.
```

---

## Contratos exactos

### `MarketBar.cs` actualizado

```csharp
using System;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Models
{
    /// <summary>
    /// Barra de mercado consolidada (OHLCV) en un timeframe dado para un instrumento.
    /// Estructura del dominio, sin acoplamiento a ningún motor.
    /// </summary>
    public sealed class MarketBar
    {
        public InstrumentId InstrumentId { get; }
        public decimal Open { get; }
        public decimal High { get; }
        public decimal Low { get; }
        public decimal Close { get; }
        public decimal Volume { get; }
        public DateTime TimestampUtc { get; }

        /// <summary>
        /// Constructor primario con OHLCV completo. Es el que deben usar los productores
        /// de barras (adaptadores de motor, datos sintéticos para tests, parsers de archivos históricos).
        /// </summary>
        public MarketBar(
            InstrumentId instrumentId,
            decimal open,
            decimal high,
            decimal low,
            decimal close,
            decimal volume,
            DateTime timestampUtc)
        {
            InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            TimestampUtc = timestampUtc;
        }

        /// <summary>
        /// Constructor legado: cuando una barra solo expone close (por ejemplo, código antiguo
        /// que aún no migró a OHLCV). Inicializa Open/High/Low con el mismo close y Volume en 0.
        /// </summary>
        /// <remarks>
        /// Marcado [Obsolete] como guía de migración, no como error: el sistema sigue funcionando.
        /// Cuando se elimine, debe ser después de verificar que todos los productores pasaron a OHLCV.
        /// </remarks>
        [Obsolete("Usar el constructor con OHLCV completo. Este constructor existe para compatibilidad temporal hasta que todos los productores de barras pasen a OHLCV.")]
        public MarketBar(InstrumentId instrumentId, decimal close, DateTime timestampUtc)
            : this(instrumentId, close, close, close, close, 0m, timestampUtc)
        {
        }
    }
}
```

### `StrategyDefinition.cs` actualizado

Agregar al final de la clase, después de `MaxBars`:

```csharp
/// <summary>
/// Lista de regímenes de mercado compatibles con esta estrategia (declarados como strings
/// en el JSON, ej. ["Trend", "Squeeze"]).
///
/// Semántica:
/// - null/ausente del JSON: la estrategia es compatible con todos los regímenes (fail-safe).
/// - Lista vacía []: inválido; el loader debe rechazarlo (ver StrategyConfigLoader).
/// - Lista con valores: solo se emiten señales cuando el clasificador reporta un régimen
///   incluido en esta lista.
///
/// La conversión de strings a el enum RegimeLabel ocurre en una capa superior cuando esa
/// abstracción exista (Paso 2 de Hito B). Aquí se mantiene como string para evitar acoplar
/// el DTO de configuración a una abstracción que aún no existe.
///
/// El tipo es List&lt;string&gt; concreto (no IReadOnlyList&lt;string&gt;) siguiendo el patrón
/// establecido del repo para DTOs de configuración deserializados con Newtonsoft.Json
/// (RootConfig.Timeframes es Dictionary concreto por la misma razón).
/// </summary>
public List<string>? CompatibleRegimes { get; set; }
```

Agregar `using System.Collections.Generic;` al archivo si no está ya.

### `RiskLimitBreachReason` (enum dentro de `RiskLimitBreachedEvent.cs`) actualizado

Agregar valor al final del enum:

```csharp
/// <summary>
/// Una estrategia intentó emitir una señal en un régimen de mercado declarado incompatible.
/// Este valor NO activa el kill switch global: el filtro de régimen rechaza la señal
/// específica en BarProcessingService como un guard pre-orden. El valor existe en el
/// enum para que las emisiones diagnósticas del filtro (futuras, no en este paso)
/// puedan categorizarse junto a las otras razones.
/// </summary>
RegimeIncompatibility
```

### Validación en `StrategyConfigLoader.cs`

Dentro de `ValidateOrFail`, en el bucle `foreach (var strategyDefinition in timeframeContent.Strategies)`, después de la validación de `RiskPerTradePercentage` (alrededor de la línea 4813 del código actual), agregar:

```csharp
// Validación de CompatibleRegimes: null/ausente es válido (fail-safe = compatible con todos).
// Lista vacía es inválido — indicaría una decisión confusa que el operador debe explicitar.
if (strategyDefinition.CompatibleRegimes != null &&
    strategyDefinition.CompatibleRegimes.Count == 0)
{
    collectedErrors.Add(
        $"{strategyContext}{System.Environment.NewLine}" +
        "El campo 'CompatibleRegimes' está presente pero es una lista vacía. " +
        "Si querés que la estrategia opere en todos los regímenes, omití el campo (no lo incluyas como []). " +
        "Si querés desactivar la estrategia, removela del JSON.");
}
```

### `MarketBarMapper.cs` actualizado

Buscar el método (o métodos) que construye `MarketBar` desde `TradeBar`. Reemplazar la construcción para pasar OHLCV completo. Algo conceptualmente así (ajustar al patrón concreto del archivo):

```csharp
// Antes (presumiblemente):
return new MarketBar(instrumentId, tradeBar.Close, tradeBar.Time.ToUniversalTime());

// Después:
return new MarketBar(
    instrumentId,
    tradeBar.Open,
    tradeBar.High,
    tradeBar.Low,
    tradeBar.Close,
    tradeBar.Volume,
    tradeBar.Time.ToUniversalTime());
```

Si hay múltiples sobrecargas en `MarketBarMapper`, actualizar todas. Si alguna llama al constructor `[Obsolete]`, refactorizarla para que use el constructor completo. Los avisos de compilación por `[Obsolete]` deben aparecer SOLO en código de tests o helpers, no en código de producción.

---

## Tests obligatorios

### `Trading.Domain.Tests/MarketBarTests.cs`

```csharp
using FluentAssertions;
using NUnit.Framework;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Tests
{
    [TestFixture]
    public class MarketBarTests
    {
        private static readonly InstrumentId Btc = new InstrumentId("BTCUSDT");
        private static readonly DateTime SampleTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Constructor_OHLCV_AsignaTodosLosCamposCorrectamente()
        {
            var bar = new MarketBar(
                instrumentId: Btc,
                open: 100m,
                high: 110m,
                low: 95m,
                close: 105m,
                volume: 1500m,
                timestampUtc: SampleTime);

            bar.InstrumentId.Should().Be(Btc);
            bar.Open.Should().Be(100m);
            bar.High.Should().Be(110m);
            bar.Low.Should().Be(95m);
            bar.Close.Should().Be(105m);
            bar.Volume.Should().Be(1500m);
            bar.TimestampUtc.Should().Be(SampleTime);
        }

        [Test]
        public void Constructor_OHLCV_LanzaSiInstrumentIdEsNull()
        {
            Action act = () => new MarketBar(
                instrumentId: null!,
                open: 100m, high: 110m, low: 95m, close: 105m, volume: 1500m,
                timestampUtc: SampleTime);

            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
#pragma warning disable CS0618 // probando deliberadamente el constructor obsoleto
        public void ConstructorObsoleto_DelegaCorrectamenteAlCompleto()
        {
            var bar = new MarketBar(Btc, close: 105m, timestampUtc: SampleTime);

            bar.InstrumentId.Should().Be(Btc);
            bar.Open.Should().Be(105m);
            bar.High.Should().Be(105m);
            bar.Low.Should().Be(105m);
            bar.Close.Should().Be(105m);
            bar.Volume.Should().Be(0m);
            bar.TimestampUtc.Should().Be(SampleTime);
        }
#pragma warning restore CS0618
    }
}
```

### Test de validación de `CompatibleRegimes` en el loader

Buscar en el repo el archivo de tests existente del `StrategyConfigLoader` (probablemente vive en `Trading.Application.Tests/` o en un proyecto análogo). Agregar un test del tipo:

```csharp
[Test]
public void Load_FallaSiCompatibleRegimesEstaPresenteVacio()
{
    // Construir un JSON temporal en disco con CompatibleRegimes: [] en una estrategia.
    // Llamar a loader.Load(path).
    // Aseverar que lanza StrategyConfigurationException con mensaje que contiene
    // "CompatibleRegimes" y "lista vacía".
}

[Test]
public void Load_AceptaSiCompatibleRegimesEstaAusente()
{
    // JSON sin el campo CompatibleRegimes en absoluto.
    // Debe cargar correctamente, y el StrategyDefinition resultante debe tener
    // CompatibleRegimes == null.
}

[Test]
public void Load_AceptaSiCompatibleRegimesTieneValores()
{
    // JSON con CompatibleRegimes: ["Trend", "Squeeze"].
    // Debe cargar correctamente.
    // StrategyDefinition.CompatibleRegimes debe ser una lista de 2 elementos.
}
```

Si el patrón del repo construye fixtures de JSON en disco con archivos temporales, seguir ese patrón. Si usa strings inline + un loader que acepta TextReader, seguir ese patrón. Adaptarse a lo existente, no inventar nuevo estilo.

---

## Validaciones de salida (a ejecutar por el usuario, NO por el asistente)

Después de que el asistente termine:

```bash
# Invariante arquitectónica: Domain y Application no conocen QuantConnect.
grep -rn "^using QuantConnect" Trading.Domain/ Trading.Application/ Trading.Application.Tests/ Trading.Domain.Tests/
# Debe devolver vacío.

# Build
dotnet build

# Tests
dotnet test
```

**Tests esperados después de Paso 1:**
- Todos los tests existentes siguen verdes (~57 tests antes del paso, según contexto del proyecto).
- Tests nuevos verdes: `MarketBarTests` (3 tests) + tests del loader para `CompatibleRegimes` (3 tests).
- Total esperado: ~63 tests verdes, 0 errores.

**Si el backtest se corre, debe producir resultados idénticos al backtest pre-Paso-1** (mismo número de operaciones, mismo PnL final, misma equity curve). El Paso 1 no agrega filtros nuevos, solo amplía estructuras. Si los resultados difieren, hay un bug de regresión en `MarketBarMapper` y hay que diagnosticarlo antes de seguir.

---

## Riesgos conocidos del Paso 1 y cómo el asistente debe manejarlos

1. **Compilación rompe en código que pasaba `MarketBar(id, close, time)` antes.** Si después de marcar el constructor como `[Obsolete]` aparecen warnings, son **esperados** en código de tests y helpers que aún no migraron — no es un error. Los warnings se convierten en errores solo si `TreatWarningsAsErrors` está habilitado en algún `.csproj`. Si pasa, reportar al usuario y proponer dos opciones: (a) migrar todos los call sites al constructor completo en este mismo paso, (b) suprimir el warning específico `CS0618` en los proyectos afectados. Por defecto preferir (a) si los call sites son ≤5; si son muchos, consultar.

2. **El `MarketBarMapper` puede tener helpers para construir barras "lite" en algún test.** Si Claude Code encuentra usos del constructor obsoleto en código de tests existentes, NO los migra (esos tests no necesitan OHLCV; el constructor obsoleto sigue funcional). Solo migra los call sites en código de producción.

3. **El `strategies.json` real del repo tiene actualmente una sola estrategia activa:** `EmaCrossStrategy` para BTCUSDT en timeframe 1h, con `RiskPerTradePercentage: 2.0`. El sistema arranca correctamente con esta configuración. El asistente NO debe tocar `strategies.json` en este paso. La estructura del archivo se modificará en Paso 2 cuando se agregue el campo `CompatibleRegimes` a la entrada del EmaCross.

   Nota sobre el archivo: existen dos copias del `strategies.json` en el repo. La fuente de verdad es `Trading.Strategies/strategies.json` (versionada en git). La copia en `Trading.Strategies/bin/Debug/net10.0/strategies.json` la genera el build cuando el archivo está marcado con `CopyToOutputDirectory` en el `.csproj`. El asistente edita únicamente la fuente de verdad; la copia del `bin/` se actualiza automáticamente al recompilar.

4. **Si Claude Code encuentra inconsistencias entre el `TodoMiCodigo.txt` que se le pasó y el código real en disco** (por ejemplo, un archivo que no existe o que tiene contenido diferente), debe detenerse, reportar la inconsistencia con detalle, y NO improvisar. El usuario decide cómo resolverla.

---

## Resumen para el usuario al final del Paso 1

Cuando Claude Code termine, el delta esperado en `git status` debe ser:

- Modificado: `Trading.Domain/Models/MarketBar.cs`
- Modificado: `Trading.Domain/Models/StrategyDefinition.cs`
- Modificado: `Trading.Domain/Events/RiskLimitBreachedEvent.cs`
- Modificado: `Trading.Strategies/Adapters/MarketBarMapper.cs`
- Modificado: `Trading.Strategies/Infrastructure/StrategyConfigLoader.cs`
- Modificado: posiblemente algunos archivos de producción que usaban el constructor `[Obsolete]` y se migraron al completo.
- Creado: `Trading.Domain.Tests/MarketBarTests.cs`
- Creado o modificado: archivos de tests del `StrategyConfigLoader` (depende de la estructura existente).

**Lo que NO debe aparecer en `git status`:**
- Cambios en `strategies.json`.
- Cambios en `BarProcessingService.cs`, `TradingAlgorithmHost.cs`, `RiskOrchestrator.cs`.
- Cambios en `ROADMAP.md` o `DECISIONS.md`.
- Archivos nuevos con "Regime" en el nombre (eso es Paso 2).
- Rama nueva (cero `git checkout -b`, cero `git switch -c`).

Si algo de la lista "NO debe aparecer" aparece, el usuario debe `git restore` esos archivos antes de commitear y volver a ejecutar el brief revisado con el asistente.
