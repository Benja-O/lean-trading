# Refactor #4 — Separar IRiskMonitor de IRiskAction

## Recordatorio operativo (CRÍTICO — leer primero)

Este refactor sigue las reglas de `AI.md` sección **"🚦 Límites de Ejecución del Asistente"**. En particular, el asistente:

- **NO ejecuta `git`** en ninguna forma (ni add, ni commit, ni checkout, ni stash, ni nada).
- **NO compila** el proyecto (ni `dotnet build`, ni `dotnet clean`, ni `dotnet restore`).
- **NO ejecuta tests** (ni `dotnet test`, ni invocaciones a runners).
- **SÍ actualiza `ROADMAP.md` y `DECISIONS.md`** como parte del refactor, igual que cualquier otro archivo del proyecto. Esos cambios se hacen en la misma "tanda" de modificaciones. Si el refactor sale mal, el usuario los revierte desde Git junto al resto.

Las verificaciones (compilación, tests, backtest) y el versionado las hace el usuario manualmente.

Al finalizar, el asistente entrega un reporte resumen en el chat con qué archivos tocó (incluyendo los de tracking) y qué espera del usuario.

---

## Contexto del proyecto

Sistema de trading sistemático en C# / .NET 10 sobre QuantConnect/Lean. Cuatro proyectos:

- **Trading.Domain** — capa de dominio, CERO `using QuantConnect`.
- **Trading.Application** — orquestación pura, CERO `using QuantConnect`.
- **Trading.Strategies** — adaptadores Lean. Único proyecto con `using QuantConnect`.
- **Trading.Application.Tests** — tests xUnit.

**Invariante arquitectónica crítica:** Trading.Domain y Trading.Application NO deben tener ningún `using QuantConnect` en ningún archivo.

Documentos de referencia en la raíz del repo (LEER antes de empezar):
- `AI.md` — reglas de estilo, arquitectura y límites de ejecución.
- `ROADMAP.md` — plan completo, estado actual.
- `DECISIONS.md` — log de ADRs. El más reciente es ADR-014.

## Reglas de naming (no negociables)

1. **Identificadores en inglés.** Comentarios y mensajes en español.
2. **Campos privados** → `_camelCase`.
3. **Cero abreviaturas.**
4. **Variables descriptivas.**
5. **Mensajes de log con placeholders nombrados.**

## Motivación

Hoy `KillSwitchManager` mezcla dos responsabilidades en una sola clase:

1. **Detección de condiciones de riesgo:** chequea drawdown del portfolio, cuenta pérdidas consecutivas, evalúa si está en período de cooling-off.
2. **Acción de respuesta:** liquida toda la cartera y marca el flag `IsKillSwitchActivated`.

Esta mezcla escala mal. Cuando se agregue "régimen de mercado incompatible" como cuarto motivo de kill (Hito B), el `KillSwitchManager` empezaría a tener responsabilidades acopladas que dificultan testing y mantenimiento.

**Aplicar el Principio de Responsabilidad Única (SOLID):**

- **`IRiskMonitor`** (varios): cada uno chequea UNA condición específica y reporta si fue violada.
- **`IRiskAction`** (uno): ejecuta la acción cuando un monitor solicita kill.
- **`RiskOrchestrator`**: coordinador que itera los monitors registrados y delega a la action si alguno indica violación.

Tras este refactor, agregar "régimen incompatible" en Hito B será crear una clase `RegimeIncompatibilityMonitor : IRiskMonitor` y registrarla en el wiring. Cero modificación de código existente — patrón abierto-cerrado clásico.

## Decisiones de diseño aplicadas

- **D1 — Migración completa, no parcial:** los tres chequeos actuales (`CheckDrawdownKillSwitch`, `RegisterLoss` con consecutive losses, `EvaluateCoolingOffPeriod`) se extraen cada uno en su propio componente. El `KillSwitchManager` actual desaparece — su lógica se distribuye en monitors + un orchestrator.

- **D2 — Resultado del monitor:** cada monitor devuelve un `RiskAssessment` por evaluación con:
  - `bool ShouldTriggerKillSwitch`
  - `RiskLimitBreachReason Reason` (enum ya existente del refactor B3)
  - `string Description` (para log y evento)

  Diseño: `readonly record struct`. Pequeño, inmutable, frecuencia alta de uso.

- **D3 — Estado por monitor:** cada monitor mantiene su propio estado interno. Cada monitor se inyecta vía constructor del orchestrator.

- **D4 — Modelo de "señal" de monitor → orquestador:** los monitors NO ejecutan la acción directamente. Devuelven el `RiskAssessment` al orchestrator, que decide.

