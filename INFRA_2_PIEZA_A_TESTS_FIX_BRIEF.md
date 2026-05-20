# INFRA-2 PIEZA A FIX (tests) — Disponer JsonlFileLogSink antes de leer archivo en tests

> **Brief ejecutable para Claude Code CLI.** Fix sobre los tests de `JsonlFileLogSinkTests` que rompieron tras el fix de rotación/retención por wall clock. Los 4 tests fallidos (`Write_SingleEvent_ProducesParseableJsonLine`, `Write_WithProperties_SerializesAllFields`, `Write_WithException_SerializesTypeMessageStackTrace`, `Write_ConcurrentFromMultipleThreads_ProducesNoCorruptedLines`) intentan leer el archivo JSONL con `File.ReadAllLines` mientras el sink todavía mantiene el `FileStream` abierto en modo escritura. El conflicto de `FileShare` causa `IOException` ("process cannot access the file because it is being used by another process"). El fix es disponer el sink antes de la lectura, patrón estándar para tests de escritores de archivos.
>
> **Pre-requisitos:** Pieza A + fix A (rotación/retención por wall clock) + Pieza B + fix B (Timer en LiveMode) commiteados. El código de producción compila y funciona correctamente. Solo fallan estos 4 tests específicos.

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio:

- **Cero comandos `git` de cualquier tipo.**
- **No compilar.** El usuario compila.
- **No correr tests.** El usuario corre tests.
- **Si Claude Code detecta inconsistencia entre el código actual y este brief, detenerse y reportar. NO improvisar.**
- Al final, proponer el mensaje de commit sugerido.

---

## Contexto del bug

`JsonlFileLogSink` abre el archivo destino con:

```csharp
new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
```

`FileShare.Read` significa "otros procesos pueden LEER mientras yo escribo, pero NO escribir". Este share es el correcto para producción y NO debe cambiarse: garantiza que dos instancias del sink (o cualquier otro escritor) no corrompan el archivo simultáneamente.

Sin embargo, `File.ReadAllLines(path)` internamente abre el archivo con `FileShare.Read` **también para sí mismo**. El conflicto real es entre los modos `FileAccess`:

- Sink: `FileAccess.Write` + `FileShare.Read` → "yo escribo, otros pueden leer".
- `File.ReadAllLines`: `FileAccess.Read` + `FileShare.Read` por default.

En teoría son compatibles, pero Windows aplica el share del **lado más restrictivo**, y como el sink declaró `FileShare.Read` (no `FileShare.ReadWrite`), Windows interpreta "yo permito que otros lean, pero no permito que otros tengan handle de escritura". El handle del `File.ReadAllLines` no es de escritura, pero **algunas versiones de .NET en Windows lo abren con flags que tropiezan con el share del lado escritor**. El resultado práctico: `IOException`.

Antes del fix anterior (rotación por wall clock), los tests usaban distintos `FakeClock` que generaban nombres de archivo distintos por test, por lo que el conflicto no se manifestaba (cada test escribía y leía su propio archivo). Tras el fix, todos los tests usan el mismo nombre de archivo (`trading-{wall-clock-de-hoy}.jsonl`), y el conflicto aparece.

## Diseño del fix

**Patrón canónico para tests de escritores de archivo:**

1. Instanciar el sink (apuntando a un directorio temporal único por test).
2. Escribir los eventos.
3. **`Dispose` del sink ANTES de leer.** Esto libera el `FileStream` y el lock del sistema operativo.
4. Leer el archivo con `File.ReadAllLines` (o equivalente) y hacer asserts.

El patrón también garantiza que **el flush del writer ya pasó** (Dispose llama Flush internamente), evitando ese segundo tipo de flakiness donde la lectura corre antes que los buffers se vacíen a disco.

**No se cambia el código de producción.** El `FileShare.Read` del sink se mantiene tal cual: es el comportamiento correcto para impedir escrituras simultáneas en producción.

## Alcance del fix

### Modificación única: `Trading.Strategies.Tests/Adapters/JsonlFileLogSinkTests.cs`

Los cuatro tests siguen el mismo patrón roto. La estructura del fix es idéntica para los cuatro:

**Patrón actual (roto):**

