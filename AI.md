# AI.md - Referencia Técnica de Arquitectura

> Las instrucciones de comportamiento para Claude Code (rol, límites de ejecución, método de trabajo, formato de commits) viven en `CLAUDE.md`. Este archivo es exclusivamente referencia técnica del codebase: arquitectura, convenciones, tipos, anti-patrones.

## 🏛️ Filosofía General y Arquitectura
Este proyecto sigue estrictamente los principios de **Clean Architecture** y **Domain-Driven Design (DDD)**. Está diseñado para aislar la lógica de negocio del motor de trading subyacente (QuantConnect/Lean).

**REGLAS DE ORO (innegociables):**
1. La capa de Dominio (`Trading.Domain`) y la capa de Aplicación (`Trading.Application`) **NUNCA** deben tener referencias a la librería `QuantConnect`. Si tienes que importar `using QuantConnect;` fuera del proyecto `Trading.Strategies`, estás rompiendo la arquitectura.
2. **Prohibido `DateTime.Now` y `DateTime.UtcNow`** fuera de `Trading.Strategies`. Todo acceso al tiempo se hace a través de `IClock`. El no-determinismo temporal es un bug en gestación.
3. **Prohibido el estado estático mutable** en `Trading.Domain` y `Trading.Application`. Cualquier estado vive en objetos inyectados con ciclo de vida explícito.

## 📜 Reglas operativas: POLICY.md como fuente de verdad

El proyecto separa **reglas técnicas** (cómo se construye el sistema: arquitectura, naming, tipos, testing — todas codificadas en este `AI.md`) de **reglas operativas** (cómo se opera el sistema una vez construido: cuándo apagar una estrategia, qué inspeccionar, cómo proceder ante incidentes — codificadas en `POLICY.md` en la raíz del repo).

**Implicancias para el asistente y para Claude Code:**

1. **Cuando un componente runtime implementa una regla operativa, su especificación viene de `POLICY.md`, no se inventa en el componente.** Ejemplo canónico: `StrategyHealthMonitor` (OPS-2) lee los umbrales U1-U4 de `POLICY.md` sección 3. Los números (DD 25%, PF rolling < 1.0, etc.) NO se hardcodean en el código del monitor con valores propios; el monitor los recibe como configuración derivada de POLICY.

2. **Cuando un brief o refactor toca una regla operativa, la fuente que se cita es POLICY, no AI.md.** AI.md no debe duplicar los números de POLICY ni reescribir sus reglas; debe apuntar a POLICY. Si una regla operativa cambia, se cambia en POLICY (con entrada nueva en `DECISIONS.md`) y los componentes que la implementan se actualizan en consecuencia.

3. **POLICY.md tiene su propio régimen de versionado** documentado en su frontmatter (no se modifica en caliente durante un drawdown, los cambios sustantivos requieren ADR, etc.). El asistente respeta ese régimen igual que respeta las reglas de modificación de AI.md.

4. **Ante una decisión que cae en zona gris** (¿esto va en AI o en POLICY?): si la regla afecta cómo se escribe el código, va a AI.md; si afecta cómo se opera el sistema o cuándo se interviene manualmente, va a POLICY.md. Reglas chequeables por compilador o test → AI. Reglas que dispara o consulta un humano o un monitor de risk → POLICY.

## 📂 Estructura de Proyectos y Responsabilidades

### 1. `Trading.Domain` (El Núcleo)
- **Qué contiene:** Interfaces (`IOrderRouter`, `IClock`), Modelos puros (`MarketBar`), Value Objects (`InstrumentId`, `RiskParameters`, `Money`, `Price`, `Quantity`) y Excepciones de Dominio.
- **Reglas:** Cero dependencias externas. Los Value Objects deben validarse por construcción. Usar `InstrumentId` en lugar de `Symbol` de QuantConnect, e `IOrderHandle` en lugar de `OrderTicket`.
- **Determinismo:** El dominio es **síncrono y determinista**. Sin `async`, sin I/O, sin acceso al reloj del sistema, sin random sin semilla. Esto es lo que hace que sea testeable y reproducible en backtesting.

