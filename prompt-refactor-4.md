# Refactor #4 — Separar IRiskMonitor de IRiskAction

## Contexto del proyecto

Sistema de trading sistemático en C# / .NET 10 sobre QuantConnect/Lean. Cuatro proyectos:

- **Trading.Domain** — capa de dominio, CERO `using QuantConnect`.
- **Trading.Application** — orquestación pura, CERO `using QuantConnect`.
- **Trading.Strategies** — adaptadores Lean. Único proyecto con `using QuantConnect`.
- **Trading.Application.Tests** — tests xUnit.

**Invariante arquitectónica crítica:** Trading.Domain y Trading.Application NO deben tener ningún `using QuantConnect` en ningún archivo.

Documentos de referencia en la raíz del repo (LEER antes de empezar):
- `AI.md` — reglas de estilo y arquitectura.
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

- **D1 — Migración completa, no parcial:** los tres chequeos actuales (`CheckDrawdownKillSwitch`, `RegisterLoss` con consecutive losses, `EvaluateCoolingOffPeriod`) se extraen cada uno en su propio `IRiskMonitor`. El `KillSwitchManager` actual desaparece — su lógica se distribuye en tres monitors + un orchestrator.

- **D2 — Resultado del monitor:** cada monitor devuelve un `RiskAssessment` por evaluación con:
  - `bool ShouldTriggerKillSwitch`
  - `RiskLimitBreachReason Reason` (enum ya existente del refactor B3)
  - `string Description` (para log y evento)
  
  Diseño: `readonly record struct`. Pequeño, inmutable, frecuencia alta de uso.

- **D3 — Estado por monitor:** cada monitor mantiene su propio estado interno (ej. `_consecutiveLossesCounter` vive en `ConsecutiveLossesMonitor`, no en el orchestrator). Cada monitor se inyecta vía constructor del orchestrator.

- **D4 — Modelo de "señal" de monitor → orquestador:** los monitors NO ejecutan la acción directamente. Devuelven el `RiskAssessment` al orchestrator, que decide. Esto desacopla las dos responsabilidades.

- **D5 — `RegisterLoss` y `RegisterPortfolioValueUpdate`:** los monitors que necesitan inputs externos los exponen como métodos públicos. El orchestrator NO los conoce — los expone hacia afuera con métodos delegados. El caller (`OrderLifecycleService`) sigue llamando al orchestrator, no a los monitors directamente.

- **D6 — Activación/desactivación del kill switch:** vive en el orchestrator (un solo lugar). El estado `IsKillSwitchActivated` es del orchestrator, no de los monitors.

- **D7 — Cooling-off period:** mantiene comportamiento actual — al expirar el cooling-off, se desactiva el kill switch y se resetea estado. La lógica de reseteo vive en el `CoolingOffMonitor`, que tiene una semántica especial: cuando detecta que expiró, llama `Reset()` en los otros monitors (vía interfaz extendida `IResettableMonitor`) o el orchestrator hace el reseteo. **Detalle: el orchestrator hace el reseteo** — el monitor de cooling-off solo señala "ya expiró, podés desactivar el kill".