- **D5 — `RegisterLoss` / `RegisterWin`:** el `ConsecutiveLossesMonitor` los expone como métodos públicos. El caller (`OrderLifecycleService`) recibe el monitor concreto directamente vía constructor (tipo concreto, no interfaz), porque la semántica "registrar pérdida" es específica de ese monitor.

- **D6 — Activación/desactivación del kill switch:** vive en el orchestrator (un solo lugar). El estado `IsKillSwitchActivated` es del orchestrator, no de los monitors.

- **D7 — Cooling-off:** componente separado (`CoolingOffTracker`) que NO implementa `IRiskMonitor` porque su rol es inverso — señala cuándo DESACTIVAR el kill switch. La interfaz queda dedicada a monitors que pueden disparar kill.

- **D8 — Nombre del orquestador:** `RiskOrchestrator`. Más claro que el viejo `KillSwitchManager`.

- **D9 — Publicación de eventos:** sigue siendo responsabilidad del orchestrator. Cuando activa kill switch, publica `RiskLimitBreachedEvent`. Los monitors NO publican eventos.

- **D10 — Inyección de monitors al orchestrator:** vía `IEnumerable<IRiskMonitor>` en el constructor. Permite registrar 1 a N monitors sin tocar el orchestrator.

---

## Especificación detallada

### Archivo 1: CREAR `Trading.Domain/Abstractions/RiskAssessment.cs`

```csharp
using Trading.Domain.Events;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Veredicto de un IRiskMonitor tras evaluar las condiciones actuales.
    ///
    /// Si ShouldTriggerKillSwitch es false, los otros campos no tienen significado
    /// (se ignoran). El monitor devolvió "todo bien por mi parte".
    ///
    /// Si es true, el orchestrator activa el kill switch con la razón y descripción
    /// reportadas.
    /// </summary>
    public readonly record struct RiskAssessment(
        bool ShouldTriggerKillSwitch,
        RiskLimitBreachReason Reason,
        string Description)
    {
        public static RiskAssessment Pass() => new(false, default, string.Empty);

        public static RiskAssessment Trigger(RiskLimitBreachReason reason, string description)
            => new(true, reason, description ?? string.Empty);
    }
}
```

### Archivo 2: CREAR `Trading.Domain/Abstractions/IRiskMonitor.cs`

```csharp
namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato de un monitor de riesgo. Cada implementación chequea UNA condición específica
    /// (drawdown, pérdidas consecutivas, régimen de mercado incompatible, etc.) y reporta
    /// si fue violada.
    ///
    /// El monitor NO ejecuta acciones — solo emite veredictos. La acción (liquidar, marcar
    /// kill switch, publicar evento) la hace el RiskOrchestrator.
    ///
    /// Cada monitor mantiene su propio estado interno (counters, históricos, timestamps).
    /// El orchestrator es agnóstico al estado de cada uno.
    /// </summary>
    public interface IRiskMonitor
    {
        /// <summary>Identificador legible para logs y diagnóstico.</summary>
        string MonitorName { get; }

        /// <summary>
        /// Evalúa las condiciones actuales y devuelve el veredicto.
        /// Se invoca por el orchestrator en cada ciclo de chequeo (típicamente cada barra).
        /// </summary>
        RiskAssessment Evaluate();

        /// <summary>
        /// Resetea cualquier estado acumulado. Lo invoca el orchestrator al finalizar
        /// un período de cooling-off. Monitors sin estado pueden implementar como no-op.
        /// </summary>
        void Reset();
    }
}
```

### Archivo 3: CREAR `Trading.Domain/Abstractions/IRiskAction.cs`

```csharp
namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato de la acción ejecutada cuando se activa el kill switch.
    ///
    /// Hoy hay una sola implementación: LiquidateAllRiskAction.
    /// En el futuro podría haber acciones más sutiles (cerrar solo cierto símbolo,
    /// reducir leverage, etc.).
    /// </summary>
    public interface IRiskAction
    {
        /// <summary>
        /// Ejecuta la acción de mitigación. La idempotencia se garantiza en el orchestrator
        /// vía el flag IsKillSwitchActivated — esta interfaz no requiere ser idempotente.
        /// </summary>
        void Execute();
    }
}
```

### Archivo 4: CREAR `Trading.Application/Risk/DrawdownMonitor.cs`

