# INFRA-2 — Monitoreo básico del sistema en producción

> **Brief ejecutable para Claude Code CLI.** Segundo refactor del Bloque 3, pre-Hito C (paper trading). Dota al sistema de las tres capas mínimas de observabilidad necesarias para operar sin volar a ciegas: persistencia de logs estructurados, heartbeat local de salud, y ping externo a un servicio de dead-man's switch (Healthchecks.io). Foco en salud **del sistema** (¿está vivo?, ¿recibe datos?, ¿kill switch activo?), NO en performance de las estrategias (eso es OPS-2).
>
> **Pre-requisitos:** Bloque 1 y 2 completos. Hito A v2 y Hito B (sus tres pasos) completos. INFRA-1 commiteado y verde. Test `AccordHmmClassifierReferenceTests` skipeado documentado en ADR-020 (deuda DEUDA-1, fuera del alcance de este brief). Estado de tests al iniciar: todos verdes salvo el skipeado.

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio de los puntos críticos:

- **Cero comandos `git` de cualquier tipo.** Lista exhaustiva en `AI.md`. No worktrees, no ramas, no checkouts, no commits.
- **No compilar.** El usuario compila.
- **No correr tests.** El usuario corre tests.
- **No ejecutar el sistema.** En este brief no hay excepciones autorizadas para ejecutar código (a diferencia del Paso 3 del Hito B, donde el trainer sí se ejecutaba).
- **Si Claude Code detecta una inconsistencia entre el código actual y este brief**, detenerse y reportar. NO improvisar.
- **Al final del trabajo**, proponer el mensaje de commit sugerido según la política del `AI.md`.

**Modalidad de ejecución por piezas:** este brief se ejecuta en **tres piezas separadas (A, B, C)**, en orden. Al finalizar cada pieza, Claude Code debe **detenerse y reportar al usuario** qué se hizo, qué validar manualmente, y esperar la confirmación "seguí con la pieza siguiente" antes de continuar. Esto permite al operador commitear pieza por pieza si lo prefiere, o todas juntas al final.

---

## Contexto y motivación

El sistema actual, cuando termina un backtest, escupe logs por la consola de Lean y termina. Eso está bien para investigación. Para paper trading (Hito C) y live (Hito D) ese modelo es insuficiente por tres razones distintas que se confunden bajo la palabra "monitoreo":

1. **Detección de caídas (liveness).** Si el proceso muere a las 3am, el operador no se entera hasta la mañana siguiente. Mientras tanto, posiciones abiertas quedan sin gestión: sin kill switch activo, sin filtro de régimen, sin trailing reactivo. El tiempo entre "el sistema murió" y "el operador se entera" es riesgo puro.

2. **Detección de patologías silenciosas (correctness en vivo).** El proceso puede estar vivo y aun así estar enfermo: dejó de recibir barras (feed interrumpido pero el `.exe` sigue), el kill switch se activó y no se notó, no se generan señales hace 48hs cuando históricamente se generan varias por día. Estos son los bugs operativos más caros porque no producen excepciones, producen silencio.

3. **Persistencia de evidencia (post-mortem).** Cuando algo sale mal y se quiere entender qué pasó tres días atrás, los logs de consola de Lean ya no están. Hace falta logs estructurados, persistentes, con timestamp y nivel, que sobrevivan al ciclo de vida del proceso y sean parseables.

INFRA-2 cubre los tres con tres piezas mínimas. **No hace más que eso, y conviene que no haga más.**

**Fuera de alcance explícito:**

- Métricas de performance del trading (P&L, drawdown, Sharpe rolling). Eso es OPS-2.
- Dashboard visual de métricas. Sobre-ingeniería para una sola máquina; alcanza con que los JSONL sean inspeccionables con `jq` y el heartbeat sea leíble como archivo.
- Logs centralizados (Seq, Datadog, Loki). Sobre-ingeniería para una sola máquina; si en el futuro se corre en cloud con múltiples nodos, se evalúa.
- Métricas de salud de estrategias individuales. Eso es OPS-2 (StrategyHealthMonitor).

---

## Decisiones técnicas aplicadas (no discutir, aplicar)

