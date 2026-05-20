# INFRA-2 PIEZA B FIX — Heartbeat solo en LiveMode con Timer de wall clock

> **Brief ejecutable para Claude Code CLI.** Fix urgente sobre Pieza B de INFRA-2. El diseño anterior usaba `Schedule.On` con `TimeRules.Every(60s)`, que en backtest se dispara al ritmo del clock simulado: ~650.000 invocaciones del flush durante un backtest de 15 meses, llevando el tiempo de ejecución de 1 minuto a 20+. Este fix sustituye el scheduling por un `System.Threading.Timer` de wall clock real, y deshabilita el flush en backtest (preservando determinismo y performance).
>
> **Pre-requisitos:** Pieza A y Pieza B de INFRA-2 commiteadas. Compilación verde. Tests verdes. El backtest demora >20 minutos (síntoma a corregir).

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio:

- **Cero comandos `git` de cualquier tipo.**
- **No compilar.** El usuario compila.
- **No correr tests.** El usuario corre tests.
- **No ejecutar backtest.** El usuario lo ejecuta.
- **Si Claude Code detecta inconsistencia entre el código actual y este brief, detenerse y reportar. NO improvisar.**
- Al final, proponer el mensaje de commit sugerido.

---

## Contexto del bug

`LeanClock.UtcNow` devuelve `_algorithm.Time`, que en backtest es **tiempo simulado del backtest**, no wall clock. En el código actual de `TradingAlgorithmHost`:

```csharp
Schedule.On(
    DateRules.EveryDay(),
    TimeRules.Every(TimeSpan.FromSeconds(60)),
    () => _heartbeatFileWriter.Flush());
```

`Schedule.On` se dispara al ritmo del clock simulado del backtest. En un backtest del 2025-01-01 al 2026-03-31 (15 meses, ~657.000 minutos simulados) eso son ~650.000 flushes a disco. Cada flush: serialización JSON + escritura `.tmp` + `File.Move` atómico. Resultado: backtest que antes tardaba 1 minuto ahora tarda 20+ minutos.

**Evidencia:** el `heartbeat.json` generado tiene `CurrentUtc: 2025-04-26T13:30:00` (tiempo simulado del backtest, no wall clock).

## Diseño del fix

El heartbeat es **observabilidad externa pasiva**: solo tiene utilidad cuando un servicio externo (Healthchecks.io, en Pieza C) lo está mirando. En backtest nadie lo mira. Por lo tanto:

- **En live (`LiveMode == true`):** flush periódico vía `System.Threading.Timer` cada 60 segundos de **wall clock real**. NO usar `Schedule.On` (corre al ritmo del clock simulado, irrelevante en live pero conceptualmente acoplado al motor de trading; un timer dedicado es más simple y desacoplado).
- **En backtest (`LiveMode == false`):** flush deshabilitado. Solo se ejecuta el `Flush()` inicial al final de `Initialize()` (que deja el archivo creado con el estado al boot). Nada más durante el backtest.

**Justificación de usar `Timer` en lugar de `Schedule.On` incluso en live:**

- El heartbeat NO participa del flujo de trading. No emite señales, no envía órdenes, no afecta risk. Es I/O pasivo dirigido a un archivo local consumido por un proceso externo.
- El wall clock real es el clock correcto para este caso: el dead-man's switch externo (Healthchecks.io) opera en wall clock real, no en clock del backtest.
- La regla del `AI.md` "Timers y schedulers se inyectan vía abstracción (ITimer, IScheduler). Nada de Task.Delay directo en Application" aplica a **Trading.Application**. `TradingAlgorithmHost` está en **Trading.Strategies**, que es el único proyecto autorizado a usar primitivas crudas de I/O y temporización (es el adaptador a Lean). El `Timer` vive en el Host, no en Application.

## Alcance del fix

### Modificaciones a artefactos existentes

#### 1. `Trading.Strategies/Adapters/HeartbeatFileWriter.cs`

**No cambia en absoluto.** Sigue siendo el componente que serializa y escribe atómicamente. La diferencia es **quién y cuándo lo invoca**, no el writer en sí.

#### 2. `Trading.Strategies/TradingAlgorithmHost.cs`

**Cambios:**

**a)** Agregar un nuevo campo privado para el timer:

```csharp
private System.Threading.Timer _heartbeatFlushTimer;
```

(O en la sección de campos privados existente, con el formato que ya use el archivo.)

**b)** **ELIMINAR completamente** el bloque actual de `Schedule.On` que invoca `_heartbeatFileWriter.Flush()`:

```csharp
// ELIMINAR: este bloque entero, donde sea que esté en Initialize()
Schedule.On(
    DateRules.EveryDay(),
    TimeRules.Every(TimeSpan.FromSeconds(60)),
    () => _heartbeatFileWriter.Flush());
```