```csharp
using System;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Monitor de riesgo por drawdown del portfolio respecto a su máximo histórico.
    ///
    /// Mantiene un high-water mark interno. Cada evaluación calcula el drawdown actual
    /// (max - current) / max. Si supera maximumDrawdownFraction, dispara kill switch.
    ///
    /// El high-water mark se inicializa con InitializeWithCurrentValue(), llamado una vez
    /// tras la construcción cuando el portfolio ya está poblado con el cash inicial.
    /// </summary>
    public sealed class DrawdownMonitor : IRiskMonitor
    {
        private readonly IPortfolioState _portfolioState;
        private readonly decimal _maximumDrawdownFraction;
        private decimal _maximumPortfolioValue;

        public string MonitorName => "DrawdownMonitor";

        public DrawdownMonitor(IPortfolioState portfolioState, decimal maximumDrawdownFraction)
        {
            _portfolioState = portfolioState ?? throw new ArgumentNullException(nameof(portfolioState));
            _maximumDrawdownFraction = maximumDrawdownFraction;
        }

        /// <summary>
        /// Inicializa el high-water mark con el valor actual del portfolio.
        /// Llamar una vez tras la construcción, cuando el cash inicial ya fue depositado.
        /// </summary>
        public void InitializeWithCurrentValue()
        {
            _maximumPortfolioValue = _portfolioState.TotalPortfolioValue;
        }

        public RiskAssessment Evaluate()
        {
            decimal currentPortfolioValue = _portfolioState.TotalPortfolioValue;

            if (currentPortfolioValue > _maximumPortfolioValue)
            {
                _maximumPortfolioValue = currentPortfolioValue;
            }

            if (_maximumPortfolioValue == 0m)
            {
                return RiskAssessment.Pass();
            }

            decimal currentDrawdown =
                (_maximumPortfolioValue - currentPortfolioValue) / _maximumPortfolioValue;

            if (currentDrawdown >= _maximumDrawdownFraction)
            {
                return RiskAssessment.Trigger(
                    RiskLimitBreachReason.MaximumDrawdownExceeded,
                    $"Drawdown actual {currentDrawdown:P2} >= límite {_maximumDrawdownFraction:P2}");
            }

            return RiskAssessment.Pass();
        }

        public void Reset()
        {
            _maximumPortfolioValue = _portfolioState.TotalPortfolioValue;
        }
    }
}
```

### Archivo 5: CREAR `Trading.Application/Risk/ConsecutiveLossesMonitor.cs`

```csharp
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Monitor de riesgo por pérdidas consecutivas. El counter se incrementa cuando el caller
    /// invoca RegisterLoss(). El monitor dispara kill switch cuando el counter alcanza el límite.
    ///
    /// El caller (OrderLifecycleService o quien corresponda) es responsable de invocar
    /// RegisterLoss/RegisterWin cuando un trade se cierra. El monitor no consume eventos
    /// de fills directamente — depende de quien sabe interpretar P&amp;L.
    /// </summary>
    public sealed class ConsecutiveLossesMonitor : IRiskMonitor
    {
        private readonly int _maximumConsecutiveLosses;
        private int _consecutiveLossesCounter;
        private bool _shouldTrigger;
        private string _triggerDescription = string.Empty;

        public string MonitorName => "ConsecutiveLossesMonitor";

        public ConsecutiveLossesMonitor(int maximumConsecutiveLosses)
        {
            _maximumConsecutiveLosses = maximumConsecutiveLosses;
        }

        public void RegisterLoss()
        {
            _consecutiveLossesCounter++;
            if (_consecutiveLossesCounter >= _maximumConsecutiveLosses)
            {
                _shouldTrigger = true;
                _triggerDescription = $"{_maximumConsecutiveLosses} pérdidas consecutivas alcanzadas.";
            }
        }

        public void RegisterWin()
        {
            _consecutiveLossesCounter = 0;
        }

        public RiskAssessment Evaluate()
        {
            if (_shouldTrigger)
            {
                return RiskAssessment.Trigger(
                    RiskLimitBreachReason.ConsecutiveLossesExceeded,
                    _triggerDescription);
            }
            return RiskAssessment.Pass();
        }

        public void Reset()
        {
            _consecutiveLossesCounter = 0;
            _shouldTrigger = false;
            _triggerDescription = string.Empty;
        }
    }
}
```

### Archivo 6: CREAR `Trading.Application/Risk/CoolingOffTracker.cs`

```csharp
using System;
using Trading.Domain.Abstractions;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Componente que rastrea el período de cooling-off tras una activación del kill switch.
    ///
    /// NO implementa IRiskMonitor porque su rol es inverso: señala cuándo el kill switch
    /// debe DESACTIVARSE, no cuándo activarse. El RiskOrchestrator lo consulta cada ciclo.
    /// </summary>
    public sealed class CoolingOffTracker
    {
        private readonly IClock _clock;
        private readonly TimeSpan _coolingOffPeriod;
        private DateTime _coolingOffStartedUtc;
        private bool _isInCoolingOff;

        public CoolingOffTracker(IClock clock, TimeSpan coolingOffPeriod)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _coolingOffPeriod = coolingOffPeriod;
        }

        public void StartCoolingOff()
        {
            _coolingOffStartedUtc = _clock.UtcNow;
            _isInCoolingOff = true;
        }

        public bool HasCoolingOffExpired()
        {
            if (!_isInCoolingOff) return false;
            return _clock.UtcNow - _coolingOffStartedUtc >= _coolingOffPeriod;
        }

        public void Reset()
        {
            _isInCoolingOff = false;
        }
    }
}
```