### 2. `Trading.Application` (Casos de Uso)
- **Qué contiene:** Lógica de orquestación (`BarProcessingService`, `OrderLifecycleService`), gestión de riesgo (`KillSwitchManager`) y dimensionamiento (`PositionSizer`).
- **Reglas:** Solo depende de `Trading.Domain`. Interactúa con el exchange inyectando abstracciones (`IPortfolioState`, `IOrderRouter`). No guarda estado dependiente del framework.
- **Async:** Si una operación es asincrónica, debe aceptar `CancellationToken` como último parámetro y propagarlo. Sufijo `Async` obligatorio. Nada de `.Result`, `.Wait()`, ni `async void` (salvo event handlers explícitamente documentados).

### 3. `Trading.Strategies` (Infraestructura / Host)
- **Qué contiene:** El `TradingAlgorithmHost` (hereda de `QCAlgorithm`), Adaptadores (`LeanOrderRouter`, `SystemClock`) y Estrategias (`EmaCrossStrategy`).
- **Reglas:** Es el **ÚNICO** proyecto que conoce a QuantConnect. Hace el wiring en `Initialize()` y traduce eventos de Lean al Dominio (vía Mappers).

### 4. `Trading.TestSupport` (Soporte de Pruebas)
- **Qué contiene:** Implementaciones Fake compartidas (`FakeOrderRouter`, `FakeClock`, `FakePortfolioState`), builders de Value Objects y fixtures comunes.
- **Reglas:** Referenciado solo por proyectos de test. Nunca por proyectos de producción. Evita duplicar Fakes entre suites.

## 🛠️ Convenciones de Estilo y Nomenclatura

1. **Claridad de Variables (Zero Abbreviations):**
   - El formato de las variables locales y propiedades debe ser `camelCase` o `PascalCase` según el estándar de C#.
   - **Prohibido el uso de abreviaturas aisladas:** Todos los nombres deben representar fielmente su propósito. El código debe leerse como prosa técnica.
   - *Ejemplos:* Usa `quantity`, no `qty`. Usa `configuration`, no `config`. Usa `instrumentIdentifier`, no `instId`.
   - *Iteradores:* Incluso en bucles cortos, evita `i` o `j` si el contexto permite un nombre más claro (ej. `instrumentIndex`, `workerIndex`).
   - **Excepción `Id`:** El sufijo `Id` es convención estándar de .NET y está permitido como parte de un nombre compuesto (`OrderId`, `InstrumentId`, `userId`). Lo prohibido es `id` como nombre aislado y ambiguo.

2. **Campos Privados:**
   - Todos los campos privados y de solo lectura (`private readonly`) deben comenzar con un guion bajo `_` seguido de `camelCase`.
   - *Correcto:* `private readonly decimal _maximumDrawdownFraction = 0.25m;`
   - *Incorrecto:* `private readonly decimal maximumDrawdownFraction;` o `private decimal m_drawdown;`

3. **Inmutabilidad por defecto:**
   - **Value Objects:** se implementan con `readonly record struct` (cuando el tamaño lo justifica y se quiere semántica de valor barata) o `sealed record` con propiedades `init`. Nada de setters públicos.
   - **Entidades:** clases selladas con setters privados y mutaciones a través de métodos con nombres del dominio (`Fill`, `Cancel`, `Reject`), no por asignación directa.
   - **Colecciones expuestas:** `IReadOnlyList<T>`, `IReadOnlyCollection<T>` o `ImmutableArray<T>`. Nunca `List<T>` ni arrays mutables en la superficie pública.

4. **Paradigma y Documentación:**
   - Todas las funciones deben estar estrictamente tipadas. Prohibido `dynamic` y `object` salvo en adaptadores.
   - Prefiere **composición sobre herencia**. La herencia solo está justificada para extender `QCAlgorithm` en el Host.
   - Provee documentación estricta (XML comments `///`) para toda lógica matemática, de riesgo o de estado complejo. Incluye fórmula, unidades y rango esperado.

## 🔢 Tipos, Unidades y Money

1. **Numérico financiero:**
   - **Prohibido `double` y `float`** para precios, cantidades, notional, PnL o cualquier magnitud monetaria. Siempre `decimal`.
   - `double` solo se permite para indicadores estadísticos donde la precisión decimal no aporta (ej. correlaciones, z-scores intermedios), y nunca se persiste ni se compara con `==`.
   - Prohibida la conversión implícita o silenciosa entre `double` y `decimal`. Si hace falta, hacer la conversión explícita en el adaptador con comentario justificando.

2. **Value Objects obligatorios:**
   - `Money` (monto + `Currency`), `Price`, `Quantity`, `Notional`. Prohibido pasar `decimal` "pelado" cuando representa una de estas magnitudes.
   - Las operaciones entre monedas distintas deben lanzar `CurrencyMismatchException`. No hay conversión implícita.

