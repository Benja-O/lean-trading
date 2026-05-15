# DECISIONS - Architecture Decision Records

> **Propósito:** registro de decisiones arquitectónicas tomadas durante el desarrollo del sistema. Cada entrada explica QUÉ se decidió, POR QUÉ, y QUÉ alternativas se consideraron y descartaron.
>
> **Reglas:**
> - Entradas en orden cronológico inverso (la más reciente primero).
> - Cada entrada tiene fecha, contexto, decisión, alternativas, consecuencias.
> - Las decisiones que se revierten NO se borran: se marcan como "Revertida en ADR-NNN" y se mantienen para historia.
> - Identificador correlativo `ADR-NNN`.

---

## ADR-016 — Trading Policy escrita y monitor runtime de degradación: simetría a la regla de entrada
**Fecha:** 2026-05-15
**Estado:** Aceptada

### Contexto
El sistema tiene definida implícitamente una regla de **entrada** inquebrantable: no se opera con capital real una estrategia que no superó la validación robusta del Hito G (walk-forward + Monte Carlo + métricas estratificadas). Esta regla está distribuida en la arquitectura: tests obligatorios por estrategia (ADR-014), `IValidateOptions<T>` al boot, `RiskParameters` con invariantes.

No existe una regla análoga de **salida**: nada del sistema actual responde a la pregunta "¿cuándo una estrategia que ya está corriendo deja de tener derecho a operar?". El operador queda obligado a tomar esa decisión en runtime, generalmente bajo estrés (drawdown sostenido, racha de pérdidas, métricas degradándose) y sin criterio escrito previamente. En la práctica institucional, esa es la decisión que más frecuentemente se ejecuta mal, y la causa raíz es estructural: lo que no está codificado o documentado de forma versionada se negocia con uno mismo en el peor momento posible.

Adicionalmente, el refactor #4 (ADR-015) dejó el sistema en open-closed sobre `IRiskMonitor`: agregar un monitor nuevo no requiere modificar nada existente. Esa puerta está abierta y la degradación estadística de una estrategia en vivo es exactamente el tipo de condición que debería detectarse vía monitor.

### Decisión
Introducir dos artefactos complementarios en el Bloque 3 (pre-Hito C, paper trading):

- **OPS-1 — `POLICY.md`:** documento markdown versionado en el repo, escrito antes de iniciar paper trading. Codifica por estrategia y a nivel sistema: umbrales numéricos de drawdown que disparan reducción/pausa/kill definitivo; criterios cuantitativos de "estrategia muerta" (rolling Sharpe, profit factor, expectancy degradados respecto al backtest); cadencia de revisión humana; procedimiento de reactivación tras pausa. Los umbrales se definen con margen explícito para el haircut esperado entre backtest y live (típicamente 30-50% de degradación en Sharpe), no como porcentaje simétrico del backtest.

- **OPS-2 — `StrategyHealthMonitor`:** componente en `Trading.Application` que implementa `IRiskMonitor` y consume `OrderFilledEvent` del `IDomainEventBus` para mantener métricas rolling en vivo por estrategia. Compara contra los umbrales de `POLICY.md` y dispara `RiskLimitBreachedEvent` (extendiendo `RiskLimitBreachReason` con `StrategyDegradation`) cuando se cruzan. Se registra en el array de monitors de `RiskOrchestrator`.

OPS-1 va primero y bloquea OPS-2: define los números que OPS-2 va a chequear.

### Alternativas consideradas
- **A: Postergar al Bloque 4 ("cuando crezca").** Tentador porque OPS-1/OPS-2 no son código del motor de trading sino metadatos operativos. Descartada: paper trading sin policy escrita no cumple su función formativa (es donde se entrena el músculo de apagar una estrategia "como si fuera real"), y operar live sin OPS-2 deja la decisión más costosa del oficio (cuándo matar una estrategia que pierde) librada a la disciplina humana bajo estrés. El costo de hacerlo bien es chico; el costo de no hacerlo se paga en blow-ups.

- **B: Solo OPS-1 (documento escrito sin componente runtime).** Mejor que nada, pero documento sin enforcement es papel mojado: la inspección humana de métricas en vivo no escala a múltiples estrategias y falla bajo estrés operativo (el operador minimiza lo malo cuando el dolor está fresco). Descartada por insuficiente.