### Archivo 7: CREAR `Trading.Application/Risk/LiquidateAllRiskAction.cs`

```csharp
using System;
using Trading.Domain.Abstractions;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Acción de mitigación de riesgo que liquida toda la cartera mediante IOrderRouter.
    /// Una sola implementación de IRiskAction por ahora; arquitectura preparada para
    /// variantes futuras (liquidación parcial, reducción de leverage, etc.).
    /// </summary>
    public sealed class LiquidateAllRiskAction : IRiskAction
    {
        private readonly IOrderRouter _orderRouter;

        public LiquidateAllRiskAction(IOrderRouter orderRouter)
        {
            _orderRouter = orderRouter ?? throw new ArgumentNullException(nameof(orderRouter));
        }

        public void Execute()
        {
            _orderRouter.LiquidateAll();
        }
    }
}
```

### Archivo 8: CREAR `Trading.Application/Risk/RiskOrchestrator.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;

namespace Trading.Application.Risk
{
    /// <summary>
    /// Coordinador de los monitors de riesgo y la acción de mitigación.
    ///
    /// Cada ciclo (típicamente una vez por barra) el caller invoca EvaluateAllMonitors().
    /// El orchestrator:
    /// 1. Si el kill switch ya está activo: chequea el cooling-off. Si expiró, desactiva
    ///    el kill switch y resetea todos los monitors.
    /// 2. Si el kill switch NO está activo: itera los monitors, recoge el primer veredicto
    ///    Trigger (si lo hay) y activa el kill switch.
    ///
    /// Publica RiskLimitBreachedEvent al activar el kill switch.
    /// </summary>
    public sealed class RiskOrchestrator
    {
        private readonly IReadOnlyList<IRiskMonitor> _monitors;
        private readonly IRiskAction _riskAction;
        private readonly CoolingOffTracker _coolingOffTracker;
        private readonly IClock _clock;
        private readonly ITradingLogger _logger;
        private readonly IDomainEventBus _eventBus;

        public bool IsKillSwitchActivated { get; private set; }

