# HITO B — Paso 2: Abstracción de régimen + filtro pre-orden con classifier fake

> **Brief ejecutable para Claude Code.** Segundo paso de tres del Hito B. Introduce las abstracciones de régimen en el Domain, crea el registry en Application, integra el filtro como guard pre-orden en `BarProcessingService`, y agrega un classifier fake configurable que permite validar la plomería sin necesidad de HMM real. El Paso 3 (próxima sesión) reemplazará el fake por el `AccordHmmClassifier` real y entrenará el modelo HMM con datos históricos.
>
> **Pre-requisito:** Paso 1 commiteado y verde. El brief asume que existen ya: `MarketBar` con OHLCV, `StrategyDefinition.CompatibleRegimes`, `RiskLimitBreachReason.RegimeIncompatibility`.

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio de los puntos críticos para este brief:

- **Cero comandos `git` de cualquier tipo.** Lista exhaustiva en `AI.md` (incluye `worktree`, `checkout -b`, `switch -c`, `branch`, `stash`, etc.). Si la herramienta subyacente ofrece crear ramas o worktrees, declinar/desactivar. Trabajar sobre la rama que el usuario tiene checked-out.
- **No compilar.** El usuario compila.
- **No correr tests.** El usuario corre tests.
- **Si Claude Code detecta una inconsistencia entre el código actual y este brief**, detenerse y reportar. NO improvisar.

---

## Contexto y motivación del Paso 2

Este paso introduce todo lo necesario para que el régimen funcione como filtro pre-orden, sin la complejidad de un HMM real. El classifier es un fake configurable que devuelve siempre la etiqueta que se le pase al constructor. Esto permite:

1. **Validar el wiring completo.** Si el fake devuelve `Trend` y la estrategia EmaCross declara `CompatibleRegimes: ["Trend"]`, el filtro debe permitir todas las señales y el backtest debe ser idéntico al pre-Paso-2.
2. **Validar empíricamente que el filtro filtra.** Si en una corrida de prueba se configura el fake para devolver `HighVolatility`, el filtro debe rechazar todas las señales y el backtest debe producir cero operaciones.
3. **Aislar Accord y el HMM real para el Paso 3.** Cuando se introduzca el `AccordHmmClassifier`, solo se va a tener que reemplazar la instancia del fake por la real en el wiring, sin tocar el `BarProcessingService` ni el registry ni los contratos del Domain.

El filtro vive como **guard `continue` en `BarProcessingService`**, al lado de `IsKillSwitchActivated` y `IsInvested`. Es el patrón que el código ya tiene establecido. **NO va vía `IRiskMonitor` ni vía `RiskOrchestrator`**: el régimen no es una condición catastrófica que justifique liquidar el portfolio, es un filtro de validez de señal.

---

## Decisiones técnicas aplicadas (no discutir, aplicar)

| Decisión | Valor |
|---|---|
| Ubicación del filtro | Guard `continue` en `BarProcessingService.ProcessBar`, **después de `if (_riskOrchestrator.IsKillSwitchActivated) continue;`** y **después de `if (signalDirection == SignalDirection.Flat) continue;`**, pero **antes de las verificaciones de `IsInvested` y `HasOpenOrders`**. El orden importa: solo evaluamos régimen si la estrategia produjo una señal de entrada real, para evitar consultas innecesarias al registry. |
| Consolidator de régimen | Dedicado de 4h en `TradingAlgorithmHost.Initialize()`, independiente de los consolidators de estrategias. Se construye **una sola vez por instrumento**, no por estrategia. Hardcodeado a 4h para este paso. |
| Classifier fake | `ConfigurableMarketRegimeClassifier` en `Trading.Application/Regimes/`. Constructor recibe `InstrumentId` y `RegimeLabel`. Implementa `IMarketRegimeClassifier`. Devuelve siempre la misma clasificación con probabilidad 1.0. `IsWarmedUp` siempre true. Útil para tests y para validar wiring en backtest. |
| Configuración del fake en producción | El fake se construye en `TradingAlgorithmHost.Initialize()` con `RegimeLabel.Trend` (la etiqueta que la estrategia EmaCross declara como compatible). Esto hace que el backtest post-Paso-2 sea idéntico al pre-Paso-2. |
| Parseo de strings a `RegimeLabel` | `RegimeLabel.Parse(string)` o método análogo. Si una estrategia declara `CompatibleRegimes: ["Trend", "InvalidLabel"]`, el sistema falla loud en boot con mensaje claro identificando la estrategia, el timeframe y el valor inválido. La validación ocurre en `TradingAlgorithmHost` cuando se construyen los `StrategyExecutor`, no en el `StrategyConfigLoader` (porque `StrategyConfigLoader` vive en `Trading.Strategies` y no debería conocer `RegimeLabel`). Alternativa: agregar el parseo a una capa intermedia en `Trading.Application`. Ver sección "Wiring detallado" para resolver concretamente. |
| Comportamiento si el clasificador no está warmed-up | El registry devuelve `RegimeLabel.Unknown`. El filtro en `BarProcessingService` trata `Unknown` como **compatible con cualquier estrategia** (fail-safe: si no sabemos el régimen, no filtramos). Esta es la misma política que vimos en discusiones previas y queda asentada acá. |
| Comportamiento si la estrategia tiene `CompatibleRegimes` null | El filtro deja pasar todas las señales (fail-safe: ausencia de configuración = no filtrar). |

---

## Estructura final de archivos

### Archivos nuevos

