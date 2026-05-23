# DEUDA-2 — Idempotencia de `TradingAlgorithmHost.Initialize()` en backtest

> **Brief ejecutable para Claude Code CLI.** Cierre de la deuda técnica DEUDA-2 documentada al cerrar INFRA-2 Pieza C: `Initialize()` se invoca dos veces en backtest, produciendo doble suscripción al `IDomainEventBus`, doble instanciación de `HttpClient` y doble construcción del sink de logs. Sin impacto funcional observable sobre métricas, pero contamina los logs y deja recursos duplicados sin dispose.
>
> **Pre-requisitos:** INFRA-2 completo (JSONL + heartbeat + Healthchecks.io ping), OPS-1 y OPS-2 completos (POLICY.md + StrategyHealthMonitor). El sistema está en Bloque 3 cerrado a falta de DEUDA-1 y DEUDA-2. Este brief cierra DEUDA-2; DEUDA-1 se cierra después en brief separado.

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio de los puntos críticos:

- **Cero comandos `git` de cualquier tipo.** Lista exhaustiva en `AI.md`. No worktrees, no ramas, no checkouts, no commits.
- **Compilación permitida** apuntando a `.csproj` específicos de `Trading.*` (NO a `QuantConnect.Lean.sln`).
- **Ejecución de tests permitida** apuntando a `.csproj` específicos de `Trading.*.Tests` (NO a `QuantConnect.Lean.sln`).
- **Ejecución de backtest no permitida.** El backtest lo corre el operador para validar el fix end-to-end con Lean.
- **Si Claude Code detecta una inconsistencia entre el código actual y este brief**, detenerse y reportar. NO improvisar.
- **Al final del trabajo, proponer el mensaje de commit sugerido** según la política del `AI.md`.

---

## Contexto y motivación de DEUDA-2

Al cerrar INFRA-2 Pieza C (`HealthchecksIoPinger`), se descubrió que `Initialize()` del `TradingAlgorithmHost` se ejecuta dos veces durante un backtest. La evidencia es directa: los logs estructurados del JSONL muestran cada entrada de arranque duplicada (`"TradingAlgorithmHost.Initialize() iniciado..."`, `"HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada..."`, etc.) en el mismo run.

**Efectos observables:**

1. **Doble suscripción al `IDomainEventBus`:** cada handler suscripto en `Initialize()` queda registrado dos veces. En la práctica, cada evento de dominio se procesa dos veces. Es idempotente en efecto observable sobre métricas (las suscripciones de `HealthHeartbeatTracker` y `StrategyHealthMonitor` están bajo lock y los assertions sobre eventos esperados pasan), pero esto es coincidencia de diseño, no garantía.
2. **Doble instanciación de `HttpClient`:** se crea un segundo `HttpClient` que nunca se dispose (la primera instancia queda colgada). Leak menor en backtest (proceso corto), problema potencial en live (proceso de días/semanas).
3. **Doble construcción del `JsonlFileLogSink`:** dos handles de archivo al mismo `logs/trading-{fecha}.jsonl`. El sink usa lock interno y `FileShare.Read`, por lo que no produce excepciones, pero es un recurso duplicado innecesario.
4. **Doble construcción del `_heartbeatFlushTimer`** y todos los demás componentes instanciados en `Initialize()`.

**Hipótesis del ROADMAP:** Lean invoca `Initialize()` dos veces en backtest (presumiblemente warm-up + run real). Esta hipótesis no está confirmada por inspección directa al cierre de INFRA-2; el brief actual la valida explícitamente antes de aplicar el fix.

**Validación pendiente:** si en paper trading (Hito C) `Initialize()` se invoca **una sola vez**, la deuda es solo de backtest y el guard es defensivo sin costo. Esta validación se hará al arrancar Hito C; el guard se deja activo por defecto en cualquier modo.

---

## Decisiones técnicas aplicadas (no discutir, aplicar)