        public RiskOrchestrator(
            IEnumerable<IRiskMonitor> monitors,
            IRiskAction riskAction,
            CoolingOffTracker coolingOffTracker,
            IClock clock,
            ITradingLogger logger,
            IDomainEventBus eventBus)
        {
            _monitors = (monitors ?? throw new ArgumentNullException(nameof(monitors))).ToList();
            _riskAction = riskAction ?? throw new ArgumentNullException(nameof(riskAction));
            _coolingOffTracker = coolingOffTracker ?? throw new ArgumentNullException(nameof(coolingOffTracker));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// Punto de entrada principal. Llamar una vez por ciclo (típicamente por barra).
        /// </summary>
        public void EvaluateAllMonitors()
        {
            if (IsKillSwitchActivated)
            {
                EvaluateCoolingOffPeriod();
                return;
            }

            foreach (var monitor in _monitors)
            {
                var assessment = monitor.Evaluate();
                if (assessment.ShouldTriggerKillSwitch)
                {
                    ActivateKillSwitch(assessment.Reason, assessment.Description, monitor.MonitorName);
                    return;
                }
            }
        }

        /// <summary>
        /// Activa el kill switch externamente (no a través de un monitor). Razón asociada: Manual.
        /// </summary>
        public void ActivateKillSwitchManually(string description)
        {
            ActivateKillSwitch(RiskLimitBreachReason.Manual, description, "Manual");
        }

        private void ActivateKillSwitch(RiskLimitBreachReason reason, string description, string sourceMonitorName)
        {
            IsKillSwitchActivated = true;
            _coolingOffTracker.StartCoolingOff();

            _eventBus.Publish(new RiskLimitBreachedEvent(
                TimestampUtc: _clock.UtcNow,
                Reason: reason,
                Description: description));

            _riskAction.Execute();

            _logger.Critical(
                "KILL SWITCH ACTIVADO por {SourceMonitor}. Motivo: {Reason}. Detalle: {Description}.",
                sourceMonitorName, reason, description);
        }

        private void EvaluateCoolingOffPeriod()
        {
            if (_coolingOffTracker.HasCoolingOffExpired())
            {
                IsKillSwitchActivated = false;
                _coolingOffTracker.Reset();

                foreach (var monitor in _monitors)
                {
                    monitor.Reset();
                }

                _logger.Info("Cooling-off finalizó. Kill switch desactivado y monitors reseteados.");
            }
        }
    }
}
```

### Archivo 9: BORRAR `Trading.Application/Risk/KillSwitchManager.cs`

**ANTES de borrarlo:** leer el archivo y anotar los siguientes valores que vamos a necesitar replicar en el wiring nuevo:
- `maximumDrawdownFraction` (o equivalente).
- `maximumConsecutiveLosses` (o equivalente).
- `coolingOffPeriod` (o equivalente).

Si esos valores vienen como parámetros del constructor (no hardcodeados internamente), localizar dónde se construye el `KillSwitchManager` en `TradingAlgorithmHost` para preservarlos.

### Archivo 10: MODIFICAR `Trading.Application/Execution/BarProcessingService.cs`

Cambios:
- Reemplazar el campo `_killSwitchManager` por `_riskOrchestrator` (tipo `RiskOrchestrator`).
- Cambiar el parámetro del constructor.
- Donde se llamaba `_killSwitchManager.IsKillSwitchActivated`, llamar `_riskOrchestrator.IsKillSwitchActivated`.
- Si `BarProcessingService` invocaba métodos del `KillSwitchManager` para chequeos (drawdown, cooling-off), reemplazar TODAS esas llamadas por una sola: `_riskOrchestrator.EvaluateAllMonitors()` al inicio de `ProcessBar`. El orquestador se encarga internamente.

### Archivo 11: MODIFICAR `Trading.Application/Execution/OrderLifecycleService.cs`

Si `OrderLifecycleService` invocaba `_killSwitchManager.RegisterLoss()` cuando detectaba un trade perdedor:
- Reemplazar la dependencia de `KillSwitchManager` por **`ConsecutiveLossesMonitor`** (tipo concreto, NO interfaz).
- Invocar `RegisterLoss()` y `RegisterWin()` directamente sobre el monitor concreto.

Justificación del acoplamiento al tipo concreto: la semántica "registrar pérdida" es específica de ese monitor. Acoplar el tipo es preferible a contaminar la interfaz `IRiskMonitor` con métodos que no aplican a otros monitors.

### Archivo 12: MODIFICAR `Trading.Strategies/TradingAlgorithmHost.cs`

Reemplazar el wiring del antiguo `KillSwitchManager` por el nuevo. Estructura:

```csharp
// Eliminar:
//   _killSwitchManager = new KillSwitchManager(...);
//   _killSwitchManager.InitializePortfolioValue();

// Crear los componentes nuevos:
var drawdownMonitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: <VALOR_PRESERVADO>);
var consecutiveLossesMonitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: <VALOR_PRESERVADO>);
var coolingOffTracker = new CoolingOffTracker(_clock, <VALOR_PRESERVADO>);
var liquidateAction = new LiquidateAllRiskAction(_orderRouter);

var monitors = new List<IRiskMonitor> { drawdownMonitor, consecutiveLossesMonitor };

_riskOrchestrator = new RiskOrchestrator(
    monitors, liquidateAction, coolingOffTracker, _clock, _logger, domainEventBus);

drawdownMonitor.InitializeWithCurrentValue();

// Pasar el orchestrator a BarProcessingService.
// Pasar consecutiveLossesMonitor a OrderLifecycleService.
```

Cambiar el campo del host: `private KillSwitchManager _killSwitchManager;` → `private RiskOrchestrator _riskOrchestrator;`. Conservar también referencias necesarias a `consecutiveLossesMonitor` y `drawdownMonitor` como campos si hace falta para otros wirings.

En `OnData(Slice data)`, donde estaba:
```csharp
_killSwitchManager.EvaluateCoolingOffPeriod();
if (_killSwitchManager.IsKillSwitchActivated) return;
_killSwitchManager.CheckDrawdownKillSwitch();
```

Reemplazar por:
```csharp
_riskOrchestrator.EvaluateAllMonitors();
if (_riskOrchestrator.IsKillSwitchActivated) return;
```

**Importante:** los valores numéricos (`maximumDrawdownFraction`, `maximumConsecutiveLosses`, `coolingOffPeriod`) deben replicarse EXACTAMENTE como estaban en el wiring del `KillSwitchManager` viejo. NO inventar valores nuevos. Si los valores anteriores no pueden localizarse, parar y reportar antes de borrar el `KillSwitchManager`.

### Archivo 13: BORRAR `Trading.Application.Tests/Risk/KillSwitchManagerTests.cs` (si existe)

Borrar el archivo entero. Va a ser reemplazado por tests granulares por componente.

### Archivo 14: CREAR `Trading.Application.Tests/Fakes/FakeRiskMonitor.cs` y `FakeRiskAction.cs`

```csharp
using Trading.Domain.Abstractions;