```
Trading.Domain/
  Abstractions/Regimes/
    IMarketRegimeClassifier.cs
    RegimeLabel.cs
    RegimeClassification.cs

Trading.Application/
  Regimes/
    MarketRegimeRegistry.cs
    ConfigurableMarketRegimeClassifier.cs
    StrategyRegimeCompatibility.cs

Trading.Application.Tests/
  Regimes/
    MarketRegimeRegistryTests.cs
    ConfigurableMarketRegimeClassifierTests.cs
    StrategyRegimeCompatibilityTests.cs
    BarProcessingServiceRegimeFilterTests.cs   ← NUEVO: tests del filtro pre-orden

Trading.Domain.Tests/
  RegimeLabelTests.cs                          ← Parse, ToString, valores válidos
  RegimeClassificationTests.cs                 ← UnknownFor, invariantes
```

### Archivos a modificar

```
Trading.Application/Execution/BarProcessingService.cs
  → Nueva dependencia: MarketRegimeRegistry, IReadOnlyDictionary<string, StrategyRegimeCompatibility>.
  → Insertar guard de régimen entre el check de KillSwitch y la verificación de Flat (ver sección Wiring detallado).

Trading.Strategies/TradingAlgorithmHost.cs
  → Construir MarketRegimeRegistry con ConfigurableMarketRegimeClassifier(BTCUSDT, Trend).
  → Construir consolidator 4h por instrumento, alimentar el registry desde ese consolidator.
  → Parsear strategies[].CompatibleRegimes (List<string>) a RegimeLabel[] al construir los StrategyExecutor.
  → Construir el diccionario StrategyExecutor.ExecutorIdentifier → StrategyRegimeCompatibility.
  → Pasarlo al BarProcessingService.

Trading.Strategies/strategies.json
  → Agregar "CompatibleRegimes": ["Trend"] a la entrada de EmaCrossStrategy en 1h.

Trading.Application.Tests/StrategyExecutor (si los tests existentes lo construyen)
  → Ajustar invocaciones para que sigan compilando si la firma cambió.
```

### Archivos que NO se tocan en este paso

```
Trading.Strategies.csproj                     ← NO agregar Accord. Eso es Paso 3.
RiskOrchestrator.cs                           ← NO modificar. El régimen no va por el orquestador.
ROADMAP.md / DECISIONS.md                     ← NO modificar. El Hito B no está completo hasta el Paso 3.
StrategyConfigLoader.cs                       ← NO agregar parseo de RegimeLabel. La conversión ocurre en TradingAlgorithmHost.
Cualquier archivo con "Accord", "HMM", "Hidden Markov" → NO crear en este paso.
```

---

## Contratos exactos

### `RegimeLabel.cs`

```csharp
namespace Trading.Domain.Abstractions.Regimes
{
    /// <summary>
    /// Etiqueta semántica del régimen de mercado. La semántica es estable entre instrumentos:
    /// "Trend" significa lo mismo para BTCUSDT que para SOLUSDT, aunque los parámetros estadísticos
    /// que el clasificador subyacente aprende para detectarlo sean distintos.
    ///
    /// Los valores son fixed por design: agregar uno nuevo es un cambio de contrato del Domain
    /// que requiere un ADR.
    /// </summary>
    public enum RegimeLabel
    {
        /// <summary>
        /// El régimen no pudo determinarse: clasificador en warm-up, instrumento sin clasificador
        /// registrado, o error de inferencia. Política del sistema: Unknown se trata como
        /// "compatible con cualquier estrategia" (fail-safe: no filtramos si no sabemos).
        /// </summary>
        Unknown = 0,

        /// <summary>Tendencia sostenida (alcista o bajista). Estrategias trend-following esperan operar acá.</summary>
        Trend = 1,

        /// <summary>Mercado lateral con reversión a la media. Estrategias mean-reverting esperan operar acá.</summary>
        MeanReverting = 2,

        /// <summary>Alta volatilidad sin dirección clara. La mayoría de las estrategias deben evitar operar.</summary>
        HighVolatility = 3,

        /// <summary>Compresión de volatilidad, baja actividad. Pre-breakout típico.</summary>
        Squeeze = 4
    }

    /// <summary>
    /// Helpers de parsing/serialización para RegimeLabel. Encapsulados acá para que los consumers
    /// no se acoplen a Enum.TryParse y para que los mensajes de error tengan formato consistente.
    /// </summary>
    public static class RegimeLabelParser
    {
        /// <summary>
        /// Convierte un string (típicamente desde strategies.json) a RegimeLabel.
        /// Lanza ArgumentException si el string no corresponde a ningún valor del enum
        /// (incluyendo "Unknown", que es válido sintácticamente pero no debería usarse
        /// en configuración de estrategias — fail loud para forzar al operador a ser explícito).
        /// </summary>
        public static RegimeLabel Parse(string regimeName)
        {
            if (string.IsNullOrWhiteSpace(regimeName))
                throw new System.ArgumentException(
                    "El nombre del régimen no puede ser nulo ni vacío.", nameof(regimeName));

            if (!System.Enum.TryParse<RegimeLabel>(regimeName, ignoreCase: false, out var parsed))
                throw new System.ArgumentException(
                    $"'{regimeName}' no es un RegimeLabel válido. Valores aceptados: " +
                    "Trend, MeanReverting, HighVolatility, Squeeze. " +
                    "(Unknown no se acepta como configuración explícita: si querés que la estrategia " +
                    "opere en todos los regímenes, omití el campo CompatibleRegimes del JSON.)",
                    nameof(regimeName));

            if (parsed == RegimeLabel.Unknown)
                throw new System.ArgumentException(
                    "'Unknown' no se acepta como configuración explícita de CompatibleRegimes. " +
                    "Si querés que la estrategia opere en todos los regímenes, omití el campo del JSON.",
                    nameof(regimeName));

            return parsed;
        }
    }
}
```

