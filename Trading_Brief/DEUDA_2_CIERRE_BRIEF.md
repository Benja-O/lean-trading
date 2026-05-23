# DEUDA-2 — Cierre como NO reproducible (no se aplica guard de idempotencia)

> **Brief de cierre ejecutable para Claude Code CLI.** Continuación y cierre del brief anterior `DEUDA_2_BRIEF.md`. La Fase 1 del diagnóstico fue ejecutada por el operador y reveló que `Initialize()` se invoca **una sola vez** en backtest. La deuda documentada al cierre de INFRA-2 no es reproducible con el código actual. Este brief revierte la instrumentación temporal y cierra DEUDA-2 documentalmente como "no reproducible / resuelta incidentalmente", sin aplicar el guard de idempotencia.
>
> **Pre-requisitos:** El operador ya ejecutó la Fase 1 de `DEUDA_2_BRIEF.md` (instrumentación con `_initializeCallCount` y log con hash de instancia). El código de `TradingAlgorithmHost.cs` tiene actualmente la instrumentación temporal aplicada; este brief la remueve.

---

## Evidencia del diagnóstico (resumen para contexto)

**Consola de Lean (run del 2026-05-22):**
```
20260522 15:16:55.191 TRACE:: Debug: 1997-12-31 19:00:00 TradingAlgorithmHost.Initialize() invocado, hash de instancia 38986105, llamada #1
```

Una sola línea de invocación. `_initializeCallCount` solo se incrementó a 1.

**JSONL del run (`trading-2026-05-22.jsonl`, 6 líneas totales):**
- `HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada` → 1 ocurrencia.
- `Heartbeat flush timer deshabilitado` → 1 ocurrencia.
- Resto: 4 eventos de un trade del 8-12 de enero 2025. Ningún duplicado.

**Conclusión:** `Initialize()` corre una sola vez. Los componentes se construyen una sola vez. No hay doble suscripción, no hay doble `HttpClient`, no hay doble sink. La deuda no es reproducible.

**Decisión institucional:** NO se aplica el guard de idempotencia. Fixes solo a problemas reproducidos. El brief `DEUDA_2_BRIEF.md` previó este escenario explícitamente en su Riesgo 2.

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio de los puntos críticos:

- **Cero comandos `git` de cualquier tipo.**
- **Compilación permitida** apuntando a `.csproj` específicos de `Trading.*`.
- **Ejecución de tests permitida** apuntando a `.csproj` específicos de `Trading.*.Tests`.
- **Si Claude Code detecta una inconsistencia entre el código actual y este brief**, detenerse y reportar. NO improvisar.
- **Al final del trabajo, proponer el mensaje de commit sugerido.**

---

## Alcance del brief

Tres acciones secuenciales:

1. **Remover la instrumentación temporal** del `TradingAlgorithmHost.Initialize()` (revertir Fase 1.1 del brief anterior).
2. **Actualizar `ROADMAP.md`**: mover DEUDA-2 a "Historial completado" con resumen del cierre.
3. **Actualizar `DECISIONS.md`**: agregar nota al final de ADR-021 documentando el cierre.

**NO se crean ADRs nuevos.** La decisión de "no aplicar fix porque la deuda no es reproducible" no amerita ADR — queda capturada en el historial completado del ROADMAP y la nota a ADR-021.

**NO se modifica nada más del código de producción.** Cero líneas de lógica funcional cambian respecto al estado pre-Fase 1.

---

## Paso 1: remover la instrumentación temporal

En `Trading.Strategies/TradingAlgorithmHost.cs`, revertir las modificaciones aplicadas durante la Fase 1 del brief anterior. Concretamente:

### 1.1 Eliminar el campo privado `_initializeCallCount`

Buscar y eliminar la línea (puede estar en cualquier punto de la sección de campos privados del host, típicamente cerca del resto de campos de observabilidad):

```csharp
private int _initializeCallCount = 0;
```

### 1.2 Eliminar las dos líneas de logging temporal al inicio de `Initialize()`

Buscar y eliminar:

```csharp
_logger?.Debug(
    "TradingAlgorithmHost.Initialize() invocado, hash de instancia {InstanceHash}, llamada #{CallCount}",
    this.GetHashCode(),
    System.Threading.Interlocked.Increment(ref _initializeCallCount));
```