namespace Trading.Application.Tests.Fakes
{
    public class FakeRiskMonitor : IRiskMonitor
    {
        public string MonitorName { get; }
        public RiskAssessment NextAssessment { get; set; } = RiskAssessment.Pass();
        public int EvaluateCallCount { get; private set; }
        public int ResetCallCount { get; private set; }

        public FakeRiskMonitor(string monitorName = "FakeMonitor")
        {
            MonitorName = monitorName;
        }

        public RiskAssessment Evaluate()
        {
            EvaluateCallCount++;
            return NextAssessment;
        }

        public void Reset()
        {
            ResetCallCount++;
            NextAssessment = RiskAssessment.Pass();
        }
    }

    public class FakeRiskAction : IRiskAction
    {
        public int ExecuteCallCount { get; private set; }

        public void Execute()
        {
            ExecuteCallCount++;
        }
    }
}
```

### Archivo 15: CREAR `Trading.Application.Tests/Risk/DrawdownMonitorTests.cs`

```csharp
using FluentAssertions;
using Trading.Application.Risk;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Events;
using Xunit;

namespace Trading.Application.Tests.Risk
{
    public class DrawdownMonitorTests
    {
        private readonly FakePortfolioState _portfolioState = new();

        [Fact]
        public void Evaluate_BelowMaximumDrawdown_ReturnsPass()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 90_000m;
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_AtOrAboveMaximumDrawdown_TriggersKillSwitch()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 74_000m;
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeTrue();
            assessment.Reason.Should().Be(RiskLimitBreachReason.MaximumDrawdownExceeded);
        }

        [Fact]
        public void Evaluate_PortfolioGrows_UpdatesHighWaterMark()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 120_000m;
            monitor.Evaluate();

            _portfolioState.TotalPortfolioValue = 95_000m;
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Reset_UsesCurrentValueAsNewHighWaterMark()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 74_000m;
            monitor.Reset();

            _portfolioState.TotalPortfolioValue = 60_000m;
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }
    }
}
```

### Archivo 16: CREAR `Trading.Application.Tests/Risk/ConsecutiveLossesMonitorTests.cs`

```csharp
using FluentAssertions;
using Trading.Application.Risk;
using Trading.Domain.Events;
using Xunit;

namespace Trading.Application.Tests.Risk
{
    public class ConsecutiveLossesMonitorTests
    {
        [Fact]
        public void Evaluate_NoLossesRegistered_ReturnsPass()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_BelowThreshold_ReturnsPass()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            monitor.RegisterLoss();
            monitor.RegisterLoss();

            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_AtThreshold_TriggersKillSwitch()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            monitor.RegisterLoss();
            monitor.RegisterLoss();
            monitor.RegisterLoss();

            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeTrue();
            assessment.Reason.Should().Be(RiskLimitBreachReason.ConsecutiveLossesExceeded);
        }

        [Fact]
        public void RegisterWin_ResetsCounter()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            monitor.RegisterLoss();
            monitor.RegisterLoss();
            monitor.RegisterWin();
            monitor.RegisterLoss();

            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Reset_ClearsCounterAndTriggerState()
        {
            var monitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 3);
            monitor.RegisterLoss();
            monitor.RegisterLoss();
            monitor.RegisterLoss();
            monitor.Reset();

            var assessment = monitor.Evaluate();
            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }
    }
}
```

### Archivo 17: CREAR `Trading.Application.Tests/Risk/RiskOrchestratorTests.cs`

```csharp
using FluentAssertions;
using System;
using System.Collections.Generic;
using Trading.Application.Eventing;
using Trading.Application.Risk;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Abstractions;
using Trading.Domain.Events;
using Xunit;

namespace Trading.Application.Tests.Risk
{
    public class RiskOrchestratorTests
    {
        private readonly FakeClock _clock = new();
        private readonly FakeTradingLogger _logger = new();
        private readonly FakeRiskAction _riskAction = new();
        private readonly DomainEventBus _eventBus;

        public RiskOrchestratorTests()
        {
            _eventBus = new DomainEventBus(_logger);
        }

        private RiskOrchestrator BuildOrchestrator(IEnumerable<IRiskMonitor> monitors, TimeSpan? coolingOff = null)
        {
            var tracker = new CoolingOffTracker(_clock, coolingOff ?? TimeSpan.FromHours(24));
            return new RiskOrchestrator(monitors, _riskAction, tracker, _clock, _logger, _eventBus);
        }