- **D8 — Nombre de la clase orchestrator:** `RiskOrchestrator`. Más claro que `KillSwitchManager` (que sugiere que es solo del kill switch, cuando ahora es coordinador de monitors).

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
        /// <summary>
        /// Identificador legible del monitor para logs y diagnóstico. No tiene significado funcional.
        /// </summary>
        string MonitorName { get; }

        /// <summary>
        /// Evalúa las condiciones actuales y devuelve el veredicto.
        /// Se invoca por el orchestrator en cada ciclo de chequeo (típicamente cada barra).
        /// </summary>
        RiskAssessment Evaluate();

        /// <summary>
        /// Resetea cualquier estado acumulado en el monitor. Lo invoca el orchestrator al
        /// finalizar un período de cooling-off (cuando se desactiva el kill switch).
        /// Los monitors sin estado pueden implementar como no-op.
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
    /// Hoy hay una sola implementación: LiquidateAllRiskAction (delega a IOrderRouter.LiquidateAll).
    /// En el futuro podría haber acciones más sutiles (cerrar solo cierto símbolo, reducir leverage, etc.).
    /// </summary>
    public interface IRiskAction
    {
        /// <summary>
        /// Ejecuta la acción de mitigación. Idempotente: invocarla múltiples veces no debe
        /// producir efectos adicionales después de la primera (la lógica de "ya se ejecutó"
        /// vive en el orchestrator vía el flag IsKillSwitchActivated).
        /// </summary>
        void Execute();
    }
}
```

### Archivo 4: CREAR `Trading.Application/Risk/DrawdownMonitor.cs`

Mover la lógica de `CheckDrawdownKillSwitch` del actual `KillSwitchManager` acá.

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
    /// El high-water mark se inicializa con el valor del portfolio en la construcción
    /// (se inyecta vía InitializeWithCurrentValue, no por constructor para mantener
    /// el wiring simple).
    /// </summary>
    public sealed class DrawdownMonitor : IRiskMonitor
    {
        private readonly IPortfolioState _portfolioState;
        private readonly decimal _maximumDrawdownFraction;
        private decimal _maximumPortfolioValue;

        public string MonitorName => "DrawdownMonitor";

        /// <param name="portfolioState">Fuente del valor actual del portfolio.</param>
        /// <param name="maximumDrawdownFraction">
        /// Fracción decimal del drawdown máximo tolerado (ej. 0.25m = 25%).
        /// Si el drawdown observado iguala o supera este valor, se dispara kill switch.
        /// </param>
        public DrawdownMonitor(IPortfolioState portfolioState, decimal maximumDrawdownFraction)
        {
            _portfolioState = portfolioState ?? throw new ArgumentNullException(nameof(portfolioState));
            _maximumDrawdownFraction = maximumDrawdownFraction;
        }

        /// <summary>
        /// Inicializa el high-water mark con el valor actual del portfolio.
        /// Llamar una vez tras la construcción, cuando el portfolio ya está poblado con el cash inicial.
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

Mover la lógica de `RegisterLoss` + chequeo de consecutive losses.

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
    /// RegisterLoss cuando un trade se cierra en pérdida. El monitor no consume eventos de fills
    /// directamente — depende de quien sabe interpretar P&amp;L.
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

        /// <summary>
        /// Notifica al monitor que un trade se cerró en pérdida.
        /// </summary>
        public void RegisterLoss()
        {
            _consecutiveLossesCounter++;
            if (_consecutiveLossesCounter >= _maximumConsecutiveLosses)
            {
                _shouldTrigger = true;
                _triggerDescription = $"{_maximumConsecutiveLosses} pérdidas consecutivas alcanzadas.";
            }
        }

        /// <summary>
        /// Notifica al monitor que un trade se cerró en ganancia. Resetea el counter.
        /// </summary>
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

### Archivo 6: CREAR `Trading.Application/Risk/CoolingOffMonitor.cs`

El cooling-off NO dispara kill switch — al revés, **señala cuándo el kill switch debe DESACTIVARSE**. Por lo tanto NO encaja en la interfaz `IRiskMonitor.Evaluate()` que devuelve "trigger / pass".

**Decisión:** este componente es un tipo distinto. NO implementa `IRiskMonitor`. Se llama directamente desde el orchestrator. La interfaz queda dedicada a monitors que pueden disparar kill switch.

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
    /// 
    /// Comportamiento:
    /// - StartCoolingOff(): registra el timestamp de inicio.
    /// - HasCoolingOffExpired(): devuelve true si transcurrió el período configurado.
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
    /// Una sola implementación de IRiskAction por ahora; arquitectura preparada para variantes futuras.
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

El nuevo cerebro. Reemplaza al `KillSwitchManager`.

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
    /// Publicación de eventos: emite RiskLimitBreachedEvent al activar el kill switch.
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
                    return; // Una vez activado, no seguir evaluando este ciclo.
                }
            }
        }

        /// <summary>
        /// Activa el kill switch manualmente (no a través de un monitor). Útil para activación
        /// externa o de testing. Razón asociada: Manual.
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

Eliminar completamente. Su funcionalidad está distribuida en los archivos creados arriba.

### Archivo 10: MODIFICAR `Trading.Application/Execution/BarProcessingService.cs`

Cambiar la dependencia de `KillSwitchManager` por `RiskOrchestrator`.

- Reemplazar el campo `_killSwitchManager` por `_riskOrchestrator`.
- Cambiar el parámetro del constructor.
- En `ProcessBar`, donde se llamaba `_killSwitchManager.IsKillSwitchActivated`, llamar `_riskOrchestrator.IsKillSwitchActivated`. 

Si `BarProcessingService` invocaba `_killSwitchManager.CheckDrawdownKillSwitch()` o `_killSwitchManager.EvaluateCoolingOffPeriod()`, reemplazar TODAS esas llamadas por una sola `_riskOrchestrator.EvaluateAllMonitors()` al inicio de `ProcessBar`. El orquestador se encarga internamente.

### Archivo 11: MODIFICAR `Trading.Application/Execution/OrderLifecycleService.cs`

Si `OrderLifecycleService` invocaba `_killSwitchManager.RegisterLoss()` cuando detectaba un trade perdedor, reemplazar por una dependencia al `ConsecutiveLossesMonitor` específicamente (NO al orchestrator — el orchestrator no expone `RegisterLoss`).

**Diseño:** inyectar `ConsecutiveLossesMonitor` directamente como dependencia adicional del `OrderLifecycleService`. Razón: la semántica "registrar pérdida" es específica de ese monitor, no general del orchestrator. Acoplar tipos concretos en este caso es preferible a contaminar la interfaz general.

Si `OrderLifecycleService` también registra ganancias (no estoy seguro), agregar también `RegisterWin()` allí.

### Archivo 12: MODIFICAR `Trading.Strategies/TradingAlgorithmHost.cs`

Reemplazar el wiring del antiguo `KillSwitchManager` por el nuevo:

Reemplazar:
```csharp
_killSwitchManager = new KillSwitchManager(_portfolioState, _orderRouter, _clock, _logger, domainEventBus);
// ...
_killSwitchManager.InitializePortfolioValue();
```

Por:
```csharp
var drawdownMonitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
var consecutiveLossesMonitor = new ConsecutiveLossesMonitor(maximumConsecutiveLosses: 5);
// (Los valores 0.25 y 5 deben coincidir con los que usaba el KillSwitchManager anterior — VERIFICAR antes.)