Y también:

```csharp
// TEMP DEUDA-2: log de diagnóstico para validar hipótesis Modelo A.
// Se remueve cuando se aplique el guard de idempotencia.
Debug($"TradingAlgorithmHost.Initialize() invocado, hash de instancia {this.GetHashCode()}, llamada #{System.Threading.Interlocked.Increment(ref _initializeCallCount)}");
```

**Estado esperado tras este paso:** `Initialize()` queda idéntico al estado anterior al brief de DEUDA-2 (sin contador, sin log de diagnóstico, sin comentario `TEMP DEUDA-2`). La primera línea ejecutable vuelve a ser `_instrumentResolver = new LeanInstrumentResolver();` o equivalente según el código original.

### 1.3 Verificar limpieza

Búsquedas confirmatorias (Claude Code ejecuta y reporta resultado vacío):

```bash
grep -n "_initializeCallCount" Trading.Strategies/TradingAlgorithmHost.cs
grep -n "TEMP DEUDA-2" Trading.Strategies/TradingAlgorithmHost.cs
grep -rn "_initializeCallCount" Trading.Strategies/
grep -rn "TEMP DEUDA-2" Trading.Strategies/
```

Las cuatro búsquedas deben devolver vacío. Si alguna devuelve líneas, hay residuo de la instrumentación y hay que eliminarlo.

### 1.4 Compilación de verificación

```bash
dotnet build Trading.Strategies/Trading.Strategies.csproj
```

Debe compilar verde. Si falla, reportar y detenerse.

---

## Paso 2: actualizar `ROADMAP.md`

### 2.1 Marcar DEUDA-2 como ✅ en la tabla de Bloque 3

En la sección "🔄 BLOQUE 3 — En progreso" del ROADMAP, encontrar la fila de DEUDA-2 y cambiarla:

**De:**
```
| ⬜ | DEUDA-2 | `TradingAlgorithmHost.Initialize()` se ejecuta dos veces en backtest | Hito C (validar si aplica también en live) | Descubierta al cerrar INFRA-2 Pieza C. ... |
```

**A:**
```
| ✅ | DEUDA-2 | `TradingAlgorithmHost.Initialize()` se ejecuta dos veces en backtest | Hito C (validar si aplica también en live) | **Cerrada 2026-05-22 como NO reproducible.** Ver historial completado. El diagnóstico instrumentado reveló que `Initialize()` se ejecuta una sola vez (consola Lean reporta `llamada #1` una vez; JSONL muestra cada mensaje de arranque una vez). NO se aplicó guard de idempotencia. Validación pendiente en Hito C: confirmar comportamiento también en modo Live. |
```

### 2.2 Agregar entrada al "Historial completado"

Al inicio de la sección "Historial completado" (o donde corresponda por orden cronológico inverso si existe), agregar:

```markdown
### ✅ DEUDA-2 — `Initialize()` doble en backtest: NO reproducible al ejecutar diagnóstico
**Fecha:** 2026-05-22
**Resumen:** Diagnóstico ejecutado según brief `DEUDA_2_BRIEF.md` (Fase 1: instrumentación con contador atómico de invocaciones y log con hash de instancia). Resultado: `Initialize()` se ejecuta **UNA sola vez** en backtest. La consola de Lean reporta `llamada #1` una vez y el JSONL del run (`trading-2026-05-22.jsonl`, 6 líneas totales) muestra cada uno de los mensajes de arranque del host (`HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada`, `Heartbeat flush timer deshabilitado`) exactamente una vez. La deuda documentada al cierre de INFRA-2 (ADR-021) no es reproducible con el código actual. Causa probable: el diagnóstico original fue por inferencia (logs duplicados → ergo doble invocación), no por instrumentación directa; los duplicados observados al cierre de INFRA-2 pudieron tener otra causa que se resolvió incidentalmente con los cambios de OPS-1/OPS-2 al wiring del host (no se conserva el JSONL del cierre de INFRA-2 para confrontación directa). **NO se aplica guard de idempotencia:** fixes solo a problemas reproducidos (regla institucional, consistente con Riesgo 2 del brief `DEUDA_2_BRIEF.md`). La instrumentación temporal de Fase 1 (`_initializeCallCount` + logs de hash de instancia) fue revertida; el código de `TradingAlgorithmHost.cs` queda idéntico al estado pre-Fase 1. **Validación pendiente en Hito C:** al arrancar paper trading, inspeccionar el JSONL inicial para confirmar que el síntoma tampoco aparece en modo Live; si aparece, abrir nueva deuda con diagnóstico fresco. Sin cambios de código de producción. Sin ADR nuevo (decisión documentada en esta entrada del historial y nota al ADR-021).
```

### 2.3 Verificación post-edición

Confirmar visualmente que:
- La fila de DEUDA-2 en la tabla de Bloque 3 quedó con ✅.
- DEUDA-1 sigue ⬜ (esta DEUDA queda como única abierta del Bloque 3).
- La entrada del historial completado fue agregada en la posición correcta.

**Importante:** el Bloque 3 NO se marca como ✅ Completo todavía, porque DEUDA-1 sigue abierta. Si la sección del diagrama del Plan general muestra el estado del Bloque 3, mantenerlo como 🔄 en progreso.

---

## Paso 3: actualizar `DECISIONS.md`

Agregar nota al final del ADR-021 (en la sección "Validaciones pendientes en Hito C" del propio ADR-021, después de los puntos numerados existentes). El formato esperado del ADR-021 ya incluye una sección numerada; la nota va como un párrafo al final del ADR, no como un punto numerado adicional.

Concretamente, después del último contenido del ADR-021 y antes del separador `---` que lo separa del siguiente ADR, agregar:

```markdown