### `RegimeClassification.cs`

```csharp
using System;
using System.Collections.Generic;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions.Regimes
{
    /// <summary>
    /// Resultado inmutable de una clasificación de régimen. Incluye la etiqueta más probable
    /// y la distribución completa de probabilidades para que los consumers downstream puedan
    /// implementar políticas graduadas en el futuro (ej. position sizing proporcional a la
    /// probabilidad del régimen). En el Paso 2 solo se consulta Label; las probabilidades
    /// son parte del contrato para que el Paso 3 (HMM real) no requiera cambiar la interfaz.
    /// </summary>
    public sealed record RegimeClassification(
        InstrumentId Instrument,
        RegimeLabel Label,
        IReadOnlyDictionary<RegimeLabel, double> Probabilities,
        DateTime ClassifiedAtUtc)
    {
        /// <summary>
        /// Construye una clasificación "Unknown" para un instrumento. Política del sistema:
        /// Unknown se interpreta como "compatible con cualquier estrategia" (fail-safe).
        /// </summary>
        public static RegimeClassification UnknownFor(InstrumentId instrument, DateTime classifiedAtUtc) =>
            new(
                instrument,
                RegimeLabel.Unknown,
                new Dictionary<RegimeLabel, double> { [RegimeLabel.Unknown] = 1.0 },
                classifiedAtUtc);
    }
}
```

Notar el `double` para probabilidades: es magnitud estadística, no financiera. Permitido por `AI.md` (que prohíbe `double` solo para precios/cantidades/dinero).

### `IMarketRegimeClassifier.cs`

```csharp
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Abstractions.Regimes
{
    /// <summary>
    /// Contrato agnóstico del algoritmo de clasificación. El consumer downstream (registry,
    /// filtro en BarProcessingService) no sabe ni le importa si la implementación es HMM,
    /// k-means, red neuronal o ensemble.
    ///
    /// El clasificador es stateful: acumula barras internamente para construir la secuencia
    /// que el algoritmo subyacente necesita. Cada instancia clasifica un único instrumento.
    /// </summary>
    public interface IMarketRegimeClassifier
    {
        /// <summary>Instrumento para el cual este clasificador está entrenado/configurado.</summary>
        InstrumentId Instrument { get; }

        /// <summary>True cuando el clasificador tiene historia suficiente para clasificaciones confiables.</summary>
        bool IsWarmedUp { get; }

        /// <summary>
        /// Procesa una barra nueva y devuelve la clasificación actualizada.
        /// Si IsWarmedUp es false, debe retornar RegimeClassification.UnknownFor(...).
        /// Si la barra es de un instrumento distinto a Instrument, el comportamiento es
        /// implementation-defined (las implementaciones deben loguear y devolver UnknownFor).
        /// </summary>
        RegimeClassification Classify(MarketBar bar);
    }
}
```

### `ConfigurableMarketRegimeClassifier.cs`

```csharp
using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Regimes
{
    /// <summary>
    /// Clasificador determinista que devuelve siempre la misma etiqueta con probabilidad 1.0.
    /// Útil para:
    /// - Validar el wiring completo del filtro de régimen sin necesidad de un modelo real.
    /// - Tests del MarketRegimeRegistry y del filtro en BarProcessingService.
    /// - Backtests de control donde se quiere forzar un régimen específico para comparar.
    ///
    /// IsWarmedUp es siempre true porque no hay nada que "warmar".
    ///
    /// En el Paso 3 de Hito B este classifier coexiste con AccordHmmClassifier: ambos implementan
    /// IMarketRegimeClassifier y son intercambiables en el wiring.
    /// </summary>
    public sealed class ConfigurableMarketRegimeClassifier : IMarketRegimeClassifier
    {
        private readonly RegimeLabel _fixedLabel;
        private readonly IClock _clock;

        public InstrumentId Instrument { get; }
        public bool IsWarmedUp => true;

        public ConfigurableMarketRegimeClassifier(InstrumentId instrument, RegimeLabel fixedLabel, IClock clock)
        {
            Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            if (fixedLabel == RegimeLabel.Unknown)
                throw new ArgumentException(
                    "ConfigurableMarketRegimeClassifier no debe configurarse con Unknown: " +
                    "si querés clasificación Unknown, no registres un classifier para el instrumento " +
                    "(el registry devuelve UnknownFor automáticamente).",
                    nameof(fixedLabel));
            _fixedLabel = fixedLabel;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public RegimeClassification Classify(MarketBar bar)
        {
            // El fake no usa los datos de la barra; solo devuelve la etiqueta fija.
            // Igual respeta el contrato de timestamp para que los consumers vean coherencia temporal.
            return new RegimeClassification(
                Instrument,
                _fixedLabel,
                new Dictionary<RegimeLabel, double> { [_fixedLabel] = 1.0 },
                bar.TimestampUtc);
        }
    }
}
```

### `MarketRegimeRegistry.cs`

