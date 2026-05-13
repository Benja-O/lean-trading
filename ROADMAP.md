# ROADMAP - Sistema de Trading

> **Propósito de este documento:** mantener visibilidad del plan completo entre sesiones de trabajo. Cualquier sesión con Claude Code (o cualquier desarrollador) debe leer este archivo primero para entender en qué punto está el proyecto.
>
> **Reglas:**
> - Cada refactor completado se marca con ✅ y fecha.
> - Cada refactor en curso se marca con 🔄.
> - Cada refactor pendiente se marca con ⬜.
> - Los refactors abortados o descartados se marcan con ❌ y se anota la razón.
> - La columna "Bloque" indica el hito al que pertenece (ver sección Plan general).
> - Cuando se complete un refactor, mover su descripción detallada al final del archivo, sección "Historial completado".

---

## Plan general (hitos del proyecto)

El proyecto está organizado en bloques de trabajo. Los refactors técnicos están agrupados por bloque según cuándo es necesario hacerlos.

```
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 0 — Estado actual (refactors ya completados)         │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 1 — Antes del Hito A (Automatización de backtest)    │
├─────────────────────────────────────────────────────────────┤
│ Refactor A2 — Logging estructurado con placeholders  ✅     │
│ Refactor B1 — Result<T> donde hay magic values        ✅    │
│ Refactor B3 — Eventos de dominio (OrderSubmitted/Filled/...) ✅│
└─────────────────────────────────────────────────────────────┘
        ✅ BLOQUE 1 COMPLETO — Sistema listo para Hito A
                            ↓
              ✅ HITO A: Tests de referencia de
                  indicadores y estrategias
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 2 — Antes del Hito B (Regímenes de mercado)          │
├─────────────────────────────────────────────────────────────┤
│ Refactor #4 — Separar IRiskMonitor de IRiskAction    ✅     │
└─────────────────────────────────────────────────────────────┘
        ✅ BLOQUE 2 COMPLETO — Sistema listo para Hito B
                            ↓
                  HITO B: Clasificación de regímenes
                  de mercado (k-means o HMM)
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 3 — Antes del Hito C (Paper trading)                 │
├─────────────────────────────────────────────────────────────┤
│ Path absoluto a configuración                               │
│ Monitoreo básico (alertas si algo se cae)                   │
└─────────────────────────────────────────────────────────────┘
                            ↓
                  HITO C: Paper trading
                            ↓
                  HITO D: Live trading con capital chico
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 4 — Cuando el sistema crezca (no urgente)            │
├─────────────────────────────────────────────────────────────┤
│ Value Objects Money/Price/Quantity (cuando haya 2do asset)  │
│ OrderNormalizer separado (cuando haya múltiples callers)    │
│ Jerarquía DomainException                                   │
│ Trading.TestSupport proyecto separado                       │
└─────────────────────────────────────────────────────────────┘
```

---

## Refactors pendientes

### ✅ BLOQUE 1 — Completo

*(Todos los refactors del Bloque 1 están completados. Ver Historial completado.)*

### ✅ BLOQUE 2 — Completo

*(Todos los refactors del Bloque 2 están completados. Ver Historial completado.)*

### ⬜ BLOQUE 3 — Pendiente

| Estado | ID | Refactor | Bloquea | Comentario |
|---|---|---|---|---|
| ⬜ | INFRA-1 | Path absoluto de `strategies.json` a configuración inyectable | Hito C | Hoy hardcodeado en `TradingAlgorithmHost.Initialize()`. Pasar a parámetro de entorno o config externa antes de deploy a VPS. |
| ⬜ | INFRA-2 | Monitoreo básico del sistema en producción | Hito C | Alertas mínimas: caída del proceso, ausencia de market data, kill switch activado. Stack a definir (Healthchecks.io, Uptime Kuma, etc.). |

### ⬜ BLOQUE 4 — Postergado (no urgente)