| Decisión | Valor |
|---|---|
| Formato de logs persistentes | **JSONL** (una línea JSON por evento), `System.Text.Json`. Una sola línea por evento, sin indent. |
| Path de logs | `logs/trading-{yyyy-MM-dd}.jsonl` relativo a `AppContext.BaseDirectory`. Crear directorio `logs/` si no existe. |
| Rotación de logs | Diaria automática. Nombre del archivo calculado en cada `Write` desde `IClock.UtcNow.Date`. Al cambiar el día, cerrar writer actual, abrir uno nuevo. |
| Retención de logs | **30 días.** Parametrizable por constructor, default 30. Al rotar (o al arranque), eliminar archivos `trading-*.jsonl` con fecha anterior a `IClock.UtcNow.Date.AddDays(-30)`. |
| Path del heartbeat | `health/heartbeat.json` relativo a `AppContext.BaseDirectory`. Crear directorio `health/` si no existe. |
| Cadencia del flush del heartbeat | **60 segundos.** Disparado vía `Schedule.On(...)` de QC (NO `System.Threading.Timer`, NO `Task.Delay`). |
| Escritura atómica del heartbeat | Escribir a `heartbeat.json.tmp`, luego `File.Move` con sobrescritura. Evita que lector externo vea archivo a medio escribir. |
| Servicio externo de alerta | **Healthchecks.io** (plan gratis). Integración con Telegram. Cuenta y check los crea el operador manualmente; el código consume la URL del ping. |
| Cadencia del ping externo | **5 minutos** (throttle interno; el callback corre cada 60s pero el pinger solo dispara HTTP real cada 5min). Tolerancia configurada en Healthchecks.io: 15 minutos sin ping → alerta. |
| Configuración del ping URL | Variable de entorno `HEALTHCHECKS_PING_URL`. **No se commitea** al repo. Si la variable no está definida o el formato no es válido, el pinger queda en no-op y se loguea Warning una sola vez al arranque. |
| Lifecycle del HttpClient | Un único `HttpClient` instanciado en `TradingAlgorithmHost.Initialize()`, vivo todo el run. No usar `IHttpClientFactory` (sobre-ingeniería). |
| Thread-safety | Tanto `JsonlFileLogSink` como `HealthHeartbeatTracker` deben tener lock interno: callbacks de fills pueden venir de threads distintos al thread principal de barras. |
| Acceso al tiempo | Exclusivamente vía `IClock` inyectado. Cero `DateTime.UtcNow` directo en código nuevo. |

---

## Alcance detallado por pieza

El brief se ejecuta en tres piezas estrictamente secuenciales: **A → B → C**. Cada una es commit-eable por separado y debe estar verde antes de avanzar.

---

### Pieza A — Persistencia de logs estructurados (JSONL)

**Objetivo:** que cada llamada a `ITradingLogger` quede persistida como línea JSON en archivo local, en paralelo al sink actual que escribe a `QCAlgorithm.Log/Debug/Error`.

#### Nuevos artefactos a crear

**1. `Trading.Domain/Abstractions/LogLevel.cs`** (nuevo enum)

```csharp
namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Niveles de severidad de log del dominio. Espejo de los cinco métodos de ITradingLogger.
    /// NO referenciar Microsoft.Extensions.Logging.LogLevel (rompería la regla de cero
    /// dependencias externas en Domain).
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
}
```

**2. `Trading.Domain/Abstractions/IStructuredLogSink.cs`** (nueva interfaz)

```csharp
using System;
using System.Collections.Generic;

namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Contrato para sinks de logs estructurados. Implementaciones (ej. JsonlFileLogSink)
    /// viven fuera del dominio. Esta interfaz expresa solo el contrato.
    /// </summary>
    public interface IStructuredLogSink
    {
        void Write(
            LogLevel level,
            string messageTemplate,
            IReadOnlyList<KeyValuePair<string, object?>> properties,
            Exception? exception);
    }
}
```

**3. `Trading.Strategies/Adapters/LogTemplateRenderer.cs`** (nuevo helper estático)

Extraer la lógica de parseo de placeholders nombrados que hoy vive embebida en `LeanLogger.Format`. Expone dos métodos:

```csharp
public static class LogTemplateRenderer
{
    /// <summary>
    /// Renderiza el template aplicando los argumentos. Equivalente a la lógica actual
    /// de LeanLogger.Format. Si los conteos no coinciden, devuelve lo que pudo renderizar
    /// sin lanzar excepción.
    /// </summary>
    public static string Render(string messageTemplate, object[] arguments);

    /// <summary>
    /// Extrae los nombres de los placeholders del template y los emparenta posicionalmente
    /// con los argumentos. Si el template no tiene placeholders o si los conteos no
    /// coinciden, devuelve los pares que pudo emparentar. NUNCA lanza excepción.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, object?>> ExtractProperties(
        string messageTemplate, object[] arguments);
}
```

La regex `\{([A-Za-z_][A-Za-z0-9_]*)\}` ya existe en `LeanLogger`; trasladarla al helper.

**4. `Trading.Strategies/Adapters/JsonlFileLogSink.cs`** (implementación de `IStructuredLogSink`)

Responsabilidades:

- Constructor recibe: `IClock clock`, `string baseDirectoryPath` (default `AppContext.BaseDirectory`), `int retentionDays` (default 30).
- Calcular el directorio `logs/` y crearlo si no existe.
- Mantener un `StreamWriter` interno + `FileStream` con `FileShare.Read` (para permitir `tail -f` o `jq` mientras el sistema escribe).
- Al cada `Write`: comparar `clock.UtcNow.Date` con la fecha del archivo abierto; si cambió, cerrar el actual, abrir uno nuevo para la nueva fecha, ejecutar limpieza de retención.
- `flush` por línea (sin buffering interno) para que los logs sean visibles inmediatamente.
- Lock interno alrededor de `Write` para thread-safety.
- Serializar cada evento como una sola línea JSON con este esquema:

```json
{
  "timestamp": "2026-05-20T14:32:17.123Z",
  "level": "Info",
  "messageTemplate": "Order {OrderId} filled at {Price}",
  "renderedMessage": "Order ABC123 filled at 50000",
  "properties": { "OrderId": "ABC123", "Price": 50000 },
  "exception": null
}
```

- Si hay `exception`, serializarla como objeto con `Type` (full name), `Message`, `StackTrace`.
- Si la escritura falla (disco lleno, permisos, etc.): tragar la excepción internamente, NO propagar. Un log mal escrito no puede romper trading. Opcionalmente registrar en un campo interno `_lastWriteFailure` para diagnóstico, pero no rethrow.
- Implementar `IDisposable` para flush + close del writer al final del proceso.

**Limpieza de retención:** método privado que lista archivos `trading-*.jsonl` en el directorio `logs/`, parsea la fecha del nombre, y elimina los anteriores a `clock.UtcNow.Date.AddDays(-retentionDays)`. Si el parseo del nombre falla (archivo ajeno en el directorio), saltarlo sin error. Se invoca: (a) al arranque (constructor), (b) al rotar archivo.

#### Modificaciones a artefactos existentes

**5. `Trading.Strategies/Adapters/LeanLogger.cs`** (refactor)

- Agregar parámetro `IStructuredLogSink structuredLogSink` al constructor.
- Cada método (`Debug`, `Info`, `Warning`, `Error`, `Critical`):
  1. Llamar a `LogTemplateRenderer.ExtractProperties(template, args)` para obtener las propiedades.
  2. Llamar a `_structuredLogSink.Write(LogLevel.X, template, properties, exception: null)`.
  3. Llamar al `QCAlgorithm` como hoy (preservar comportamiento actual).
- Los métodos públicos de `ITradingLogger` **no cambian de firma**. Esto es solo internal refactor + nuevo sink en paralelo.
- Mover la lógica de la regex `NamedPlaceholderPattern` y el método `Format` al `LogTemplateRenderer` (re-export como helper estático). `LeanLogger.Format` puede mantenerse como wrapper privado que llama al renderer, o eliminarse llamando directamente al renderer.

**6. `Trading.Strategies/TradingAlgorithmHost.cs`** (wiring)

En `Initialize()`, después de instanciar `_clock` y antes de `_logger`:

```csharp
_structuredLogSink = new JsonlFileLogSink(_clock);
_logger = new LeanLogger(this, _structuredLogSink);
```

Documentar con comentario que el sink escribe a `logs/trading-{fecha}.jsonl` y que la retención por default es 30 días.

#### Tests requeridos (Pieza A)

Ubicación: `Trading.Application.Tests` para `LogTemplateRendererTests`, `Trading.Strategies.Tests` (crear si no existe) para `JsonlFileLogSinkTests`.

**`LogTemplateRendererTests`:**

- `Render_WithMatchingArgs_ReplacesPlaceholders`.
- `Render_WithFewerArgsThanPlaceholders_DoesNotThrow_ReturnsBestEffort`.
- `Render_WithMoreArgsThanPlaceholders_DoesNotThrow_IgnoresExtras`.
- `Render_WithEscapedBraces_PreservesThem` (caso `"{{literal}}"`).
- `Render_WithNoPlaceholders_ReturnsTemplateUnchanged`.
- `ExtractProperties_WithMatchingArgs_ReturnsPairs`.
- `ExtractProperties_WithNoPlaceholders_ReturnsEmpty`.
- `ExtractProperties_WithMismatchedCounts_ReturnsBestEffortPairs_DoesNotThrow`.

**`JsonlFileLogSinkTests`** (usar `FakeClock` de `Trading.TestSupport` y `Path.GetTempPath()` con subdirectorio único por test):

- `Write_SingleEvent_ProducesParseableJsonLine`.
- `Write_WithProperties_SerializesAllFields`.
- `Write_WithException_SerializesTypeMessageStackTrace`.
- `Write_AcrossDayBoundary_RotatesFile` (avanzar `FakeClock` un día entre dos `Write`).
- `Constructor_AtStartup_DeletesFilesOlderThanRetention` (crear archivos con fechas viejas, instanciar sink, verificar eliminación).
- `Rotation_DeletesFilesOlderThanRetention` (similar, pero la eliminación se dispara al rotar).
- `Write_ConcurrentFromMultipleThreads_ProducesNoCorruptedLines` (Task.WhenAll de N writes, leer archivo, verificar que cada línea es JSON parseable).
- `Write_WhenFileSystemFails_DoesNotPropagateException` (forzar fallo, por ejemplo path inválido, verificar que `Write` no lanza).
- `RetentionCleanup_IgnoresFilesWithUnparseableNames` (crear archivo `foo.jsonl` ajeno, verificar que no se elimina y no se loggea error fatal).