```csharp
using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Regimes
{
    /// <summary>
    /// Registry de clasificadores por instrumento. Mantiene:
    /// - El mapa instrumento → IMarketRegimeClassifier.
    /// - La última clasificación procesada por instrumento (cache para consultas del filtro).
    ///
    /// Las barras se le pasan vía ClassifyBar (típicamente desde un consolidator del timeframe
    /// del régimen). Las consultas del filtro usan GetLastClassification para evitar
    /// re-clasificación en cada barra de timeframe inferior.
    ///
    /// Si un instrumento no tiene clasificador registrado, GetLastClassification devuelve
    /// RegimeClassification.UnknownFor(...): política fail-safe.
    /// </summary>
    public sealed class MarketRegimeRegistry
    {
        private readonly Dictionary<InstrumentId, IMarketRegimeClassifier> _classifiers;
        private readonly Dictionary<InstrumentId, RegimeClassification> _lastClassifications;
        private readonly IClock _clock;
        private readonly ITradingLogger _logger;

        public MarketRegimeRegistry(
            IEnumerable<IMarketRegimeClassifier> classifiers,
            IClock clock,
            ITradingLogger logger)
        {
            if (classifiers == null) throw new ArgumentNullException(nameof(classifiers));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _classifiers = new Dictionary<InstrumentId, IMarketRegimeClassifier>();
            _lastClassifications = new Dictionary<InstrumentId, RegimeClassification>();

            foreach (var classifier in classifiers)
            {
                if (_classifiers.ContainsKey(classifier.Instrument))
                    throw new InvalidOperationException(
                        $"MarketRegimeRegistry: ya existe un clasificador registrado para {classifier.Instrument}. " +
                        "Cada instrumento debe tener exactamente un clasificador.");
                _classifiers[classifier.Instrument] = classifier;
            }
        }

        /// <summary>
        /// Procesa una barra para el instrumento correspondiente y actualiza la última clasificación.
        /// Si no hay clasificador registrado para el instrumento de la barra, loguea Debug y no hace nada.
        /// Nunca lanza ante errores de instrumento desconocido (fail-safe).
        /// </summary>
        public RegimeClassification ClassifyBar(MarketBar bar)
        {
            if (bar == null) throw new ArgumentNullException(nameof(bar));

            if (!_classifiers.TryGetValue(bar.InstrumentId, out var classifier))
            {
                _logger.Debug(
                    "MarketRegimeRegistry: barra recibida para {InstrumentId} pero no hay clasificador registrado. Se ignora.",
                    bar.InstrumentId);
                var unknown = RegimeClassification.UnknownFor(bar.InstrumentId, bar.TimestampUtc);
                _lastClassifications[bar.InstrumentId] = unknown;
                return unknown;
            }

            var classification = classifier.Classify(bar);
            _lastClassifications[bar.InstrumentId] = classification;
            return classification;
        }

        /// <summary>
        /// Devuelve la última clasificación procesada para el instrumento. Si nunca se procesó
        /// una barra para ese instrumento, devuelve UnknownFor con timestamp del reloj actual.
        /// </summary>
        public RegimeClassification GetLastClassification(InstrumentId instrument)
        {
            if (instrument == null) throw new ArgumentNullException(nameof(instrument));

            if (_lastClassifications.TryGetValue(instrument, out var classification))
                return classification;

            return RegimeClassification.UnknownFor(instrument, _clock.UtcNow);
        }

        public bool HasClassifier(InstrumentId instrument)
        {
            if (instrument == null) throw new ArgumentNullException(nameof(instrument));
            return _classifiers.ContainsKey(instrument);
        }
    }
}
```

### `StrategyRegimeCompatibility.cs`

```csharp
using System;
using System.Collections.Generic;
using Trading.Domain.Abstractions.Regimes;

namespace Trading.Application.Regimes
{
    /// <summary>
    /// Define qué regímenes son compatibles con una estrategia específica. Construido al boot
    /// a partir de StrategyDefinition.CompatibleRegimes (strings) tras parseo a RegimeLabel.
    ///
    /// Semántica de IsCompatibleWith:
    /// - Si AllowedRegimes es null/vacío: la estrategia es compatible con todos los regímenes
    ///   (fail-safe: ausencia de configuración no filtra nada).
    /// - Si AllowedRegimes tiene valores: la estrategia es compatible solo con esos regímenes.
    /// - RegimeLabel.Unknown siempre devuelve true (si no sabemos el régimen, no filtramos).
    /// </summary>
    public sealed class StrategyRegimeCompatibility
    {
        private readonly IReadOnlySet<RegimeLabel> _allowedRegimes;

        /// <summary>Identificador del executor (StrategyExecutor.ExecutorIdentifier) al que aplica esta compatibilidad.</summary>
        public string ExecutorIdentifier { get; }

        public StrategyRegimeCompatibility(string executorIdentifier, IReadOnlySet<RegimeLabel>? allowedRegimes)
        {
            if (string.IsNullOrWhiteSpace(executorIdentifier))
                throw new ArgumentException("ExecutorIdentifier no puede ser nulo ni vacío.", nameof(executorIdentifier));

            ExecutorIdentifier = executorIdentifier;
            // null y vacío se tratan igual: "compatible con todos". Internamente normalizamos a HashSet vacío.
            _allowedRegimes = allowedRegimes ?? new HashSet<RegimeLabel>();
        }

        /// <summary>True si la estrategia puede operar en el régimen dado.</summary>
        public bool IsCompatibleWith(RegimeLabel regime)
        {
            if (regime == RegimeLabel.Unknown) return true;       // fail-safe
            if (_allowedRegimes.Count == 0) return true;          // no configurado = compatible con todos
            return _allowedRegimes.Contains(regime);
        }
    }
}
```

---

## Modificación de `BarProcessingService`

Constructor con dos nuevas dependencias:

```csharp
private readonly MarketRegimeRegistry _regimeRegistry;
private readonly IReadOnlyDictionary<string, StrategyRegimeCompatibility> _strategyCompatibilities;

public BarProcessingService(
    IPortfolioState portfolioState,
    IOrderRouter orderRouter,
    RiskOrchestrator riskOrchestrator,
    PositionSizer positionSizer,
    ITradingLogger logger,
    IDomainEventBus eventBus,
    IClock clock,
    MarketRegimeRegistry regimeRegistry,
    IReadOnlyDictionary<string, StrategyRegimeCompatibility> strategyCompatibilities)
{
    // ... asignaciones existentes ...
    _regimeRegistry = regimeRegistry ?? throw new ArgumentNullException(nameof(regimeRegistry));
    _strategyCompatibilities = strategyCompatibilities ?? throw new ArgumentNullException(nameof(strategyCompatibilities));
}
```

Cambio en `ProcessBar`, justo después del check de KillSwitch y del check de Flat, antes de los checks de `IsInvested` y `HasOpenOrders`:

```csharp
if (_riskOrchestrator.IsKillSwitchActivated) continue;

SignalDirection signalDirection = strategyExecutor.Strategy.EvaluateSignal(marketBar);

if (signalDirection == SignalDirection.Flat) continue;

// ===== NUEVO: filtro de régimen pre-orden =====
// Si la estrategia declara CompatibleRegimes y el régimen actual del instrumento no está
// en esa lista, la señal se descarta. Régimen Unknown nunca filtra (fail-safe).
if (_strategyCompatibilities.TryGetValue(strategyExecutor.ExecutorIdentifier, out var compatibility))
{
    var currentRegime = _regimeRegistry.GetLastClassification(instrumentId);
    if (!compatibility.IsCompatibleWith(currentRegime.Label))
    {
        _logger.Debug(
            "BarProcessingService: señal {Direction} descartada para {ExecutorIdentifier}. " +
            "Régimen actual de {InstrumentId} es {CurrentRegime}, no está en CompatibleRegimes de la estrategia.",
            signalDirection, strategyExecutor.ExecutorIdentifier, instrumentId, currentRegime.Label);
        continue;
    }
}
// Si no hay compatibility registrada para este executor, no filtramos (fail-safe).

// Bloqueamos entrada si ya hay posición o hay órdenes pending.
if (_portfolioState.IsInvested(instrumentId)) continue;
// ... resto idéntico ...
```

**Notas sobre el orden de los guards:**
- El check de Flat va antes que el filtro de régimen porque si no hay señal, no tiene sentido consultar régimen.
- El filtro de régimen va antes que `IsInvested` porque queremos que el log de "señal descartada por régimen" se emita aunque coincida con que ya hay posición (es información útil para diagnóstico).
- Los checks de `IsInvested` y `HasOpenOrders` quedan exactamente donde estaban.

---

## Wiring detallado en `TradingAlgorithmHost.Initialize()`

### Construcción del registry y del fake

Insertar **después** de la construcción de `_positionSizer` y **antes** de la carga del `strategies.json`:

```csharp
// ===== Régimen de mercado =====
// Paso 2 de Hito B: classifier fake (devuelve siempre Trend) que valida el wiring del filtro.
// Paso 3 reemplazará este fake por AccordHmmClassifier con modelo entrenado offline.
var btcInstrumentId = new InstrumentId("BTCUSDT");
var regimeClassifierBtc = new ConfigurableMarketRegimeClassifier(
    btcInstrumentId, RegimeLabel.Trend, _clock);
var regimeRegistry = new MarketRegimeRegistry(
    new IMarketRegimeClassifier[] { regimeClassifierBtc }, _clock, _logger);
```

### Parseo de CompatibleRegimes y construcción de StrategyRegimeCompatibility

Modificar el loop de construcción de executors. Donde dice "===== Construcción de executors =====", agregar después de la creación de cada `StrategyExecutor`:

```csharp
var strategyCompatibilities = new Dictionary<string, StrategyRegimeCompatibility>();

// ... loop foreach (var timeframeNode in rootConfiguration.Timeframes) { ...
//     foreach (var strategyGroup in strategiesBySymbol) { ...
//         foreach (var strategyDefinition in strategyGroup) {
//             var strategyExecutor = new StrategyExecutor(...);  // ya existe
//             _strategyExecutors.Add(strategyExecutor);
//             localStrategyExecutors.Add(strategyExecutor);

//             // NUEVO: parseo de CompatibleRegimes y registro en el diccionario.
//             IReadOnlySet<RegimeLabel>? allowedRegimes = null;
//             if (strategyDefinition.CompatibleRegimes != null)
//             {
//                 var parsedLabels = new HashSet<RegimeLabel>();
//                 foreach (var regimeName in strategyDefinition.CompatibleRegimes)
//                 {
//                     try
//                     {
//                         parsedLabels.Add(RegimeLabelParser.Parse(regimeName));
//                     }
//                     catch (ArgumentException ex)
//                     {
//                         throw new InvalidOperationException(
//                             $"Estrategia '{strategyExecutor.ExecutorIdentifier}' (timeframe {timeframe}, símbolo {symbolTicker}): " +
//                             $"valor inválido en CompatibleRegimes. {ex.Message}", ex);
//                     }
//                 }
//                 allowedRegimes = parsedLabels;
//             }
//             strategyCompatibilities[strategyExecutor.ExecutorIdentifier] =
//                 new StrategyRegimeCompatibility(strategyExecutor.ExecutorIdentifier, allowedRegimes);
//         }
//     }
// }
```