| Decisión | Valor |
|---|---|
| Modelo asumido | **Modelo A:** Lean efectivamente llama `Initialize()` dos veces sobre la misma instancia del `TradingAlgorithmHost`. Se valida con logging temporal antes del fix (ver Fase 1). |
| Tipo de fix | Guard de idempotencia: `if (_initialized) return;` al inicio de `Initialize()`, con flag `private bool _initialized` y log defensivo cuando el guard se dispara. |
| Posición del guard | Primera línea de `Initialize()`, antes de cualquier construcción de adaptadores o servicios. |
| Log al disparar guard | Nivel `Debug` (no `Warning` ni `Info`). El operador puede subir el nivel si necesita inspeccionar el comportamiento en algún momento, pero el caso normal en backtest debería ser un único log en `Debug` por run. |
| Naming del flag | `_initialized` (campo privado con guion bajo, convención del proyecto según `AI.md`). |
| Tests | Agregar tests de idempotencia al proyecto de tests del host. Si no existe proyecto de tests para `TradingAlgorithmHost`, ver sección "Si no existe `TradingAlgorithmHostTests`" más abajo. |
| Actualización documental | Mover DEUDA-2 a "Historial completado" del ROADMAP con fecha y resumen. Nota al final del ADR-021 indicando que DEUDA-2 fue resuelta. **No requiere ADR nuevo** (decisión mecánica sin trade-offs de diseño). |

---

## Fase 1: validación del Modelo A (diagnóstico previo al fix)

**No saltar esta fase.** El fix asume Modelo A (Lean llama dos veces sobre la misma instancia). Si en realidad el problema es Modelo B (dos instancias del host por wiring incorrecto en otro lado del sistema), el guard parchea el síntoma pero no la causa raíz.

### Paso 1.1: agregar logging temporal en `Initialize()`

Modificar `TradingAlgorithmHost.Initialize()` agregando como **primera línea ejecutable**:

```csharp
_logger?.Debug(
    "TradingAlgorithmHost.Initialize() invocado, hash de instancia {InstanceHash}, llamada #{CallCount}",
    this.GetHashCode(),
    System.Threading.Interlocked.Increment(ref _initializeCallCount));
```

Donde `_initializeCallCount` es un campo nuevo:

```csharp
private int _initializeCallCount = 0;
```

**Importante:** `_logger` puede ser `null` en la primera línea ejecutable (todavía no se construyó). Usar operador `?.` y aceptar que la **primera** invocación no logueará por consola via `_logger` — pero el contador se incrementa y la **segunda** invocación sí va a loguear porque `_logger` ya está poblado del primer paso. Eso alcanza para detectar la doble invocación.

Como respaldo adicional para no perder la primera invocación, agregar también `Debug($"...")` nativo de `QCAlgorithm` (no es interpolación de string en log structurado; es el método `Debug` heredado de `QCAlgorithm`, que escribe a la consola de Lean directamente):

```csharp
Debug($"TradingAlgorithmHost.Initialize() invocado, hash de instancia {this.GetHashCode()}, llamada #{System.Threading.Interlocked.Increment(ref _initializeCallCount)}");
```

**Excepción autorizada:** se permite el uso de interpolación de strings (`$"..."`) en este `Debug` específico **porque es un log temporal de diagnóstico**, no un log estructurado de producción. Se remueve en la Fase 2. Documentar el motivo con comentario inline:

```csharp
// TEMP DEUDA-2: log de diagnóstico para validar hipótesis Modelo A.
// Se remueve cuando se aplique el guard de idempotencia.
Debug($"TradingAlgorithmHost.Initialize() invocado, hash de instancia {this.GetHashCode()}, llamada #{System.Threading.Interlocked.Increment(ref _initializeCallCount)}");
```

### Paso 1.2: compilación

```bash
dotnet build Trading.Strategies/Trading.Strategies.csproj
```

Debe compilar verde. Si hay error, reportar y detenerse.

### Paso 1.3: detener y reportar al operador