        [Fact]
        public void EvaluateAllMonitors_AllPass_DoesNothing()
        {
            var monitor = new FakeRiskMonitor();
            var orchestrator = BuildOrchestrator(new[] { monitor });

            orchestrator.EvaluateAllMonitors();

            orchestrator.IsKillSwitchActivated.Should().BeFalse();
            _riskAction.ExecuteCallCount.Should().Be(0);
        }

        [Fact]
        public void EvaluateAllMonitors_OneTriggers_ActivatesKillSwitchAndPublishesEvent()
        {
            var monitor = new FakeRiskMonitor("DrawdownFake")
            {
                NextAssessment = RiskAssessment.Trigger(RiskLimitBreachReason.MaximumDrawdownExceeded, "test")
            };
            var orchestrator = BuildOrchestrator(new[] { monitor });
            var captured = new CapturingEventSubscriber<RiskLimitBreachedEvent>(_eventBus);

            orchestrator.EvaluateAllMonitors();

            orchestrator.IsKillSwitchActivated.Should().BeTrue();
            _riskAction.ExecuteCallCount.Should().Be(1);
            captured.CapturedEvents.Should().HaveCount(1);
            captured.CapturedEvents[0].Reason.Should().Be(RiskLimitBreachReason.MaximumDrawdownExceeded);
        }

        [Fact]
        public void EvaluateAllMonitors_WhenKillSwitchActive_SkipsMonitorEvaluation()
        {
            var monitor = new FakeRiskMonitor
            {
                NextAssessment = RiskAssessment.Trigger(RiskLimitBreachReason.Manual, "")
            };
            var orchestrator = BuildOrchestrator(new[] { monitor });

            orchestrator.EvaluateAllMonitors();
            int callsBeforeSecondEvaluate = monitor.EvaluateCallCount;

            orchestrator.EvaluateAllMonitors();
            monitor.EvaluateCallCount.Should().Be(callsBeforeSecondEvaluate);
        }