**Tests preexistentes:** todos los tests de `LeanLogger`, `KillSwitchManager`, `OrderLifecycleService`, etc. deben seguir verdes. Si alguno se rompe por el cambio de constructor de `LeanLogger`, actualizar el wiring del test pero NO cambiar la lógica testeada.

#### Criterio de aceptación de Pieza A

- Compilación verde sin warnings nuevos.
- Todos los tests preexistentes verdes.
- Todos los tests nuevos verdes.
- (Verificación manual del operador post-commit, no parte del brief): correr el backtest existente, verificar que el archivo `logs/trading-{fecha}.jsonl` existe en `Launcher/bin/Debug/logs/`, que es JSON válido por línea, y que aparecen los cinco niveles de log.

#### Acción al finalizar Pieza A

**Detenerse.** Reportar al usuario:
- Archivos creados/modificados.
- Tests agregados y conteo total verde.
- Verificación manual recomendada.
- Esperar confirmación explícita "seguí con Pieza B" antes de continuar.

---

### Pieza B — Heartbeat local

**Objetivo:** componente que mantiene en memoria el estado de salud del sistema (suscrito a eventos de dominio) y lo flushea periódicamente a un archivo JSON local.

#### Nuevos artefactos a crear

**1. `Trading.Domain/Events/BarProcessedEvent.cs`** (nuevo evento)

```csharp
using System;
using Trading.Domain.ValueObjects;

namespace Trading.Domain.Events
{
    /// <summary>
    /// Se emite al final del procesamiento exitoso de una barra por BarProcessingService.
    /// NO se emite en los caminos de early-return (skip por config, error de sizing, etc).
    /// Consumido por HealthHeartbeatTracker para mantener el timestamp de la última
    /// barra procesada exitosamente, insumo del heartbeat de liveness del feed de mercado.
    /// </summary>
    public sealed record BarProcessedEvent(
        DateTime TimestampUtc,
        DateTime BarTimestampUtc,
        InstrumentId InstrumentId) : IDomainEvent;
}
```

**2. `Trading.Application/Health/HealthSnapshot.cs`** (nuevo record inmutable)

```csharp
using System;

namespace Trading.Application.Health
{
    /// <summary>
    /// Snapshot inmutable del estado de salud del sistema en un instante.
    /// Serializado a JSON por HeartbeatFileWriter. Todos los campos nullable
    /// reflejan "todavía no ocurrió" (estado normal al inicio del proceso).
    /// </summary>
    public sealed record HealthSnapshot(
        DateTime CurrentUtc,
        DateTime ProcessStartedUtc,
        DateTime? LastBarProcessedUtc,
        DateTime? LastBarTimestampUtc,
        DateTime? LastOrderSubmittedUtc,
        DateTime? LastOrderFilledUtc,
        DateTime? LastRiskBreachUtc,
        string? LastRiskBreachReason,
        bool KillSwitchActive);
}
```

**3. `Trading.Application/Health/HealthHeartbeatTracker.cs`** (componente con estado)

Responsabilidades:

- Constructor recibe: `IDomainEventBus eventBus`, `IClock clock`, `ITradingLogger logger`.
- En el constructor: guardar `_processStartedUtc = clock.UtcNow`, y suscribirse a los cuatro eventos:
  - `BarProcessedEvent` → actualizar `_lastBarProcessedUtc` y `_lastBarTimestampUtc`.
  - `OrderSubmittedEvent` → actualizar `_lastOrderSubmittedUtc`.
  - `OrderFilledEvent` → actualizar `_lastOrderFilledUtc`.
  - `RiskLimitBreachedEvent` → actualizar `_lastRiskBreachUtc`, `_lastRiskBreachReason`, `_killSwitchActive = true`.
- Método público `Snapshot()` devuelve `HealthSnapshot` con todos los campos actuales + `currentUtc = clock.UtcNow`.
- Lock interno alrededor de cada actualización y de `Snapshot()` para garantizar consistencia.

**4. `Trading.Strategies/Adapters/HeartbeatFileWriter.cs`** (writer)

Responsabilidades:

- Constructor recibe: `HealthHeartbeatTracker tracker`, `IClock clock`, `ITradingLogger logger`, `string baseDirectoryPath` (default `AppContext.BaseDirectory`).
- Calcular `_targetPath = Path.Combine(baseDirectoryPath, "health", "heartbeat.json")` y crear directorio `health/` si no existe.
- Método `Flush()`:
  1. Obtener `tracker.Snapshot()`.
  2. Serializar con `System.Text.Json` (`WriteIndented = true` para legibilidad manual).
  3. Escribir a `_targetPath + ".tmp"`.
  4. `File.Move(tmp, _targetPath, overwrite: true)` para escritura atómica.
  5. Si cualquier paso falla: loggear Warning con el detalle, NO propagar excepción.