### Consolidator de 4h dedicado al régimen

Insertar **después** del loop de consolidators de estrategias y **antes** de la construcción de `_barProcessingService`. Por cada instrumento que tenga clasificador registrado en el registry, crear un consolidator 4h independiente:

```csharp
// ===== Consolidator dedicado para el régimen de mercado (4h) =====
// Independiente de los consolidators de estrategias. Alimenta al MarketRegimeRegistry.
// Hardcodeado a 4h en este paso; futuras iteraciones pueden parametrizar el timeframe por instrumento.
foreach (var instrumentId in new[] { btcInstrumentId })
{
    if (!regimeRegistry.HasClassifier(instrumentId)) continue;

    var symbol = _instrumentResolver.Resolve(instrumentId);
    var regimeConsolidator = new TradeBarConsolidator(TimeSpan.FromHours(4));

    regimeConsolidator.DataConsolidated += (sender, tradeBarData) =>
    {
        if (IsWarmingUp) return;
        var marketBar = MarketBarMapper.ToMarketBar((TradeBar)tradeBarData, _instrumentResolver);
        regimeRegistry.ClassifyBar(marketBar);
    };

    SubscriptionManager.AddConsolidator(symbol, regimeConsolidator);
}
```

### Construcción del BarProcessingService con las nuevas dependencias

Donde hoy dice:

```csharp
_barProcessingService = new BarProcessingService(
    _portfolioState, _orderRouter, _riskOrchestrator, _positionSizer, _logger, domainEventBus, _clock);
```

Reemplazar por:

```csharp
_barProcessingService = new BarProcessingService(
    _portfolioState, _orderRouter, _riskOrchestrator, _positionSizer,
    _logger, domainEventBus, _clock,
    regimeRegistry, strategyCompatibilities);
```

### Imports nuevos en TradingAlgorithmHost.cs

```csharp
using System;
using System.Collections.Generic;
using Trading.Application.Regimes;
using Trading.Domain.Abstractions.Regimes;
```

---

## Modificación de `strategies.json`

Estado actual (única estrategia activa):

```json
"1h": {
  "Strategies": [
    {
      "StrategyName": "EmaCrossStrategy",
      "FileModelName": "",
      "Symbol": "BTCUSDT",
      "StopTakeMode": "Percentage",
      "StopLossPercentage": 1.0,
      "TakeProfitPercentage": 2.0,
      "StopLossAtrMultiplier": 0,
      "TakeProfitAtrMultiplier": 0,
      "RiskPerTradePercentage": 2.0,
      "MaxBars": 20,
      "CombineWithTimeExit": true
    }
  ]
}
```

Estado final del Paso 2 — agregar el campo `CompatibleRegimes`:

```json
"1h": {
  "Strategies": [
    {
      "StrategyName": "EmaCrossStrategy",
      "FileModelName": "",
      "Symbol": "BTCUSDT",
      "StopTakeMode": "Percentage",
      "StopLossPercentage": 1.0,
      "TakeProfitPercentage": 2.0,
      "StopLossAtrMultiplier": 0,
      "TakeProfitAtrMultiplier": 0,
      "RiskPerTradePercentage": 2.0,
      "MaxBars": 20,
      "CombineWithTimeExit": true,
      "CompatibleRegimes": ["Trend"]
    }
  ]
}
```

Razón: EmaCross es una estrategia trend-following. Solo debe operar en régimen Trend. Como el fake del Paso 2 devuelve `Trend`, el backtest post-Paso-2 debe ser idéntico al pre-Paso-2.

**Recordatorio importante sobre los dos `strategies.json`:** la fuente de verdad es `Trading.Strategies/strategies.json` (versionada en git). La copia en `Trading.Strategies/bin/Debug/net10.0/strategies.json` la genera el build vía `CopyToOutputDirectory`. El asistente edita únicamente la fuente de verdad.

---

## Tests obligatorios

### `RegimeLabelTests.cs` (en `Trading.Domain.Tests`)

```csharp
[Fact] public void Parse_DevuelveTrendCorrectamente() { /* ... */ }
[Fact] public void Parse_DevuelveTodosLosValoresValidos() { /* iterar Trend, MeanReverting, HighVolatility, Squeeze */ }
[Fact] public void Parse_LanzaSiStringEsVacio() { /* ... */ }
[Fact] public void Parse_LanzaSiStringEsNulo() { /* ... */ }
[Fact] public void Parse_LanzaSiStringEsInvalido() { /* "FooBar" → ArgumentException con mensaje claro */ }
[Fact] public void Parse_LanzaSiStringEsUnknown() { /* mensaje específico explicando que Unknown no se acepta */ }
[Fact] public void Parse_RespetaCaseSensitive() { /* "trend" → falla (acepta solo "Trend") */ }
```

### `RegimeClassificationTests.cs` (en `Trading.Domain.Tests`)

```csharp
[Fact] public void UnknownFor_DevuelveClasificacionConLabelUnknown() { /* ... */ }
[Fact] public void UnknownFor_DevuelveProbabilidadUnoEnUnknown() { /* ... */ }
[Fact] public void Constructor_AsignaTodosLosCamposCorrectamente() { /* ... */ }
```

### `ConfigurableMarketRegimeClassifierTests.cs` (en `Trading.Application.Tests/Regimes/`)