var coolingOffTracker = new CoolingOffTracker(_clock, TimeSpan.FromHours(24));
// (El período 24h debe coincidir con el que usaba el KillSwitchManager — VERIFICAR.)

var liquidateAction = new LiquidateAllRiskAction(_orderRouter);

var monitors = new List<IRiskMonitor> { drawdownMonitor, consecutiveLossesMonitor };

_riskOrchestrator = new RiskOrchestrator(
    monitors, liquidateAction, coolingOffTracker, _clock, _logger, domainEventBus);

// Llamar después de SetCash, igual que antes:
drawdownMonitor.InitializeWithCurrentValue();
```

Cambiar el tipo del campo del host: `private KillSwitchManager _killSwitchManager;` → `private RiskOrchestrator _riskOrchestrator;`.

Cambiar el wiring de los servicios que dependían de `_killSwitchManager`: pasarles `_riskOrchestrator` (o `consecutiveLossesMonitor` específicamente para `OrderLifecycleService`).

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

**IMPORTANTE:** los valores `maximumDrawdownFraction`, `maximumConsecutiveLosses`, `coolingOffPeriod` que usaba el `KillSwitchManager` anterior pueden estar hardcodeados en el constructor antiguo o pasados desde otro lugar. ANTES DE BORRAR `KillSwitchManager.cs`, leer los defaults que usa y replicarlos en el wiring nuevo. Si son configurables vía constructor del `TradingAlgorithmHost`, mantener la misma fuente.

### Archivo 13: MODIFICAR tests existentes de `KillSwitchManagerTests`

El archivo `Trading.Application.Tests/Risk/KillSwitchManagerTests.cs` (si existe) referencia tipos que vamos a borrar. Tres opciones:

1. **Renombrar a `RiskOrchestratorTests`** y adaptar todos los tests al nuevo orchestrator + monitors. Trabajo grande.
2. **Borrar el archivo** y escribir tests nuevos directamente para los componentes nuevos.
3. **Borrar y crear nuevos archivos por componente:** `DrawdownMonitorTests`, `ConsecutiveLossesMonitorTests`, `RiskOrchestratorTests`.

**Voto:** opción 3. Tests unitarios por componente son más mantenibles y dan cobertura más granular. Borrar `KillSwitchManagerTests.cs` completamente y crear:

#### `Trading.Application.Tests/Risk/DrawdownMonitorTests.cs`

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

            _portfolioState.TotalPortfolioValue = 90_000m; // 10% drawdown
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }

        [Fact]
        public void Evaluate_AtOrAboveMaximumDrawdown_TriggersKillSwitch()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 74_000m; // 26% drawdown
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

            _portfolioState.TotalPortfolioValue = 120_000m; // sube
            monitor.Evaluate(); // actualiza high-water mark a 120k

            _portfolioState.TotalPortfolioValue = 95_000m; // ~20.8% drawdown desde 120k
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeFalse(); // < 25%
        }

        [Fact]
        public void Reset_RestoresHighWaterMarkToCurrentValue()
        {
            _portfolioState.TotalPortfolioValue = 100_000m;
            var monitor = new DrawdownMonitor(_portfolioState, maximumDrawdownFraction: 0.25m);
            monitor.InitializeWithCurrentValue();

            _portfolioState.TotalPortfolioValue = 74_000m;
            monitor.Reset();

            // Ahora 74k es el nuevo máximo. Para gatillar, hay que caer a 74k*0.75=55.5k
            _portfolioState.TotalPortfolioValue = 60_000m; // ~18.9% drawdown desde 74k
            var assessment = monitor.Evaluate();

            assessment.ShouldTriggerKillSwitch.Should().BeFalse();
        }
    }
}
```