**Importante:** este bloque puede estar fragmentado en varias líneas con formato distinto, o tener comentarios alrededor. Eliminar TODO el `Schedule.On` que dispara el `Flush()` del heartbeat. NO eliminar otros `Schedule.On` no relacionados al heartbeat si existen (revisar caso por caso).

**c)** **AGREGAR** el wiring del timer al final de `Initialize()`, después del `Flush()` inicial existente:

```csharp
// Flush inicial: deja el heartbeat.json creado con el estado al boot.
// Esto se ejecuta tanto en backtest como en live.
_heartbeatFileWriter.Flush();

// Timer de wall clock para el flush periódico. Solo activo en live:
// en backtest el heartbeat no tiene consumidor (Healthchecks.io alertaría
// con 15 meses de "silencio" en cuestión de microsegundos del wall clock).
//
// Se usa System.Threading.Timer en lugar de Schedule.On porque:
// 1) Schedule.On corre al ritmo del clock simulado del backtest (657k disparos
//    en 15 meses), no al ritmo del wall clock real que es lo que necesita el
//    dead-man's switch externo.
// 2) El heartbeat es observabilidad pasiva, no participa del flujo de trading,
//    por lo que no requiere el determinismo del scheduler de QC.
// 3) Trading.Strategies (este proyecto) es el adaptador autorizado a usar
//    primitivas de timing crudas; la regla del AI.md de "Timers inyectados
//    vía ITimer" aplica a Trading.Application, no acá.
if (LiveMode)
{
    _heartbeatFlushTimer = new System.Threading.Timer(
        callback: _ =>
        {
            try
            {
                _heartbeatFileWriter.Flush();
            }
            catch (System.Exception ex)
            {
                // Defensa en profundidad: el writer ya garantiza no propagar,
                // pero acá estamos en thread de Timer y una excepción no manejada
                // terminaría el proceso. Loggear y continuar.
                _logger.Warning(
                    "Heartbeat flush timer falló: {ExceptionType} {Message}",
                    ex.GetType().Name, ex.Message);
            }
        },
        state: null,
        dueTime: System.TimeSpan.FromSeconds(60),
        period: System.TimeSpan.FromSeconds(60));

    _logger.Info("Heartbeat flush timer iniciado (cadencia: 60s wall clock).");
}
else
{
    _logger.Info(
        "Heartbeat flush timer deshabilitado (modo backtest). " +
        "El archivo heartbeat.json refleja el estado al boot.");
}
```

**d)** **AGREGAR** liberación del timer al final de la vida del algoritmo. Si el `TradingAlgorithmHost` ya tiene un override de `OnEndOfAlgorithm`, agregar ahí la liberación. Si no lo tiene, crear el override:

```csharp
public override void OnEndOfAlgorithm()
{
    base.OnEndOfAlgorithm();

    if (_heartbeatFlushTimer != null)
    {
        _heartbeatFlushTimer.Dispose();
        _heartbeatFlushTimer = null;
    }
}
```

**Importante sobre `OnEndOfAlgorithm`:** este método de QC se invoca al final del backtest **y** al cerrar el live (ej. SIGINT). Disponer el timer ahí cubre ambos casos. En backtest el timer nunca arrancó (el `if (LiveMode)` impidió que se instanciara), por lo que el `Dispose` solo corre cuando hay timer real para liberar.

### Tests a actualizar (si los hay)

**Verificar:** si en Pieza B se creó algún test para `TradingAlgorithmHost` que verifique el agendamiento del `Schedule.On`, hay que actualizarlo o eliminarlo. Dado que `TradingAlgorithmHost` hereda de `QCAlgorithm` y es muy difícil de testear unitariamente sin levantar Lean, lo más probable es que NO existan tests directos del wiring del scheduler. Si existen, reportar antes de modificar.

**No agregar tests nuevos para este fix.** Razones:

- El comportamiento crítico (que `HeartbeatFileWriter.Flush()` produzca JSON válido y atómicamente) ya está testeado en `HealthHeartbeatTrackerTests` y los tests opcionales del writer si Claude Code los creó.
- El wiring del `Timer` en `TradingAlgorithmHost` no es testeable sin levantar el motor QC.
- El fix es de **integración con QC**, validado por el operador empíricamente: el backtest debe volver a tardar ~1 minuto y producir métricas idénticas a las de antes de INFRA-2.

## Validaciones de salida (a ejecutar por el operador)

```bash
# Confirmar que NO queda ningún Schedule.On asociado al heartbeat:
grep -n "Schedule\.On" Trading.Strategies/TradingAlgorithmHost.cs
# Si aparece alguno, debe ser por otra razón (no heartbeat). Revisar uno por uno.

grep -n "Heartbeat" Trading.Strategies/TradingAlgorithmHost.cs
# Debe aparecer la nueva sección con if (LiveMode) y el Timer.

# Build y tests
dotnet build
dotnet test
```

