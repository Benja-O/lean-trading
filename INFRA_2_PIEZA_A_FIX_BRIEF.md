# INFRA-2 PIEZA A FIX — JsonlFileLogSink usa wall clock real en rotación y retención

> **Brief ejecutable para Claude Code CLI.** Fix sobre Pieza A de INFRA-2. El `JsonlFileLogSink` actual usa `_clock.UtcNow` (clock simulado del backtest) para nombrar archivos y ejecutar retención. En backtest esto causa cientos de rotaciones espurias (una por cada día simulado) y elimina la mayor parte del JSONL por retención de 30 días "simulados". Este fix sustituye el clock simulado por `DateTime.UtcNow` solo para rotación y retención, manteniendo el clock simulado para el campo `timestamp` de cada evento (que sí debe reflejar el tiempo del backtest para correlacionar con órdenes y barras).
>
> **Pre-requisitos:** Pieza A y Pieza B de INFRA-2 + fix de Pieza B commiteados. Compilación verde. Tests verdes. El backtest tarda ~1 minuto y métricas coinciden con baseline. JSONL existe en disco pero solo contiene los últimos ~30 días simulados del backtest (síntoma a corregir).

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

`LeanClock.UtcNow` devuelve `_algorithm.Time` (clock simulado del backtest). Durante un backtest de 15 meses (2025-01-01 a 2026-03-31), el clock simulado avanza desde el epoch inicial hasta el endDate. **Por cada cambio de día simulado, `JsonlFileLogSink.Write` detecta que `_clock.UtcNow.Date != _currentFileDate` y dispara `RotateFile`**. Eso significa:

- ~450 rotaciones de archivo durante el backtest (una por cada día simulado).
- En cada rotación se ejecuta `DeleteOldFiles`, que elimina archivos con fecha anterior a `_clock.UtcNow.Date.AddDays(-30)`. Como el clock simulado avanza, este cutoff también avanza, y se eliminan los archivos viejos del propio backtest.
- Al final del backtest sobreviven solo los últimos ~30 días simulados de JSONL. La mayor parte del run se perdió.

**Esto no es un bug del wiring ni del comportamiento del sink: el sink hace exactamente lo que se le pidió. El error está en el diseño original de Pieza A:** se asumió que `IClock` era el clock correcto para todo, incluida la rotación de archivos. La decisión correcta —análoga a la que ya tomamos en el fix del `Timer` del heartbeat— es que **rotación y retención son housekeeping de I/O, no participan del flujo determinista de trading, y deben operar en wall clock real**.

## Diseño del fix

**Tres tipos de "tiempo" en el sink, claramente separados:**

1. **`_clock.UtcNow` (clock simulado):** se sigue usando para el campo `timestamp` de cada evento JSON. Es lo que el operador necesita para correlacionar logs con órdenes, fills y barras del backtest. **No cambia.**

2. **`DateTime.UtcNow` (wall clock real):** se usa para decidir el nombre del archivo del día (rotación) y para calcular el cutoff de retención (housekeeping). **Cambio único de este fix.**

3. **No hay un tercer clock.** El sink NO recibe un nuevo parámetro ni una nueva abstracción. Solo cambia qué clock se usa internamente en dos lugares específicos.

**Justificación de usar `DateTime.UtcNow` directo (sin envolver en abstracción):**

- El `JsonlFileLogSink` vive en `Trading.Strategies/Adapters/`. La regla del `AI.md` "prohibido `DateTime.UtcNow` fuera de `Trading.Strategies`" aplica a Domain y Application; `Trading.Strategies` es el adaptador a Lean y está autorizado a usar primitivas crudas de tiempo. Lo mismo aplicó al `Timer` del heartbeat en el fix anterior.
- Introducir una nueva abstracción `IWallClock` solo para esto sería overkill. Es un único componente, una única responsabilidad de housekeeping, y el comportamiento "usar wall clock" es lo natural en cualquier adaptador de I/O.
- Los tests del sink que dependen del clock para el campo `timestamp` siguen funcionando con `FakeClock`. Los que dependían del clock para verificar rotación tienen que cambiar de enfoque (ver sección de tests).

## Alcance del fix

### Modificación principal: `Trading.Strategies/Adapters/JsonlFileLogSink.cs`

**Cambios mínimos y quirúrgicos.** El resto de la clase (estructura, lock, esquema JSON, manejo de excepciones) no se toca.

**a)** En el constructor, reemplazar las dos invocaciones a `_clock.UtcNow.Date` por `DateTime.UtcNow.Date`:

```csharp
public JsonlFileLogSink(
    IClock clock,
    string baseDirectoryPath,
    int retentionDays = 30)
{
    _clock = clock;
    _logsDirectory = Path.Combine(baseDirectoryPath, "logs");
    _retentionDays = retentionDays;

    Directory.CreateDirectory(_logsDirectory);

    // Rotación y retención usan wall clock real, NO el clock simulado del backtest.
    // En backtest, el clock simulado puede arrancar en el epoch del motor (ej. 1997-12-31)
    // y avanzar cientos de días simulados, lo que dispara rotaciones espurias y retenciones
    // que eliminan los propios logs del run en curso. El timestamp DENTRO de cada evento
    // sí usa _clock.UtcNow para correlacionar con órdenes/barras del backtest (ver Write).
    var wallClockToday = DateTime.UtcNow.Date;
    OpenWriterForDate(wallClockToday);
    DeleteOldFiles(wallClockToday);
}
```

**b)** En `Write(...)`, reemplazar el chequeo de rotación. **El timestamp del evento sigue siendo `_clock.UtcNow`** (no cambia), pero la decisión de rotar usa wall clock:

```csharp
public void Write(
    LogLevel level,
    string messageTemplate,
    IReadOnlyList<KeyValuePair<string, object?>> properties,
    Exception? exception)
{
    lock (_lock)
    {
        try
        {
            // Timestamp del evento: clock del sistema (simulado en backtest, real en live).
            // Esto es lo que el operador necesita para correlacionar logs con órdenes y barras.
            var now = _clock.UtcNow;

            // Decisión de rotación: wall clock real. Independiente del clock simulado.
            var wallClockToday = DateTime.UtcNow.Date;

            if (wallClockToday != _currentFileDate)
            {
                RotateFile(wallClockToday);
            }

            var args = PropertiesToArgs(properties);
            string renderedMessage = LogTemplateRenderer.Render(messageTemplate, args);

            var entry = BuildEntry(now, level, messageTemplate, renderedMessage, properties, exception);
            string json = JsonSerializer.Serialize(entry);

            _writer!.WriteLine(json);
            _writer.Flush();
        }
        catch (Exception ex)
        {
            _lastWriteFailure = ex;
        }
    }
}
```

**Importante:** la variable `now` (línea 1 del try) NO cambia su origen — sigue siendo `_clock.UtcNow`. Es lo que se pasa a `BuildEntry` y termina como `timestamp` en el JSON. **Solo cambia la variable usada para el chequeo de rotación.**

**c)** El método `RotateFile(DateTime newDate)` no cambia. Sigue recibiendo una fecha y llamando a `OpenWriterForDate` y `DeleteOldFiles`. El cambio es semántico, no estructural: el parámetro `newDate` ahora representa "fecha de wall clock real", no "fecha del clock del sistema".

**d)** El método `DeleteOldFiles(DateTime today)` tampoco cambia internamente. La función calcula `cutoff = today.AddDays(-_retentionDays)`, lo cual sigue siendo correcto: ahora `today` es wall clock real y `cutoff` también, por lo tanto la retención de 30 días es de **wall clock real** (que es lo deseado para post-mortem real del operador).

### Tests a actualizar: `Trading.Strategies.Tests/JsonlFileLogSinkTests.cs` (o donde estén)

Los tests preexistentes que dependen del `FakeClock` para verificar rotación o retención **van a romper** porque el sink ya no rota en respuesta al `FakeClock`. Hay que adaptarlos.

**Tests que NO cambian (siguen funcionando como están):**

- `Write_SingleEvent_ProducesParseableJsonLine`: verifica formato JSON. El `timestamp` sigue saliendo del `FakeClock`, sin cambio.
- `Write_WithProperties_SerializesAllFields`: ídem.
- `Write_WithException_SerializesTypeMessageStackTrace`: ídem.
- `Write_ConcurrentFromMultipleThreads_ProducesNoCorruptedLines`: ídem.
- `Write_WhenFileSystemFails_DoesNotPropagateException`: ídem.
- `RetentionCleanup_IgnoresFilesWithUnparseableNames`: ídem (el test verifica que un archivo con nombre ajeno no rompe `DeleteOldFiles`; no depende de qué clock se usa para calcular el cutoff).

**Tests que cambian de enfoque:**

Los tests que verificaban rotación y retención avanzando el `FakeClock` ya no pueden funcionar así, porque el sink no escucha al `FakeClock` para estas operaciones. Hay dos enfoques posibles para mantener cobertura:

**Enfoque preferido: simplificar y verificar el comportamiento NUEVO de rotación.**

Los tests de rotación y retención dependen del wall clock real, lo cual es muy difícil de testear unitariamente. Lo correcto es:

- **`Constructor_CreatesFileForTodaysWallClockDate`** (test nuevo): instanciar el sink, escribir un evento, verificar que existe un archivo cuyo nombre coincide con `DateTime.UtcNow.Date.ToString("yyyy-MM-dd")`. Esto valida que la rotación inicial usa wall clock.
- **`Constructor_AtStartup_DeletesFilesOlderThanRetention`** (existente, adaptar): pre-crear archivos con fechas viejas relativas a `DateTime.UtcNow.Date.AddDays(-31)`, instanciar el sink, verificar eliminación. NO usar `FakeClock` para el cutoff (ya no aplica). El test queda "ligado" a `DateTime.UtcNow` real, que es aceptable porque el comportamiento es estable día a día.
- **Eliminar tests obsoletos:**
  - `Write_AcrossDayBoundary_RotatesFile` (el viejo, que avanzaba `FakeClock` un día y esperaba rotación) — **ELIMINAR**. Reemplazar conceptualmente por el test nuevo descrito arriba.
  - `Rotation_DeletesFilesOlderThanRetention` similar versión vieja — **ELIMINAR o reemplazar** según corresponda.

**Si Claude Code tiene dudas sobre exactamente qué tests existen y cuáles eliminar:** ejecutar primero un listado del archivo de tests actual con `Get-Content` (o equivalente) y reportar al operador qué tests va a tocar antes de modificarlos. Mejor pausar para confirmar que romper algo silenciosamente.

**Nota importante sobre testing del `DateTime.UtcNow` directo:** sí, idealmente todo `DateTime.UtcNow` se envuelve en una abstracción mockeable. Para este caso el costo/beneficio no lo justifica: una abstracción nueva (`IWallClock`) introduciría más superficie de código que el código que está testeando, y los tests "ligados al wall clock real" son aceptables para housekeeping de I/O. Si en el futuro la complejidad del sink crece y los tests de housekeeping se vuelven importantes, ahí evaluamos introducir la abstracción. Por ahora, principio de proporcionalidad al riesgo (ADR-008).

### Tests preexistentes en OTROS lugares

Tests de `LeanLogger`, `LogTemplateRenderer`, etc., no se tocan. Ninguno depende del clock para rotación.

## Validaciones de salida (a ejecutar por el operador)

```powershell
# 1. Confirmar que el sink ya NO usa _clock.UtcNow para rotación/retención:
Select-String -Path "F:\DesarrolloTrading\QuantConnect\Lean\Trading.Strategies\Adapters\JsonlFileLogSink.cs" -Pattern "_clock\.UtcNow"
# Debe aparecer EXACTAMENTE UNA línea: la del método Write donde se calcula `now` para el timestamp del evento.

# 2. Confirmar que se introdujeron las dos referencias a DateTime.UtcNow esperadas:
Select-String -Path "F:\DesarrolloTrading\QuantConnect\Lean\Trading.Strategies\Adapters\JsonlFileLogSink.cs" -Pattern "DateTime\.UtcNow"
# Debe aparecer DOS líneas: una en el constructor (wallClockToday) y una en Write (wallClockToday).

# Build y tests
dotnet build
dotnet test
```

**Tests esperados:** todos verdes después del fix. Los tests obsoletos que dependían de avanzar el `FakeClock` para verificar rotación deben haber sido reemplazados o eliminados (Claude Code debe haber reportado cuáles tocó).

**Validación operativa crítica (la hace el operador):**

1. **Correr el backtest existente.** Tiempo de ejecución sigue siendo ~1 minuto (sin cambio respecto al fix previo de Pieza B).
2. **Métricas idénticas:** 225 órdenes, mismas estadísticas. Cero impacto funcional sobre el motor de trading.
3. **Verificar el directorio `logs/`:** debe existir **un único archivo** `trading-{fecha-de-hoy-wall-clock}.jsonl` con TODO el JSONL del backtest. Verificar:
   ```powershell
   $logsDir = "F:\DesarrolloTrading\QuantConnect\Lean\Launcher\bin\Debug\logs"
   Get-ChildItem $logsDir -Filter "trading-*.jsonl" | Select-Object Name, Length
   ```
   - Antes del fix: ~30 archivos (los últimos 30 días simulados del backtest).
   - Después del fix: 1 archivo, con todo el contenido del run.