#### Modificaciones a artefactos existentes

**5. `Trading.Application/BarProcessingService.cs`** (emitir nuevo evento)

Al final del camino exitoso del método principal de procesamiento de barra (después de cualquier emisión de orden, antes del return), publicar:

```csharp
_domainEventBus.Publish(new BarProcessedEvent(
    _clock.UtcNow,
    marketBar.TimestampUtc,
    marketBar.InstrumentId));
```

**Importante:** NO emitir en los caminos de early-return (skip por config inválida, skip por sizing fallido, skip por filtro de régimen, etc). El evento significa "barra procesada exitosamente end-to-end", no "barra recibida".

**6. `Trading.Strategies/TradingAlgorithmHost.cs`** (wiring + scheduler)

- En `Initialize()`, después del `_domainEventBus` y antes del wiring de risk:
  ```csharp
  _healthHeartbeatTracker = new HealthHeartbeatTracker(domainEventBus, _clock, _logger);
  _heartbeatFileWriter = new HeartbeatFileWriter(_healthHeartbeatTracker, _clock, _logger);
  ```
- Al final de `Initialize()`, hacer un `_heartbeatFileWriter.Flush()` inicial para que el archivo exista desde el arranque.
- Agendar el flush periódico:
  ```csharp
  Schedule.On(
      DateRules.EveryDay(),
      TimeRules.Every(TimeSpan.FromSeconds(60)),
      () => _heartbeatFileWriter.Flush());
  ```
  Documentar con comentario por qué se usa el scheduler de QC y NO `System.Threading.Timer` (preservar determinismo del backtest).

#### Tests requeridos (Pieza B)

**`HealthHeartbeatTrackerTests`** (Trading.Application.Tests):

- `Construction_WithEmptyBus_SnapshotHasNullsExceptProcessStarted`.
- `BarProcessedEvent_UpdatesLastBarProcessedAndBarTimestamp`.
- `OrderSubmittedEvent_UpdatesLastOrderSubmitted`.
- `OrderFilledEvent_UpdatesLastOrderFilled`.
- `RiskLimitBreachedEvent_UpdatesAllRiskFieldsAndSetsKillSwitchActive`.
- `Snapshot_FromMultipleThreadsConcurrently_DoesNotCorruptState` (Parallel.For invocando Snapshot mientras se publican eventos).

**`BarProcessingServiceTests`** (extender los existentes):

- `Process_HappyPath_PublishesBarProcessedEvent`.
- `Process_WhenSkippedByRegimeFilter_DoesNotPublishBarProcessedEvent`.
- `Process_WhenSizingFails_DoesNotPublishBarProcessedEvent`.

**Tests de `HeartbeatFileWriter`** son **opcionales** y de bajo valor (escribe a disco, lógica trivial). Si Claude Code los considera valiosos, agregarlos en `Trading.Strategies.Tests` con directorio temporal. Si los omite, OK.

#### Criterio de aceptación de Pieza B

- Compilación verde sin warnings nuevos.
- Tests preexistentes verdes (incluyendo todos los del Bloque 1/2 y de Hito B).
- Tests nuevos verdes.
- (Verificación manual del operador post-commit): correr el backtest, verificar que aparece el archivo `health/heartbeat.json`, que es JSON válido, y que sus timestamps avanzan durante el run.

#### Acción al finalizar Pieza B

**Detenerse.** Reportar al usuario, esperar confirmación "seguí con Pieza C".

---

### Pieza C — Ping externo a Healthchecks.io

**Objetivo:** dead-man's switch externo. Cada 5 minutos, el sistema hace un HTTP GET a una URL de Healthchecks.io configurada vía variable de entorno. Si el ping no llega cuando se espera, Healthchecks.io alerta al operador vía Telegram.

#### Nuevos artefactos a crear

**1. `Trading.Strategies/Adapters/HealthchecksIoPinger.cs`** (cliente HTTP minimal)

Responsabilidades:

- Constructor recibe: `string? pingUrl` (nullable), `HttpClient httpClient`, `IClock clock`, `ITradingLogger logger`.
- Validación del `pingUrl` en el constructor:
  - Si es null o vacío → modo no-op. Loggear UN Warning UNA vez: `"Healthchecks.io ping deshabilitado: variable HEALTHCHECKS_PING_URL no definida."`. Marcar campo `_enabled = false`.
  - Si no matchea regex `^https://(hc-ping\.com|healthchecks\.io)/.+` → modo no-op. Loggear Warning con el detalle de por qué se rechaza. Marcar `_enabled = false`.
  - Si es válido → `_enabled = true`, guardar URL, guardar `_lastPingUtc = null`.