```csharp
[Fact]
public void Write_SingleEvent_ProducesParseableJsonLine()
{
    var tempDir = CreateTempDirectory();
    var sink = new JsonlFileLogSink(_clock, tempDir);

    sink.Write(LogLevel.Info, "test message", ..., null);

    var files = Directory.GetFiles(tempDir, "trading-*.jsonl");
    var lines = File.ReadAllLines(files[0]);  // ← falla acá

    Assert.Single(lines);
    // ... más asserts

    sink.Dispose();
}
```

**Patrón corregido:**

```csharp
[Fact]
public void Write_SingleEvent_ProducesParseableJsonLine()
{
    var tempDir = CreateTempDirectory();
    string targetFile;

    using (var sink = new JsonlFileLogSink(_clock, tempDir))
    {
        sink.Write(LogLevel.Info, "test message", ..., null);
        // El sink se va a disponer al salir del using, liberando el FileStream.
    }
    // Ahora el archivo está cerrado por el sink y puede leerse sin conflictos.

    var files = Directory.GetFiles(tempDir, "trading-*.jsonl");
    targetFile = files[0];
    var lines = File.ReadAllLines(targetFile);

    Assert.Single(lines);
    // ... más asserts
}
```

**Aplicar este patrón a los cuatro tests:**

1. **`Write_SingleEvent_ProducesParseableJsonLine`** (línea ~55 según el stack trace).
2. **`Write_WithProperties_SerializesAllFields`** (línea ~74).
3. **`Write_WithException_SerializesTypeMessageStackTrace`** (línea ~92).
4. **`Write_ConcurrentFromMultipleThreads_ProducesNoCorruptedLines`** (línea ~155).

**Adaptación al test concurrente:**

El test `Write_ConcurrentFromMultipleThreads_ProducesNoCorruptedLines` probablemente usa `Task.WhenAll(...)` para hacer N escrituras concurrentes y después lee el archivo para verificar que ninguna línea quedó corrupta. El patrón es idéntico:

```csharp
[Fact]
public async Task Write_ConcurrentFromMultipleThreads_ProducesNoCorruptedLines()
{
    var tempDir = CreateTempDirectory();
    const int writeCount = 100;

    string targetFile;
    using (var sink = new JsonlFileLogSink(_clock, tempDir))
    {
        var tasks = Enumerable.Range(0, writeCount).Select(i =>
            Task.Run(() => sink.Write(
                LogLevel.Info,
                "concurrent write {Index}",
                new[] { new KeyValuePair<string, object?>("Index", i) },
                null)));

        await Task.WhenAll(tasks);
        // Al salir del using, todos los writes ya pasaron (await garantizó completion)
        // y el sink se va a disponer correctamente.
    }

    targetFile = Directory.GetFiles(tempDir, "trading-*.jsonl")[0];
    var lines = File.ReadAllLines(targetFile);

    Assert.Equal(writeCount, lines.Length);
    foreach (var line in lines)
    {
        // Parsear cada línea para confirmar que es JSON válido (no corrupta).
        var parsed = JsonDocument.Parse(line);
        Assert.NotNull(parsed);
    }
}
```

### Tests que NO se tocan

Los cuatro tests que **ya pasan** quedan tal cual:

- `Constructor_AtStartup_DeletesFilesOlderThanRetention` (verde).
- `Constructor_CreatesFileForTodaysWallClockDate` (verde).
- `RetentionCleanup_IgnoresFilesWithUnparseableNames` (verde).
- `Write_WhenFileSystemFails_DoesNotPropagateException` (verde).

Estos no necesitan leer el archivo después de escribir, por eso no tropezaron con el conflicto.

### Sobre el directorio temporal

Los tests probablemente usan un helper `CreateTempDirectory()` o similar para crear un directorio único por test. **NO modificar ese helper** salvo que Claude Code identifique que es la causa del conflicto (lo cual no es el caso según los stack traces).

Verificación rápida: si el helper actual genera directorios con `Guid.NewGuid()` o `Path.GetRandomFileName()`, está bien. Si genera siempre el mismo directorio (`Path.Combine(Path.GetTempPath(), "JsonlFileLogSinkTests")` fijo), también hay que arreglarlo para que sea único por test — pero esto es ortogonal al fix principal y solo si se observa.

## Validaciones de salida (a ejecutar por el operador)

```powershell
# Build y tests
dotnet build
dotnet test --filter "FullyQualifiedName~JsonlFileLogSinkTests"
```