| Estado | ID | Refactor | Comentario |
|---|---|---|---|
| ⬜ | A4/A5 | Value Objects `Money`, `Price`, `Quantity`, `Notional` | Hacer cuando se agregue un segundo asset class o cuando aparezca un bug por confusión `decimal` → `decimal`. |
| ⬜ | A6 | `OrderNormalizer` separado del `PositionSizer` | Hacer cuando exista un segundo caller del `IOrderRouter` que no pase por el `PositionSizer`. |
| ⬜ | B2 | Jerarquía `DomainException` base | Mejora ergonomía, no previene bugs. Hacer cuando la cantidad de excepciones de dominio justifique la base común. |
| ⬜ | B5 | Proyecto separado `Trading.TestSupport` para fakes compartidos | Hacer cuando exista una segunda suite de tests que necesite los fakes. |
| ⬜ | A3 | `IOrderIdGenerator` inyectable | Purismo: testabilidad determinista del registry. `Guid.NewGuid()` funciona y no afecta dinero. |
| ⬜ | AUDIT-1 | Auditor independiente en Python con TA-Lib | Para live trading serio: auditoría verdaderamente independiente del runtime de QC. El auditor actual en C# detecta bugs de flujo de control y estado interno, pero comparte motor de cálculo con QC. Python + TA-Lib provee independencia plena. |

---

## Historial completado

> Los refactors completados se mueven acá con su fecha y un resumen de qué cambió. Orden cronológico: más antiguo arriba.

### ✅ Refactor inicial — Naming consistente
**Fecha:** sesión 1
**Resumen:** todos los identificadores en inglés, campos privados con `_`, eliminación de abreviaturas. Comportamiento preservado.

### ✅ Refactor de RiskParameters como Value Object
**Fecha:** sesión 1
**Resumen:** creación de `RiskParameters` value object con invariantes (stop, take profit, riesgo por trade) verificadas en construcción. Eliminado fallback silencioso `if (stopLossPercentage <= 0) ...= 0.03m` del `PositionSizer`. Conversión `/100m` centralizada en `FromPercentages`. 21 tests xUnit.

### ✅ Refactor de desacople de QuantConnect
**Fecha:** sesión 1
**Resumen:** creación de `Trading.Application` como proyecto separado. Introducción de abstracciones del dominio: `IPortfolioState`, `IInstrumentMetadata`, `IOrderRouter`, `IOrderHandle`, `IClock`, `ITradingLogger`, `IPriceRounder`. `MarketBar` reemplaza `MarketData`. `InstrumentId` reemplaza `Symbol` de QC en el dominio. Adaptadores Lean creados en `Trading.Strategies/Adapters`. 12 tests adicionales del `KillSwitchManager` con fakes. Invariante: `Trading.Domain` y `Trading.Application` cero `using QuantConnect`.

### ✅ Unificación de carpetas `Interfaces/` → `Abstractions/`
**Fecha:** sesión 1
**Resumen:** eliminada la carpeta `Trading.Domain/Interfaces/`. Movidos `IStrategy` e `IStrategyConfigLoader` a `Trading.Domain/Abstractions/`.

### ✅ RiskPerTradePercentage por estrategia en JSON
**Fecha:** sesión 1
**Resumen:** campo `RiskPerTradePercentage` obligatorio en `strategies.json`. `StrategyConfigLoader` falla loud si está ausente (decimal nullable para distinguir "ausente" de "presente con valor 0"). Eliminado el default 2% hardcodeado en `TradingAlgorithmHost`.

### ✅ Eliminación de stringly-typed tags
**Fecha:** sesión 1
**Resumen:** enum `OrderPurpose { Entry, StopLoss, TakeProfit, TimeExit }` reemplaza strings ENTRY/SL/TP/TIME. `OrderRegistry` central en `Trading.Application` mapea tags opacos (`ord_xxxxxxxx`) a registraciones estructuradas. `IOrderRouter` cambia firma para recibir `OrderPurpose` + `executorIdentifier`. `OrderLifecycleEvent` expone `Purpose` y `ExecutorIdentifier` resueltos. Cleanup automático del registry tras eventos terminales. 9 tests adicionales del `OrderRegistry`.

### ✅ Fix eventos huérfanos y sobreescritura de tags
**Fecha:** sesión 2
**Resumen:** descubierto en log de operaciones que `OrderTicket.Cancel(reason)` de Lean sobreescribe el `Tag` del ticket. Fix en `LeanOrderHandle.Cancel`: ya no propaga el reason a Lean. `OrderEventMapper` distingue tag con nuestro prefijo (residual esperado, Debug) de tag externo (liquidación global, Debug con mensaje distinto). `OrderLifecycleService` loguea Info con motivo de cancelación antes de invocar `Cancel`. Logs limpios: 0 mensajes anómalos en backtest posterior.