- Método `async Task PingAsync(CancellationToken ct)`:
  - Si `!_enabled`: return inmediato.
  - Throttle: si `_lastPingUtc != null` y `clock.UtcNow - _lastPingUtc < TimeSpan.FromMinutes(5)`: return sin pingear.
  - Hacer `httpClient.GetAsync(_pingUrl, ct)` con timeout de 10 segundos (vía `CancellationTokenSource` con `CancelAfter`).
  - Si responde 2xx: actualizar `_lastPingUtc = clock.UtcNow`, return ok.
  - Si responde otro código, lanza `HttpRequestException`, o timeout: loggear Warning con detalle, NO propagar.

**Importante:** este componente NUNCA debe lanzar excepción al caller. Un ping fallido no puede romper trading.

#### Modificaciones a artefactos existentes

**2. `Trading.Strategies/TradingAlgorithmHost.cs`** (wiring + integración con scheduler)

- En `Initialize()`:
  ```csharp
  var pingUrl = Environment.GetEnvironmentVariable("HEALTHCHECKS_PING_URL");
  _httpClient = new HttpClient();
  _healthchecksPinger = new HealthchecksIoPinger(pingUrl, _httpClient, _clock, _logger);
  ```
- Modificar el callback del scheduler agendado en Pieza B para que, después del `Flush()`, dispare el ping:
  ```csharp
  Schedule.On(
      DateRules.EveryDay(),
      TimeRules.Every(TimeSpan.FromSeconds(60)),
      () =>
      {
          _heartbeatFileWriter.Flush();
          // async void deliberado: el scheduler de QC espera Action síncrona.
          // El pinger garantiza no propagar excepciones internamente.
          _ = _healthchecksPinger.PingAsync(CancellationToken.None);
      });
  ```
  Documentar con comentario el "fire and forget" y por qué es seguro acá.
- Implementar (o extender si ya existe) el `OnEndOfAlgorithm` para hacer `_httpClient.Dispose()`.

#### Tests requeridos (Pieza C)

Ubicación: `Trading.Strategies.Tests` (o `Trading.Application.Tests` si se prefiere por proximidad con los otros tests de adapters).

**`HealthchecksIoPingerTests`** (usar un `HttpMessageHandler` fake para interceptar las llamadas HTTP):

- `Construction_WithNullUrl_LogsWarningAndDisablesPinger`.
- `Construction_WithEmptyUrl_LogsWarningAndDisablesPinger`.
- `Construction_WithMalformedUrl_LogsWarningAndDisablesPinger` (ej. `"http://example.com/foo"`).
- `Construction_WithValidUrl_EnablesPinger` (no loggea Warning, queda armado).
- `PingAsync_WhenDisabled_DoesNotCallHttpClient` (handler que lanza si se invoca; verificar que no se invoca).
- `PingAsync_WhenEnabledAndFirstCall_InvokesHttpClient`.
- `PingAsync_TwiceWithinFiveMinutes_OnlyInvokesHttpClientOnce` (throttle test con FakeClock).
- `PingAsync_AfterFiveMinutes_InvokesHttpClientAgain` (avanzar FakeClock 5 minutos).
- `PingAsync_WhenServerReturns500_LogsWarning_DoesNotThrow`.
- `PingAsync_WhenHttpRequestExceptionThrown_LogsWarning_DoesNotThrow`.
- `PingAsync_WhenTimeoutOccurs_LogsWarning_DoesNotThrow` (handler que demora más de 10s, verificar que el ping retorna).

#### Criterio de aceptación de Pieza C

- Compilación verde sin warnings nuevos.
- Tests preexistentes verdes.
- Tests nuevos verdes.
- (Verificación manual del operador post-commit): setear la variable `HEALTHCHECKS_PING_URL` con la URL real del check creado en Healthchecks.io, correr el backtest unos minutos, verificar en el dashboard de Healthchecks.io que aparecen pings.

#### Acción al finalizar Pieza C

**Detenerse.** Reportar al usuario, esperar confirmación para proceder con la actualización de documentos.

---

### Cierre: actualización de documentos

Después de que las tres piezas estén verdes (con o sin commits intermedios, según prefiera el operador), en una pasada final:

**1. `ROADMAP.md`:**
- Mover INFRA-2 de la tabla "Refactors pendientes" a la sección "Historial completado".
- Marcar como ✅ con fecha del día.
- Resumen breve: tres piezas (logs JSONL, heartbeat local, ping Healthchecks.io), 30 días de retención, cadencia 60s flush / 5min ping externo.

**2. `DECISIONS.md`:** agregar **ADR-021** con la siguiente estructura:

- **Título:** "Monitoreo básico para paper trading: JSONL local + heartbeat + Healthchecks.io"
- **Fecha:** del día.
- **Estado:** Aceptada.
- **Contexto:** el problema de los tres ejes (liveness, patologías silenciosas, post-mortem) que motiva INFRA-2 antes del Hito C. La consola de Lean no persiste, no alerta, no permite reconstruir eventos pasados.
- **Decisión:** tres piezas con los valores específicos (paths, cadencias, retención, servicio externo).
- **Alternativas consideradas:**
  - Seq / Datadog / Loki (logs centralizados): sobre-ingeniería para una máquina, infra adicional, costo recurrente. Descartado.
  - Uptime Kuma (alternativa self-hosted a Healthchecks.io): requiere hostear el monitor en la misma máquina que se quiere monitorear, lo cual derrota el propósito. Descartado.
  - Pingdom / UptimeRobot: similares a Healthchecks.io en concepto, pero menos orientados al patrón "dead-man's switch" (chequean URLs públicas, no esperan pings entrantes). Descartado.
  - Posponer al Bloque 4: descartado, son la mínima precondición operativa razonable para paper trading.
- **Consecuencias:**
  - El sistema queda con observabilidad local (JSONL + heartbeat) + alerta externa de caída total (Healthchecks.io).
  - Deuda implícita: no hay dashboard visual de métricas operativas. Inspección vía `jq` y lectura manual de `heartbeat.json`. Aceptable mientras corra una sola estrategia en una sola máquina.
  - Variable de entorno `HEALTHCHECKS_PING_URL` requerida en el ambiente de producción. Si no está, el ping queda deshabilitado y se loggea Warning (graceful degradation; no rompe arranque).
  - Las métricas de performance del trading (P&L, drawdown rolling) NO están cubiertas por INFRA-2; corresponden a OPS-2.

**3. `AI.md`:** ampliar la sección "Logging, Observabilidad y Auditoría" con:

- Persistencia de logs: cada llamada a `ITradingLogger` se persiste como línea JSONL en `logs/trading-{fecha}.jsonl` con rotación diaria y retención de 30 días. El sink JSON es paralelo al sink de consola de Lean.
- Heartbeat: el estado de salud del sistema se mantiene en `HealthHeartbeatTracker` y se flushea atómicamente cada 60 segundos a `health/heartbeat.json`. Contiene timestamps de última barra procesada, última orden, último breach de risk, estado del kill switch.
- Sección nueva "Variables de entorno" (al nivel de Logging y Errores): documentar `HEALTHCHECKS_PING_URL` como opcional. Formato esperado: `https://hc-ping.com/{UUID}` o `https://healthchecks.io/{UUID}`. Si no está definida, el ping externo queda deshabilitado y el sistema loggea Warning al arranque. Esta variable contiene un secreto operativo y **no se commitea** al repositorio.

---

## Validaciones de salida (a ejecutar por el usuario)

```bash
# Invariantes arquitectónicas
grep -rn "^using QuantConnect" Trading.Domain/ Trading.Application/ Trading.Application.Tests/ Trading.Domain.Tests/
# Debe devolver vacío.

grep -rn "DateTime\.UtcNow\|DateTime\.Now" Trading.Domain/ Trading.Application/
# Debe devolver vacío (todo acceso al tiempo vía IClock).

grep -rn "System\.Threading\.Timer\|Task\.Delay" Trading.Application/ Trading.Strategies/
# Validar que el único Task.Delay (si existe) esté justificado. El scheduling
# del heartbeat debe usar Schedule.On de QC, NO Timer ni Task.Delay.

# Build
dotnet build

# Tests
dotnet test

# Post-backtest manual:
ls -la Launcher/bin/Debug/net10.0/logs/
ls -la Launcher/bin/Debug/net10.0/health/
jq '.level' Launcher/bin/Debug/net10.0/logs/trading-*.jsonl | sort | uniq -c
jq . Launcher/bin/Debug/net10.0/health/heartbeat.json
```

**Tests esperados después de INFRA-2:**

- Todos los tests previos siguen verdes.
- Pieza A agrega ~15-20 tests entre `LogTemplateRendererTests` y `JsonlFileLogSinkTests`.
- Pieza B agrega ~9 tests entre `HealthHeartbeatTrackerTests` y las extensiones de `BarProcessingServiceTests`.
- Pieza C agrega ~10 tests en `HealthchecksIoPingerTests`.
- Total nuevos: aproximadamente 35-40 tests.

---

## Riesgos conocidos y cómo el asistente debe manejarlos

1. **`System.Text.Json` con `WriteIndented` en el heartbeat es deliberado.** Es el único JSON del proyecto que va indentado. Los logs JSONL van compactos (una línea por evento). No "uniformar" formatos.

2. **El proyecto `Trading.Strategies.Tests` puede no existir aún.** Si no existe, crearlo con la estructura mínima (referencia a `Trading.Strategies`, `Trading.TestSupport`, `xunit`, `Moq` si se está usando). Reportar la creación al operador.

