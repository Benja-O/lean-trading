# OPS-2 — `StrategyHealthMonitor` (implementación runtime de `POLICY.md` sección 3)

> Componente autónomo en `Trading.Application` que consume `OrderFilledEvent` del bus, mantiene métricas rolling por estrategia, evalúa los umbrales U1-U4 de POLICY 3.1 y, al disparar, liquida la posición de la estrategia degradada vía `IOrderRouter.LiquidateInstrument` + marca a la estrategia como excluida. Sin tocar `RiskOrchestrator`.

---

## 1. Pre-requisitos

Antes de iniciar este brief, el repositorio debe estar en este estado:

- Commit limpio sobre la rama de trabajo actual (no crear ramas — ver §3).
- OPS-1 commiteado: `POLICY.md` presente en la raíz y `ADR-022` presente en `DECISIONS.md`.
- INFRA-2 commiteado: `Trading.Application/Health/HealthHeartbeatTracker.cs` existente y suscripto a `RiskLimitBreachedEvent`.
- Refactor #4 commiteado: `IRiskMonitor`, `IRiskAction`, `RiskOrchestrator`, `LiquidateAllRiskAction`, `CoolingOffTracker`, `DrawdownMonitor`, `ConsecutiveLossesMonitor` existentes.
- `dotnet build Trading.Strategies/Trading.Strategies.csproj` verde.
- `dotnet test Trading.Domain.Tests/Trading.Domain.Tests.csproj` verde.
- `dotnet test Trading.Application.Tests/Trading.Application.Tests.csproj` verde.

Si alguna no se cumple → **detenerse y reportar al operador antes de tocar nada**.

---

## 2. Reglas operativas inquebrantables

Este brief opera bajo las reglas de la sección **"Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio explícito de los puntos críticos:

- **Prohibido cualquier comando `git`** (`checkout`, `branch`, `stash`, `commit`, `add`, `reset`, etc.). Trabajar sobre la rama checked-out actual sin cambiarla. Solo `git status`, `git log`, `git diff` si son estrictamente necesarios para diagnóstico, reportando el output al operador.
- **Prohibido `dotnet build` sobre `QuantConnect.Lean.sln`.** Solo sobre `.csproj` específicos de `Trading.*`.
- **Prohibido `dotnet test` sobre `QuantConnect.Lean.sln`.** Solo sobre `.csproj` específicos de `Trading.*.Tests`.
- **Prohibidos `dotnet clean`, `dotnet publish`, `dotnet pack`** y runners alternativos (`vstest.console`, `xunit.console`).
- **No modificar tests existentes** salvo los listados explícitamente en §6 como "tests existentes a actualizar". Si un test no listado falla por un cambio del brief, **detenerse y reportar**.
- **No crear archivos `.md` versionados** más allá de los listados en §6.

Ambigüedades no listadas en este brief → **detenerse y reportar**, no improvisar.

---

## 3. Contexto y motivación

POLICY sección 3 define cuatro umbrales por estrategia (U1-U4) que, al cruzarse, exigen liquidación inmediata de la posición de esa estrategia y exclusión del flujo de generación de señales. Hoy los umbrales solo viven como texto en `POLICY.md`. OPS-2 los implementa runtime: un componente que consume `OrderFilledEvent` del bus, mantiene métricas rolling por estrategia, dispara liquidación dirigida y excluye a la estrategia degradada.