3. **Tick size y lot size:**
   - Toda orden generada por Application se redondea según el `InstrumentSpecification` (tick size, lot size, min notional) antes de llegar al `IOrderRouter`. Esto es responsabilidad de `OrderNormalizer`, no de la estrategia.

4. **Porcentajes vs fracciones:**
   - La configuración JSON lee porcentajes (ej. `2.0`). En el dominio (`RiskParameters`), se convierten inmediatamente y se trabaja **solo con fracciones decimales** (ej. `0.02m`).
   - Cualquier propiedad de dominio que sea una fracción lleva el sufijo `Fraction` (ej. `maximumDrawdownFraction`). Lo que viene del JSON lleva `Percentage`.

## ⏱️ Tiempo y Determinismo

1. **Acceso al tiempo:** exclusivamente vía `IClock.UtcNow`. `SystemClock` vive en `Trading.Strategies`. `FakeClock` vive en `Trading.TestSupport`.
2. **Zonas horarias:** el dominio trabaja siempre en UTC (`DateTimeOffset` con offset cero o `DateTime` con `Kind=Utc`). La traducción a horario de mercado vive en el Host.
3. **Timers y schedulers:** se inyectan vía abstracción (`ITimer`, `IScheduler`). Nada de `Task.Delay` directo en Application.
4. **`LeanClock` — regla crítica: usar `_algorithm.UtcTime`, nunca `_algorithm.Time`.**
   - `_algorithm.Time` devuelve la hora en el timezone del algoritmo (en este deployment, EDT=UTC-4 en horario de verano). Comparado contra `DateTime.UtcNow` produce un offset de 4h que rompe cualquier cálculo de staleness o lag temporal.
   - `_algorithm.UtcTime` siempre devuelve UTC real: tiempo simulado UTC en backtest, tiempo UTC del exchange en live.
   - `LeanClock.UtcNow` además incluye fallback a `DateTime.UtcNow` cuando `_algorithm.UtcTime < año 2000` (epoch de QC durante `Initialize()` antes de que el motor inicialice su reloj).
   - Esta regla se aprendió en Hito C (2026-06-03): el bug causó loops de restart cada ~15 min con `staleness=14400s`. Ver ADR-031.

## ⚠️ Errores y Resultados

1. **Excepciones vs Result:**
   - **Excepciones** para condiciones verdaderamente excepcionales: invariantes rotos, configuración inválida al boot, fallas de infraestructura, kill switch activado.
   - **`Result<T>` o `OperationOutcome`** para flujos de negocio esperados que pueden "fallar": orden rechazada por riesgo, instrumento sin liquidez, posición ya cerrada. Estos no son excepciones, son estados.
   - Regla práctica: si el caller razonablemente puede manejarlo y continuar, es `Result`. Si rompe una invariante o exige aborto, es excepción.

2. **Tipos de excepción:**
   - Prohibido lanzar `Exception`, `ApplicationException` o `SystemException` directamente.
   - Cada capa define sus excepciones: `DomainException` (base en `Trading.Domain`), `RiskException`, `OrderRoutingException`, etc.
   - Las excepciones llevan contexto estructurado, no solo mensajes string.

3. **Validación:**
   - Value Objects validan en el constructor y lanzan `DomainException` con detalle del invariante violado.
   - DTOs de configuración se validan con `IValidateOptions<T>` al arranque. Falla rápido: si la config es inválida, el sistema no arranca.

4. **Kill Switch:**
   - Una vez activado, todo intento de enrutar orden retorna `Result.Failure(KillSwitchActive)`. No lanza excepción (es flujo esperado). La activación misma sí registra un evento crítico.

## 📊 Logging, Observabilidad y Auditoría

1. **Logger:** `ILogger<T>` de `Microsoft.Extensions.Logging` inyectado por constructor. Nunca `static` ni `Console.WriteLine`.
2. **Niveles:**
   - `Trace`: detalle de tick/bar (apagado en producción).
   - `Debug`: decisiones de estrategia.
   - `Information`: ciclo de vida de órdenes, cambios de estado.
   - `Warning`: rechazos, retries, condiciones degradadas.
   - `Error`: excepciones manejadas con contexto.
   - `Critical`: kill switch, drawdown breach, pérdida de conexión.