3. **`HttpClient` long-lived es deliberado.** No es socket exhaustion porque hay un solo cliente. NO introducir `IHttpClientFactory` "por buenas prácticas": agregaría dependencia `Microsoft.Extensions.Http` por nada.

4. **El `async void` del callback del scheduler es deliberado.** El scheduler de QC espera `Action` síncrona, y `PingAsync` retorna `Task`. La forma idiomática es `_ = _healthchecksPinger.PingAsync(...)` (fire-and-forget). Es seguro porque el pinger garantiza no propagar excepciones internamente. Documentar el porqué en el comentario.

5. **Si Claude Code detecta que `BarProcessingService` no recibe `IDomainEventBus` por constructor**, reportar y detener. (Debería recibirlo desde Refactor B3 del Bloque 1; si no es así, hay desincronización con el estado actual del código).

6. **Si Claude Code encuentra que `LeanLogger` ha cambiado significativamente respecto a lo descripto en este brief**, reportar y detener. NO improvisar el refactor sobre una base distinta.

7. **El throttle del pinger usa `IClock`, no `DateTime.UtcNow`.** Esto es crítico para que los tests con `FakeClock` puedan controlar el avance del tiempo. NO usar `DateTime.UtcNow` en ningún lugar del código nuevo.

8. **Si por alguna razón Accord.NET (de Hito B) interfiere con la serialización JSON nueva**: reportar, no inventar workarounds. El JSON de logs y del heartbeat usa `System.Text.Json` puro, no debería haber conflicto.

---

## Mensaje de commit sugerido (al cerrar el trabajo)

Si el operador prefiere un solo commit para INFRA-2 completo:

```
feat(infra): INFRA-2 monitoreo básico para paper trading

- Pieza A: persistencia de logs estructurados a JSONL en logs/trading-{fecha}.jsonl
  con rotación diaria y retención de 30 días. Nuevas abstracciones IStructuredLogSink
  y LogLevel en Trading.Domain. Implementación JsonlFileLogSink en Trading.Strategies.
  Helper LogTemplateRenderer extraído de LeanLogger. Sink JSON paralelo al sink de
  consola de Lean (firmas de ITradingLogger sin cambios).

- Pieza B: heartbeat local de salud vía HealthHeartbeatTracker suscripto a eventos
  de dominio (BarProcessedEvent nuevo, OrderSubmittedEvent, OrderFilledEvent,
  RiskLimitBreachedEvent). Flush atómico a health/heartbeat.json cada 60 segundos
  vía Schedule.On de QC. BarProcessingService emite BarProcessedEvent en el camino
  feliz (no en early-returns).

- Pieza C: ping externo a Healthchecks.io cada 5 minutos con throttle interno.
  URL configurable vía variable de entorno HEALTHCHECKS_PING_URL (graceful no-op
  si no está definida). HttpClient long-lived, dispose en OnEndOfAlgorithm.
  Nunca propaga excepciones al caller (no puede romper trading).

- DECISIONS: ADR-021 nuevo documentando las decisiones de monitoreo.
- AI.md: sección Logging ampliada, sección Variables de entorno nueva.
- ROADMAP: INFRA-2 marcado completo. Movido al historial.

Cierra INFRA-2 del Bloque 3. Próximo: OPS-1 (POLICY.md).
Refs ADR-021
```

Si el operador prefiere commits separados por pieza:

```
# Pieza A
feat(observability): persistir logs estructurados a JSONL con rotación diaria

[detalle de Pieza A]

Parte 1/3 de INFRA-2.

# Pieza B
feat(observability): heartbeat local de salud del sistema

[detalle de Pieza B]

Parte 2/3 de INFRA-2.

# Pieza C
feat(observability): ping externo a Healthchecks.io para dead-man's switch

[detalle de Pieza C]

Cierra INFRA-2 del Bloque 3. Refs ADR-021
```

---

## Resumen para el operador al final de INFRA-2

Al cerrar INFRA-2, el sistema queda con:

- **Logs persistentes y estructurados** en JSONL, rotados diariamente, con 30 días de retención. Inspección vía `jq` desde la línea de comandos.
- **Heartbeat local** que refleja el estado de salud del sistema en `health/heartbeat.json`, actualizado cada 60 segundos.
- **Dead-man's switch externo** vía Healthchecks.io que alerta a Telegram si el sistema deja de pingear durante 15 minutos.
- **INFRA-2 completo.** Tres piezas cerradas, ADR-021 documentado, AI.md y ROADMAP actualizados.

**Próxima decisión operativa del operador después de commitear:**

- Crear cuenta en Healthchecks.io si todavía no se hizo, integrar Telegram, crear el check con período 5 min / grace 15 min, exportar la URL como variable de entorno `HEALTHCHECKS_PING_URL` en el ambiente de ejecución.
- Iniciar OPS-1 (Trading Policy Document, POLICY.md) — siguiente refactor del Bloque 3.