        [Fact]
        public void EvaluateAllMonitors_AfterCoolingOffExpires_DeactivatesAndResetsMonitors()
        {
            _clock.UtcNow = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var monitor = new FakeRiskMonitor
            {
                NextAssessment = RiskAssessment.Trigger(RiskLimitBreachReason.Manual, "")
            };
            var orchestrator = BuildOrchestrator(new[] { monitor }, coolingOff: TimeSpan.FromHours(1));

            orchestrator.EvaluateAllMonitors();
            orchestrator.IsKillSwitchActivated.Should().BeTrue();

            _clock.UtcNow = new DateTime(2025, 1, 1, 13, 1, 0, DateTimeKind.Utc);
            orchestrator.EvaluateAllMonitors();

            orchestrator.IsKillSwitchActivated.Should().BeFalse();
            monitor.ResetCallCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public void ActivateKillSwitchManually_UsesManualReason()
        {
            var orchestrator = BuildOrchestrator(new List<IRiskMonitor>());
            var captured = new CapturingEventSubscriber<RiskLimitBreachedEvent>(_eventBus);

            orchestrator.ActivateKillSwitchManually("test reason");

            orchestrator.IsKillSwitchActivated.Should().BeTrue();
            captured.CapturedEvents[0].Reason.Should().Be(RiskLimitBreachReason.Manual);
            captured.CapturedEvents[0].Description.Should().Be("test reason");
        }
    }
}
```

### Archivo 18: MODIFICAR `ROADMAP.md`

1. Borrar la fila de "Refactor #4 — Separar IRiskMonitor de IRiskAction" de la sección "Refactors pendientes" (Bloque 2).
2. En el diagrama del "Plan general", marcar `Refactor #4` con ✅ visible.
3. Agregar al **final** de "Historial completado" la entrada nueva:

```markdown
### ✅ Refactor #4 — Separar IRiskMonitor de IRiskAction
**Fecha:** <usar fecha de hoy en formato YYYY-MM-DD>
**Resumen:** KillSwitchManager (que mezclaba detección y acción) descompuesto en componentes
con responsabilidad única: IRiskMonitor (detección) + IRiskAction (mitigación) + RiskOrchestrator
(coordinación). Tres componentes de risk: DrawdownMonitor, ConsecutiveLossesMonitor (ambos
IRiskMonitor) y CoolingOffTracker (componente separado porque señala desactivación, no activación).
LiquidateAllRiskAction como única implementación de IRiskAction. El sistema queda preparado para
Hito B: agregar RegimeIncompatibilityMonitor será crear una clase nueva sin modificar nada
existente (open-closed). 14 tests nuevos. Backtest produce operaciones idénticas (162).
```

### Archivo 19: MODIFICAR `DECISIONS.md`

Agregar **ADR-015** al inicio del archivo (después del template), antes del ADR-014:

```markdown
## ADR-015 — Separación de responsabilidades en gestión de riesgo: IRiskMonitor / IRiskAction / Orchestrator
**Fecha:** <usar fecha de hoy en formato YYYY-MM-DD>
**Estado:** Aceptada

### Contexto
El KillSwitchManager original mezclaba detección de condiciones de riesgo (drawdown, pérdidas
consecutivas, cooling-off) y acción de respuesta (liquidar cartera, marcar flag, publicar evento)
en una sola clase. Antes del Hito B (regímenes de mercado), se anticipaba un cuarto motivo de
kill que escalaría mal en el monolito.

### Decisión
Aplicar SRP: separar en componentes especializados.
- IRiskMonitor: contrato de detección. Cada implementación chequea UNA condición.
- IRiskAction: contrato de mitigación. Hoy una sola implementación (LiquidateAllRiskAction).
- RiskOrchestrator: coordinador. Itera monitors, delega a action, gestiona kill switch state,
  publica eventos.
- CoolingOffTracker: componente separado (NO implementa IRiskMonitor) porque señala
  desactivación, no activación.

### Alternativas consideradas
- A: Mantener monolítico y agregar cuarto chequeo cuando llegue. Descartada: empeora SRP cada vez.
- B: Migración parcial (solo extraer DrawdownMonitor). Descartada: sistema inconsistente con
  dos patrones coexistiendo.
- C (elegida): Migración completa con SRP estricto.

### Consecuencias
- Agregar futuras condiciones de riesgo es trivial: clase que implementa IRiskMonitor + registro.
  Cero modificación del código existente.
- Tests más granulares y rápidos.
- CoolingOffTracker rompe la simetría de IRiskMonitor — decisión consciente.
- ConsecutiveLossesMonitor se inyecta como tipo concreto en OrderLifecycleService porque
  RegisterLoss/RegisterWin son específicos de ese monitor.
```

---

## Reporte final esperado del asistente

Al terminar todas las modificaciones, el asistente entrega un único mensaje en el chat con esta estructura:

```
Archivos creados:
- Trading.Domain/Abstractions/RiskAssessment.cs
- Trading.Domain/Abstractions/IRiskMonitor.cs
- Trading.Domain/Abstractions/IRiskAction.cs
- Trading.Application/Risk/DrawdownMonitor.cs
- Trading.Application/Risk/ConsecutiveLossesMonitor.cs
- Trading.Application/Risk/CoolingOffTracker.cs
- Trading.Application/Risk/LiquidateAllRiskAction.cs
- Trading.Application/Risk/RiskOrchestrator.cs
- Trading.Application.Tests/Fakes/FakeRiskMonitor.cs (incluye FakeRiskAction)
- Trading.Application.Tests/Risk/DrawdownMonitorTests.cs
- Trading.Application.Tests/Risk/ConsecutiveLossesMonitorTests.cs
- Trading.Application.Tests/Risk/RiskOrchestratorTests.cs

Archivos modificados:
- Trading.Application/Execution/BarProcessingService.cs
- Trading.Application/Execution/OrderLifecycleService.cs
- Trading.Strategies/TradingAlgorithmHost.cs
- ROADMAP.md
- DECISIONS.md

Archivos borrados:
- Trading.Application/Risk/KillSwitchManager.cs
- Trading.Application.Tests/Risk/KillSwitchManagerTests.cs (si existía)

Valores preservados del wiring viejo (replicados exactamente):
- maximumDrawdownFraction: <valor encontrado>
- maximumConsecutiveLosses: <valor encontrado>
- coolingOffPeriod: <valor encontrado>

Tests nuevos esperados verde: ~14
- DrawdownMonitorTests: 4
- ConsecutiveLossesMonitorTests: 5
- RiskOrchestratorTests: 5

Verificaciones que debe hacer el usuario:
1. Compilar la solución.
2. Verificar invariante arquitectónica:
   grep -rn "^using QuantConnect" Trading.Domain/ Trading.Application/ Trading.Application.Tests/
   (No debe devolver nada.)
3. Verificar que no quedan referencias huérfanas al KillSwitchManager:
   grep -rn "KillSwitchManager" Trading.Domain/ Trading.Application/ Trading.Strategies/ Trading.Application.Tests/
   (No debe devolver nada.)
4. Ejecutar todos los tests. Esperar todos verdes, incluyendo los ~14 nuevos.
5. Correr el backtest. Verificar que totalNumberOfTrades es idéntico al backtest previo (162).

Si todas las verificaciones pasan, el usuario commitea los cambios desde Git.
Si algo falla, el usuario revierte desde Git y reporta el error.
```

Tras entregar el reporte, el asistente se detiene. No ejecuta comandos adicionales. No espera input para continuar — terminó.
