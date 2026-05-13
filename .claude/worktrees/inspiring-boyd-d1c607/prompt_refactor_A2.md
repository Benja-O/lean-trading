# Refactor A2 — Logging estructurado con placeholders nombrados

## Contexto previo (leer antes de tocar nada)

Antes de empezar leé en este orden:
1. `AI.md` — sección **📊 Logging, Observabilidad y Auditoría** y la lista de **🚫 Anti-patrones**.
2. `DECISIONS.md` — entrada **ADR-007** (define la forma de este refactor).
3. `ROADMAP.md` — fila del refactor **A2** en Bloque 1.

**Regla rectora (ADR-007):** mantener `ITradingLogger` como abstracción del dominio. **No agregar** dependencia a `Microsoft.Extensions.Logging` en `Trading.Domain` ni `Trading.Application`. El refactor cambia la **firma** del contrato y migra todos los callers. La implementación interna (`LeanLogger`) puede usar `ILogger<T>` por debajo si lo considerás útil, pero no es obligatorio para este refactor.

---

## Objetivo

Reemplazar las llamadas con interpolación de strings (`$"..."`) por **structured logging** con templates + argumentos nombrados, agregar los niveles `Warning` y `Critical`, y eliminar prefijos manuales de timestamp en los mensajes.

---

## Cambios concretos

### 1. Nuevo contrato `ITradingLogger` (`Trading.Domain/Abstractions/ITradingLogger.cs`)

Reemplazar el contrato actual por:

```csharp
namespace Trading.Domain.Abstractions
{
    /// <summary>
    /// Logger del dominio con soporte para structured logging.
    ///
    /// Los mensajes se pasan como TEMPLATE con placeholders nombrados estilo Serilog/MEL
    /// (ej. "Order {OrderId} filled at {Price}") y los valores se pasan como argumentos
    /// posicionales en el mismo orden. La implementación es responsable de combinar template
    /// y argumentos para producir el mensaje final o de preservar la estructura para
    /// agregadores que la consuman (ej. Seq, Elastic).
    ///
    /// PROHIBIDO en los callers: interpolación de strings ($"...") como messageTemplate.
    /// Eso anula el propósito de structured logging y reintroduce el anti-patrón que este
    /// contrato elimina (ver AI.md sección Logging y Anti-patrones).
    /// </summary>
    public interface ITradingLogger
    {
        /// <summary>Detalle fino para diagnóstico (eventos residuales, transiciones internas).</summary>
        void Debug(string messageTemplate, params object[] arguments);

        /// <summary>Ciclo de vida normal: órdenes, cambios de estado.</summary>
        void Info(string messageTemplate, params object[] arguments);

        /// <summary>Condiciones degradadas, rechazos, retries.</summary>
        void Warning(string messageTemplate, params object[] arguments);

        /// <summary>Excepciones manejadas o errores con contexto.</summary>
        void Error(string messageTemplate, params object[] arguments);

        /// <summary>Eventos críticos: kill switch, drawdown breach, pérdida de conexión.</summary>
        void Critical(string messageTemplate, params object[] arguments);
    }
}
```

Notas:
- **Cinco niveles**: `Debug`, `Info`, `Warning`, `Error`, `Critical`. `Trace` se omite por ahora — no hay nada que loggear a ese nivel hoy, y agregarlo vacío es ruido.
- **Sin sobrecargas sin args**: si alguien quiere loggear un literal, igual pasa `params object[] {}` vacío. Mantiene la API mínima.
- **No agregar overloads con `Exception ex`** en esta pasada — ningún call site actual lo necesita y abre superficie sin uso.

### 2. Adaptar `LeanLogger` (`Trading.Strategies/Adapters/LeanLogger.cs`)

`QCAlgorithm` solo expone `Debug(string)`, `Log(string)`, `Error(string)` — no tiene noción de niveles intermedios ni de structured logging. La adaptación combina template + args con `string.Format` y mapea niveles así:

| Nivel dominio | Método QC |
|---|---|
| `Debug` | `_algorithm.Debug(...)` |
| `Info` | `_algorithm.Log(...)` |
| `Warning` | `_algorithm.Log("WARN: ...")` |
| `Error` | `_algorithm.Error(...)` |
| `Critical` | `_algorithm.Error("CRITICAL: ...")` |

**Detalle importante sobre `string.Format` y placeholders nombrados:** `string.Format` solo entiende placeholders posicionales (`{0}`, `{1}`). Los placeholders nombrados (`{OrderId}`, `{Price}`) son convención de structured logging (MEL/Serilog) — `string.Format` los trata como literales y falla. Por eso `LeanLogger` debe convertir los placeholders nombrados a posicionales antes de formatear.