### Cierre de DEUDA-2 (2026-05-22)

Al ejecutar el diagnóstico planificado (brief `DEUDA_2_BRIEF.md`, Fase 1: instrumentación con `_initializeCallCount` atómico y log con hash de instancia), `Initialize()` se ejecutó **una sola vez** en backtest. Evidencia:

- Consola de Lean: `Debug: 1997-12-31 19:00:00 TradingAlgorithmHost.Initialize() invocado, hash de instancia 38986105, llamada #1` aparece una sola vez en el run.
- JSONL `trading-2026-05-22.jsonl` (6 líneas totales): los mensajes de arranque del host (`HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada`, `Heartbeat flush timer deshabilitado`) aparecen exactamente una vez cada uno.

Los logs duplicados observados al cierre de INFRA-2 que motivaron la documentación de DEUDA-2 no se manifiestan con el código actual. Causa exacta no determinada — no se conservó el JSONL del cierre de INFRA-2 para comparación directa, pero el diagnóstico original fue inferencial (logs duplicados → conclusión de doble invocación), no instrumentado.

**NO se aplicó guard de idempotencia.** Fixes solo a problemas reproducidos. Decisión consistente con el Riesgo 2 del brief `DEUDA_2_BRIEF.md` que previó explícitamente este escenario.