Sin OPS-2, paper trading (Hito C) arranca con la mitad de POLICY armada: el kill global por DD sistémico funciona (refactor #4), pero la detección de degradación por estrategia individual queda librada a inspección humana semanal. POLICY 1 (P4) dice que cuando monitor y operador disienten gana el monitor; sin monitor automatizado eso no es enforce-able.

### Arquitectura elegida (cerrada en chat — no se reabre)

**`StrategyHealthMonitor` NO implementa `IRiskMonitor`.** Es un componente autónomo en `Trading.Application/Health/`. Razones documentadas en ADR-023 (a crear como parte de este brief):

- `IRiskMonitor` está diseñado para condiciones de **kill switch global** (`MaximumDrawdownExceeded`, `ConsecutiveLossesExceeded`, `Manual`). Activar el orchestrator significa `LiquidateAll` + cooling-off compartido de 24h. La degradación de una estrategia no debe parar el sistema entero ni meterlo en cooling-off.
- Hay precedente: ADR-017 ya estableció que el filtro de régimen no va por `IRiskMonitor` por la misma razón conceptual ("rechazar señal específica" ≠ "liquidar todo"). OPS-2 está en la misma categoría.
- Reusa `IOrderRouter.LiquidateInstrument(InstrumentId, OrderPurpose, executorIdentifier)` que ya existe (verificado en código). No hace falta nueva `IRiskAction` ni tocar `RiskOrchestrator`.
- El monitor publica `RiskLimitBreachedEvent` con razón `StrategyDegradation` al bus. `HealthHeartbeatTracker` ya está suscripto a ese evento y captura razón + timestamp. Sin código nuevo en el heartbeat.

### Decisiones cerradas en chat

1. **Alcance medio sin recorte.** Los cuatro umbrales U1-U4 entran en este refactor.
2. **Sin persistencia entre reinicios.** Métricas en memoria. Si el proceso reinicia, se pierde historial reciente y el monitor entra en warm-up. Deuda conocida (ADR-014). Se reabre como ADR propio antes de live serio.
3. **Sin reconfiguración en caliente.** Los U1-U4 se inyectan desde un POCO de configuración construido en `TradingAlgorithmHost` con los literales de POLICY 3.1. Recompilar si POLICY cambia: feature, no bug (POLICY 6.2).
4. **Dos niveles de semáforo** (OK / Apagar). Sin "reducir size". ADR-022 D1.
5. **Calibración absoluta** (25%, 15%/5d, 1.0/10t, 0/10t, 50 trades arman U3-U4). ADR-022 D2.
6. **Liquidación inmediata a mercado** al disparar. ADR-022 D3.
7. **Liquidación dirigida** vía `IOrderRouter.LiquidateInstrument(instrumentId, OrderPurpose.TimeExit, executorIdentifier)`. La interfaz ya soporta esto.
8. **División en Pieza A y Pieza B** con detención obligatoria entre ambas. Sin riesgo de mezclar errores.
9. **`OrderFilledEvent.ExecutorIdentifier` como clave del monitor.** Verificado en código: `StrategyExecutor.ExecutorIdentifier = $"{definition.StrategyName}_{instrumentId.Ticker}_{timeframe}"` — estable, único, determinista. Hipótesis confirmada antes de empezar.

### Fuera de alcance (no scope-creep)

- Persistencia del estado del monitor entre reinicios.
- Lectura de umbrales desde `POLICY.md` o JSON.
- Métricas adicionales no listadas en POLICY 3 (Sharpe, Sortino, Calmar).
- Notificación externa más allá de log `Critical` + `RiskLimitBreachedEvent` (que el heartbeat ya consume).
- Escalón intermedio "reducir size".
- Modificación de `strategies.json` en runtime para excluir la estrategia. La exclusión efectiva al reiniciar el proceso es operación manual del operador post-incidente (POLICY 3.1). Durante la sesión activa, el flag interno del monitor + el guard nuevo en `BarProcessingService` cumplen la función.
- Modificación de `RiskOrchestrator`, `IRiskMonitor`, `IRiskAction`, `RiskLimitBreachedEvent`. Permanecen intactos.
- Adaptación de monitors futuros (`RegimeIncompatibilityMonitor`, `EventCalendarMonitor`) al patrón nuevo.

---

## 4. Decisiones técnicas aplicadas (no se discuten, se aplican)

| ID | Decisión | Valor |
|---|---|---|
| D-T1 | Tipo numérico | `decimal` para precios, P&L, equity. `int` para contadores. Prohibido `double`/`float`. |
| D-T2 | Acceso al tiempo en `Trading.Application` | `IClock.UtcNow` exclusivamente. Wall clock real (`DateTime.UtcNow`) NO se usa dentro de `Trading.Application`. La cadencia diaria de U2 se ata al `IClock.UtcNow.Date` que avanza con cada `OrderFilledEvent` recibido. Esto funciona en backtest (días simulados disparan evaluaciones diarias deterministas) y funciona en live (días reales disparan evaluaciones diarias reales, porque `IClock` en live = wall clock real). |
| D-T3 | Identificador de estrategia | `OrderFilledEvent.ExecutorIdentifier` (string). Verificado: emitido por `OrderLifecycleService` con valor `$"{StrategyName}_{Ticker}_{Timeframe}"`. |
| D-T4 | "Trade" según POLICY 3.2 | Ciclo Entry → cierre (SL/TP/TimeExit). El monitor mantiene `OpenPosition?` por estrategia. Una posición abierta a la vez por estrategia. Si llega `Entry` con posición ya abierta para esa estrategia → `DomainException`. Si llega cierre sin posición previa → `DomainException`. |
| D-T5 | Cálculo de P&L realizado | `realizedPnl = (exitPrice − entryPrice) × \|fillQuantity\| × directionSign`. `directionSign` = +1 si `OpenPosition.EntryQuantity > 0` (Long), −1 si `< 0` (Short). **Verificado en código:** `OrderLifecycleService.HandleEntryFill` usa el signo de `fillQuantity` como dirección. El cierre llega con `FillQuantity` del fill del SL/TP/TimeExit (signo opuesto al entry); el monitor solo necesita los precios y la magnitud, no el signo del cierre. Sin comisiones modeladas en el P&L del monitor (vienen reflejadas implícitamente en el equity del portfolio en otro nivel; el monitor opera sobre P&L bruto de fills). |
| D-T6 | Ventanas rolling | U3 y U4 sobre los últimos 30 trades cerrados. U2 sobre los últimos 30 días de `IClock.UtcNow.Date`. U1 sin ventana (ATH desde el primer trade en vivo). |
| D-T7 | Estructura para trades cerrados | `LinkedList<ClosedTrade>` por estrategia. `AddLast` al cerrar, `RemoveFirst` si `Count > 30`. |
| D-T8 | Estructura para serie diaria | `LinkedList<DailyEquityPoint>` por estrategia. Cada punto: `(DateOnly Date, decimal EquityAtEndOfDay)`. Se agrega cuando cambia el día de `IClock.UtcNow.Date`. `RemoveFirst` si `Count > 30`. |
| D-T9 | Flag "degradada" | `Dictionary<string, bool>` por estrategia. Una vez `true`, el monitor descarta todo `OrderFilledEvent` posterior de esa estrategia y `IsExcluded(executorIdentifier)` retorna `true`. **No hay reactivación runtime**: la única reactivación es reinicio manual del proceso (POLICY 3.1). |
| D-T10 | Profit factor indefinido | Si `gross_loss == 0` sobre los últimos 30 trades → U3 "no evaluable" (skip). El contador "sostenido N trades" se resetea a 0. POLICY 3.2 lo describe explícitamente. |
| D-T11 | Acción al disparar | El monitor: (1) llama `_orderRouter.LiquidateInstrument(instrumentId, OrderPurpose.TimeExit, executorIdentifier)` SI hay posición abierta para esa estrategia en el instante del breach. (2) Setea el flag `degraded[executorIdentifier] = true`. (3) Publica `RiskLimitBreachedEvent` con `Reason = StrategyDegradation` y `Description` que incluye `executorIdentifier`, umbral disparado, valor de la métrica. (4) Loguea `Critical` con placeholders. **Si no hay posición abierta en el instante del breach** (caso: el último cierre fue el que disparó U1/U2/U3/U4): paso (1) se saltea silenciosamente, paso (2) y (3) y (4) se ejecutan igual. Esto es deliberado: el flag `degraded` previene futuros entries; el evento al bus permite al heartbeat reflejar el estado. Loguear `Information` "{ExecutorIdentifier} degradada sin posición abierta a liquidar" cuando se da este caso, para que el JSONL deje rastro. |
| D-T12 | Guard en `BarProcessingService` | Nueva consulta antes de generar señal: si `_strategyHealthMonitor.IsExcluded(executorIdentifier)` → `continue`. Se inserta **después** del guard `IsKillSwitchActivated` y **antes** del filtro de régimen. Patrón idéntico al filtro de régimen (ADR-017): guard `continue` en `BarProcessingService`, no `IRiskMonitor`. |
| D-T13 | Contrato del guard | `BarProcessingService` recibe el monitor como `IStrategyHealthMonitor` (interfaz nueva en `Trading.Domain.Abstractions`) con un solo método: `bool IsExcluded(string executorIdentifier)`. La interfaz vive en Domain por la misma razón que `IMarketRegimeClassifier` vive ahí: es un contrato consumido por Application sin acoplar a la implementación concreta. La implementación concreta `StrategyHealthMonitor` vive en `Trading.Application/Health/`. |
| D-T14 | Suscripción al bus | `StrategyHealthMonitor` se suscribe a `OrderFilledEvent` en su constructor, mismo patrón que `HealthHeartbeatTracker`. El handler es síncrono. El bus garantiza serialización (verificado en `DomainEventBus`: invoca suscriptores en orden, mismo thread). |
| D-T15 | Thread safety | Lock interno (`object _lock`) protegiendo todos los diccionarios de estado y el método `IsExcluded`. Patrón idéntico al de `HealthHeartbeatTracker`. |
| D-T16 | Logging | `ITradingLogger` inyectado (consistente con el resto de Application — verificado: ni `DrawdownMonitor` ni `ConsecutiveLossesMonitor` usan `ILogger<T>`; el proyecto usa `ITradingLogger`). Niveles: `Information` cuando una estrategia cruza 50 trades y se arman U3-U4; `Critical` cuando dispara. Placeholders nombrados, sin interpolación. |
| D-T17 | Ubicación de archivos | Domain: `Trading.Domain/Abstractions/IStrategyHealthMonitor.cs`. Application: `Trading.Application/Health/StrategyHealthMonitor.cs`, `Trading.Application/Health/StrategyHealthThresholds.cs`. Tests: `Trading.Application.Tests/Health/StrategyHealthMonitorTests.cs`. |
| D-T18 | Resolución de `InstrumentId` para liquidar | El monitor mantiene en `OpenPosition` el `InstrumentId` que vino en el `OrderFilledEvent` del Entry. Al disparar, usa ese `InstrumentId` para llamar `LiquidateInstrument`. No depende de portfolio externo ni de lookup adicional. |

---

## 5. Riesgos conocidos y cómo manejarlos

| Riesgo | Acción de Claude Code |
|---|---|
| `OrderFilledEvent.ExecutorIdentifier` resulta no ser estable (algo cambió desde la verificación). | **Detenerse y reportar.** No improvisar otra clave. |
| Llega un segundo `Entry` sin cierre previo para la misma estrategia. | `DomainException` (D-T4). Sistema fail-loud. |
| Llega un `StopLoss/TakeProfit/TimeExit` sin posición previa abierta para esa estrategia. | `DomainException` (D-T4). Sistema fail-loud. |
| Tests existentes (no listados en §6 como "a actualizar") fallan tras los cambios. | Reportar el listado completo de fallos antes de tocarlos. La modificación esperada de tests existentes es solo en `BarProcessingService` y sus tests (por el guard nuevo). Cualquier otro fallo no es esperado. |
| `BarProcessingService` ya tiene una firma muy cargada y agregarle otro parámetro la complica. | El constructor ya recibe 9 parámetros. Agregar `IStrategyHealthMonitor` lo lleva a 10. Aceptable; no refactorizar la firma a un objeto contenedor en este brief (scope creep). |
| El backtest tras OPS-2 produce número distinto de órdenes que el baseline (~225 según INFRA-2). | **Detenerse y reportar.** El monitor está activo pero `EmaCrossStrategy` en el backtest no supera 50 trades (U3-U4 no se arman) y no llega a 25% de DD desde ATH ni 15% rolling 30 días sostenido 5 días (U1-U2 no disparan). Si el backtest cambia, hay un bug en la lógica o en la suscripción al bus. |
| `IPortfolioState` no expone "posición abierta de la estrategia X". | No se necesita: el monitor tiene su propio tracking de `OpenPosition` desde los fills. La liquidación se hace vía `IOrderRouter.LiquidateInstrument` que solo necesita `InstrumentId` + `executorIdentifier`. |
| El handler del bus lanza excepción y el bus la captura silenciosamente. | El bus actual (`DomainEventBus`) ya captura y loguea excepciones de suscriptores y continúa. Es el comportamiento deseado: una métrica que falla no debe interrumpir el flujo de trading. Si esto preocupa, el log queda en el JSONL — visible en revisión diaria. |

Ambigüedades fuera de esta tabla → **detenerse y reportar**.

---

## 6. Alcance detallado por piezas

División en **Pieza A** (cableado mínimo y guard en `BarProcessingService`) y **Pieza B** (monitor completo). Detención obligatoria entre ambas.

---

### Pieza A — Cableado: `IStrategyHealthMonitor`, `RiskLimitBreachReason.StrategyDegradation`, guard en `BarProcessingService`

**Objetivo:** dejar el sistema preparado para que el monitor de Pieza B se enchufe sin tocar nada existente cuando llegue. Al finalizar Pieza A el sistema sigue comportándose idéntico al pre-OPS-2.

#### A.1 — Extender `RiskLimitBreachReason`

**Archivo:** `Trading.Domain/Events/RiskLimitBreachedEvent.cs` (existente).

Agregar al enum el valor `StrategyDegradation` al final, después de `RegimeIncompatibility`:

```csharp
/// <summary>
/// Una estrategia individual cruzó alguno de los umbrales U1-U4 de POLICY sección 3.
/// NO activa el kill switch global: solo liquida la posición de la estrategia y la
/// excluye de generación de señales hasta reinicio manual del proceso.
/// Emitido por StrategyHealthMonitor (OPS-2).
/// </summary>
StrategyDegradation
```

No reordenar valores existentes.

#### A.2 — Crear `IStrategyHealthMonitor`

**Archivo nuevo:** `Trading.Domain/Abstractions/IStrategyHealthMonitor.cs`.

```csharp
namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato del monitor de salud por estrategia. La implementación mantiene métricas
    /// rolling por estrategia y, ante degradación, excluye a esa estrategia del flujo de
    /// generación de señales. Consultado por BarProcessingService como guard pre-orden.
    ///
    /// NO es un IRiskMonitor: la degradación de una estrategia NO activa el kill switch
    /// global. Ver ADR-023.
    /// </summary>
    public interface IStrategyHealthMonitor
    {
        /// <summary>
        /// Indica si la estrategia identificada por executorIdentifier está excluida
        /// (degradada). Si retorna true, BarProcessingService descarta señales de esa
        /// estrategia hasta reinicio manual del proceso.
        ///
        /// Para identificadores desconocidos retorna false (estrategia nueva o sin
        /// historia: no excluida).
        /// </summary>
        bool IsExcluded(string executorIdentifier);
    }
}
```

#### A.3 — Adaptar `BarProcessingService` con el guard nuevo

**Archivo:** `Trading.Application/Execution/BarProcessingService.cs` (existente — verificar nombre y ruta exactos al abrirlo; lo importante es que es el servicio que itera estrategias por barra y consulta `_riskOrchestrator.IsKillSwitchActivated`).

Cambios:

1. Constructor recibe un parámetro nuevo `IStrategyHealthMonitor strategyHealthMonitor` y lo guarda en `_strategyHealthMonitor`.
2. Validar `ArgumentNullException` consistente con los otros parámetros del constructor.
3. En el loop principal, después del guard `if (_riskOrchestrator.IsKillSwitchActivated) continue;` (línea ~202 del archivo actual) y **antes** del cálculo de `signalDirection`, insertar:

```csharp
if (_strategyHealthMonitor.IsExcluded(strategyExecutor.ExecutorIdentifier))
{
    continue;
}
```

Sin logging acá (el log Critical ya se emitió en el momento del breach por el monitor; loguear en cada barra subsiguiente que la estrategia está excluida sería ruido).

#### A.4 — Actualizar tests existentes de `BarProcessingService`

**Archivos:**
- `Trading.Application.Tests/Execution/BarProcessingServiceBarProcessedEventTests.cs` (verificado en `TodoMiCodigo.txt` línea 1557).
- `Trading.Application.Tests/Execution/BarProcessingServiceRegimeFilterTests.cs` (verificado en `TodoMiCodigo.txt` línea 2750).

En el método helper `BuildService(...)` (existe uno en cada archivo) agregar a la construcción de `BarProcessingService` un `FakeStrategyHealthMonitor` con comportamiento por defecto "nunca excluye". Mantener todos los tests existentes en verde sin cambiar sus aserciones.

#### A.5 — Crear `FakeStrategyHealthMonitor`

**Archivo nuevo:** `Trading.Application.Tests/Fakes/FakeStrategyHealthMonitor.cs`.

```csharp
using System.Collections.Generic;
using Trading.Domain.Abstractions;

namespace Trading.Application.Tests.Fakes
{
    /// <summary>
    /// Fake del monitor de salud por estrategia. Por defecto no excluye nada.
    /// Tests pueden agregar identifiers al HashSet ExcludedIdentifiers para forzar
    /// la exclusión de estrategias específicas.
    /// </summary>
    internal sealed class FakeStrategyHealthMonitor : IStrategyHealthMonitor
    {
        public HashSet<string> ExcludedIdentifiers { get; } = new();

        public bool IsExcluded(string executorIdentifier)
            => ExcludedIdentifiers.Contains(executorIdentifier);
    }
}
```

#### A.6 — Tests nuevos del guard en `BarProcessingService`

**Archivo:** `Trading.Application.Tests/Execution/BarProcessingServiceBarProcessedEventTests.cs` (extender el existente, no crear archivo nuevo).

Agregar **2 tests nuevos**:

1. `Process_StrategyExcluidaPorHealthMonitor_NoGeneraSignal_NiEmiteBarProcessedEvent`: configurar `FakeStrategyHealthMonitor.ExcludedIdentifiers.Add(strategyExecutor.ExecutorIdentifier)`; pasar una barra normal; verificar que no se emite ninguna orden ni se llama a `EvaluateSignal` de la estrategia.
2. `Process_StrategyNoExcluida_PathHabitual_GeneraSignalNormal`: con `FakeStrategyHealthMonitor.ExcludedIdentifiers` vacío; verificar que el flujo se ejecuta como antes (smoke test del no-impact).

#### A.7 — Wiring stub en `TradingAlgorithmHost`

**Archivo:** `Trading.Strategies/TradingAlgorithmHost.cs` (existente — verificado en `TodoMiCodigo.txt` línea 5874).

En esta pieza A todavía NO existe `StrategyHealthMonitor` (es Pieza B). Para no romper el constructor de `BarProcessingService`, **se introduce una implementación trivial inline** en `Trading.Application.Health/`:

**Archivo nuevo:** `Trading.Application/Health/NullStrategyHealthMonitor.cs`.

```csharp
using Trading.Domain.Abstractions;

namespace Trading.Application.Health
{
    /// <summary>
    /// Implementación pasiva de IStrategyHealthMonitor. Nunca excluye. Existe para usar
    /// como placeholder durante el wiring de Pieza A (antes de que StrategyHealthMonitor
    /// real esté disponible en Pieza B) y como fallback testeable.
    ///
    /// En el wiring final de Pieza B, este Null se reemplaza por StrategyHealthMonitor real.
    /// </summary>
    public sealed class NullStrategyHealthMonitor : IStrategyHealthMonitor
    {
        public bool IsExcluded(string executorIdentifier) => false;
    }
}
```

En `TradingAlgorithmHost`, en el bloque "Servicios de Application" (cerca de la línea 5961), construir `var nullHealthMonitor = new NullStrategyHealthMonitor();` y pasarlo a la construcción de `BarProcessingService`. Mantener el orden de construcción existente — el monitor real lo reemplaza en B.7.

#### Criterio de aceptación de Pieza A (obligatorio antes de Pieza B)

- `dotnet build Trading.Strategies/Trading.Strategies.csproj` → verde.
- `dotnet test Trading.Domain.Tests/Trading.Domain.Tests.csproj` → verde, sin cambios en cantidad de tests (esperado: el cambio del enum no agrega tests; los tests existentes de `RegimeLabelTests`, `RegimeClassificationTests`, etc., siguen pasando).
- `dotnet test Trading.Application.Tests/Trading.Application.Tests.csproj` → verde con +2 tests nuevos (los del guard en `BarProcessingService`).
- Operador corre backtest manualmente → mismas ~225 órdenes (verificado en INFRA-2 baseline). El `NullStrategyHealthMonitor` nunca excluye, así que el comportamiento es idéntico.

**Detención obligatoria.** Reportar al operador:
- Archivos creados: `IStrategyHealthMonitor.cs`, `NullStrategyHealthMonitor.cs`, `FakeStrategyHealthMonitor.cs`.
- Archivos modificados: `RiskLimitBreachedEvent.cs` (+1 valor enum), `BarProcessingService.cs` (constructor + guard), `TradingAlgorithmHost.cs` (wiring), `BarProcessingServiceBarProcessedEventTests.cs` (+2 tests + builder helper), `BarProcessingServiceRegimeFilterTests.cs` (builder helper).
- Conteo de tests y resultado del backtest.
- **Esperar confirmación explícita** del operador antes de iniciar Pieza B.

---

### Pieza B — `StrategyHealthMonitor` completo

**Objetivo:** implementar el monitor que consume `OrderFilledEvent`, mantiene métricas por estrategia, evalúa U1-U4 y dispara liquidación + exclusión cuando corresponde. Reemplaza el `NullStrategyHealthMonitor` en el wiring.

#### B.1 — POCO `StrategyHealthThresholds`

**Archivo nuevo:** `Trading.Application/Health/StrategyHealthThresholds.cs`.

```csharp
namespace Trading.Application.Health
{
    /// <summary>
    /// Umbrales numéricos por estrategia, derivados literalmente de POLICY sección 3.1.
    /// Inmutable. Sin reconfiguración runtime. Si POLICY cambia, recompilación es la única vía.
    /// Documentado en ADR-023.
    /// </summary>
    public sealed class StrategyHealthThresholds
    {
        public decimal AbsoluteDrawdownFromAthFraction { get; }       // U1: 0.25m
        public decimal RollingDrawdownThirtyDaysFraction { get; }     // U2: 0.15m
        public int    RollingDrawdownSustainedDays { get; }           // U2: 5
        public decimal RollingProfitFactorThreshold { get; }          // U3: 1.0m
        public int    RollingProfitFactorSustainedTrades { get; }     // U3: 10
        public decimal RollingExpectancyThreshold { get; }            // U4: 0m
        public int    RollingExpectancySustainedTrades { get; }       // U4: 10
        public int    MinimumTradesToArmRollingThresholds { get; }    // U3/U4 arming: 50
        public int    RollingWindowTrades { get; }                    // 30
        public int    RollingWindowDays { get; }                      // 30

        public StrategyHealthThresholds(
            decimal absoluteDrawdownFromAthFraction,
            decimal rollingDrawdownThirtyDaysFraction,
            int rollingDrawdownSustainedDays,
            decimal rollingProfitFactorThreshold,
            int rollingProfitFactorSustainedTrades,
            decimal rollingExpectancyThreshold,
            int rollingExpectancySustainedTrades,
            int minimumTradesToArmRollingThresholds,
            int rollingWindowTrades,
            int rollingWindowDays)
        {
            // Validar todos > 0 salvo expectancyThreshold (puede ser 0). Lanzar
            // InvalidRiskParametersException (o equivalente del proyecto — verificar
            // qué excepción usa RiskParameters al validar). Mensaje con campo + valor recibido.
            // ...
            AbsoluteDrawdownFromAthFraction = absoluteDrawdownFromAthFraction;
            // ... resto de asignaciones
        }

        /// <summary>
        /// Factory con los defaults literales de POLICY 3.1 al momento de OPS-2.
        /// Cualquier cambio a POLICY 3.1 exige actualizar estos defaults + ADR.
        /// </summary>
        public static StrategyHealthThresholds FromPolicyDefaults() =>
            new(
                absoluteDrawdownFromAthFraction: 0.25m,
                rollingDrawdownThirtyDaysFraction: 0.15m,
                rollingDrawdownSustainedDays: 5,
                rollingProfitFactorThreshold: 1.0m,
                rollingProfitFactorSustainedTrades: 10,
                rollingExpectancyThreshold: 0m,
                rollingExpectancySustainedTrades: 10,
                minimumTradesToArmRollingThresholds: 50,
                rollingWindowTrades: 30,
                rollingWindowDays: 30);
    }
}
```

(Verificar al implementar qué excepción usa el proyecto para invariantes de configuración. `RiskParameters` lanza `InvalidRiskParametersException` — si no aplica acá, usar `ArgumentException` con mensaje descriptivo. NO usar `Exception` plano ni `ApplicationException` — prohibidos por `AI.md`.)

#### B.2 — `StrategyHealthMonitor`

**Archivo nuevo:** `Trading.Application/Health/StrategyHealthMonitor.cs`.

**Dependencias inyectadas:**
- `StrategyHealthThresholds thresholds`.
- `IClock clock`.
- `IOrderRouter orderRouter` (para `LiquidateInstrument` al disparar).
- `ITradingLogger logger`.
- `IDomainEventBus eventBus` (para suscribir + publicar).

**Estado interno (todos en `Dictionary<string, T>` indexados por `executorIdentifier`):**
- `Dictionary<string, OpenPosition?> _openPositions`.
- `Dictionary<string, LinkedList<ClosedTrade>> _closedTrades`.
- `Dictionary<string, decimal> _equity` (P&L realizado acumulado).
- `Dictionary<string, decimal> _ath` (high-water mark del equity).
- `Dictionary<string, LinkedList<DailyEquityPoint>> _dailyEquity`.
- `Dictionary<string, DateOnly?> _lastEvaluatedDay` (último día evaluado para U2).
- `Dictionary<string, int> _u2SustainedDaysCounter`.
- `Dictionary<string, int> _u3SustainedTradesCounter`.
- `Dictionary<string, int> _u4SustainedTradesCounter`.
- `Dictionary<string, bool> _degraded`.
- `Dictionary<string, bool> _rollingThresholdsArmed` (flag para loguear una sola vez el armado).
- `Dictionary<string, int> _totalClosedTrades` (para chequear el armado de U3/U4 al cruzar 50).

Todos bajo un solo `object _lock`. `IsExcluded(string)` toma el lock para lectura.

**Tipos auxiliares (en el mismo archivo, `private` o `internal`):**

```csharp
private sealed record OpenPosition(
    decimal EntryPrice,
    decimal EntryQuantity,   // con signo: + para Long, - para Short
    InstrumentId InstrumentId,
    DateTime EntryTimeUtc);

private sealed record ClosedTrade(
    decimal RealizedPnl,
    DateTime ClosedAtUtc);

private sealed record DailyEquityPoint(
    DateOnly Date,
    decimal EquityAtEndOfDay);
```

**Constructor:**

```csharp
public StrategyHealthMonitor(
    StrategyHealthThresholds thresholds,
    IClock clock,
    IOrderRouter orderRouter,
    ITradingLogger logger,
    IDomainEventBus eventBus)
{
    _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
    _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    _orderRouter = orderRouter ?? throw new ArgumentNullException(nameof(orderRouter));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

    eventBus.Subscribe<OrderFilledEvent>(OnOrderFilled);
}
```

**Implementación de `IStrategyHealthMonitor.IsExcluded`:**

```csharp
public bool IsExcluded(string executorIdentifier)
{
    if (string.IsNullOrEmpty(executorIdentifier)) return false;
    lock (_lock)
    {
        return _degraded.TryGetValue(executorIdentifier, out var d) && d;
    }
}
```

**Handler `OnOrderFilled(OrderFilledEvent e)`:**

```csharp
private void OnOrderFilled(OrderFilledEvent e)
{
    lock (_lock)
    {
        var id = e.ExecutorIdentifier;

        // Estrategia ya degradada: descartar todo. El motor de trading no debería
        // emitir órdenes nuevas de una estrategia excluida (el guard de BarProcessingService
        // lo previene), pero las órdenes de cierre que ya estaban en vuelo pueden seguir
        // llegando. Las ignoramos en el monitor.
        if (_degraded.TryGetValue(id, out var isDegraded) && isDegraded) return;

        EnsureBuckets(id);

        switch (e.Purpose)
        {
            case OrderPurpose.Entry:
                if (_openPositions[id] is not null)
                {
                    throw new DomainException(
                        $"OPS-2 invariante violado: Entry de '{id}' con posición ya abierta.");
                }
                _openPositions[id] = new OpenPosition(
                    EntryPrice: e.FillPrice,
                    EntryQuantity: e.FillQuantity,
                    InstrumentId: e.InstrumentId,
                    EntryTimeUtc: e.TimestampUtc);
                return;

            case OrderPurpose.StopLoss:
            case OrderPurpose.TakeProfit:
            case OrderPurpose.TimeExit:
                ProcessTradeClose(id, e);
                break;

            default:
                // Defensive: si aparece un OrderPurpose nuevo no manejado, fail loud.
                throw new DomainException(
                    $"OPS-2 invariante violado: OrderPurpose '{e.Purpose}' no manejado.");
        }
    }
}
```

**`ProcessTradeClose(string id, OrderFilledEvent e)`:**

1. `var open = _openPositions[id] ?? throw new DomainException("OPS-2 invariante violado: cierre de '{id}' sin posición previa.");`
2. Calcular `directionSign = Math.Sign(open.EntryQuantity)` (es `1` o `-1`; jamás `0` porque `Entry` con `quantity=0` no debería existir; si llega → `DomainException`).
3. `var realizedPnl = (e.FillPrice - open.EntryPrice) * Math.Abs(open.EntryQuantity) * directionSign;`
4. Cerrar: `_openPositions[id] = null;`
5. `var closed = new ClosedTrade(realizedPnl, e.TimestampUtc); _closedTrades[id].AddLast(closed); if (_closedTrades[id].Count > _thresholds.RollingWindowTrades) _closedTrades[id].RemoveFirst();`
6. Actualizar equity: `_equity[id] += realizedPnl; if (_equity[id] > _ath[id]) _ath[id] = _equity[id];`
7. `_totalClosedTrades[id]++;`
8. Manejar avance de día y registro diario:
    - `var currentDay = DateOnly.FromDateTime(_clock.UtcNow);`
    - Si `_lastEvaluatedDay[id]` es `null` o `currentDay > _lastEvaluatedDay[id]`:
        - Agregar nuevo `DailyEquityPoint(currentDay, _equity[id])` al `_dailyEquity[id]` (si ya hay un punto del mismo día por re-procesamiento — no debería pasar — reemplazar; en la práctica el flujo solo agrega uno por día).
        - Si `_dailyEquity[id].Count > _thresholds.RollingWindowDays` → `RemoveFirst`.
        - Evaluar U2 (ver abajo).
        - `_lastEvaluatedDay[id] = currentDay;`
    - Si el día no avanzó (varios trades el mismo día): NO se agrega nuevo punto diario, NO se reevalúa U2 (la semántica de U2 es "evaluación diaria al cierre del día", POLICY 3.2).
9. Evaluar U1 (siempre, desde el primer trade).
10. Si `_totalClosedTrades[id]` cruza el umbral de armado y aún no estaban armados → loguear `Information` "U3 y U4 armados para {ExecutorIdentifier} tras alcanzar {Trades} trades acumulados." Setear flag.
11. Si los rolling están armados y hay al menos `RollingWindowTrades` cerrados: evaluar U3 y U4.
12. Si alguna evaluación disparó un breach → ejecutar `TriggerDegradation(id, breachKey, breachValue)` y `return` (no evaluar más umbrales en el mismo evento; el primero que dispara gana).

**Evaluadores (privados, retornan `(bool triggered, string description)`):**

```csharp
// U1: DD absoluto desde ATH > 25%
private (bool, string) EvaluateU1(string id)
{
    if (_ath[id] <= 0m) return (false, string.Empty);
    var dd = (_ath[id] - _equity[id]) / _ath[id];
    if (dd > _thresholds.AbsoluteDrawdownFromAthFraction)
        return (true, $"U1: DD absoluto {dd:P2} > umbral {_thresholds.AbsoluteDrawdownFromAthFraction:P2}");
    return (false, string.Empty);
}

// U2: DD rolling 30 días > 15% sostenido 5 días
private (bool, string) EvaluateU2(string id)
{
    var dailySeries = _dailyEquity[id];
    if (dailySeries.Count < 2) return (false, string.Empty);
    var maxInWindow = dailySeries.Max(p => p.EquityAtEndOfDay);
    var lastInWindow = dailySeries.Last!.Value.EquityAtEndOfDay;
    if (maxInWindow <= 0m) { _u2SustainedDaysCounter[id] = 0; return (false, string.Empty); }
    var ddRolling = (maxInWindow - lastInWindow) / maxInWindow;
    if (ddRolling > _thresholds.RollingDrawdownThirtyDaysFraction)
    {
        _u2SustainedDaysCounter[id]++;
        if (_u2SustainedDaysCounter[id] >= _thresholds.RollingDrawdownSustainedDays)
            return (true, $"U2: DD rolling 30d {ddRolling:P2} sostenido {_u2SustainedDaysCounter[id]} días.");
    }
    else
    {
        _u2SustainedDaysCounter[id] = 0;
    }
    return (false, string.Empty);
}

// U3: PF rolling 30 trades < 1.0 sostenido 10 trades
private (bool, string) EvaluateU3(string id)
{
    var trades = _closedTrades[id];
    if (trades.Count < _thresholds.RollingWindowTrades) return (false, string.Empty);
    decimal grossProfit = 0m, grossLoss = 0m;
    foreach (var t in trades)
    {
        if (t.RealizedPnl > 0) grossProfit += t.RealizedPnl;
        else if (t.RealizedPnl < 0) grossLoss += -t.RealizedPnl;
    }
    if (grossLoss == 0m)
    {
        _u3SustainedTradesCounter[id] = 0;   // PF indefinido → resetea
        return (false, string.Empty);
    }
    var pf = grossProfit / grossLoss;
    if (pf < _thresholds.RollingProfitFactorThreshold)
    {
        _u3SustainedTradesCounter[id]++;
        if (_u3SustainedTradesCounter[id] >= _thresholds.RollingProfitFactorSustainedTrades)
            return (true, $"U3: PF rolling {pf:F2} sostenido {_u3SustainedTradesCounter[id]} trades.");
    }
    else
    {
        _u3SustainedTradesCounter[id] = 0;
    }
    return (false, string.Empty);
}

// U4: expectancy rolling 30 trades < 0 sostenido 10 trades
private (bool, string) EvaluateU4(string id)
{
    var trades = _closedTrades[id];
    if (trades.Count < _thresholds.RollingWindowTrades) return (false, string.Empty);
    var wins = trades.Where(t => t.RealizedPnl > 0).ToList();
    var losses = trades.Where(t => t.RealizedPnl < 0).ToList();
    var total = (decimal)trades.Count;
    var winRate = wins.Count / total;
    var lossRate = losses.Count / total;
    var avgWin = wins.Count > 0 ? wins.Sum(t => t.RealizedPnl) / wins.Count : 0m;
    var avgLoss = losses.Count > 0 ? -losses.Sum(t => t.RealizedPnl) / losses.Count : 0m;  // positivo
    var expectancy = (winRate * avgWin) - (lossRate * avgLoss);
    if (expectancy < _thresholds.RollingExpectancyThreshold)
    {
        _u4SustainedTradesCounter[id]++;
        if (_u4SustainedTradesCounter[id] >= _thresholds.RollingExpectancySustainedTrades)
            return (true, $"U4: expectancy rolling {expectancy:F2} sostenido {_u4SustainedTradesCounter[id]} trades.");
    }
    else
    {
        _u4SustainedTradesCounter[id] = 0;
    }
    return (false, string.Empty);
}
```

**`TriggerDegradation(string id, string description)`:**

```csharp
private void TriggerDegradation(string id, string description)
{
    _degraded[id] = true;

    var open = _openPositions[id];
    if (open is not null)
    {
        _orderRouter.LiquidateInstrument(
            open.InstrumentId,
            OrderPurpose.TimeExit,
            id);
        _openPositions[id] = null;
        _logger.Critical(
            "OPS-2 disparó degradación de '{ExecutorIdentifier}'. {Description} Liquidando posición.",
            id, description);
    }
    else
    {
        _logger.Critical(
            "OPS-2 disparó degradación de '{ExecutorIdentifier}' sin posición abierta. {Description}",
            id, description);
        _logger.Info(
            "'{ExecutorIdentifier}' degradada sin posición abierta a liquidar.",
            id);
    }

    _eventBus.Publish(new RiskLimitBreachedEvent(
        TimestampUtc: _clock.UtcNow,
        Reason: RiskLimitBreachReason.StrategyDegradation,
        Description: $"{id}: {description}"));
}
```

**`EnsureBuckets(string id)`:** lazy-initialize todos los diccionarios para esa estrategia si no existen.

**Notas finales del componente:**
- El monitor NO tiene método `Reset()` (no implementa `IRiskMonitor`). El reseteo de una estrategia degradada es reinicio del proceso (POLICY 3.1).
- El monitor NO emite logs por cada `OrderFilledEvent` que recibe (sería ruido). Solo loguea: armado de U3/U4 al cruzar 50 trades (`Information`) y disparo de degradación (`Critical` + `Information` si no había posición).

#### B.3 — Wiring real en `TradingAlgorithmHost`

**Archivo:** `Trading.Strategies/TradingAlgorithmHost.cs`.

Reemplazar el `NullStrategyHealthMonitor` de Pieza A por el `StrategyHealthMonitor` real. Ubicación: en el bloque "Servicios de Application" (cerca de línea 5961), **después** de construir `domainEventBus` y **antes** de construir `BarProcessingService`:

```csharp
var strategyHealthThresholds = StrategyHealthThresholds.FromPolicyDefaults();
var strategyHealthMonitor = new StrategyHealthMonitor(
    strategyHealthThresholds,
    _clock,
    _orderRouter,
    _logger,
    domainEventBus);
```

Pasar `strategyHealthMonitor` (en lugar del `NullStrategyHealthMonitor` de Pieza A) a `BarProcessingService`.

Eliminar la construcción del `NullStrategyHealthMonitor` del wiring.

`NullStrategyHealthMonitor` permanece en `Trading.Application/Health/` como fallback testeable público; no se elimina.

#### B.4 — Tests del monitor

**Archivo nuevo:** `Trading.Application.Tests/Health/StrategyHealthMonitorTests.cs`.

Patrón: usar `FakeClock`, `FakeTradingLogger`, `FakeOrderRouter`, `DomainEventBus` real (síncrono, ya existe), `CapturingEventSubscriber<RiskLimitBreachedEvent>`. Construir el monitor pasando un `StrategyHealthThresholds` con defaults conocidos para los tests (puede ser `FromPolicyDefaults()` o uno customizado para forzar disparos sin tener que simular 50 trades reales en cada test).

**Helper interno del archivo de tests:**

```csharp
private OrderFilledEvent BuildFill(
    string executorId,
    OrderPurpose purpose,
    decimal price,
    decimal quantity,
    DateTime? timestamp = null,
    InstrumentId? instrumentId = null)
    => new OrderFilledEvent(
        TimestampUtc: timestamp ?? _clock.UtcNow,
        ExecutorIdentifier: executorId,
        InstrumentId: instrumentId ?? Btc,
        Purpose: purpose,
        FillQuantity: quantity,
        FillPrice: price);
```

`Btc` y `_clock` se definen como campos del test class (`Btc` = `new InstrumentId("BTCUSDT", AssetClass.Crypto)` o lo que use el proyecto — verificar).

**Casos de test (objetivo: ~22 tests, ajustar si la implementación lo justifica):**

**Trade lifecycle:**
1. `OnEntry_AbrePosicionParaLaEstrategia`.
2. `OnEntryConPosicionAbierta_LanzaDomainException` (D-T4).
3. `OnCierreSinEntry_LanzaDomainException` (D-T4).
4. `OnCierre_CalculaPnlRealizado_CasoLong`: Entry @100 con qty=+1, SL @95 → P&L=-5.
5. `OnCierre_CalculaPnlRealizado_CasoShort`: Entry @100 con qty=-1, TP @95 → P&L=+5.
6. `OnCierre_CalculaPnlRealizado_TimeExitConGanancia`.
7. `OnCierre_ConBalancePositivo_ActualizaAth`.

**U1 (DD absoluto):**
8. `U1_PorDebajoDelUmbral_NoDispara`.
9. `U1_SuperaUmbral_DisparaYLiquidaPosicionAbierta`: forzar Entry + cierre que dispara U1 con otra posición abierta de la misma estrategia. (Sutileza: el trade que dispara cierra una posición; "otra posición abierta" no aplica en este sistema sin hedging — entonces el caso a testear es: Entry + cierre con pérdida grande → U1 dispara → posición ya está cerrada por el SL/TP/TE que disparó. Verificar que `LiquidateInstrument` **no se llama** porque no hay posición abierta, pero `RiskLimitBreachedEvent` se publica y el flag `degraded` queda en `true`).
10. `U1_DisparaConPosicionAbiertaPendiente`: caso artificial donde por algún flujo extraño existe una posición abierta al momento del breach. **Construir desde el tests usando una secuencia válida del flujo real**: Entry — el monitor registra la posición — antes del cierre llega un evento que dispara el breach… que en realidad no puede pasar en este sistema porque los breaches se evalúan en cierres. Conclusión: este test no es factible con la semántica actual del monitor — **eliminarlo** y dejar solo el #9 que cubre el caso real (sin posición abierta al disparar). Documentar en el ADR-023 que la liquidación dirigida es defensiva: el monitor solo dispara en cierres, así que en la práctica no hay posición a liquidar en el instante exacto del breach.
11. `U1_DespuesDeDisparar_PosterioresEventosSeIgnoran`.

**U2 (DD rolling 30 días sostenido 5 días):**
12. `U2_DdRollingBajoUmbral_NoDispara`.
13. `U2_DdRollingSobreUmbralPor4Dias_NoDispara`: avanzar el `FakeClock` un día por trade, 4 días consecutivos con DD rolling > 15%, contador llega a 4 sin disparar.
14. `U2_DdRollingSobreUmbralPor5Dias_Dispara`.
15. `U2_DdRollingBajaAlUmbralEnElMedio_ResetaContador`: 3 días sobre umbral, 1 día bajo, 4 días sobre umbral → no dispara (necesita 5 consecutivos).
16. `U2_VariosTradesElMismoDia_NoAcumulaPuntosDiarios`: 3 trades el mismo día UTC → solo 1 `DailyEquityPoint` registrado.

**U3 (PF rolling sostenido 10 trades):**
17. `U3_MenosDe50TradesAcumulados_NoSeEvalua_AunSiPfBajo`: 49 trades con PF=0.5, no dispara, sin log de armado.
18. `U3_AlAlcanzar50Trades_LogueaInformacionDeArmado`: trade #50 cerrado → log `Information` que menciona armado y el `ExecutorIdentifier`.
19. `U3_ConPfSobre1_NoDispara`.
20. `U3_ConPfBajo1Por10TradesConsecutivos_Dispara`.
21. `U3_ConGrossLossCero_NoEvalua_YResetea`: 30 trades cerrados todos ganadores → PF indefinido, contador a 0.

**U4 (expectancy rolling sostenido 10 trades):**
22. `U4_ConExpectancyNegativaPor10TradesConsecutivos_Dispara`.

**Multi-estrategia y exclusión:**
23. `IsExcluded_EstrategiaNoConocida_RetornaFalse`.
24. `IsExcluded_DespuesDeDegradacion_RetornaTrue`.
25. `DosEstrategiasIndependientes_UnaDegradadaNoAfectaALaOtra`.

**RiskLimitBreachedEvent:**
26. `AlDispararCualquierUmbral_PublicaRiskLimitBreachedEventConStrategyDegradation`: capturar el evento, verificar `Reason == StrategyDegradation` y que `Description` contiene el `ExecutorIdentifier` y el umbral.

(El conteo final puede oscilar entre 22 y 26 según cuán paranoico se sea con cobertura. Apuntar a ~24 sólidos antes que >30 redundantes.)

**Importante:** los tests usan `FakeOrderRouter` (ya existe en `Trading.Application.Tests/Fakes/`). Verificar al implementar que el fake registra llamadas a `LiquidateInstrument` (si no las registra todavía, agregar el conteo: 3-4 líneas).

#### B.5 — Tests de `StrategyHealthThresholds`

**Archivo nuevo:** `Trading.Application.Tests/Health/StrategyHealthThresholdsTests.cs`.

3-4 tests cortos:
1. `FromPolicyDefaults_RetornaValoresDePolicy3_1`: verificar los 10 campos contra los literales esperados.
2. `Constructor_ConValorNegativoEnDdAbsoluto_LanzaExcepcion`.
3. `Constructor_ConSustainedDaysCero_LanzaExcepcion`.
4. `Constructor_ConExpectancyThresholdCero_NoLanza` (caso válido borderline).

#### B.6 — ADR-023

**Archivo:** `DECISIONS.md` (existente).

Agregar al **inicio** del archivo (encima de ADR-022) entrada nueva. Estructura estándar de los ADRs del proyecto (Contexto, Decisión, Alternativas, Consecuencias). Contenido mínimo:

- **Título:** `ADR-023 — StrategyHealthMonitor: componente autónomo fuera del array de IRiskMonitor del orchestrator`.
- **Fecha:** la del día de cierre del refactor.
- **Estado:** Aceptada.
- **Contexto:** POLICY 3 exige liquidación dirigida + exclusión por estrategia ante degradación. El orchestrator del refactor #4 gestiona kill switch global con cooling-off de 24h compartido. Forzar el `StrategyHealthMonitor` al array de `IRiskMonitor` rompería la semántica de POLICY (apagaría el sistema entero ante una estrategia mala). Hay precedente en ADR-017 (filtro de régimen también vive fuera).
- **Decisión:**
  - `StrategyHealthMonitor` autónomo en `Trading.Application/Health/`, no implementa `IRiskMonitor`.
  - Se suscribe a `OrderFilledEvent`, mantiene estado por `ExecutorIdentifier`.
  - Al disparar U1-U4 según POLICY 3.1: `IOrderRouter.LiquidateInstrument` + flag `degraded` interno + `RiskLimitBreachedEvent` con razón nueva `StrategyDegradation`.
  - `BarProcessingService` consulta `IStrategyHealthMonitor.IsExcluded` como guard pre-orden, análogo al filtro de régimen.
  - Umbrales en POCO inmutable con factory `FromPolicyDefaults()` que codifica POLICY 3.1.
- **Alternativas consideradas:**
  - **A: `StrategyHealthMonitor : IRiskMonitor`.** Descartada por colisión semántica con el orchestrator (activa kill switch global) y porque obligaría a refactorizar el orchestrator a dispatch razón→acción + flags por estrategia, ampliando el blast radius del refactor. Documentada y descartada en chat.
  - **B (elegida): componente autónomo.**
  - **C: Persistencia de estado en disco entre reinicios.** Descartada para OPS-2 (alcance medio); aceptada como deuda futura antes de live serio.
- **Consecuencias:**
  - El concepto "monitor de risk" del proyecto se aclara: `IRiskMonitor` = monitor de kill switch global; otros monitors (régimen, salud por estrategia) viven fuera con sus propios contratos.
  - Las métricas no persisten entre reinicios. Si el proceso reinicia tras 30 trades, vuelve a 0 y arma U3-U4 cuando complete 50 nuevos. Aceptable para paper; deuda explícita en ROADMAP/Bloque 4.
  - El `HealthHeartbeatTracker` (INFRA-2 Pieza B) ya captura `RiskLimitBreachedEvent` por suscripción al bus; refleja `StrategyDegradation` sin código nuevo (verificado al hacer el wiring de Pieza B).
  - El `RiskOrchestrator` queda intacto. Los próximos monitors per-strategy (`RegimeIncompatibilityMonitor` runtime futuro, etc.) siguen el patrón de OPS-2 (guard en `BarProcessingService`), no el de `IRiskMonitor`.

#### B.7 — Actualizar `ROADMAP.md`

**Archivo:** `ROADMAP.md`.

1. En "Plan general" (diagrama del Bloque 3 con el bloque ASCII) marcar OPS-2 con ✅.
2. En "Refactors pendientes" → tabla del Bloque 3 → fila OPS-2: cambiar estado a ✅ y mover la fila a "Historial completado" al final del archivo, con fecha de hoy y resumen.
3. **Agregar nueva entrada en Bloque 4 postergado:** `OPS-3 — Persistencia del estado de StrategyHealthMonitor entre reinicios del proceso`. Trigger: antes de migrar a live serio (post Hito D). Comentario: "Hoy las métricas viven in-memory desde el arranque del proceso (ADR-014 + ADR-023). Si el proceso reinicia, se pierde historial reciente; el monitor entra en warm-up y los rolling se re-arman tras los próximos 50 trades. Aceptable para paper. En live serio, una caída del proceso seguida de restart resetea la detección U3/U4 silenciosamente: si la estrategia veníá generando alertas sostenidas que aún no llegaban a 10 trades de N consecutivos, el contador se borra. Fix esperado: serializar `HealthSnapshot`-equivalente por estrategia al `health/strategy-health-{executorIdentifier}.json` con flush atómico cada N trades cerrados; cargar al boot. Decisión técnica abierta: ¿qué pasa si el archivo está corrupto al cargar? (fail loud vs. arrancar warm). Requiere ADR propio."
4. Las DEUDA-1/2/3 NO se tocan.

#### B.8 — Actualizar `POLICY.md` sección 7.1

**Archivo:** `POLICY.md`.

En la sección 7.1 (`EmaCrossStrategy / BTCUSDT / 1h`), al final del bloque "Acción al disparar cualquier umbral:" agregar una línea:

```
(Implementado runtime por StrategyHealthMonitor — OPS-2, ADR-023, YYYY-MM-DD.)
```

(Reemplazar `YYYY-MM-DD` por la fecha real.)

Sin cambios a los umbrales numéricos ni al texto operativo.

#### Criterio de aceptación de Pieza B

- `dotnet build Trading.Strategies/Trading.Strategies.csproj` → verde.
- `dotnet test Trading.Domain.Tests/Trading.Domain.Tests.csproj` → verde sin cambios (Pieza B no toca Domain salvo si `DomainException` no existe y hay que crearla — verificar; el proyecto ya usa excepciones como `InvalidRiskParametersException` y `StrategyConfigurationException`, ambas existen en `Trading.Domain/Exceptions/`; si no hay `DomainException` base, usar la más cercana en semántica: probablemente vale crear una `StrategyHealthInvariantException` siguiendo el patrón del proyecto, pero **detenerse y reportar antes de crearla** porque introduce un tipo nuevo de excepción al dominio).
- `dotnet test Trading.Application.Tests/Trading.Application.Tests.csproj` → verde con +~28 tests nuevos (~24 de `StrategyHealthMonitorTests` + 4 de `StrategyHealthThresholdsTests`).
- Operador corre backtest manualmente → mismas ~225 órdenes. El monitor está activo pero `EmaCrossStrategy` no alcanza condiciones de disparo en el backtest baseline.
- Grep de invariantes:
  - `grep -rn "using QuantConnect" Trading.Domain/ Trading.Application/` → cero matches.
  - `grep -rn "DateTime\.UtcNow" Trading.Application/Health/` → cero matches (todo via `IClock`).
  - `grep -rn "0\.25m\|0\.15m\|1\.0m\|0m" Trading.Application/Health/` → matches solo dentro de `StrategyHealthThresholds.cs` (factory de defaults).

---

## 7. Validaciones de salida (qué corre el operador)

Después de cada pieza:

```bash
# Build:
dotnet build Trading.Domain/Trading.Domain.csproj
dotnet build Trading.Application/Trading.Application.csproj
dotnet build Trading.Strategies/Trading.Strategies.csproj

# Tests:
dotnet test Trading.Domain.Tests/Trading.Domain.Tests.csproj
dotnet test Trading.Application.Tests/Trading.Application.Tests.csproj
dotnet test Trading.Strategies.Tests/Trading.Strategies.Tests.csproj   # si existe

# Invariantes arquitectónicas:
grep -rn "using QuantConnect" Trading.Domain/ Trading.Application/             # esperado: 0
grep -rn "DateTime\.\(Now\|UtcNow\)" Trading.Application/Health/              # esperado: 0
grep -rn "throw new Exception\|ApplicationException" Trading.Application/Health/  # esperado: 0
grep -rn "Console\.WriteLine" Trading.Application/Health/                      # esperado: 0

# Backtest no-regresión: lanzar el algoritmo en QuantConnect y verificar:
# - Mismo número de órdenes que pre-OPS-2 (~225).
# - Tiempo de ejecución no degradado significativamente respecto al baseline.
# - JSONL del run no contiene líneas Critical inesperadas.
```

---

## 8. Mensaje de commit sugerido

### Pieza A (commit independiente)

```
refactor(health): preparar cableado de StrategyHealthMonitor sin tocar RiskOrchestrator

- IStrategyHealthMonitor (Domain): contrato bool IsExcluded(executorIdentifier).
- NullStrategyHealthMonitor (Application): implementación pasiva como placeholder.
- RiskLimitBreachReason.StrategyDegradation agregado al enum.
- BarProcessingService gana parámetro IStrategyHealthMonitor en constructor y
  guard pre-señal: continue si IsExcluded. Posicionado después de
  IsKillSwitchActivated y antes del filtro de régimen.
- FakeStrategyHealthMonitor en Trading.Application.Tests/Fakes/.
- Tests existentes adaptados: builders agregan FakeStrategyHealthMonitor.
- 2 tests nuevos en BarProcessingServiceBarProcessedEventTests verifican
  el guard nuevo.
- Wiring de TradingAlgorithmHost usa NullStrategyHealthMonitor como placeholder
  hasta Pieza B.

Refs ADR-015, ADR-017, ADR-022. Preparación para Pieza B.
Backtest idéntico al pre-OPS-2 (~225 órdenes).
```

### Pieza B (commit independiente)

```
feat(health): StrategyHealthMonitor implementa POLICY 3.1 (umbrales U1-U4)

- StrategyHealthMonitor (Trading.Application/Health/): consume OrderFilledEvent,
  mantiene métricas rolling por ExecutorIdentifier (equity, ATH, trades cerrados,
  serie diaria), evalúa U1-U4 según POLICY 3.1, dispara liquidación dirigida
  vía IOrderRouter.LiquidateInstrument y publica RiskLimitBreachedEvent con
  StrategyDegradation. NO implementa IRiskMonitor (ver ADR-023).
- StrategyHealthThresholds: POCO inmutable con factory FromPolicyDefaults()
  que codifica los literales de POLICY 3.1.
- Wiring en TradingAlgorithmHost reemplaza el NullStrategyHealthMonitor por
  el monitor real.
- ~28 tests nuevos cubren los 4 umbrales, el armado tras 50 trades, el reseteo
  de contadores "sostenido N", el flag degraded, los fail-loud (Entry duplicado,
  cierre sin entry), las dos estrategias independientes y el cálculo del P&L
  realizado para Long/Short.
- ROADMAP actualizado: OPS-2 ✅, OPS-3 (persistencia entre reinicios) agregada
  como deuda postergada en Bloque 4.
- POLICY 7.1 nota la implementación runtime de OPS-2.

Refs ADR-022, ADR-023. Closes OPS-2 del Bloque 3.
Backtest idéntico al pre-OPS-2 (~225 órdenes, U3-U4 no se arman, U1-U2 no
disparan).
```

### Commit unificado (si se prefiere uno solo)

```
feat(health): OPS-2 — StrategyHealthMonitor implementa POLICY sección 3

Pieza A (cableado): IStrategyHealthMonitor contrato, RiskLimitBreachReason.
StrategyDegradation, guard pre-señal en BarProcessingService, fakes y tests.

Pieza B (monitor): StrategyHealthMonitor consume OrderFilledEvent, mantiene
métricas rolling por estrategia, dispara liquidación dirigida + exclusión al
cruzar U1-U4 de POLICY 3.1. Sin tocar RiskOrchestrator (ver ADR-023).

~30 tests nuevos. Backtest idéntico al pre-OPS-2.
Refs ADR-022, ADR-023. Closes OPS-2.
```

Recomendación: **commits independientes** por pieza, para que el revert de B no arrastre el cableado de A si algo sale mal.

---

## 9. Resumen para el operador al cerrar

- POLICY sección 3 está implementada runtime. `EmaCrossStrategy / BTCUSDT / 1h` queda monitoreada con U1 y U2 activos desde el primer trade, y U3-U4 armándose tras 50 trades acumulados en paper trading.
- `BarProcessingService` ahora descarta señales de estrategias degradadas vía el guard nuevo. Patrón idéntico al filtro de régimen (ADR-017).
- `RiskOrchestrator` queda intacto, sigue gestionando solo kill switch global.
- Bloque 3 cierra completo (✅ INFRA-1, ✅ INFRA-2, ✅ OPS-1, ✅ OPS-2). Quedan DEUDA-1/2/3 abiertas (no bloquean Hito C).
- Deuda nueva introducida: `OPS-3 — Persistencia del estado de StrategyHealthMonitor entre reinicios`. Anotada en Bloque 4 postergado.
- Siguiente paso del ROADMAP: **Hito C — Paper trading**. Antes del Hito C, validar manualmente:
  1. Que el `HealthHeartbeatTracker` refleja `StrategyDegradation` cuando una estrategia dispara (forzar un breach sintético en una sesión de paper, o test de integración separado).
  2. Que las DEUDA-1/2/3 no requieren atención antes de paper.
  3. Que POLICY 7.1 está poblada con la estrategia y los umbrales (este brief lo deja listo).