Implementación sugerida (regex simple, suficiente para nuestros templates que no contienen llaves escapadas ni format specifiers):

```csharp
using System;
using System.Text.RegularExpressions;
using QuantConnect.Algorithm;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Adapters
{
    /// <summary>
    /// Adapta los métodos de log de QCAlgorithm al contrato ITradingLogger del dominio.
    ///
    /// Convierte templates con placeholders nombrados ({OrderId}, {Price}) a posicionales
    /// ({0}, {1}) antes de pasarlos a string.Format. QCAlgorithm no soporta structured
    /// logging nativo; si en el futuro se persisten eventos estructurados a un sink externo,
    /// se hará desde una capa distinta sin tocar este adaptador.
    /// </summary>
    public sealed class LeanLogger : ITradingLogger
    {
        // Captura {Identifier} donde Identifier es alfanumérico (sin format specifier
        // como {Foo:N2}, que no usamos por convención en este sistema).
        private static readonly Regex NamedPlaceholderPattern =
            new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

        private readonly QCAlgorithm _algorithm;

        public LeanLogger(QCAlgorithm algorithm)
        {
            _algorithm = algorithm;
        }

        public void Debug(string messageTemplate, params object[] arguments)
            => _algorithm.Debug(Format(messageTemplate, arguments));

        public void Info(string messageTemplate, params object[] arguments)
            => _algorithm.Log(Format(messageTemplate, arguments));

        public void Warning(string messageTemplate, params object[] arguments)
            => _algorithm.Log("WARN: " + Format(messageTemplate, arguments));

        public void Error(string messageTemplate, params object[] arguments)
            => _algorithm.Error(Format(messageTemplate, arguments));

        public void Critical(string messageTemplate, params object[] arguments)
            => _algorithm.Error("CRITICAL: " + Format(messageTemplate, arguments));

        private static string Format(string messageTemplate, object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
            {
                return messageTemplate;
            }

            int placeholderIndex = 0;
            string positionalTemplate = NamedPlaceholderPattern.Replace(
                messageTemplate,
                _ => "{" + placeholderIndex++ + "}");

            return string.Format(positionalTemplate, arguments);
        }
    }
}
```

### 3. Migrar `FakeTradingLogger` (en `Trading.Application.Tests/Fakes/FakeTradingLogger.cs`)

Reemplazar las tres `List<string>` por una lista única de entradas estructuradas. Esto preserva la semántica de structured logging en tests y permite asserts sobre argumentos individuales.

```csharp
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;

namespace Trading.Application.Tests.Fakes
{
    public enum LogLevel { Debug, Info, Warning, Error, Critical }

    public sealed record CapturedLogEntry(
        LogLevel Level,
        string MessageTemplate,
        IReadOnlyList<object> Arguments);

    public sealed class FakeTradingLogger : ITradingLogger
    {
        private readonly List<CapturedLogEntry> _entries = new();

        public IReadOnlyList<CapturedLogEntry> Entries => _entries;

        public IEnumerable<CapturedLogEntry> EntriesAtLevel(LogLevel level)
            => _entries.Where(entry => entry.Level == level);

        public IReadOnlyList<CapturedLogEntry> DebugEntries
            => EntriesAtLevel(LogLevel.Debug).ToList();
        public IReadOnlyList<CapturedLogEntry> InfoEntries
            => EntriesAtLevel(LogLevel.Info).ToList();
        public IReadOnlyList<CapturedLogEntry> WarningEntries
            => EntriesAtLevel(LogLevel.Warning).ToList();
        public IReadOnlyList<CapturedLogEntry> ErrorEntries
            => EntriesAtLevel(LogLevel.Error).ToList();
        public IReadOnlyList<CapturedLogEntry> CriticalEntries
            => EntriesAtLevel(LogLevel.Critical).ToList();

        public void Debug(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Debug, messageTemplate, arguments));
        public void Info(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Info, messageTemplate, arguments));
        public void Warning(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Warning, messageTemplate, arguments));
        public void Error(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Error, messageTemplate, arguments));
        public void Critical(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Critical, messageTemplate, arguments));
    }
}
```

### 4. Actualizar el test único que assertea contenido

`KillSwitchManagerTests.cs`, test `ActivateKillSwitch_LiquidatesAndLogsError`. Cambiar:

```csharp
// Antes
Assert.Single(_logger.ErrorMessages);
Assert.Contains("test reason", _logger.ErrorMessages[0]);
```

por:

```csharp
// Después
Assert.Single(_logger.ErrorEntries);
Assert.Contains("test reason", _logger.ErrorEntries[0].Arguments.Cast<object>().Select(arg => arg?.ToString()));
```

(Si `FluentAssertions` ya está disponible — verificá en los `.csproj` de test — preferir `_logger.ErrorEntries.Should().ContainSingle().Which.Arguments.Should().Contain("test reason");`. Esto es lo que pide AI.md sección Testing punto 1. Si no está, dejar xUnit puro como arriba y NO agregar el paquete en este refactor.)

---

## Migración de los 10 call sites

A continuación cada call site con su forma actual y la forma objetivo. **Asumí placeholders en `PascalCase`** (convención de structured logging) y respetá los nombres exactos de propiedades de dominio (`OrderId`, `ExecutorIdentifier`, `Status`, `Purpose`, `InstrumentId`, `Reason`, `Price`).

### `Trading.Application/OrderProcessing/OrderLifecycleService.cs`

**L161 (Error):**
```csharp
// Antes
_logger.Error(
    $"OrderLifecycleService: ExecutorIdentifier '{lifecycleEvent.ExecutorIdentifier}' " +
    $"no encontrado. Evento ignorado (Status={lifecycleEvent.Status}, Purpose={lifecycleEvent.Purpose}).");

// Después
_logger.Error(
    "OrderLifecycleService: ExecutorIdentifier '{ExecutorIdentifier}' no encontrado. " +
    "Evento ignorado (Status={Status}, Purpose={Purpose}).",
    lifecycleEvent.ExecutorIdentifier, lifecycleEvent.Status, lifecycleEvent.Purpose);
```

**L182 / L189 / L195 (Info, patrón repetido):** son tres mensajes "Cancelando X de '{executor}' por Y Hit". Mantenelos como tres llamadas separadas (no extraer helper en este refactor — scope creep), pero unificá el template:

```csharp
// L182
_logger.Info(
    "Cancelando TakeProfit de '{ExecutorIdentifier}' por {Reason}.",
    strategyExecutor.ExecutorIdentifier, "Stop Loss Hit");

// L189
_logger.Info(
    "Cancelando StopLoss de '{ExecutorIdentifier}' por {Reason}.",
    strategyExecutor.ExecutorIdentifier, "Take Profit Hit");

// L195
_logger.Info(
    "Cancelando StopLoss y TakeProfit de '{ExecutorIdentifier}' por {Reason}.",
    strategyExecutor.ExecutorIdentifier, "Time Exit Hit");
```

### `Trading.Application/Risk/KillSwitchManager.cs`

**L505 (Error → Critical):** este es un evento crítico de negocio (kill switch). AI.md §Logging pide `Critical` para esto. Además se **elimina el prefijo de timestamp manual** (responsabilidad del logger, no del caller).

```csharp
// Antes
_logger.Error($"{_clock.UtcNow:u}: !!! KILL SWITCH ACTIVADO ({reason}). !!!");

// Después
_logger.Critical("Kill switch activado. Reason={Reason}", reason);
```

**L531 (Debug, mismo tratamiento de timestamp):**

```csharp
// Antes
_logger.Debug($"{_clock.UtcNow:u}: [SISTEMA] Reseteo completo. Reanudando.");

// Después
_logger.Info("Cooling-off period finalizado. Sistema reanudado.");
```

Notá dos cambios en L531: (a) timestamp manual fuera, (b) sube de `Debug` a `Info` porque "reanudación del sistema tras kill switch" es un cambio de estado relevante, no detalle de diagnóstico.

### `Trading.Application/Sizing/PositionSizer.cs`

**L578 (Error):**

```csharp
// Antes
_logger.Error(
    $"PositionSizer: precio inválido ({price}) para {strategyExecutor.InstrumentId}. Orden bloqueada.");

// Después
_logger.Error(
    "PositionSizer: precio inválido ({Price}) para {InstrumentId}. Orden bloqueada.",
    price, strategyExecutor.InstrumentId);
```

**Nota para vos (Claude Code):** este call site va a desaparecer en el refactor B1 (`Result<T>` reemplaza el magic value `0m`). NO lo elimines en A2 — A2 solo migra logging. B1 lo borra después.

### `Trading.Strategies/Adapters/OrderEventMapper.cs`

**L3271 (Error):**

```csharp
// Antes
logger.Error(
    $"OrderEventMapper: evento sin tag (OrderId={orderEvent.OrderId}, " +
    $"Status={orderEvent.Status}). Posible orden externa o liquidación global. Ignorado.");

// Después
logger.Error(
    "OrderEventMapper: evento sin tag (OrderId={OrderId}, Status={Status}). " +
    "Posible orden externa o liquidación global. Ignorado.",
    orderEvent.OrderId, orderEvent.Status);
```