**Claude Code se detiene acá.** El operador ejecuta el backtest manualmente y reporta de vuelta lo que observa en:

1. La consola de Lean: cuántas veces aparece el `Debug("TradingAlgorithmHost.Initialize() invocado...")`.
2. El JSONL del run: cuántas veces aparece el evento estructurado del `_logger.Debug(...)` (recordar: la primera invocación no va a loguear via `_logger` porque era `null`; la segunda sí).
3. **Crítico:** verificar si el `InstanceHash` reportado en ambas invocaciones es el **mismo número** o **distintos números**.

**Interpretación esperada:**

- **Caso esperado (Modelo A confirmado):** dos invocaciones, **mismo `InstanceHash`** en ambas. Lean re-invoca sobre la misma instancia. → seguir con Fase 2 (aplicar el guard).
- **Caso alternativo (Modelo B):** dos invocaciones, **distintos `InstanceHash`**. Hay dos instancias del `TradingAlgorithmHost` siendo construidas por algún wiring del sistema. → **detenerse**, no aplicar el guard, reportar al operador. El fix correcto en este caso es localizar el origen de la doble construcción, no parchar el síntoma.
- **Caso inesperado:** una sola invocación. → DEUDA-2 no es reproducible en el run actual. Reportar al operador y consultar (puede ser que algo del entorno cambió desde INFRA-2; no asumir que la deuda "se resolvió sola").

---

## Fase 2: aplicación del guard (solo si Modelo A confirmado)

**Esta fase se ejecuta SOLO después de que el operador confirme que Modelo A es la hipótesis correcta (dos invocaciones, mismo `InstanceHash`).**

### Paso 2.1: aplicar el guard

En `TradingAlgorithmHost.cs`:

1. **Agregar campo privado** debajo de la sección de adaptadores Lean (junto al resto de los campos privados):

```csharp
// DEUDA-2: guard de idempotencia. Lean re-invoca Initialize() en backtest
// (validado empíricamente, ver ADR-021 nota al pie y ROADMAP).
// El guard protege contra doble suscripción al bus, doble HttpClient, etc.
// En live se valida en Hito C; el guard es defensivo en cualquier modo.
private bool _initialized;
```

2. **Reemplazar el inicio del método `Initialize()`** para agregar el guard como primera operación lógica del método. La estructura final del método queda así:

```csharp
public override void Initialize()
{
    if (_initialized)
    {
        // Si esto aparece en logs, confirma que Lean re-invocó.
        // El guard protege contra doble suscripción al bus, doble HttpClient,
        // doble JsonlFileLogSink, etc. Ver DEUDA-2 en ROADMAP.
        Debug("TradingAlgorithmHost.Initialize() re-invocado, ignorado por guard de idempotencia.");
        return;
    }
    _initialized = true;

    // ===== Adaptadores Lean =====
    // ... resto del Initialize sin cambios ...
}
```

**Importante:** no usar `_logger.Debug(...)` en el log del guard porque el guard se dispara antes de que `_logger` se construya en una eventual segunda llamada (el flujo nunca llega a las líneas de construcción). Usar el `Debug(...)` heredado de `QCAlgorithm` que sí está disponible desde la primera línea.

### Paso 2.2: remover el logging temporal de la Fase 1

Eliminar:

- El campo `private int _initializeCallCount;`.
- La línea `_logger?.Debug("TradingAlgorithmHost.Initialize() invocado, hash de instancia...")`.
- La línea `Debug($"TradingAlgorithmHost.Initialize() invocado, hash de instancia...")` con su comentario `TEMP DEUDA-2`.

El único log que queda del diagnóstico es el del guard cuando se dispara (paso 2.1).

### Paso 2.3: compilación

```bash
dotnet build Trading.Strategies/Trading.Strategies.csproj
```

Debe compilar verde.

---

## Fase 3: tests de idempotencia

### Si existe `Trading.Strategies.Tests` con clase de tests del host