### ✅ Habilitar Long y Short en estrategias
**Fecha:** sesión 2
**Resumen:** enum `SignalDirection { Flat, Long, Short }` reemplaza el `bool` de `IStrategy.EvaluateSignal`. `EmaCrossStrategy` ahora produce `Short` en cruces bajistas (antes los ignoraba). `BarProcessingService` aplica signo a la cantidad según dirección. `PositionSizer` sigue devolviendo magnitud positiva (sin cambios). Sin tests nuevos por decisión de minimalismo.

### ✅ Refactor B1 — Result<T> donde había magic values (alcance: PositionSizer)
**Fecha:** 2026-05-12
**Resumen:** Se crearon dos tipos genéricos en `Trading.Domain/Abstractions/`: `Result<TValue, TFailureReason>` y `Result<TFailureReason>` como `readonly record struct` para evitar allocations en el hot path. Se creó el enum `SizingFailureReason` con tres valores: `InvalidPrice`, `QuantityRoundsToZero`, `BelowMinimumNotional`. `PositionSizer.CalculateQuantity` cambió de retornar `decimal` (magic value `0m` ante error) a `Result<decimal, SizingFailureReason>`: ahora distingue explícitamente éxito, precio inválido y cantidad que redondea a cero. `PositionSizer.IsValidNotional` fue renombrado a `ValidateNotional` y retorna `Result<SizingFailureReason>`. `BarProcessingService` fue adaptado como caller: agrega `ITradingLogger` al constructor (también wireado en `TradingAlgorithmHost`) y loguea en Debug el motivo de skip al recibir un `Failure`. Se crearon `FakeInstrumentMetadata`, `FakeStrategy` en el proyecto de tests. Se añadieron 7 tests nuevos en `PositionSizerTests`. Total: 29 tests verdes (0 errores). Invariante arquitectónica preservada: cero `using QuantConnect` en Domain/Application/Tests.

### ✅ Refactor B3 — Eventos de dominio tipados
**Fecha:** 2026-05-12
**Resumen:** Se creó la marker interface `IDomainEvent` y cuatro eventos tipados en `Trading.Domain/Events/`: `OrderSubmittedEvent`, `OrderFilledEvent`, `OrderCanceledEvent` y `RiskLimitBreachedEvent` (con enum `RiskLimitBreachReason`). Se definió la interfaz `IDomainEventBus` en `Trading.Domain/Abstractions/` y se implementó `DomainEventBus` en `Trading.Application/Eventing/`: bus síncrono in-memory con snapshot de suscriptores bajo lock, aislamiento de fallos (un suscriptor que lanza loguea Error y el bus continúa). `KillSwitchManager`, `BarProcessingService` y `OrderLifecycleService` reciben `IDomainEventBus` e `IClock` por constructor y emiten el evento correspondiente en cada transición crítica. `TradingAlgorithmHost` construye el bus y lo inyecta en todos los servicios. Se agregaron `CapturingEventSubscriber<TEvent>` para tests, 7 tests nuevos en `DomainEventBusTests` y 1 test nuevo en `KillSwitchManagerTests`. Total: 37 tests verdes (0 errores). Bloque 1 completo; sistema listo para Hito A.

### ✅ Refactor A2 — Logging estructurado con placeholders nombrados
**Fecha:** sesión 3 (2026-05-11)
**Resumen:** `ITradingLogger` extendido a 5 niveles (`Debug`, `Info`, `Warning`, `Error`, `Critical`) con firma `(string messageTemplate, params object[] arguments)`. `LeanLogger` convierte placeholders nombrados a posicionales via regex antes de `string.Format`. `FakeTradingLogger` reemplaza las tres `List<string>` por `List<CapturedLogEntry>` con `Level`, `MessageTemplate` y `Arguments`. Migrados 10 call sites en `OrderLifecycleService`, `KillSwitchManager`, `PositionSizer` y `OrderEventMapper`: eliminada toda interpolación `$"..."`. `ActivateKillSwitch` sube de `Error` a `Critical`; `EvaluateCoolingOffPeriod` sube de `Debug` a `Info`. Eliminados prefijos manuales de timestamp. Test `ActivateKillSwitch_LiquidatesAndLogsError` actualizado a `CriticalEntries`. Logs parseables por herramientas de observabilidad (Seq, Datadog). Sin cambios de comportamiento funcional. 21 tests Domain + 20 Application = 41 verde.

### ❌ Fix — SignalAuditor: tolerancia relativa en lugar de absoluta (REVERTIDO)
**Fecha original:** 2026-05-12
**Fecha de reversión:** 2026-05-13
**Razón:** todo el enfoque del SignalAuditor fue eliminado. Ver ADR-014.