**Resultado esperado:** 8 tests verdes de `JsonlFileLogSinkTests` (los 4 que ya pasaban + los 4 reparados).

**Validación completa de todo el repo:**

```powershell
dotnet test
```

**Resultado esperado:** todos los tests del proyecto verdes (incluyendo los preexistentes de Bloque 1/2, Hito A, Hito B, INFRA-2 Pieza A/B).

**Validación operativa del backtest** (para confirmar que el JSONL del backtest también se ve correcto, ya validado con el fix anterior pero verificable de nuevo):

```powershell
# Limpiar JSONL viejos:
Remove-Item "F:\DesarrolloTrading\QuantConnect\Lean\Launcher\bin\Debug\logs\*.jsonl" -ErrorAction SilentlyContinue

# Correr el backtest (desde el IDE).

# Validar:
Get-ChildItem "F:\DesarrolloTrading\QuantConnect\Lean\Launcher\bin\Debug\logs"
# Esperado: UN único archivo trading-{fecha-hoy}.jsonl con miles de líneas.

Get-Content "F:\DesarrolloTrading\QuantConnect\Lean\Launcher\bin\Debug\logs\trading-*.jsonl" | Measure-Object -Line
# Esperado: varios miles de líneas (cada evento del backtest).

Get-ChildItem "F:\DesarrolloTrading\QuantConnect\Lean\Launcher\bin\Debug\logs\trading-*.jsonl" |
    ForEach-Object { Select-String -Path $_.FullName -Pattern "Heartbeat flush timer deshabilitado" }
# Esperado: una línea encontrada.
```

## Riesgos conocidos

1. **Si los tests NO usan `using` block sino que llaman `sink.Dispose()` explícitamente al final del método:** el problema es que la lectura ocurre ANTES del Dispose. Mover el Dispose antes de la lectura (o adoptar `using` block) resuelve igual. El brief recomienda `using` por idiomático y por garantía contra excepciones intermedias.

2. **Si Claude Code detecta que los tests usan algún `IDisposable` adicional o setup que se rompa con el cambio:** reportar y consultar antes de modificar.

3. **El test concurrente puede tener una variante donde el aserto de "no corrupción" sea no triviialmente verificable.** Confirmar que se mantiene la semántica original: contar líneas, parsear cada una como JSON, verificar que ninguna esté truncada o concatenada con otra.

4. **Si por alguna razón los tests NO compilan después del fix** (porque el código original usaba un patrón que no contemplé): reportar el código actual del test antes de modificar.

5. **Importante: NO cambiar el código de producción del `JsonlFileLogSink.cs`.** El `FileShare.Read` actual es deliberado. El fix es solo en los tests.

## Mensaje de commit sugerido

```
fix(tests): JsonlFileLogSinkTests dispone sink antes de leer archivo

Los 4 tests Write_* fallaban con IOException "process cannot access
the file because it is being used by another process" tras el fix
anterior (rotación por wall clock). Antes, cada test usaba un FakeClock
distinto y por lo tanto un archivo distinto, evitando el conflicto.
Ahora todos los tests escriben al mismo trading-{wall-clock-hoy}.jsonl
y la lectura con File.ReadAllLines tropieza con el lock de escritura
del sink.

Fix: adoptar patrón canónico de tests para escritores de archivo:
disponer el sink (vía using block) ANTES de leer el archivo. Esto
libera el FileStream y los buffers, permitiendo que File.ReadAllLines
abra el archivo sin conflictos.

El código de producción del sink NO se toca: el FileShare.Read es
deliberado y correcto para impedir escrituras simultáneas en producción.

Tests afectados:
- Write_SingleEvent_ProducesParseableJsonLine
- Write_WithProperties_SerializesAllFields
- Write_WithException_SerializesTypeMessageStackTrace
- Write_ConcurrentFromMultipleThreads_ProducesNoCorruptedLines
```

## Resumen para el operador al cerrar

Al aplicar este fix:

- Los 4 tests de `Write_*` pasan a verde.
- El comportamiento del sink en producción no cambia.
- El backtest sigue generando un único JSONL por sesión de wall clock con todo el run.

**Próximo paso:** validar tests verdes y avanzar con Pieza C de INFRA-2 (ping externo a Healthchecks.io).