#### `Trading.Application.Tests/Risk/ConsecutiveLossesMonitorTests.cs`

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
            monitor.RegisterLoss(); // 2 pérdidas, límite 3

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
            monitor.RegisterLoss(); // counter ahora en 1

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

#### `Trading.Application.Tests/Risk/RiskOrchestratorTests.cs`

Crear tests que cubran:
- `EvaluateAllMonitors` con un monitor que dispara → activa kill switch, publica evento, llama Execute() de la action.
- `EvaluateAllMonitors` con todos pasando → no hace nada.
- Tras activación, las siguientes invocaciones de `EvaluateAllMonitors` solo chequean cooling-off (no llaman a los monitors).
- Tras expirar cooling-off → desactiva kill switch, llama `Reset()` en todos los monitors.
- `ActivateKillSwitchManually` → activa con `Reason.Manual`.

Usar fakes: `FakeRiskMonitor` (configurable para devolver Pass o Trigger), `FakeRiskAction` (registra cuántas veces se llamó Execute), `CapturingEventSubscriber<RiskLimitBreachedEvent>`, `FakeClock`, `FakeTradingLogger`, `DomainEventBus` real.

Si `FakeRiskMonitor` no existe en `Tests/Fakes/`, crearlo:

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

Y los tests del orchestrator (esqueleto — Claude Code completa los detalles):

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
        public void EvaluateAllMonitors_OneTriggers_ActivatesKillSwitch()
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

            orchestrator.EvaluateAllMonitors(); // activa
            int callsBeforeSecondEvaluate = monitor.EvaluateCallCount;

            orchestrator.EvaluateAllMonitors(); // ya activo, no debe llamar
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

            orchestrator.EvaluateAllMonitors(); // activa
            orchestrator.IsKillSwitchActivated.Should().BeTrue();

            _clock.UtcNow = new DateTime(2025, 1, 1, 13, 1, 0, DateTimeKind.Utc); // +1h 1min
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