**Validación pendiente en Hito C:** al arrancar paper trading, inspeccionar el JSONL inicial para confirmar que el síntoma tampoco aparece en modo Live. Si reaparece, abrir nueva deuda con diagnóstico fresco (no reabrir DEUDA-2: el diagnóstico de hoy quedó cerrado).
```

---

## Paso 4: validación final

### 4.1 Compilación completa

```bash
dotnet build Trading.Strategies/Trading.Strategies.csproj
```

Debe compilar verde.

### 4.2 Tests existentes deben seguir pasando

```bash
dotnet test Trading.Application.Tests/Trading.Application.Tests.csproj
dotnet test Trading.Domain.Tests/Trading.Domain.Tests.csproj
dotnet test Trading.Strategies.Tests/Trading.Strategies.Tests.csproj
```

Todos verdes. Ningún test esperado cambia su comportamiento (cero cambios funcionales en código de producción).

### 4.3 Diff esperado del commit

Tres archivos modificados, ninguno creado, ninguno eliminado:

```
M  Trading.Strategies/TradingAlgorithmHost.cs   (remoción de instrumentación temporal)
M  ROADMAP.md                                    (DEUDA-2 a ✅ + historial completado)
M  DECISIONS.md                                  (nota al final de ADR-021)
```

El diff de `TradingAlgorithmHost.cs` debe ser **estrictamente sustractivo**: solo elimina las líneas de instrumentación temporal, no agrega ninguna línea nueva. Si el diff agrega líneas, hay residuo no esperado y hay que investigar.

---

## Riesgos conocidos y cómo el asistente debe manejarlos

1. **No se encuentra `_initializeCallCount` o las líneas de instrumentación temporal en `TradingAlgorithmHost.cs`.** Significa que la Fase 1 del brief anterior no se aplicó como documentado, o que ya fue removida. **Detenerse y reportar al operador** con la salida de los `grep` confirmatorios. No agregar ni modificar nada hasta entender el estado real del archivo.

2. **El `grep -rn "_initializeCallCount" Trading.Strategies/`** encuentra el identificador en algún archivo distinto a `TradingAlgorithmHost.cs`. Inesperado. Reportar al operador antes de tocar nada.

3. **El diff de `TradingAlgorithmHost.cs` no es estrictamente sustractivo** (agrega líneas). Indicaría que la limpieza tocó algo más allá de lo documentado. Detenerse, mostrar el diff completo al operador, no commitear.

4. **Compilación falla tras la limpieza.** Inesperado (la instrumentación temporal era autocontenida). Reportar el error de compilación y detenerse.

5. **Algún test que antes pasaba ahora falla.** Inesperado por la misma razón que en (4). Reportar y detenerse.

6. **Si Claude Code encuentra inconsistencias entre este brief y el código real**, detenerse y reportar. NO improvisar.

---

## Mensaje de commit sugerido (al cerrar el trabajo)

```
chore(host): cerrar DEUDA-2 como no reproducible, revertir instrumentación

Diagnóstico ejecutado según DEUDA_2_BRIEF.md (Fase 1: contador atómico
+ log con hash de instancia). Resultado: Initialize() se ejecuta una
sola vez en backtest.

Evidencia:
- Consola Lean: "llamada #1" aparece una vez en el run del 2026-05-22.
- JSONL trading-2026-05-22.jsonl (6 líneas totales): cada mensaje de
  arranque del host (HealthchecksIoPinger, Heartbeat flush timer)
  aparece exactamente una vez.

Conclusión: la deuda documentada al cierre de INFRA-2 no es reproducible
con el código actual. Causa exacta no determinada. NO se aplica guard
de idempotencia (regla institucional: fixes solo a problemas
reproducidos). Validación pendiente al arrancar Hito C (modo Live).

Cambios:
- Trading.Strategies/TradingAlgorithmHost.cs: revertir la instrumentación
  temporal de Fase 1 (eliminar _initializeCallCount y los dos logs de
  diagnóstico). El método Initialize() queda idéntico al estado pre-Fase 1.
- ROADMAP: DEUDA-2 marcada ✅. Entrada agregada al historial completado
  documentando el resultado del diagnóstico.
- DECISIONS: ADR-021 con nota al final documentando el cierre de DEUDA-2
  y la validación pendiente en Hito C.

Sin ADR nuevo (decisión de no aplicar fix no amerita ADR, queda capturada
en historial del ROADMAP y nota a ADR-021).

Closes DEUDA-2
Refs ADR-021
```

---

## Resumen para el operador al final del cierre

Al cerrar este brief, el sistema queda con:

- **`TradingAlgorithmHost.cs` idéntico al estado pre-DEUDA-2:** sin guard de idempotencia, sin instrumentación temporal, sin contador de invocaciones. La función `Initialize()` vuelve a su forma original.
- **DEUDA-2 cerrada como NO reproducible** en el ROADMAP, con trazabilidad documental completa (entrada en historial completado + nota al ADR-021).
- **Validación pendiente diferida a Hito C:** confirmar que el síntoma tampoco aparece en modo Live. Si aparece, se abre nueva deuda; DEUDA-2 queda cerrada definitivamente con el diagnóstico de hoy.

**Próximo paso operativo:**

- DEUDA-1 (test `AccordHmmClassifierReferenceTests` skipeado) es la única deuda abierta del Bloque 3. Se aborda con el brief `DEUDA_1_BRIEF.md` (ya producido en sesión separada).
- DEUDA-3 (timestamps del epoch de QC durante `Initialize()`) sigue abierta y no bloquea Hito C.

Una vez cerrada DEUDA-1, el Bloque 3 queda 100% completo.