Buscar primero si existe un archivo `Trading.Strategies.Tests/TradingAlgorithmHostTests.cs` o equivalente. Si existe, agregar los tests acá.

**Si NO existe** (caso esperado: `TradingAlgorithmHost` es el adaptador de Lean y el patrón histórico es no testearlo directamente porque depende de `QCAlgorithm`):

Crear el archivo `Trading.Strategies.Tests/Hosting/TradingAlgorithmHostIdempotencyTests.cs` con un test mínimo de validación de la lógica del guard. **No** se puede levantar un `QCAlgorithm` real en xUnit (regla institucional: cero QC en tests unitarios — ver AI.md). En lugar de eso, el test valida la **lógica del guard de forma aislada**.

Hay dos opciones técnicas:

**Opción A (recomendada): extraer el guard a una utilidad testeable.**

Esto es excesivamente intrusivo para una deuda de baja complejidad. Descartada por overengineering.

**Opción B (elegida): test sintético directo sobre la clase.**

Como `TradingAlgorithmHost.Initialize()` depende de `QCAlgorithm`, no es testeable como unidad pura. La validación del guard queda **delegada al operador** que corre el backtest después del fix y verifica que el log `"TradingAlgorithmHost.Initialize() re-invocado, ignorado por guard de idempotencia."` aparece exactamente una vez (en la segunda invocación que Lean hace) y que el log de arranque del host aparece una sola vez en lugar de dos.

**Resultado:** **no se agregan tests automatizados para DEUDA-2.** La validación es operativa (backtest manual + inspección del JSONL post-fix). Esto es defendible porque:

1. El guard es 3 líneas de lógica trivial.
2. El test que aportaría valor real (validar comportamiento con QC) violaría la regla institucional de no levantar QC en tests unitarios.
3. La señal de regresión está en el comportamiento observable del JSONL (no duplicación de logs de arranque), que es lo que el operador verifica en la Fase 4.

Documentar la decisión en el resumen final para que el operador lo sepa.

---

## Fase 4: validación operativa (la ejecuta el operador, no Claude Code)

Después del fix, el operador corre el backtest y verifica:

1. **El JSONL `logs/trading-{fecha-actual}.jsonl` muestra las entradas de arranque del host una sola vez.** Antes del fix, las entradas como `"HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada..."`, `"JsonlFileLogSink: archivo de log abierto en..."`, `"DomainEventBus: suscripción registrada..."`, etc., aparecían dos veces. Post-fix: una sola vez.

2. **El log de QC (consola de Lean) muestra `"TradingAlgorithmHost.Initialize() re-invocado, ignorado por guard de idempotencia."` exactamente una vez.** Esto confirma que el guard efectivamente disparó (y por tanto que la deuda existía).

3. **El backtest produce las mismas métricas que antes del fix.** Concretamente:
   - **6 órdenes** (baseline post-OPS-2 según ADR-023, válido hasta Hito G).
   - P&L final equivalente (puede haber diferencias numéricas mínimas si el doble procesamiento estaba afectando algo silenciosamente, en cuyo caso esto es **información valiosa**: el fix reveló que el comportamiento "idempotente en efecto" no era 100% idempotente).
   - `heartbeat.json` válido al final del run.

4. **`heartbeat.json` post-run muestra `ProcessStartedUtc` una sola vez con el valor correcto** (modulo DEUDA-3, que sigue apareciendo en el epoch de QC del 1997-12-31; eso no se resuelve en este brief).

5. **El `subscriptions_count` del `DomainEventBus` al final del run no es el doble de lo esperado.** Si el bus expone una métrica de cantidad de suscripciones, debe ser igual al baseline pre-fix. Si no la expone, este punto se omite.

**Si alguna de las validaciones falla**, el operador reporta a Claude Code para diagnóstico adicional. **Si todas pasan**, DEUDA-2 está resuelta y se procede al cierre documental.

---

## Validación pendiente: aplicabilidad en live (Hito C)