3. **Structured logging:** siempre con placeholders y propiedades nombradas. `_logger.LogInformation("Order {OrderId} filled at {Price}", orderId, fillPrice);` — nunca interpolación de strings.
4. **Correlation:** toda orden lleva un `OrderId` opaco que se propaga en todos los logs y eventos asociados a su ciclo de vida.
5. **Eventos de dominio:** para auditoría regulatoria, las transiciones críticas (`OrderSubmitted`, `OrderFilled`, `RiskLimitBreached`) emiten eventos de dominio inmutables que se persisten para reconstrucción posterior.

6. **Persistencia y observabilidad (post-INFRA-2):**
   - Cada llamada a `ITradingLogger` se persiste como línea JSONL en `logs/trading-{wall-clock-date}.jsonl` con rotación diaria por wall clock real y retención de 30 días. Este sink corre en paralelo al sink de consola de Lean, sin cambiar las firmas de `ITradingLogger`. El campo `timestamp` dentro de cada evento JSON usa el clock del sistema (simulado en backtest, real en live), pero la rotación y retención usan **wall clock real** (`DateTime.UtcNow`) para evitar comportamientos absurdos en backtest (cientos de rotaciones espurias que eliminan los propios logs del run).
   - El estado de salud del sistema se mantiene en `HealthHeartbeatTracker` (suscripto a eventos de dominio) y se flushea atómicamente cada 60 segundos de **wall clock real** a `health/heartbeat.json`. Solo activo en `LiveMode`; en backtest queda congelado al estado del boot.
   - Existe además un dead-man's switch externo vía Healthchecks.io: ping HTTP cada 5 minutos a una URL configurable. Si el ping no llega en 15 minutos, alerta a Telegram. Detalles operativos en ADR-021.

7. **Criterio arquitectónico — `IClock` vs wall clock real:**
   - Componentes del **flujo determinista de trading** (en `Trading.Domain` y `Trading.Application`): siempre `IClock`. Es lo que hace reproducible el backtest.
   - Componentes de **observabilidad y housekeeping de I/O** en `Trading.Strategies` (rotación de archivos de log, timers de heartbeat, pings externos, retención): usan **wall clock real** (`DateTime.UtcNow` directo, o `System.Threading.Timer` cuando corresponda). El housekeeping no participa del determinismo del backtest y debe operar en tiempo real.
   - Esta distinción es la que diferencia comportamiento sensato en backtest vs. comportamiento que dispara cientos de operaciones espurias por minuto simulado. Aprendizaje registrado en ADR-021.

## 🔐 Variables de Entorno

Configuración operativa que NO se commitea al repositorio. Lectura vía `Environment.GetEnvironmentVariable(...)` en `TradingAlgorithmHost.Initialize()` o en componentes específicos de `Trading.Strategies`. Documentar acá toda variable nueva al momento de introducirla.

| Variable | Obligatoria | Default si ausente | Formato esperado | Componente |
|---|---|---|---|---|
| `HEALTHCHECKS_PING_URL` | No | Ping deshabilitado, loguea Warning una sola vez al arranque | `https://hc-ping.com/{UUID}` o `https://healthchecks.io/{UUID}` | `HealthchecksIoPinger` (LeanPaper/LeanLive) |
| `MICROSTRUCTURE_STORE_DIR` | No | `{AppContext.BaseDirectory}\microstructure-live` | Ruta absoluta, ej. `C:\Lean\MicrostructureStore` | `TradingAlgorithmHost` (lectura) |
| `RECORDER_STORAGE_DIR` | No | `{AppContext.BaseDirectory}\microstructure-live` | Ruta absoluta — debe coincidir con `MICROSTRUCTURE_STORE_DIR` | `Trading.Recorder` (escritura) |
| `RECORDER_STRATEGIES_JSON` | No | `{AppContext.BaseDirectory}\strategies.json` | Ruta absoluta al archivo strategies.json | `Trading.Recorder` |
| `RECORDER_RETENTION_DAYS` | No | `7` | Entero positivo | `Trading.Recorder` |
| `RECORDER_WS_URL` | No | `wss://fstream.binance.com` | URL base del WebSocket de Binance Futures | `Trading.Recorder` |
| `RECORDER_WS_USE_SYSTEM_PROXY` | No | `false` (bypass — conexión directa) | `true`/`1` para usar el proxy de sistema del VPS | `SystemWebSocketAdapter` (Recorder) |
| `RECORDER_FEED` | No | `rest` | `rest` (REST polling de aggTrades) o `ws` (WebSocket) | `Trading.Recorder` (selección de `ITradeFeed`) |
| `RECORDER_REST_URL` | No | `https://fapi.binance.com` | URL base del REST de Binance Futures | `BinanceFuturesAggTradesApi` (Recorder) |
| `RECORDER_REST_POLL_SECONDS` | No | `10` | Entero positivo — cadencia de polling por ciclo | `BinanceAggTradeRestFeed` (Recorder) |
| `RECORDER_SEED_ON_STARTUP` | No | `true` | `false`/`0` desactiva el gap-fill desde Vision al arrancar | `StartupSeeder` (Recorder) |
| `RECORDER_SEED_DAYS` | No | `= RECORDER_RETENTION_DAYS` | Entero positivo — días a sembrar si el store está vacío | `StartupSeeder` (Recorder) |
| `RECORDER_HEALTHCHECKS_URL` | No | Ping deshabilitado | `https://hc-ping.com/{UUID}` | `HealthchecksIoPinger` (LeanRecorder) |