```csharp
[Fact] public void Classify_DevuelveSiempreLaEtiquetaFija() { /* pasar varias barras, todas devuelven Trend */ }
[Fact] public void IsWarmedUp_EsSiempreTrue() { /* ... */ }
[Fact] public void Constructor_LanzaSiFixedLabelEsUnknown() { /* ArgumentException con mensaje claro */ }
[Fact] public void Constructor_LanzaSiInstrumentEsNulo() { /* ArgumentNullException */ }
[Fact] public void Classify_PropagaTimestampDeLaBarra() { /* el RegimeClassification.ClassifiedAtUtc == bar.TimestampUtc */ }
```

### `MarketRegimeRegistryTests.cs` (en `Trading.Application.Tests/Regimes/`)

```csharp
[Fact] public void ClassifyBar_LlamaAlClassifierCorrespondiente() { /* fake classifier captura la llamada */ }
[Fact] public void ClassifyBar_ParaInstrumentoSinClasificador_DevuelveUnknown() { /* ... */ }
[Fact] public void ClassifyBar_ParaInstrumentoSinClasificador_LogueaDebug() { /* FakeTradingLogger captura */ }
[Fact] public void GetLastClassification_DevuelveUnknownSiNuncaSeClasificoNada() { /* ... */ }
[Fact] public void GetLastClassification_DevuelveLaUltimaClasificacionProcesada() { /* ... */ }
[Fact] public void Constructor_LanzaSiHayDosClassifiersParaElMismoInstrumento() { /* InvalidOperationException */ }
[Fact] public void HasClassifier_DevuelveTrueParaInstrumentoRegistrado() { /* ... */ }
```

### `StrategyRegimeCompatibilityTests.cs` (en `Trading.Application.Tests/Regimes/`)

```csharp
[Fact] public void IsCompatibleWith_SiAllowedRegimesEsNull_DevuelveSiempreTrue() { /* ... */ }
[Fact] public void IsCompatibleWith_SiAllowedRegimesEstaVacio_DevuelveSiempreTrue() { /* ... */ }
[Fact] public void IsCompatibleWith_SiAllowedRegimesContieneEtiqueta_DevuelveTrue() { /* ... */ }
[Fact] public void IsCompatibleWith_SiAllowedRegimesNoContieneEtiqueta_DevuelveFalse() { /* ... */ }
[Fact] public void IsCompatibleWith_Unknown_SiempreDevuelveTrue() { /* fail-safe */ }
[Fact] public void Constructor_LanzaSiExecutorIdentifierEsNuloOVacio() { /* ... */ }
```

### `BarProcessingServiceRegimeFilterTests.cs` (en `Trading.Application.Tests/Regimes/`)

Este es el test más importante del Paso 2: valida que el filtro funciona end-to-end.

```csharp
[Fact]
public void ProcessBar_SiRegimenEsCompatible_PermiteEnvioDeOrden()
{
    // Arrange: classifier fake devuelve Trend. Estrategia tiene CompatibleRegimes = [Trend].
    // FakeStrategy emite SignalDirection.Long.
    // Act: ProcessBar.
    // Assert: FakeOrderRouter recibió SubmitMarketOrder.
}

[Fact]
public void ProcessBar_SiRegimenEsIncompatible_DescartaSenal()
{
    // Arrange: classifier fake devuelve HighVolatility. Estrategia tiene CompatibleRegimes = [Trend].
    // FakeStrategy emite SignalDirection.Long.
    // Act: ProcessBar.
    // Assert: FakeOrderRouter NO recibió SubmitMarketOrder.
    // Assert: FakeTradingLogger registró el descarte en Debug.
}

[Fact]
public void ProcessBar_SiEstrategiaNoTieneCompatibilityRegistrada_PermiteOrden()
{
    // Arrange: classifier fake devuelve HighVolatility. La estrategia NO está en strategyCompatibilities.
    // Act: ProcessBar.
    // Assert: FakeOrderRouter recibió SubmitMarketOrder (fail-safe).
}

[Fact]
public void ProcessBar_SiRegistryDevuelveUnknown_PermiteOrden()
{
    // Arrange: registry sin clasificadores. Estrategia tiene CompatibleRegimes = [Trend].
    // Act: ProcessBar.
    // Assert: FakeOrderRouter recibió SubmitMarketOrder (fail-safe ante Unknown).
}

[Fact]
public void ProcessBar_FiltroDeRegimen_OcurreAntesDeChecksDeInvested()
{
    // Arrange: classifier devuelve HighVolatility. CompatibleRegimes = [Trend]. Portfolio NO invested.
    // Act: ProcessBar.
    // Assert: el filtro descarta antes de consultar IsInvested → FakePortfolioState NO recibe llamada a IsInvested
    //         (o si la recibe, el log de descarte por régimen está presente, lo que confirma orden).
}

[Fact]
public void ProcessBar_FiltroDeRegimen_NoSeEvaluaSiSenalEsFlat()
{
    // Arrange: FakeStrategy emite Flat. CompatibleRegimes = [Trend]. Registry devuelve HighVolatility.
    // Act: ProcessBar.
    // Assert: el registry NO recibe llamada a GetLastClassification (porque salimos en el check de Flat antes).
    //         Se valida con un FakeMarketRegimeRegistry que cuenta invocaciones.
}
```

Para estos tests va a hacer falta un `FakeMarketRegimeRegistry` que extienda `MarketRegimeRegistry` o un wrapper que capture llamadas. **Recomendación:** crear `FakeMarketRegimeRegistry` en `Trading.Application.Tests/Fakes/` con la misma API pública, configurable vía constructor con la clasificación a devolver, y contadores de llamadas. Si el patrón del repo usa más bien interfaces para mockear, considerar extraer un `IMarketRegimeRegistry` y mockearlo. Pero notar que el resto del código no usa esa interfaz, así que crear la interfaz solo para tests sería over-engineering. Mejor: usar la clase concreta con un classifier fake controlado.