**Tests esperados:** todos los previos siguen verdes (incluyendo los de Pieza A y Pieza B). Ningún test nuevo en este fix.

**Validación operativa crítica (la hace el operador):**

1. **Correr el backtest existente.** Debe tardar **aproximadamente lo mismo que antes de INFRA-2 Pieza B** (~1 minuto). Si sigue tardando >5 minutos, hay otro problema y hay que parar y diagnosticar.

2. **Verificar métricas del backtest:** número de órdenes, fills, P&L final deben coincidir **exactamente** con el backtest previo a INFRA-2 Pieza B. Si difieren, el fix introdujo un side effect inesperado.

3. **Verificar `heartbeat.json`:** debe existir, ser JSON válido, y reflejar el estado al boot (porque el flush inicial corre antes del backtest, y después no se actualiza más en backtest). Los timestamps van a quedar congelados en el momento del boot del algoritmo. Esto es el comportamiento esperado en backtest.

4. **Verificar JSONL de logs:** debe estar presente la línea que dice `"Heartbeat flush timer deshabilitado (modo backtest)..."` con nivel Info.

## Riesgos conocidos

1. **Si `TradingAlgorithmHost` no tiene un campo `_logger` accesible en el scope donde se agrega el wiring del timer**, reportar antes de modificar. Es muy probable que sí lo tenga (lo usa en otros lugares del Initialize), pero verificar.

2. **Si Claude Code encuentra que la propiedad `LiveMode` de `QCAlgorithm` tiene un nombre distinto en la versión usada de QC** (algunas versiones la exponen como `Algorithm.LiveMode`, otras como `IsLive`), reportar y consultar antes de hacer asunciones.

3. **El timer corre en un thread del thread pool, no en el thread principal de QC.** El `HeartbeatFileWriter` y el `HealthHeartbeatTracker` ya son thread-safe (lock interno), por lo que esto está cubierto. NO agregar locks adicionales en el callback del timer.

4. **El `Dispose` del timer en `OnEndOfAlgorithm` es síncrono: espera a que cualquier callback en curso termine.** Esto es lo deseado: garantiza que no quede un flush huérfano escribiendo después del fin del proceso.

5. **Si por algún motivo en la versión actual de `TradingAlgorithmHost` el `Initialize()` tiene un `return` temprano antes del bloque donde se agrega el wiring**, reportar. El wiring debe estar en una ruta de código que se ejecute siempre.

## Mensaje de commit sugerido

```
fix(observability): heartbeat con Timer de wall clock, solo en LiveMode

El Schedule.On(TimeRules.Every(60s)) de Pieza B se disparaba al ritmo
del clock simulado del backtest (~650k flushes en un backtest de 15 meses),
elevando el tiempo de ejecución del backtest de 1min a 20+ minutos.

Fix:
- En live: flush vía System.Threading.Timer de wall clock real (60s).
  El timer corre desacoplado del motor de trading (es observabilidad
  pasiva, no participa del flujo de señales/órdenes/risk).
- En backtest: timer no se instancia. heartbeat.json refleja el estado
  al boot vía el Flush() inicial existente y no se actualiza más durante
  el backtest. Comportamiento determinista, performance restaurada.
- OnEndOfAlgorithm dispose del timer (cubre fin de backtest y SIGINT en live).

Justificación de usar Timer en lugar de Schedule.On incluso en live:
el heartbeat opera en wall clock real porque su consumidor externo
(Healthchecks.io) opera en wall clock. La regla del AI.md de "timers
vía ITimer" aplica a Trading.Application; TradingAlgorithmHost está
en Trading.Strategies y es el adaptador autorizado para primitivas
de timing crudas.

Restaura performance del backtest a baseline pre-INFRA-2. Sin cambios
funcionales en métricas del backtest (verificado por el operador).
```

## Resumen para el operador al cerrar

Al aplicar este fix, el sistema queda con:

- **Backtest:** tiempo de ejecución restaurado a ~1 minuto. `heartbeat.json` creado al boot y congelado durante el backtest (correcto: nadie lo lee). Métricas del backtest idénticas al baseline pre-INFRA-2 Pieza B.
- **Live:** `heartbeat.json` actualizado cada 60s de wall clock real vía `System.Threading.Timer`. Listo para que Pieza C (ping a Healthchecks.io) lo consuma.

**Próximo paso después de validar el fix:**

- Confirmar que el backtest vuelve a tardar ~1 minuto y métricas coinciden.
- Avanzar con Pieza C de INFRA-2 (ping externo a Healthchecks.io).