### ❌ Fix — SignalAuditor: eliminar falsos positivos en recálculo de EMA (REVERTIDO)
**Fecha original:** 2026-05-12
**Fecha de reversión:** 2026-05-13
**Razón:** todo el enfoque del SignalAuditor fue eliminado. Ver ADR-014.

### ❌ Hito A — Auditor de fidelidad de señales en backtest (REVERTIDO)
**Fecha original:** 2026-05-12
**Fecha de reversión:** 2026-05-13
**Razón:** diseño equivocado. Recalcular indicadores en vivo durante el backtest dentro del mismo proceso es duplicación, no auditoría. Tras cuatro fixes iterativos (buffer, warm-up, tolerancia, algoritmo) persistían ~33% de discrepancias sin causa raíz clara. Reemplazado por tests unitarios estáticos contra valores de referencia (baseline QC), que es el estándar institucional documentado por la propia QuantConnect. Ver ADR-014.

### ✅ Refactor #4 — Separar IRiskMonitor de IRiskAction
**Fecha:** 2026-05-13
**Resumen:** `KillSwitchManager` (God Object: detectaba drawdown, contaba pérdidas, accionaba liquidación, gestionaba cooling-off) fue descompuesto en cinco componentes de responsabilidad única. `DrawdownMonitor : IRiskMonitor` detecta drawdown sobre high-water mark. `ConsecutiveLossesMonitor : IRiskMonitor` registra y detecta rachas de pérdidas (API: `RegisterLoss()`, `RegisterWin()`). `CoolingOffTracker` (no monitor: rol inverso, señala desactivación). `LiquidateAllRiskAction : IRiskAction` ejecuta la liquidación. `RiskOrchestrator` coordina el ciclo: evalúa monitors, activa kill switch, ejecuta acción, gestiona cooling-off; expone `IsKillSwitchActivated` y `EvaluateAllMonitors()`. `BarProcessingService` recibe `RiskOrchestrator`. `OrderLifecycleService` recibe `ConsecutiveLossesMonitor` directamente (el orquestador no necesita saber de fills individuales). `TradingAlgorithmHost` compone el grafo completo. `KillSwitchManager.cs` y `KillSwitchManagerTests.cs` eliminados. Nuevos tests: `DrawdownMonitorTests` (4), `ConsecutiveLossesMonitorTests` (5), `RiskOrchestratorTests` (5). Fake `FakeRiskMonitor` + `FakeRiskAction` agregados. Total tras refactor: 57 tests. Invariante preservada: cero `using QuantConnect` en Domain/Application.

### ✅ Hito A (versión 2) — Tests de referencia de indicadores y estrategias
**Fecha:** 2026-05-13
**Resumen:** eliminado completamente el SignalAuditor y todo el código del enfoque anterior (9 archivos borrados, 4 modificados). Reemplazado por dos tipos de tests unitarios estándares institucionales: (1) tests de referencia que verifican que ExponentialMovingAverage de QC produce valores equivalentes al baseline QC sobre serie sintética conocida (QC valida internamente contra TA-Lib), (2) tests de comportamiento de EmaCrossStrategy con datos sintéticos diseñados para forzar cruces alcistas y bajistas. Cobertura institucional sin overhead runtime. 6 tests nuevos. Total verde: 43 tests. Sanity check final humano (verificación de 3-5 señales en TradingView antes de pasar a paper trading) queda como práctica recomendada, no automatizada.

---

## Cómo usar este archivo

### Al iniciar una sesión nueva (con Claude o solo):
1. Abrir `ROADMAP.md` y leer la sección **"Refactors pendientes"**.
2. Confirmar cuál es el próximo refactor (el primero marcado 🔄 o el primer ⬜ del bloque actual).
3. Leer `DECISIONS.md` para entender las decisiones arquitectónicas tomadas que afectan el refactor.
4. Leer `AI.md` para las reglas de estilo y arquitectura.

### Al completar un refactor:
1. Mover la fila del refactor a la sección **"Historial completado"** con fecha y resumen.
2. Si surgieron decisiones arquitectónicas nuevas, agregarlas a `DECISIONS.md`.
3. Si una decisión cambió una regla del proyecto (ej. cambia el contrato de logging), actualizar `AI.md`.
4. Commitear los tres archivos junto con el código del refactor.

### Si un refactor se aborta:
1. Marcarlo como ❌.
2. Agregar nota con la razón.
3. Si la decisión amerita registro, agregar entrada en `DECISIONS.md`.