---

## Validaciones de salida (a ejecutar por el usuario, NO por el asistente)

```bash
# Invariante arquitectónica: Domain y Application no conocen QuantConnect ni Accord.
grep -rn "^using QuantConnect" Trading.Domain/ Trading.Application/ Trading.Application.Tests/ Trading.Domain.Tests/
# Debe devolver vacío.

grep -rn "Accord\|HiddenMarkov" Trading.Domain/ Trading.Application/ Trading.Strategies/
# Debe devolver vacío en Paso 2. (Paso 3 introduce Accord en Trading.Strategies.)

# Build
dotnet build

# Tests
dotnet test
```

**Tests esperados después de Paso 2:**
- Todos los tests existentes siguen verdes (los del Paso 1 incluidos).
- Tests nuevos verdes: aproximadamente 30+ tests entre `RegimeLabelTests`, `RegimeClassificationTests`, `ConfigurableMarketRegimeClassifierTests`, `MarketRegimeRegistryTests`, `StrategyRegimeCompatibilityTests`, `BarProcessingServiceRegimeFilterTests`.

**Validación empírica del filtro (manual, opcional pero recomendada):**

1. Correr backtest con el código tal cual queda al final de Paso 2 (fake devuelve `Trend`, EmaCross declara `CompatibleRegimes: ["Trend"]`). Debe producir resultados **idénticos** al backtest pre-Paso-2 (mismo número de operaciones, mismo PnL).

2. Cambiar **temporalmente** en `TradingAlgorithmHost.Initialize()`:

   ```csharp
   var regimeClassifierBtc = new ConfigurableMarketRegimeClassifier(
       btcInstrumentId, RegimeLabel.HighVolatility, _clock);
   ```

   Recompilar y re-correr backtest. Debe producir **cero operaciones de entrada** (los time-exits sobre posiciones preexistentes pueden seguir ejecutándose si las hubiera, pero entradas nuevas: cero).

3. Revertir a `RegimeLabel.Trend` antes de commitear.

Esto es la prueba empírica de que el filtro filtra. Es opcional pero altamente recomendado: si el backtest con `HighVolatility` no produce cero operaciones, hay un bug en el wiring.

---

## Riesgos conocidos del Paso 2 y cómo el asistente debe manejarlos

1. **Path absoluto del `strategies.json` en `TradingAlgorithmHost`.** El archivo apunta a `F:\DesarrolloTrading\QuantConnect\Lean\Trading.Strategies\bin\Debug\net10.0\strategies.json`. **NO modificar este path** en este paso (INFRA-1 del Bloque 3 del ROADMAP lo resolverá después). Si Claude Code lo nota raro, ignorarlo: es deuda técnica conocida.

2. **El `strategies.json` puede estar en estado "limpio" (solo EmaCross en 1h) o tener otras estrategias mezcladas.** El brief asume el estado limpio que el usuario confirmó. Si Claude Code encuentra otras estrategias en el JSON, **agregar `CompatibleRegimes: ["Trend"]` SOLO a la entrada de EmaCrossStrategy con Symbol BTCUSDT** y dejar las demás sin tocar.

3. **Si Claude Code intenta extraer un `IMarketRegimeRegistry` o un `IBarProcessingService`** para facilitar tests, NO hacerlo. El patrón del repo es usar fakes concretos (`FakeOrderRouter` extiende la clase, no implementa una interfaz separada). Mantener consistencia con eso.

4. **Si Claude Code encuentra inconsistencias entre este brief y el código real** (un método con firma distinta, un archivo que no existe), detenerse y reportar. NO improvisar.

5. **Si la sesión de Claude Code crea un worktree** (Desktop app), el usuario ya pidió usar el CLI específicamente para evitar esto. Si por alguna razón aparece un worktree de todos modos, detener la sesión y reportar.

---

## Resumen del delta esperado en `git status`

Modificados:
- `Trading.Application/Execution/BarProcessingService.cs`
- `Trading.Strategies/TradingAlgorithmHost.cs`
- `Trading.Strategies/strategies.json` (y posiblemente `bin/Debug/net10.0/strategies.json` se regenera al compilar)
- Posiblemente algún test existente si la firma de `BarProcessingService` rompe construcciones de fixtures.

Creados:
- `Trading.Domain/Abstractions/Regimes/IMarketRegimeClassifier.cs`
- `Trading.Domain/Abstractions/Regimes/RegimeLabel.cs`
- `Trading.Domain/Abstractions/Regimes/RegimeClassification.cs`
- `Trading.Application/Regimes/MarketRegimeRegistry.cs`
- `Trading.Application/Regimes/ConfigurableMarketRegimeClassifier.cs`
- `Trading.Application/Regimes/StrategyRegimeCompatibility.cs`
- Tests asociados en `Trading.Domain.Tests/` y `Trading.Application.Tests/Regimes/`.
- `Trading.Application.Tests/Fakes/FakeMarketRegimeRegistry.cs` (si se decide ese camino) o equivalente.

**NO debe aparecer:**
- Cambios en `ROADMAP.md` ni `DECISIONS.md` (eso es Paso 3).
- Archivos con "Accord", "HMM", "HiddenMarkov" en el nombre.
- Cambios en `RiskOrchestrator.cs`.
- Worktrees ni ramas `claude/*`.