- **C: Solo OPS-2 (monitor runtime sin documento).** Código sin criterio: los umbrales que el monitor compara tienen que venir de algún lado, y si no están escritos y versionados en el repo terminan hardcodeados en el código o en un JSON sin contexto. Descartada por incompleta: OPS-1 es la fuente de verdad humana, OPS-2 es la ejecución.

- **D (elegida): OPS-1 + OPS-2 en el Bloque 3, en ese orden.** OPS-1 antes que OPS-2 porque define los números. Ambos antes que Hito C porque el paper trading es donde se valida el conjunto.

### Consecuencias
- El sistema queda con las tres puertas críticas definidas: entrada (validación robusta antes de operar — Hito G), operación (risk monitors en runtime — refactor #4), salida (policy de degradación y muerte de estrategia — OPS-1/OPS-2). Hasta hoy faltaba la tercera.
- `RiskLimitBreachReason` se extenderá con un valor nuevo (`StrategyDegradation`). El refactor #4 ya garantiza que agregar un monitor no toca código existente, así que el blast radius de OPS-2 es chico.
- Se introduce deuda técnica conocida: las métricas rolling del `StrategyHealthMonitor` se calculan en memoria desde el inicio de cada sesión. Si el proceso reinicia, se pierde el historial reciente y el monitor entra en warm-up. Para paper trading es aceptable; para live serio habrá que persistir el estado. Queda anotado para Bloque 4.
- El documento `POLICY.md` introduce un nuevo tipo de artefacto al repo (operacional, no código), que se versiona con el mismo rigor que cualquier otro: cualquier cambio a un umbral se commitea con justificación, y se revierte con `git` como cualquier código mal pensado.

---
**Fecha:** 2026-05-13
**Estado:** Aceptada

### Contexto
`KillSwitchManager` concentraba cuatro responsabilidades distintas: detectar drawdown excesivo, contar pérdidas consecutivas, ejecutar la liquidación global, y gestionar el período de cooling-off tras la activación. Al planificar Hito B (regímenes de mercado), surgirá una quinta responsabilidad: detectar "régimen incompatible" como condición de pausa. Agregar esa lógica a `KillSwitchManager` escalaría el problema.

Adicionalmente, `OrderLifecycleService` dependía de `KillSwitchManager` solo para `RegisterLoss()` y `RegisterWin()`: dependencia hacia un God Object por una API diminuta.

### Decisión
Descomponer `KillSwitchManager` en cinco componentes de responsabilidad única:
- `DrawdownMonitor : IRiskMonitor` — detecta drawdown desde high-water mark.
- `ConsecutiveLossesMonitor : IRiskMonitor` — registra rachas de pérdidas; expone `RegisterLoss()` / `RegisterWin()` como API pública.
- `CoolingOffTracker` — gestiona el período de cooling-off (no es `IRiskMonitor`: su rol es señalizar desactivación, no activación).
- `LiquidateAllRiskAction : IRiskAction` — ejecuta la liquidación.
- `RiskOrchestrator` — coordina el ciclo completo: evalúa monitors, activa kill switch con la acción, gestiona cooling-off; exposing `IsKillSwitchActivated` y `EvaluateAllMonitors()`.

`ConsecutiveLossesMonitor` se inyecta directamente en `OrderLifecycleService` (no a través del orquestador: el orquestador no necesita saber de fills individuales).

### Alternativas consideradas
- **A: Refactorizar KillSwitchManager internamente** sin separar interfaces. Descartada: el naming engañoso y el tamaño de la clase seguirían siendo problemas de mantenimiento.
- **B: IRiskMonitor con método RegisterEvent() genérico** para que OrderLifecycleService informe al monitor via interfaz. Descartada: innecesariamente abstracto; `ConsecutiveLossesMonitor` es un concepto concreto que merece API explícita.
- **C (elegida): Inyección directa del monitor concreto** en OrderLifecycleService. `RiskOrchestrator` lo recibe como `IRiskMonitor` via DI; `OrderLifecycleService` lo recibe como tipo concreto para acceder a la API de registro. Un objeto, dos facetas.

### Consecuencias
- Agregar un nuevo monitor en Hito B (régimen de mercado) requiere implementar `IRiskMonitor` y registrarlo en el array que recibe `RiskOrchestrator`. Sin modificar nada existente.
- `KillSwitchManager.cs` y `KillSwitchManagerTests.cs` eliminados.
- 14 tests nuevos cubren los tres componentes principales. Total: 57 tests.
- El término "KillSwitch" desaparece del código; reemplazado por `IsKillSwitchActivated` en `RiskOrchestrator` (nombre descriptivo del estado) y `RiskLimitBreachedEvent` (nombre del evento).

---

## ADR-014 — Reversión del SignalAuditor: validación de indicadores por tests unitarios estáticos
**Fecha:** 2026-05-13
**Estado:** Aceptada (revierte ADR-010, ADR-011, ADR-012, ADR-013 en lo que respecta a auditoría runtime)

### Contexto
El Hito A original implementaba un SignalAuditor que durante el backtest mantenía un buffer rolling de barras observadas y, cuando una estrategia emitía señal, recalculaba los indicadores en C# independientemente y comparaba con los valores que la estrategia declaraba haber usado.

Tras cuatro fixes iterativos (buffer 200→2000, warm-up 200, tolerancia absoluta 1e-9 → relativa 1e-6, reemplazo del algoritmo SMA-seed→EMA-puro), persistían ~33% de señales reportadas como inconsistentes sin causa raíz clara. El sistema acumulaba complejidad arquitectónica sin resolver el problema de fondo.

Búsqueda posterior reveló que la práctica institucional estándar (documentada por la propia QuantConnect en sus tests de regresión de indicadores) es validar indicadores mediante tests unitarios contra valores de referencia de librerías open source (TA-Lib, QuantLib) almacenados en CSV o arrays estáticos. NO se hace auditoría en vivo durante backtest.

### Decisión
Eliminar completamente el SignalAuditor y todos sus componentes asociados (9 archivos borrados). Reemplazar por dos tests unitarios:
1. Test de indicador: verifica que ExponentialMovingAverage de QC produce valores equivalentes al baseline QC (validado internamente por QC contra TA-Lib) sobre serie sintética de referencia.
2. Test de estrategia: verifica que EmaCrossStrategy emite señales correctas con datos sintéticos diseñados.

Para cualquier indicador o estrategia nueva que se agregue al sistema, replicar este patrón en lugar de re-introducir auditoría runtime.

### Alternativas consideradas
- **A: Continuar iterando sobre el SignalAuditor.** Descartada: cuatro fixes sin convergencia indica que el diseño es fundamentalmente incorrecto, no que falte un fix más.
- **B: Auditor independiente en Python con TA-Lib durante el backtest.** Descartada: agrega un pipeline cross-language al desarrollo cotidiano por un problema que tests unitarios resuelven mejor. Reservar este enfoque para validación pre-live trading (ver TODO AUDIT-1 en ROADMAP).
- **C (elegida): Tests unitarios estáticos contra valores de referencia.** Estándar institucional documentado. Costo runtime cero. Cobertura efectiva.

### Consecuencias
- El sistema runtime queda más simple: BarProcessingService y TradingAlgorithmHost vuelven a no conocer auditoría.
- La verificación de fidelidad de señales se hace una sola vez en CI (al correr tests), no en cada backtest.
- ADRs anteriores (ADR-010 a ADR-013) quedan superseded en lo que respecta a auditoría runtime, pero se mantienen como registro histórico del aprendizaje.
- Práctica recomendada antes de pasar a paper trading: verificación manual de 3-5 señales en TradingView (sanity check humano final). No automatizada, no bloqueante.
- TODO AUDIT-1 (auditor Python independiente) sigue en ROADMAP Bloque 4 para fase pre-live con capital significativo.

---

## ADR-012 — Auditor de señales: tolerancia relativa, no absoluta
**Fecha:** 2026-05-12
**Estado:** Aceptada

### Contexto
El SignalAuditor compara valores declarados por la estrategia (usando `double` internamente en QuantConnect) contra valores recalculados (usando `decimal` en el dominio). El error numérico inherente a este cross-precision puede llegar al orden de 1e-5 a 1e-7 relativo, dependiendo de la cantidad de operaciones acumuladas. Una tolerancia absoluta no escala con la magnitud del activo: el mismo umbral que es razonable para BTC en 100,000 USD es absurdamente laxo para FX en 1.10 o para una acción de 5 USD.

### Decisión
Usar tolerancia RELATIVA en SignalAuditor: `|declarado - recalculado| / max(|declarado|, |recalculado|, 1) < tolerance`. Default `1e-6`. El denominador con piso de 1 evita división por cero cuando ambos valores son ~0; en ese caso degrada elegantemente a comparación absoluta con umbral igual a la tolerancia.

### Alternativas consideradas
- **A: Tolerancia absoluta tuneada por activo.** Requiere mantener un mapa `instrumentId → tolerancia`. Frágil al agregar nuevos activos. Descartada.
- **B: Tolerancia absoluta global laxa (ej. 0.1).** Funciona para BTC pero enmascara discrepancias reales en activos baratos. Descartada.
- **C (elegida): Tolerancia relativa.** Se adapta automáticamente al rango numérico. Estándar institucional para comparaciones numéricas cross-precision.

### Consecuencias
- El auditor ahora discrimina correctamente entre ruido numérico (cross-precision `double`↔`decimal`) y bugs financieramente significativos.
- La constante `1e-6` se vuelve precedente: cualquier futuro auditor numérico del proyecto debe usar tolerancia relativa con magnitud similar, salvo justificación específica.
- El campo `SignalDiscrepancy.AbsoluteDifference` se mantiene en el reporte porque sigue siendo útil para diagnóstico humano cuando una discrepancia es genuina.

---

## ADR-011 — Auditor de señales: warm-up por símbolo en lugar de buffer infinito
**Fecha:** 2026-05-12
**Estado:** Aceptada

### Contexto
El SignalAuditor recalcula indicadores independientemente para validar fidelidad de señales. La estrategia acumula EMA desde la primera barra del backtest; el auditor mantiene un buffer rolling y reseed con SMA cada vez. Esa asimetría matemática genera discrepancias sistemáticas mientras el buffer no es suficientemente largo respecto al período del indicador.

### Decisión
En lugar de mantener un buffer infinito desde el inicio del backtest (matemáticamente equivalente, O(N) memoria), usar un buffer finito grande (2000 barras, default) + período de warm-up explícito (200 barras, default) durante el cual el auditor NO emite resultados. El contador `SignalsSkippedDuringWarmUp` se reporta para transparencia.

### Alternativas consideradas
- **A: Buffer infinito.** Matemáticamente puro: el auditor procesa exactamente las mismas barras que la estrategia. Costo: memoria crece linealmente con el tiempo del backtest. Para backtests largos (años en 1h) puede superar 100MB por símbolo. Descartada.
- **B (elegida): Buffer 2000 + warm-up 200.** Para EMA(60) el peso del seed inicial decae a ~10^-30 después de 2000 barras. Indistinguible de cero. Memoria O(constante).

### Consecuencias
- Las primeras señales del backtest no se auditan (warm-up). Aceptable porque típicamente coinciden con el período de calibración inicial de las propias estrategias.
- Si en el futuro se agregan indicadores con períodos > 60, el buffer de 2000 puede no ser suficiente. Regla general: buffer >= 30x el período más largo.
- El patrón "warm-up explícito" se vuelve precedente para futuros auditores (otros indicadores, otros tipos de señal).

---

## ADR-010 — Auditor de señales en C# dentro del mismo backtest, no Python independiente
**Fecha:** 2026-05-12
**Estado:** Aceptada

### Contexto
El Hito A requería validar que las señales generadas por las estrategias sean fieles a las reglas declaradas. Había dos enfoques institucionalmente válidos: (1) auditor dentro del mismo proceso del backtest, escrito en C#, reusando librerías de QuantConnect; (2) auditor en Python con TA-Lib leyendo un CSV exportado durante el backtest, verdaderamente independiente del runtime.

### Decisión
Implementar el auditor en C# dentro del mismo backtest. Reporte de resumen vía `OnEndOfAlgorithm` a consola. Sin script Python separado por ahora.

### Alternativas consideradas
- **A: Auditor Python + TA-Lib aparte.** Descartada por costo de mantenimiento de un segundo codebase y por el momento del proyecto (pre-paper trading). Verdaderamente independiente, pero overkill para el riesgo actual.
- **B (elegida): Auditor C# en el mismo proceso.** Detecta bugs de flujo de control y estado interno (que es el 80% del valor). Limitación honesta documentada: no detecta bugs en QuantConnect mismo.

### Consecuencias
- El auditor comparte motor con la estrategia: si QC tiene un bug en EMA, el auditor lo replica y no lo detecta.
- Se registra TODO AUDIT-1 en ROADMAP.md para implementar el auditor Python antes de pasar a live con plata significativa.
- La interfaz `IIndicatorRecomputer` permite agregar nuevas estrategias auditables sin tocar el `SignalAuditor`.
- `PreviousSignal` en EmaCross queda fuera del audit porque requiere replicar estado histórico — limitación documentada y aceptada.

---

## ADR-009 — Bus de eventos de dominio síncrono in-memory, sin librerías externas
**Fecha:** 2026-05-12
**Estado:** Aceptada

### Contexto
El sistema necesita comunicación interna entre componentes (trading→métricas) sin acoplar los publicadores a los consumidores. La alternativa obvia es una librería de mensajería (MediatR, MassTransit). El sistema es de baja frecuencia (barras de 5m), corre en backtest y debe ser completamente determinista.

### Decisión
Implementar un `DomainEventBus` propio: clase en `Trading.Application/Eventing/`, síncrono, in-memory, con `Subscribe<TEvent>` y `Publish<TEvent>`. Suscripción manual desde `TradingAlgorithmHost`. Sin frameworks externos.

### Alternativas consideradas
- **A: MediatR.** Descartado. Añade un NuGet externo, introduce IRequest/INotification con su propio lifecycle, y oculta el flujo de control bajo indirección. Para un sistema de un solo proceso y baja frecuencia es sobrediseño.
- **B: MassTransit / mensajería async.** Descartado. Introduce complejidad operacional (broker, serialización, retries) incompatible con el requisito de determinismo en backtest.
- **C (elegida): bus propio síncrono.** El flujo de control queda visible. El comportamiento en backtest es idéntico al de live. El aislamiento de fallos en suscriptores (loguea y continúa) garantiza que una métrica mal escrita no rompe el flujo de trading. Si en el futuro se necesita async (ej. escritura a DB), se puede agregar un suscriptor que encole sin cambiar los publicadores.

### Consecuencias
Los publicadores no deben asumir que los suscriptores son rápidos: cada `Publish` bloquea hasta que todos los callbacks retornan. Aceptable para el Hito A (métricas en memoria). Si en el futuro se agregan suscriptores de I/O (DB, red), revisar si hace falta un canal async.

---

## ADR-008 — Postergar refactors no críticos del AI.md hasta después de cada hito
**Fecha:** sesión 2
**Estado:** Aceptada

### Contexto
El `AI.md` actualizado describe un sistema institucional maduro. El código actual viola varias reglas (logging no estructurado, magic values en lugar de `Result<T>`, `decimal` crudo en lugar de Value Objects de dinero, etc.). Hacer todos los refactors antes de avanzar con los hitos del proyecto bloquearía el progreso indefinidamente.

### Decisión
Aplicar **principio de proporcionalidad al riesgo**: solo refactorizar lo que pueda causar pérdida de dinero real o bloqueé un hito específico. El resto se posterga a "cuando el sistema crezca".

Criterio explícito:
1. ¿Puede causar pérdida monetaria directa o vía bug que dispare orden equivocada? **Y**
2. ¿La probabilidad de que la falla ocurra es razonable (no escenario de manual)?

Si las dos respuestas son sí → hacer antes del próximo hito.
Si alguna es no → postergar al Bloque 4 ("cuando el sistema crezca").

### Alternativas consideradas
- **A: Aplicar todas las reglas del AI.md ahora.** Descartada: bloquea progreso, no hay live trading inminente.
- **B: Ignorar el AI.md y avanzar a hitos.** Descartada: se acumula deuda que va a explotar en producción.
- **C (elegida): Priorización por riesgo + hito que bloquea.**

### Consecuencias
- El AI.md se trata como "estrella polar", no como checklist obligatorio para hoy.
- Cada refactor postergado queda explícitamente registrado en `ROADMAP.md` con condición de "trigger" (ej. "cuando se agregue 2do asset class").
- El sistema vive con deuda técnica conocida y trackeada, no oculta.

---

## ADR-007 — `ITradingLogger` se mantiene como abstracción del dominio
**Fecha:** sesión 2
**Estado:** Aceptada (a aplicar en refactor A2)

### Contexto
El AI.md exige `ILogger<T>` de `Microsoft.Extensions.Logging` con placeholders nombrados (structured logging). El código actual usa `ITradingLogger` propio con interpolación `$"..."`.

### Decisión
Mantener `ITradingLogger` como abstracción de dominio. Cambiar su contrato para aceptar template + parámetros (`Info(string template, params object[] args)`). Implementación interna (`LeanLogger`) puede usar `ILogger<T>` por debajo si conviene.

### Alternativas consideradas
- **A: Reemplazar `ITradingLogger` por `ILogger<T>` directo.** Descartada: agregar dependencia de `Microsoft.Extensions.Logging` a `Trading.Domain` y `Trading.Application` rompe el principio de "dominio sin dependencias externas".
- **B (elegida): Mantener `ITradingLogger`, refactorizar para placeholders.** Logra structured logging sin ensuciar el dominio.

### Consecuencias
- `Trading.Domain` y `Trading.Application` no necesitan referenciar `Microsoft.Extensions.Logging`.
- El refactor A2 toca solo la signatura de `ITradingLogger` y todas las llamadas (~15 en total).
- Si en el futuro se necesitan features avanzadas (scopes, structured properties complejas), se puede revisar la decisión.

---

## ADR-006 — `Long`/`Short` en estrategias usando enum simple, no `SignalDecision` con factory methods
**Fecha:** sesión 2
**Estado:** Aceptada

### Contexto
Para habilitar shorts, `IStrategy.EvaluateSignal` debía dejar de devolver `bool`. Había dos opciones: tipo rico (`SignalDecision` con Direction + Confidence + futuras propiedades) o enum simple (`SignalDirection { Flat, Long, Short }`).

### Decisión
Enum simple `SignalDirection`. Sin clase wrapper, sin factory methods, sin confidence.

### Alternativas consideradas
- **A: `SignalDecision` con factory methods `Long()`, `Short()`, `Flat()` + campo `Confidence`.** Descartada por el usuario: agrega complejidad sin necesidad presente. Confidence no se conecta al sizing en este refactor; almacenarla sin usarla no aporta.
- **B (elegida): enum simple.** Mínimo cambio, máximo desbloqueo (shorts habilitados). Si en el futuro se necesita `Confidence`, se puede agregar como `SignalDecision` envolviendo el enum.

### Consecuencias
- `BarProcessingService` aplica el signo: Long → cantidad positiva, Short → cantidad negativa. El `PositionSizer` sigue devolviendo magnitud (sin cambio).
- `EmaCrossStrategy` ahora produce señales en ambas direcciones; backtest puede mostrar resultados muy distintos al previo (mismo set de cruces, pero ahora la mitad eran short y se ignoraban).

---

## ADR-005 — Cleanup automático del `OrderRegistry` tras eventos terminales
**Fecha:** sesión 2
**Estado:** Aceptada

### Contexto
El `OrderRegistry` mapea tags opacos a registraciones de órdenes. Sin cleanup, retendría miles de registraciones obsoletas en una sesión live de varios días.

### Decisión
`OrderEventMapper` llama `OrderRegistry.Forget(clientTag)` tras procesar exitosamente un evento terminal (Filled/Canceled/Invalid). El registry retiene solo órdenes vivas.

### Alternativas consideradas
- **A: Mantener todas las registraciones (memoria barata, simple).** Descartada: complica diagnóstico forense — el operador no puede distinguir órdenes activas de históricas.
- **B (elegida): Forget tras evento terminal.** Mantiene el registry como "vista de lo vivo".

### Consecuencias observadas
- En backtest aparecen eventos residuales (rollover de futuros, fills parciales tardíos) que llegan **después** del Forget. El `OrderEventMapper` los detecta y loguea en Debug con mensaje específico ("evento residual esperado").
- No afecta la corrección funcional: la posición ya se cerró cuando llegó el primer evento terminal.

---

## ADR-004 — Tags opacos formato `ord_xxxxxxxx` (GUID corto), no contador incremental
**Fecha:** sesión 1
**Estado:** Aceptada

### Contexto
El `OrderRegistry` genera tags opacos para asociar órdenes a su contexto. Había que elegir formato.

### Decisión
`"ord_" + Guid.NewGuid().ToString("N").Substring(0, 8)`. 8 caracteres hex como identificador opaco.

### Alternativas consideradas
- **A: Contador incremental (`ord_000001`, `ord_000002`).** Descartada: requiere lock para thread-safety. En live trading los callbacks de fills llegan en threads distintos.
- **B (elegida): GUID corto.** No requiere coordinación. Probabilidad de colisión en 8 chars hex: ~1 en 4 mil millones, mitigada con loop defensivo en el generador.

### Consecuencias
- Tests deterministas son más difíciles (los tags son aleatorios). El AI.md exige `IOrderIdGenerator` inyectable para fix; postergado al Bloque 4.

---

## ADR-003 — `OrderRegistry` vive en `Trading.Application`, no en `Trading.Strategies`
**Fecha:** sesión 1
**Estado:** Aceptada

### Contexto
El `OrderRegistry` es la pieza central del refactor #1 (eliminar stringly-typed tags). Podía vivir en Application (lógica pura) o en Strategies (junto a los adaptadores Lean).

### Decisión
Vive en `Trading.Application/Execution/OrderRegistry.cs`. Es lógica pura (dictionary + generación de tag), sin dependencia de Lean. El `LeanOrderRouter` recibe la instancia por constructor.

### Alternativas consideradas
- **A: En Strategies, junto al `LeanOrderRouter`.** Descartada: rompería testabilidad. El registry es lógica que el dominio puede consumir; vivir en Strategies lo ataría a Lean innecesariamente.
- **B (elegida): En Application.** Testeable en milisegundos con tests unitarios sin Lean.

---

## ADR-002 — `RiskPerTradePercentage` falla loud si no está en `strategies.json`
**Fecha:** sesión 1
**Estado:** Aceptada

### Contexto
Al pasar el `RiskPerTradePercentage` de constante hardcodeada (2%) a campo del JSON, había que decidir qué pasa si una entrada del JSON no lo trae.

### Decisión
El sistema **no arranca** si falta. `StrategyDefinition.RiskPerTradePercentage` es `decimal?` (nullable) para distinguir "campo ausente" de "campo presente con valor 0". Ambos casos fallan, pero el `StrategyConfigLoader` produce mensajes distintos para diagnóstico.

### Alternativas consideradas
- **A: Default 2% si no está (retrocompatibilidad suave).** Descartada por el usuario: viola política institucional de fail-loud.
- **B (elegida): Falla loud, no arranca.** El operador es forzado a ser explícito sobre el riesgo por trade en cada estrategia.

---

## ADR-001 — Desacople quirúrgico de QuantConnect: dominio Lean-free, adaptadores en Strategies
**Fecha:** sesión 1
**Estado:** Aceptada (es el refactor más grande del proyecto hasta hoy)

### Contexto
El código original tenía `using QuantConnect;` en casi todas las clases de lógica de negocio (`KillSwitchManager`, `PositionSizer`, `OrderEventHandler`, etc.). Esto bloqueaba: testabilidad sin levantar Lean, cualquier intención futura de cambiar de motor, y la claridad del dominio.

### Decisión
Aplicar Clean Architecture **parcial pero estricta**:
- `Trading.Domain` y `Trading.Application` → cero `using QuantConnect`.
- `Trading.Strategies` → único proyecto con `using QuantConnect`. Contiene el host (`TradingAlgorithmHost : QCAlgorithm`) y los adaptadores.
- Abstracciones del dominio: `IPortfolioState`, `IInstrumentMetadata`, `IOrderRouter`, `IOrderHandle`, `IClock`, `ITradingLogger`, `IPriceRounder`.
- Value objects propios: `InstrumentId` (en lugar de `Symbol` de QC), `MarketBar` (en lugar de `TradeBar`).

### Alternativas consideradas
- **A: Desacople total (también `Trading.Strategies` debería ser pluggable).** Descartada: el host y los consolidators son específicos de Lean; abstraerlos sería overkill.
- **B: Mantener acoplamiento, mejorar nombres.** Descartada: no resuelve los problemas de fondo (testabilidad, portabilidad futura).
- **C (elegida): Desacople quirúrgico.** Dominio puro, adaptadores en una sola capa.

### Consecuencias
- Tests del `KillSwitchManager` corren en milisegundos sin Lean.
- Si en el futuro se evalúa NautilusTrader o conexión FIX directa, solo se reescribe `Trading.Strategies`.
- Invariante checkable: `grep -rn "^using QuantConnect" Trading.Domain/ Trading.Application/` debe estar vacío.

---

## Template para nuevas entradas

```markdown
## ADR-NNN — Título corto y descriptivo
**Fecha:** YYYY-MM-DD o "sesión N"
**Estado:** Propuesta / Aceptada / Revertida

### Contexto
Qué problema motivó la decisión.

### Decisión
Qué se decidió hacer concretamente.

### Alternativas consideradas
- **A: ...** Por qué se descartó.
- **B (elegida): ...** Por qué se eligió.

### Consecuencias
Qué cambia en el sistema. Si la decisión introduce deuda técnica conocida, marcarla acá.
```