DEUDA-2 fue descubierta en backtest. Queda pendiente validar si Lean también re-invoca `Initialize()` en modo live (paper trading o capital real). Esta validación se ejecuta **al arrancar Hito C** (paper trading), no en este brief.

**Procedimiento de validación en Hito C:**

1. Al arrancar el sistema en modo live por primera vez, inspeccionar el JSONL inicial.
2. Buscar el log `"TradingAlgorithmHost.Initialize() re-invocado, ignorado por guard de idempotencia."`.
3. Si aparece: la deuda aplica también a live, el guard sigue protegiendo. Status DEUDA-2 sigue cerrado, válido en ambos modos.
4. Si **no** aparece: la deuda era solo de backtest. El guard es defensivo sin costo. Status DEUDA-2 sigue cerrado, válido. Documentar el hallazgo en `DECISIONS.md/incidents/` con una nota técnica.

En cualquiera de los dos casos, el guard se queda en el código permanentemente (no se remueve). El costo es despreciable (un `if` y un campo `bool`).

---

## Estructura final de archivos

### Archivos modificados

```
Trading.Strategies/TradingAlgorithmHost.cs
  → Agregar campo privado `_initialized` con XML doc explicando DEUDA-2.
  → Agregar guard al inicio de Initialize() con Debug() del log defensivo.
  → NO modificar nada más del Initialize() (resto del método queda idéntico).

ROADMAP.md
  → Tabla "🔄 BLOQUE 3 — En progreso":
    Marcar fila DEUDA-2 con ✅ y fecha.
  → Si DEUDA-1 sigue ⬜, el Bloque 3 sigue en progreso. Si DEUDA-1 ya está cerrada
    (no en este brief), recordatorio: cerrar el bloque cuando ambas estén ✅.
  → Sección "Historial completado": agregar entrada para DEUDA-2 con resumen
    según formato del proyecto (ver entradas previas como INFRA-2, OPS-2).

DECISIONS.md
  → ADR-021 (sección "Validaciones pendientes en Hito C"):
    Agregar al final de la lista de validaciones una nota:
    "DEUDA-2 (Initialize() doble) resuelta el {FECHA} vía guard de idempotencia.
    Validación de aplicabilidad en live: confirmar en Hito C inspeccionando el JSONL
    inicial si aparece el log 'TradingAlgorithmHost.Initialize() re-invocado'."
  → NO agregar ADR nuevo. La decisión es mecánica sin trade-offs.
```

### Archivos que NO se tocan

```
Trading.Domain/**                              ← Domain intacto, deuda es de adaptación
Trading.Application/**                         ← Application intacto, deuda es del host
Trading.Domain.Tests/**                        ← sin cambios
Trading.Application.Tests/**                   ← sin cambios
strategies.json                                ← sin cambios
POLICY.md                                      ← sin cambios
AI.md                                          ← sin cambios
```

---

## Riesgos conocidos y cómo el asistente debe manejarlos

1. **El diagnóstico de Fase 1 revela Modelo B (dos instancias distintas).** Si el `InstanceHash` reportado por el operador en la Fase 1 es distinto entre las dos invocaciones, **detenerse y NO aplicar el guard**. El problema es más profundo (probablemente en el wiring de Lean o en algún DI container) y requiere investigación específica. Reportar al operador con análisis del comportamiento observado.

2. **El diagnóstico de Fase 1 revela una sola invocación.** Si solo aparece el log de diagnóstico una vez, DEUDA-2 no es reproducible en el run actual. Posibles causas: (a) cambio en el entorno desde INFRA-2 que la resolvió sola, (b) el run específico no dispara el segundo `Initialize()` (¿depende del rango de fechas o del warm-up?). **Detenerse y consultar al operador.** No aplicar el guard si la deuda no es reproducible: aplicar fixes a problemas que no existen genera ruido innecesario.