---

## Verificaciones finales obligatorias

1. **Compilación limpia** sin errores ni warnings nuevos.

2. **Invariante arquitectónica:**
   ```bash
   grep -rn "^using QuantConnect" Trading.Domain/ Trading.Application/ Trading.Application.Tests/
   ```
   NO debe devolver nada.

3. **Sin referencias huérfanas al viejo `KillSwitchManager`:**
   ```bash
   grep -rn "KillSwitchManager" Trading.Domain/ Trading.Application/ Trading.Strategies/ Trading.Application.Tests/
   ```
   NO debe devolver nada (debe haber desaparecido por completo).

4. **Tests preexistentes (no relacionados a risk):** todos siguen pasando.

5. **Tests nuevos:** `DrawdownMonitorTests` (~4), `ConsecutiveLossesMonitorTests` (~5), `RiskOrchestratorTests` (~5). Total ~14 tests nuevos.

6. **Comportamiento runtime (backtest):**
   - El sistema arranca normalmente.
   - El número de operaciones generadas debe ser **idéntico** al backtest previo. El refactor NO cambia lógica de decisión, solo cómo está organizada.
   - Si el backtest produce diferente cantidad de operaciones, hay algún detalle (valor de umbral, orden de evaluación) que cambió. Reportar.

## Estilo

- Documentación XML en español.
- FluentAssertions con `Should()` en tests.
- Mantener orden de usings.

## Si encuentras algún problema

- Si el `KillSwitchManager` actual tiene comportamiento que no quedó cubierto en mi descripción (ej. métodos adicionales no contemplados), parar y reportar antes de borrar.
- Si los valores hardcodeados de los umbrales (`maximumDrawdownFraction`, `maximumConsecutiveLosses`, `coolingOffPeriod`) no se pueden localizar en el código actual, parar y preguntar.
- Si el comportamiento del backtest cambia, parar y reportar antes de actualizar tracking.

Ejecutar en este orden:

1. Crear los archivos nuevos del dominio (`RiskAssessment`, `IRiskMonitor`, `IRiskAction`).
2. Crear los componentes de Application (`DrawdownMonitor`, `ConsecutiveLossesMonitor`, `CoolingOffTracker`, `LiquidateAllRiskAction`, `RiskOrchestrator`).
3. Crear los fakes en Tests (`FakeRiskMonitor`, `FakeRiskAction`).
4. Crear los tres archivos de tests (`DrawdownMonitorTests`, `ConsecutiveLossesMonitorTests`, `RiskOrchestratorTests`).
5. Modificar `BarProcessingService`, `OrderLifecycleService`, `TradingAlgorithmHost` para usar el nuevo orchestrator.
6. **Borrar `KillSwitchManager.cs` y `KillSwitchManagerTests.cs`** al final, una vez que nada los referencia.
7. `dotnet clean && dotnet build`.
8. Correr todos los tests.
9. Correr backtest. Verificar cantidad de operaciones idéntica.
10. Reportar resultados.

---

## Actualización de documentación al cierre

Una vez que todas las verificaciones obligatorias pasan, actualizá los archivos de tracking.

### ROADMAP.md