**Reglas operativas:**

- Las variables de entorno **NO se commitean** al repositorio bajo ningún concepto. Contienen secretos operativos o configuración por ambiente.
- Si una variable es opcional y no está definida, el componente debe hacer **graceful degradation**: loguear Warning una sola vez al arranque y operar en modo no-op. Nunca romper el arranque del sistema.
- Si una variable es obligatoria y no está definida, el componente debe fallar fast con excepción descriptiva al boot. Hoy no hay variables obligatorias.
- Cuando se introduzca una variable nueva, agregar fila a la tabla arriba en el mismo refactor que la introduce.

**Convención de rutas de publish local — OBLIGATORIO:**

> Toda salida de `dotnet publish` va en `f:\DesarrolloTrading\QuantConnect\Lean\publish\{NombreProyecto}\`.
> Ejemplos: `publish\recorder\`, `publish\lean-launcher\`. Nunca en otra ruta.
> Comando estándar: `dotnet publish {proyecto}.csproj -c Release -r win-x64 --self-contained true -p:ErrorOnDuplicatePublishOutputFiles=false -o f:\DesarrolloTrading\QuantConnect\Lean\publish\{NombreProyecto}`.
> La carpeta `publish\` está en `.gitignore`; nunca commitear artefactos de publish.

**Modelo de deployment VPS (desde Hito C, 2026-06-03):**

Estructura de directorios en el VPS — cada servicio es dueño de su carpeta; el store compartido vive al mismo nivel:

```
C:\Lean\
  Paper\                 ← LeanPaper:    binarios, config.json, logs\, data\
  Live\                  ← LeanLive:     binarios, config.json, logs\, data\
  Recorder\              ← LeanRecorder: binarios, strategies.json, logs\
  MicrostructureStore\   ← Compartido:   escritura = LeanRecorder, lectura = LeanPaper + LeanLive
```

> **Regla de ownership:** `MicrostructureStore\` no pertenece a ningún proceso Lean. Solo el Recorder escribe ahí. Nunca poner el store dentro de `Paper\` ni de `Live\`.

- **OS:** Windows Server.
- **Gestor de servicio:** NSSM (`nssm.exe`). Servicios: `LeanPaper`, `LeanLive`, `LeanRecorder`.
- **Directorio del binario (LeanPaper):** `C:\Lean\Paper\`. El ejecutable principal es `QuantConnect.Lean.Launcher.exe`.
- **Variables de entorno del proceso:** inyectadas via NSSM `AppEnvironmentExtra` (no via `%SystemRoot%\system32\cmd.exe /c set ...`). Formato en la configuración NSSM: `HEALTHCHECKS_PING_URL=https://hc-ping.com/{UUID}`.
- **Restart automático:** `AppExit Default Restart`. El watchdog en `TradingAlgorithmHost` llama `Environment.Exit(1)` ante stall de feed > 1200s; NSSM re-levanta el proceso automáticamente.
- **DLL propia a desplegar:** `Trading.Strategies.dll` (y sus dependencias transitivas `Trading.Application.dll`, `Trading.Domain.dll`, etc.) desde `Trading.Strategies/bin/Debug/net10.0/`. Patrón de deploy: stop servicio → backup DLL anterior → copiar DLL nueva → start servicio.
  - **Desde Hito D (ADR-044/ADR-045):** el fork también diverge en `QuantConnect.Common.dll` (Common: `BinanceOrderProperties.ReduceOnly`) y `QuantConnect.Brokerages.Binance.dll` (`reduceOnly` en `CreateOrderBody`). El atajo incremental "solo Trading.* DLL" ya NO alcanza para esos cambios: hacer `dotnet publish` y copiar el output completo (que incluye esos DLLs), o copiar también esos dos DLLs. Nota de publish: usar `-p:ErrorOnDuplicatePublishOutputFiles=false` (hay `Accord.dll.config` y `config.example.json` duplicados que rompen el publish self-contained). El output no trae `config.json` (se provee en el server). Para correr live: config con `environment: live-futures-binance`, `minimal-position-mode: true`, y API key con Futures trading + IP del VPS en whitelist.