4. **El archivo debe contener líneas de cada nivel y de varios momentos del backtest:**
   ```powershell
   Get-Content "$logsDir\trading-*.jsonl" | Measure-Object -Line
   # Debe dar varios miles de líneas (cada evento del backtest).
   ```
5. **Verificar que el timestamp DENTRO del JSON refleja el clock simulado** (no el wall clock):
   ```powershell
   Get-Content "$logsDir\trading-*.jsonl" -TotalCount 5 | ForEach-Object { $_ | ConvertFrom-Json | Select-Object timestamp, level, messageTemplate }
   ```
   Los primeros eventos deben tener timestamps tipo `2025-01-01T...` o cerca (clock del backtest), NO la fecha de hoy.

## Riesgos conocidos

1. **Si la línea inicial del backtest ("Heartbeat flush timer deshabilitado...") fue loggeada ANTES de que se construyera el sink:** no debería pasar — el sink se construye en el constructor de `LeanLogger`, que se construye antes de cualquier `Log()` del sistema. Pero confirmar visualmente que esa línea aparece en el JSONL post-fix.

2. **Si en tu zona horaria es un día y el wall clock UTC es otro:** el sink usa UTC consistentemente (`DateTime.UtcNow`). Eso es lo correcto. Es posible que el nombre del archivo no coincida con tu fecha local; es esperado, no es bug.

3. **Si el backtest cruza las 00:00 UTC del wall clock (porque tarda 20+ minutos):** sí, en ese caso habría dos archivos JSONL (uno por cada día UTC de wall clock que duró el run). En el caso normal (backtest de 1 minuto) esto no pasa nunca, pero está bien que pase si pasa.

4. **El campo `timestamp` del JSON sigue siendo el clock simulado.** Eso es deliberado: el operador busca eventos por momento del backtest (ej. "qué pasó en marzo del 2025"), no por wall clock. La rotación del archivo es housekeeping puro y el operador casi nunca lo va a notar (todos los eventos están en un archivo).

5. **Si Claude Code detecta otros usos de `_clock.UtcNow` en `JsonlFileLogSink.cs` además de la línea del `now` en `Write`:** detenerse y reportar antes de modificar. Solo debe haber una referencia restante.

6. **Tests de `RotationOnDayBoundary` o similar que existan y dependan de `FakeClock`:** consultar antes de eliminar. Confirmar con el operador si son los tests preexistentes que pierden sentido con el fix.

## Mensaje de commit sugerido

```
fix(observability): JSONL usa wall clock para rotación y retención

Pieza A de INFRA-2 usaba _clock.UtcNow (clock simulado del backtest)
para nombrar archivos JSONL y ejecutar retención. Durante un backtest
de 15 meses, el clock simulado avanza día a día disparando ~450 rotaciones
y retenciones que eliminaban los propios logs del run en curso. Al final
del backtest sobrevivían solo los últimos ~30 días simulados.

Fix:
- Rotación y retención usan DateTime.UtcNow.Date (wall clock real).
- El campo "timestamp" de cada evento JSON sigue usando _clock.UtcNow
  para correlacionar logs con órdenes/barras del backtest.
- En backtest se genera UN único archivo trading-{wall-clock-date}.jsonl
  con todo el run. En live el comportamiento es idéntico a antes (clock
  simulado y wall clock coinciden, rotación diaria normal).

Justificación arquitectónica: la rotación y retención son housekeeping
de I/O, no participan del flujo determinista de trading. Trading.Strategies
es el adaptador autorizado a usar DateTime.UtcNow directo, igual que el
Timer del heartbeat (ADR-021).

Tests de rotación/retención que dependían de avanzar FakeClock fueron
reemplazados por tests ligados a wall clock real (aceptable para
housekeeping; principio de proporcionalidad al riesgo, ADR-008).
```

## Resumen para el operador al cerrar

Al aplicar este fix, el sistema queda con:

- **Backtest:** un único archivo `logs/trading-{fecha-de-hoy}.jsonl` con todo el run (miles de líneas). Inspección post-mortem completa con `jq` o `Select-String`.
- **Live:** rotación diaria normal por wall clock real. Idéntico al comportamiento que tendría un sistema de logging tradicional.

**Próximo paso después de validar el fix:**

- Verificar archivo único con todo el JSONL.
- Avanzar con Pieza C de INFRA-2 (ping externo a Healthchecks.io).