1. Borrar la fila del refactor #4 de la sección "Refactors pendientes" del BLOQUE 2.
2. En el diagrama del "Plan general", marcar `Refactor #4` con ✅ visible.
3. Agregar al final de "Historial completado" una entrada nueva:

   ```markdown
   ### ✅ Refactor #4 — Separar IRiskMonitor de IRiskAction
   **Fecha:** [YYYY-MM-DD]
   **Resumen:** KillSwitchManager (que mezclaba detección y acción) descompuesto en componentes
   con responsabilidad única: IRiskMonitor (detección) + IRiskAction (mitigación) + RiskOrchestrator
   (coordinación). Tres monitors creados: DrawdownMonitor, ConsecutiveLossesMonitor, CoolingOffTracker
   (este último con interfaz distinta porque señala desactivación, no activación). LiquidateAllRiskAction
   como única implementación de IRiskAction por ahora. El sistema queda preparado para Hito B:
   agregar RegimeIncompatibilityMonitor en su momento será literalmente crear una clase nueva sin
   modificar nada existente (open-closed principle). 14 tests nuevos. Backtest produce operaciones
   idénticas.
   ```

### DECISIONS.md

Agregar **ADR-015** al inicio del archivo (después del template):

```markdown
## ADR-015 — Separación de responsabilidades en gestión de riesgo: IRiskMonitor / IRiskAction / Orchestrator
**Fecha:** [YYYY-MM-DD]
**Estado:** Aceptada

### Contexto
El KillSwitchManager original mezclaba dos responsabilidades en una sola clase: (1) detección
de condiciones de riesgo (drawdown, pérdidas consecutivas, cooling-off), (2) acción de respuesta
(liquidar cartera, marcar flag de kill switch, publicar evento). Antes del Hito B (regímenes de
mercado), se anticipaba un cuarto motivo de kill ("régimen incompatible con la estrategia activa")
que escalaría mal en el monolito existente.

### Decisión
Aplicar Single Responsibility Principle: separar el monolito en componentes especializados.

- IRiskMonitor: contrato de detección. Cada implementación chequea UNA condición y devuelve
  RiskAssessment (Pass o Trigger con motivo).
- IRiskAction: contrato de mitigación. Hoy una sola implementación (LiquidateAllRiskAction);
  arquitectura preparada para variantes (liquidación parcial, reducción de leverage, etc.).
- RiskOrchestrator: coordinador. Itera monitors, delega a action, gestiona kill switch state,
  publica eventos. Es el único componente que conoce el flujo completo.
- CoolingOffTracker: componente separado (NO implementa IRiskMonitor). Su rol es inverso —
  señala cuándo desactivar el kill switch, no cuándo activarlo. Forzarlo en la interfaz
  IRiskMonitor habría requerido contorsiones semánticas innecesarias.

### Alternativas consideradas
- **A: Mantener KillSwitchManager monolítico y agregar cuarto chequeo cuando llegue.** Descartada:
  rompía SRP cada vez más, dificultaba testing por clase con muchas responsabilidades.
- **B: Migración parcial (solo extraer DrawdownMonitor, dejar el resto adentro).** Descartada:
  sistema queda inconsistente con dos patrones coexistiendo; el segundo refactor inevitable
  costaría casi lo mismo que hacer todo de una.
- **C (elegida): Migración completa con SRP estricto.** Sistema homogéneo. Open-closed principle
  garantizado para futuros monitors.

### Consecuencias
- Agregar futuras condiciones de riesgo es trivial: crear clase que implementa IRiskMonitor,
  registrarla en el wiring. Cero modificación del código existente.
- Tests más granulares y rápidos (un test por monitor, no por todo el manager).
- El CoolingOffTracker rompe la simetría de la interfaz IRiskMonitor. Decisión consciente:
  forzarlo en la interfaz introducía complejidad mayor que el beneficio de uniformidad.
- ConsecutiveLossesMonitor se inyecta DIRECTO (tipo concreto) en OrderLifecycleService porque
  el método RegisterLoss es específico de ese monitor. Acoplamiento de tipo concreto justificado
  por especificidad semántica.
```

### Verificación final

Mostrar el diff resumido. NO commitear automáticamente — esperar confirmación.

Si las verificaciones del refactor NO pasan, NO actualizar tracking. Reportar error y esperar instrucciones.