- **Logs del servicio NSSM:** en `C:\Lean\Paper\service-out-{timestamp}.log` (stdout) y `service-err-{timestamp}.log` (stderr). Útiles para diagnosticar fallos de arranque antes de que el JSONL propio esté disponible. El servicio requiere `AppStdout`/`AppStderr` configurados en NSSM o no se generan estos logs.
- **Arranque de LeanLive — `deploy/Start-LeanLive.ps1`:** pre-flight idempotente que valida (fail-fast, sin arrancar a loop de crash) las precondiciones aprendidas en el primer deploy: binarios del fork presentes; `data-folder` con `market-hours-database.json` + `symbol-properties-database.csv` (obligatorios — un `data\` vacío crashea `LiveTradingRealTimeHandler`; los copia de `C:\Lean\Paper\data` si faltan); `config.json` sin `PLACEHOLDER_`, `environment` y `minimal-position-mode` correctos; reloj vs Binance < umbral (anti `-1021`, ADR-043); store fresco; y la config NSSM (stdout/stderr, restart, env vars). Correr como admin. El script fuerza TLS 1.2 (PS 5.1 del VPS no lo hace).

## 🧪 Testing

1. **Framework:** xUnit + FluentAssertions. Prohibido `Assert.Equal` pelado cuando hay un assert fluido más expresivo.
2. **Fakes vs Mocks:** preferir **Fakes** (implementaciones reales pero simplificadas, en `Trading.TestSupport`) sobre **Mocks** (Moq/NSubstitute). Los mocks solo para verificar interacciones específicas, no para simular comportamiento.
3. **Nunca levantes QuantConnect en tests unitarios.** Si necesitás test de integración con Lean, va en un proyecto separado `Trading.IntegrationTests` y corre fuera del pipeline rápido.
4. **Property-based testing:** para invariantes de riesgo y matemáticas de dimensionamiento usar FsCheck. Ejemplo: "para cualquier `RiskParameters` válido, el tamaño de posición resultante nunca excede el `maximumPositionFraction`".
5. **Determinismo en tests:** `FakeClock` con tiempo controlado, semillas fijas para cualquier RNG, sin `Thread.Sleep`.
6. **Naming de tests:** `MethodUnderTest_Scenario_ExpectedBehavior` o estilo Given-When-Then. Cero ambigüedad.
7. **Tests obligatorios al crear una estrategia nueva o agregar un indicador:**
   Toda estrategia nueva (cualquier clase que implemente `IStrategy`) debe ir acompañada en el mismo commit/refactor de dos tipos de tests:

   - **Test de referencia del/los indicador(es) usados.** Por cada indicador nuevo que la estrategia utilice (que no haya sido validado antes en otra estrategia), se agrega un test en `Trading.Application.Tests/Indicators/` que verifica que el indicador de QuantConnect produce valores equivalentes a una librería de referencia (TA-Lib o equivalente) sobre una serie sintética conocida. Tolerancia relativa 1e-6. Si el indicador ya tiene test de referencia por una estrategia previa, no se duplica.

   - **Test de comportamiento de la estrategia con datos sintéticos.** Se agrega un test en `Trading.Application.Tests/Strategies/` que:
     - Construye una serie de barras sintéticas donde las condiciones que dispararían la señal se cumplen deliberadamente en una barra conocida.
     - Pasa esas barras una por una a `EvaluateSignal`.
     - Hace assert sobre la señal emitida en la barra esperada (`Long`, `Short` o `Flat`) y sobre las barras anteriores/posteriores.

   Razón: la fidelidad de las señales se garantiza con tests unitarios estáticos contra valores de referencia, NO con auditoría runtime durante el backtest. Esta política está documentada en ADR-014. Cualquier estrategia que se agregue al sistema sin estos dos tests rompe la cobertura institucional del proyecto.

   Patrón de referencia: ver `ExponentialMovingAverageReferenceTests` y `EmaCrossStrategyTests` como ejemplos vivos del estándar.

## ⚙️ Convenciones Críticas de Dominio

1. **Gestión de Órdenes Asíncronas (`OrderRegistry`):**
   - Cuando el Dominio envía una orden, genera un tag opaco (ej. `ord_123456`). Lean procesa y responde. El `OrderEventMapper` lee el tag para recuperar el contexto (Entry, StopLoss, etc.) e invocar un `OrderLifecycleEvent`.
   - El tag es generado por un `IOrderIdGenerator` inyectado, no por concatenación inline. Esto permite tests deterministas.

2. **Fracciones vs Porcentajes:** ver sección "Tipos y Unidades" arriba.

3. **Configuración:**
   - Schema vive en `Trading.Configuration/Schemas`. Validación con `IValidateOptions<T>` al boot.
   - Si falta un campo obligatorio o un valor está fuera de rango, el sistema **no arranca**. Mensaje de error debe indicar archivo, campo y rango esperado.

## 🔬 Pipeline de Research e Incorporación de Estrategias

Proceso estándar para cualquier nueva hipótesis de trading. **Las fases son lineales con early exit**: si una falla, la hipótesis se descarta y no se avanza a la siguiente. El rationale completo y las alternativas consideradas están en ADR-040.

### Fase 0 — M4 (Python, validación rápida)

Script en `Trading.Research/m4_*.py`. No requiere tocar código C#.

- Período IS: 2021-01-01 → 2024-12-31. Activos: BTC, ETH, SOL. Comisión: 0.04% round-trip.
- **Gate:** Sharpe ≥ 0.5 en al menos 2 de los 3 activos.
- **Regla crítica (bloqueante):** el script DEBE trackear estado de posición abierta. Señales solapadas sin posición cerrada inflan el Sharpe artificialmente y producen un falso positivo en M4. Sin este tracking, el gate no tiene valor estadístico.
- Si falla → documentar en `Trading.Research/strategy_experiments.md`. No implementar `IStrategy`.

### Fase 1 — Implementación `IStrategy`

- Crear clase con `WarmUpBars` = período del indicador más lento.
- Registrar en `StrategyFactory.Create`.
- Escribir en el **mismo commit**: test de referencia del indicador + test de comportamiento con barras sintéticas (ver sección Testing).
- Commit sin resultados de backtest todavía.

### Fase 2 — QC IS (In-Sample 2021-2024)

`strategies.json`: BTC/ETH/SOL 1h, SL/TP según la estrategia, Risk=1%.
`StrategyHealthMonitor`: **NullStrategyHealthMonitor** (mide el edge puro sin que OPS-2 interrumpa).
`ConsecutiveLossesMonitor`: activo (guard de riesgo del portfolio).
`SetStartDate(2021, 1, 1); SetEndDate(2024, 12, 31);`

Procedimiento operativo obligatorio antes de cada run:
1. `dotnet build Trading.Strategies/Trading.Strategies.csproj`
2. Copiar `Trading.Strategies/strategies.json` → `Launcher/bin/Debug/strategies.json`
3. Verificar que **no existe** `Launcher/bin/Debug/net10.0/Trading.Strategies.dll` (el Lean Loader prioriza ese subdirectorio — si existe una versión vieja ahí, se carga esa en lugar del DLL compilado).

**Gate QC IS (M1):** Sharpe del portfolio combinado ≥ 0.5.
Si falla → `git rm` de la clase y sus tests. Documentar en `Trading.Research/strategy_experiments.md`.

### Fase 3 — QC OOS (Out-of-Sample 2025-presente)

Mismo `strategies.json`. Solo cambia el período:
`SetStartDate(2025, 1, 1); SetEndDate(año, mes, día);` — fecha lo más cercana al día de evaluación.
Mismo procedimiento operativo que Fase 2.
Exportar `transaction-log.csv` con nombre explícito: `{hipótesis}-oos-2025-{año}.csv`.
No hay gate numérico en esta fase — el CSV alimenta Fase 4.

### Fase 4 — Trading.Analytics (Gate 1 + Gate 2)

```
dotnet run --project Trading.Analytics -- --is-log <csv> --oos-log <csv> --strategy <nombre> --output <dir>
```

Gate 1 (OOS determinista — todos deben pasar): Trades ≥ 50, NetProfit > 0, Sharpe ≥ 0.3, PF ≥ 1.1, Expectancy > 0.
Gate 2 (Monte Carlo 10k — todos deben pasar): P(Sharpe < 0) ≤ 20%, Mediana MaxDD ≤ 55%, P5 CAGR > −5%.

Si falla → `git rm`. Documentar en `Trading.Research/strategy_experiments.md`.

**Si APROBADA:**
- Agregar entrada en `POLICY.md` sección 7 (estado pre-paper, umbrales U1-U4).
- Actualizar `ROADMAP.md` con métricas IS/OOS.
- Commit: `feat(hito-G): <Nombre>Strategy APROBADA IS=X.XX / OOS=X.XX`.

### Monitor en IS vs producción

| Contexto | StrategyHealthMonitor |
|---|---|
| QC IS / QC OOS (investigación) | `NullStrategyHealthMonitor` |
| Paper / Live | `StrategyHealthMonitor` real (OPS-2) |

### Nomenclatura de archivos de resultados

```
F:\Lean\data\results\backtest-logs\{hipótesis}-{estrategia}-is-{año_ini}-{año_fin}.csv
F:\Lean\data\results\backtest-logs\{hipótesis}-{estrategia}-oos-{año_ini}-{año_fin}.csv
F:\Lean\data\results\analytics\validation-{estrategia}-{YYYYMMDD}.md
```

---

## 📁 Convenciones de archivos — store de microestructura

El store de microestructura usa la convención `{ticker}_{timeframe}_live.csv` (p.ej. `BTCUSDT_1h_live.csv`, `ETHUSDT_5m_live.csv`). Un `PersistentMicrostructureStore` recibe el `timeframe` en su constructor y lo incorpora en el nombre de archivo.

**Columnas (desde ADR-051):** `bar_utc,ofi,cvd_delta,cvd,arrival_rate,mean_trade_size,buy_sell_ratio,price_return,open,high,low,close,volume`. El OHLCV se apenda al final (no se inserta) para no correr los índices de las features: un store viejo de 8 columnas se sigue leyendo con OHLCV en 0. El OHLC habilita el warmup de estrategias desde el store en `Initialize()` (live), reproduciendo las barras por `IStrategy.EvaluateSignal` sin depender de history de precios del broker (que en live-tick no existe). Cambiar el formato del store requiere **re-seed** (borrar los `*_live.csv` y dejar que el Recorder re-siembre desde Vision).

**Regla de escritura única:** el grabador (`Trading.Recorder`) es el **único proceso que escribe** a estos archivos. Lean (`TradingAlgorithmHost`) los lee en `Initialize()` y luego computa en memoria sin persistir. Nunca añadir llamadas a `PersistentMicrostructureStore.Append()` en el host.

---

## 🚫 Anti-patrones Prohibidos (Cheat Sheet)

Listado explícito de cosas que **nunca** deben aparecer en código de este proyecto:

- `using QuantConnect;` fuera de `Trading.Strategies`.
- `Symbol`, `OrderTicket`, `Slice`, `Securities` u otros tipos de QC fuera de `Trading.Strategies`.
- `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now` fuera de adaptadores en `Trading.Strategies`.
- `_algorithm.Time` en `LeanClock` o en cualquier adaptador de QCAlgorithm (devuelve timezone local del algoritmo, no UTC). Siempre `_algorithm.UtcTime`.
- `double` o `float` para precios, cantidades o dinero.
- Conversión implícita `double` → `decimal`.
- `decimal` "pelado" donde corresponde un Value Object (`Money`, `Price`, `Quantity`).
- `throw new Exception(...)` o `throw new ApplicationException(...)`.
- `catch (Exception)` sin re-throw o sin logging estructurado del contexto.
- `async void` (salvo event handlers documentados).
- `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` en código de producción.
- Métodos `async` sin `CancellationToken`.
- Estado `static` mutable en Domain o Application.
- `Console.WriteLine` para logging.
- Interpolación de strings en mensajes de log (`$"..."`). Usar placeholders.
- `List<T>` o arrays mutables en propiedades públicas.
- Setters públicos en Value Objects.
- Herencia entre estrategias (usar composición y `IStrategyComponent`).
- Tests que dependan del reloj del sistema o del orden de ejecución.
- Magic numbers sin constante nombrada con XML doc explicando el porqué.
- Abreviaturas aisladas: `qty`, `cfg`, `mgr`, `svc`, `repo` como nombres de variables o parámetros.