3. **Post-fix, el backtest produce métricas distintas al baseline.** Si el operador reporta que el número de órdenes cambia (era 6 antes del fix, distinto después), esto significa que el "idempotente en efecto" del análisis de INFRA-2 no era exacto. **No revertir el fix.** El comportamiento correcto es el post-fix (sin duplicación); el baseline anterior estaba contaminado por el doble procesamiento. Documentar el cambio de baseline en una nota a ADR-023 (que es donde se establece el baseline de 6 órdenes post-OPS-2) y a ADR-021. Esta no es una regresión sino una corrección.

4. **El guard se aplica pero el log defensivo `"Initialize() re-invocado..."` no aparece en el JSONL.** Esto sería extraño después de que la Fase 1 confirmó Modelo A. Verificar: (a) el comentario `// TEMP DEUDA-2` y las líneas de logging temporal fueron efectivamente removidas en Fase 2.2 (porque si quedaron, podrían estar interfiriendo), (b) la consola de Lean sí lo muestra aunque el JSONL no (el guard usa `Debug()` de QC, no `_logger`, así que aparece en consola de Lean pero no en el JSONL — esto es esperado y correcto). Si la duda persiste, reportar al operador.

5. **Si Claude Code encuentra inconsistencias entre este brief y el código real**, detenerse y reportar. NO improvisar.

---

## Mensaje de commit sugerido (al cerrar el trabajo)

```
fix(host): guard de idempotencia en TradingAlgorithmHost.Initialize()

Lean re-invoca Initialize() durante el backtest (validado empíricamente con
logging temporal, modelo A confirmado: dos invocaciones sobre la misma
instancia). El guard protege contra:
- Doble suscripción al IDomainEventBus.
- Doble instanciación de HttpClient (leak menor en backtest, problema
  potencial en live).
- Doble JsonlFileLogSink, doble HeartbeatFileWriter, doble HealthchecksIoPinger,
  doble _heartbeatFlushTimer.

Cambios:
- Trading.Strategies/TradingAlgorithmHost.cs: campo privado _initialized,
  guard como primera operación de Initialize() con Debug() del log defensivo.
- ROADMAP: DEUDA-2 marcada ✅ y movida a Historial completado.
- DECISIONS: ADR-021 actualizado con nota de cierre de DEUDA-2 y validación
  pendiente en Hito C (confirmar si la deuda aplica también en live).

No se agregan tests unitarios automatizados: la lógica del guard es trivial,
testear levantaría QCAlgorithm violando la regla institucional. Validación
del fix es operativa vía inspección del JSONL post-backtest.

Closes DEUDA-2
Refs ADR-021
```

---

## Resumen para el operador al final de DEUDA-2

Al cerrar DEUDA-2, el sistema queda con:

- **`Initialize()` idempotente** garantizado vía guard de doble construcción de adaptadores y suscripciones cuando Lean re-invoca.
- **Logs del JSONL limpios:** entradas de arranque aparecen una sola vez (antes aparecían dos veces).
- **Recursos no duplicados:** `HttpClient`, `JsonlFileLogSink`, timers, etc., se construyen una sola vez.
- **Validación pendiente diferida a Hito C** para confirmar si la deuda aplica también en modo live; el guard queda activo en cualquier modo (es defensivo sin costo).

**Próximo paso operativo:**

- DEUDA-1 (test `AccordHmmClassifierReferenceTests` skipeado) es la siguiente y única deuda abierta antes de cerrar Bloque 3. Se aborda en brief separado: `DEUDA_1_BRIEF.md`.
- DEUDA-3 (timestamps del epoch de QC durante `Initialize()`) sigue abierta pero **no bloquea Hito C** (es cosmética, no funcional). Se aborda cuando se aborde, con ADR propio.

Una vez cerradas DEUDA-1 y DEUDA-2, el Bloque 3 queda 100% completo y el sistema queda listo para arrancar Hito C (paper trading) — aunque la decisión de qué estrategia llevar a paper (EmaCross vs nueva estrategia régimen-dependiente) es una conversación operativa pendiente que precede a Hito C.