**L3287 (Debug):**

```csharp
// Antes
logger.Debug(
    $"OrderEventMapper: tag '{clientTag}' ya fue procesado (Forget previo). " +
    $"Status={orderEvent.Status}. Evento residual esperado, ignorado por el dominio.");

// Después
logger.Debug(
    "OrderEventMapper: tag '{ClientTag}' ya fue procesado (Forget previo). " +
    "Status={Status}. Evento residual esperado, ignorado por el dominio.",
    clientTag, orderEvent.Status);
```

**L3295 (Debug):**

```csharp
// Antes
logger.Debug(
    $"OrderEventMapper: tag externo '{clientTag}' no proviene del OrderRegistry. " +
    $"Status={orderEvent.Status}. Probablemente liquidación global. Ignorado por el dominio.");

// Después
logger.Debug(
    "OrderEventMapper: tag externo '{ClientTag}' no proviene del OrderRegistry. " +
    "Status={Status}. Probablemente liquidación global. Ignorado por el dominio.",
    clientTag, orderEvent.Status);
```

---

## Validación post-refactor

Antes de dar por cerrado el refactor, corré:

1. **Build limpio:** `dotnet build` sin warnings nuevos.
2. **Tests:** `dotnet test`. Los 21 existentes deben seguir pasando. Solo se toca `ActivateKillSwitch_LiquidatesAndLogsError` (forma del assert, no semántica).
3. **Grep de regresión:** ninguno de estos comandos debe devolver matches en `Trading.Application/` ni `Trading.Domain/`:

   ```bash
   grep -rn '_logger\.\(Debug\|Info\|Warning\|Error\|Critical\)(\$"' Trading.Application Trading.Domain
   grep -rn 'logger\.\(Debug\|Info\|Warning\|Error\|Critical\)(\$"' Trading.Application Trading.Domain
   ```

   (En PowerShell: `Select-String -Path Trading.Application\**\*.cs,Trading.Domain\**\*.cs -Pattern '_?logger\.(Debug|Info|Warning|Error|Critical)\(\$"'`)

   Si aparece algún match, es una llamada con interpolación que se escapó: arreglala antes de cerrar.

4. **Verificación arquitectónica (ADR-001 invariante):**

   ```bash
   grep -rn "^using QuantConnect" Trading.Domain Trading.Application
   ```

   Debe estar vacío. Si después de este refactor aparece algún match, rompiste la arquitectura: revertí ese cambio.

---

## Lo que NO se hace en este refactor

Explícito para evitar scope creep:

- **NO** se reemplaza `ITradingLogger` por `ILogger<T>`. ADR-007 mantiene la abstracción de dominio.
- **NO** se agregan scopes, correlation IDs ni propiedades estructuradas avanzadas. AI.md §Logging punto 4 habla de "OrderId propagado en todos los logs" — eso es parte del refactor B3 (eventos de dominio).
- **NO** se cambian niveles de log más allá de los dos casos justificados arriba (L505 Error→Critical, L531 Debug→Info).
- **NO** se toca `PositionSizer.CalculateQuantity` para devolver `Result<T>`. Eso es B1.
- **NO** se separa el `OrderEventMapper` estático en una clase instanciable. Es deuda registrada para revisión futura.

---

## Cuando termines

1. Actualizá `ROADMAP.md`:
   - Mover la fila A2 de "Refactors pendientes / Bloque 1" a "Historial completado" con fecha y resumen breve.
2. Actualizá `AI.md` si querés reflejar que el contrato real es `ITradingLogger` con placeholders nombrados (la sección Logging hoy menciona `ILogger<T>` como referencia conceptual; el contrato propio del dominio cumple el mismo principio). Opcional — ADR-007 ya documenta la decisión.
3. **No** agregues entradas nuevas a `DECISIONS.md` por este refactor: las decisiones relevantes ya están en ADR-007. Si encontrás un caso límite que requiera decisión nueva, paralo y consultá antes de decidir vos.

---

## Nota sobre encoding

El bundle de código que se compartió en sesión anterior mostraba mojibake en strings con acentos (`activaciÃ³n`, `pÃ©rdidas`). Asumimos que **el código real en disco está bien codificado en UTF-8**. Si al abrir los archivos encontrás caracteres corruptos, **no los reescribas mecánicamente** — pará y avisá al usuario antes de tocar nada, porque puede ser un problema de encoding del repo que necesita arreglo separado.
