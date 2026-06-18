# POLICY - Trading Policy Document

> **Propósito de este documento:** codificar las reglas operativas inquebrantables del sistema de trading. Define cuándo una estrategia pierde el derecho de operar, cuándo el sistema completo debe pausar, qué inspecciona el operador y con qué cadencia, y cómo se procede ante incidentes.
>
> **Documento simétrico a la validación de entrada:** así como "ninguna estrategia opera sin pasar tests de referencia y comportamiento", este documento define cuándo una estrategia que ya está corriendo deja de tener derecho a hacerlo.
>
> **Cómo se usa:**
> - **Como contrato con el operador:** las reglas acá escritas no se modifican durante un drawdown ni para "ajustar el umbral porque éste me parece estricto hoy". Se modifican en momentos de calma, con datos sobre la mesa.
> - **Como especificación para componentes runtime:** el `StrategyHealthMonitor` (OPS-2) lee los umbrales de la sección 3 y los implementa en código. Si esta policy cambia, OPS-2 cambia.
> - **Como documento de revisión periódica:** la cadencia de revisión humana (sección 4) incluye una revisión trimestral de la propia POLICY.
>
> **Reglas de versionado:**
> - Todo cambio a esta policy se commitea con commit message convencional (`docs(policy): ...`) y un comentario que justifique el cambio.
> - Los cambios sustantivos (umbrales, principios, procedimientos) requieren entrada en `DECISIONS.md` documentando el racional.
> - El estado actual de cada estrategia (sección 7) se actualiza con frecuencia operativa, sin requerir ADR.

---

## 1. Principios operativos inquebrantables

Estos principios son el marco mental que justifica los números y procedimientos del resto del documento. No son chequeables por código; son la fuente de verdad de **por qué** las reglas son las que son.

**P1 — Validación antes de capital.** Ninguna estrategia opera con capital real sin haber pasado: (a) tests de referencia de sus indicadores, (b) tests de comportamiento con datos sintéticos, (c) un mínimo de paper trading con métricas dentro de banda. El orden es estricto: backtest → tests unitarios → paper → live. No se saltea pasos.

**P2 — El kill switch nunca se desactiva en caliente.** Si el sistema activó el kill switch automáticamente (drawdown global, pérdidas consecutivas, degradación de estrategia), la reactivación exige: comprensión documentada de qué disparó el kill, decisión escrita de qué se ajusta antes de reanudar, y nuevo arranque manual del sistema. **Nunca** se reactiva "para esperar el rebote" ni "porque me parece que ya pasó."

**P3 — Haircut backtest → live es del 30-50%, esperado.** Las métricas en vivo van a ser sistemáticamente peores que en backtest por slippage real, latencia, comisiones más altas que las modeladas, y por overfitting silencioso. Una degradación de hasta 30-50% sobre las métricas del backtest es **el comportamiento esperado**, no una señal de degradación. Los umbrales de apagado de la sección 3 están calibrados absolutos por esta razón, no como fracción del backtest.

**P4 — Cuando el monitor automático y la intuición disienten, gana el monitor.** Si POLICY indica apagar y vos pensás "pero la próxima va a ser buena, aguanto", apagás. La razón de existir de este documento es exactamente para no darle al vos-en-caliente la posibilidad de negociar con el vos-en-frío que escribió las reglas.

**P5 — Cada cambio operativo deja huella.** Activar una estrategia, pausarla, recalibrar un umbral, agregar un instrumento, modificar el `RiskPerTradePercentage`: cada uno de estos genera una entrada datada en `DECISIONS.md` (o en `DECISIONS.md/incidents/` para reactivaciones). El historial operativo es tan importante como el historial de código.

---

## 2. Umbrales a nivel sistema (cross-strategy)

Reglas que aplican al sistema completo, independientes de qué estrategia esté operando. Implementadas hoy por `RiskOrchestrator` + sus monitors registrados.

### 2.1 Drawdown total del sistema

| Umbral | Acción | Implementación |
|---|---|---|
| Drawdown total del sistema desde ATH global > **25%** | Kill switch global: liquidación de todas las posiciones + pausa de generación de señales nuevas | `DrawdownMonitor` (existente) |

**Reactivación:** análisis escrito en `DECISIONS.md/incidents/`, reinicio manual del proceso.

### 2.2 Pérdidas consecutivas a nivel sistema

| Umbral | Acción | Implementación |
|---|---|---|
| **5 trades consecutivos** cerrados con pérdida neta (a nivel sistema, no por estrategia) | Cooling off period: pausa de generación de señales nuevas durante **24 horas de wall clock real**; posiciones abiertas se gestionan normal | `ConsecutiveLossesMonitor` + `CoolingOffTracker` (existentes) |

**Reactivación:** automática tras transcurrir las 24 horas. No requiere intervención humana, pero se loguea entrada de Info en el JSONL al activarse y al desactivarse.

### 2.3 Eventos macro programados

En ventanas de **±30 minutos del wall clock real** de eventos macro relevantes, **no se abren posiciones nuevas**. Las posiciones abiertas se gestionan normal (stops, take profits y time exits siguen activos).

**Eventos cubiertos por esta regla:**
- FOMC (decisión de tasas de la Fed)
- CPI USA (publicación mensual)
- NFP / Non-Farm Payrolls
- Halvings de Bitcoin
- Anuncios regulatorios extraordinarios sobre cripto (SEC, ESMA, equivalentes nacionales relevantes para el operador)

**Implementación actual: manual.** El operador consulta calendario económico semanalmente (ej. ForexFactory, Trading Economics) y desactiva manualmente la(s) estrategia(s) afectada(s) en `strategies.json` antes del evento, reactivándola(s) después. Cada activación/desactivación se loguea en `DECISIONS.md/incidents/`.

**Implementación futura:** `EventCalendarMonitor` (Bloque 4 del ROADMAP). Cuando se construya, esta sección se actualiza para reflejar el comportamiento automatizado.

### 2.4 Anomalías de infraestructura

| Síntoma | Acción del operador | Detección |
|---|---|---|
| `heartbeat.json` no se actualiza durante > 5 minutos en horario operativo | Verificar proceso, reiniciar si está caído; análisis post-mortem | Healthchecks.io + Telegram (ADR-021) |
| Divergencia entre estado interno del sistema y portfolio real del broker | Stop manual del sistema, reconciliación, no operar hasta resolver | Inspección manual |
| Latencia anómala sostenida del exchange (>5s en órdenes que típicamente son <1s) | Evaluar si el broker está degradado; considerar pause manual | Inspección manual |
| > 5% de órdenes rechazadas por el broker en 1 hora | Stop manual, investigar | Inspección manual |
| CSV `{ticker}_{timeframe}_live.csv` no se actualiza (última barra > timeframe × 3) | Verificar proceso `Trading.Recorder`; revisar logs del grabador; reiniciar si caído | Inspección periódica (ver runbook Grabador) |

### 2.5 Runbook — Grabador continuo de microestructura (`Trading.Recorder`)

El grabador es un proceso independiente, siempre encendido, que escribe las barras de microestructura al directorio `microstructure-live/`. Es el **único escritor** de los archivos `{ticker}_{timeframe}_live.csv`. Lean solo lee esos archivos en `Initialize()`.

**Arranque del grabador:**
```
dotnet Trading.Recorder.dll
# Variables de entorno opcionales:
RECORDER_STRATEGIES_JSON=/ruta/strategies.json
RECORDER_STORAGE_DIR=/ruta/microstructure-live
RECORDER_RETENTION_DAYS=7
RECORDER_WS_URL=wss://fstream.binance.com   # default
```

**Monitoreo:**
- El grabador imprime por stdout una línea por cada barra cerrada: `[Recorder] BTCUSDT/1h 2026-06-18 10:00 UTC | OFI=0.1234 CVD∆=1500 MTS=0.0412`
- Si el proceso está caído o el WebSocket se desconectó sin reconectar, los archivos CSV dejan de actualizarse. Síntoma en Lean al reiniciar: barras cargadas solo hasta la hora de caída del grabador.
- Umbral de alerta: última barra del CSV más antigua que `ahora − 3 × timeframe` indica que el grabador no está funcionando.

**Alta de un activo nuevo:**
1. Agregar el símbolo y timeframe a `strategies.json` en la sección correspondiente.
2. (Opcional, recomendado) Ejecutar `BinanceVisionSeeder` para sembrar historia hasta T-1:
   ```
   # Ejemplo vía script/programa que instancie BinanceVisionSeeder
   # fromDate: fecha de inicio deseada; toDate: hoy (datos hasta T-1)
   ```
3. Reiniciar el grabador para que suscriba el nuevo símbolo.
4. Habilitar la estrategia en `strategies.json` cuando el store tenga ≥ WarmUpBars barras. Sin siembra previa: esperar ~2 días de acumulación (período de observación).

**Reconexión automática:** el grabador reconecta con backoff exponencial (1s → 2s → 4s … → 60s máx) ante desconexiones del WebSocket. Barras perdidas durante la desconexión no se recuperan (sin datos → ventana omitida); Lean usará `null` de `GetBar()` para esas barras si se reinicia justo después.

**Notas:**
- Las acciones de la columna central son **manuales por ahora**. Los síntomas se detectan vía monitoreo (heartbeat, ping externo) o inspección del operador.
- La automatización de "anomalías de infraestructura" no está planificada como hito específico. Se evalúa caso por caso si algún síntoma recurrente justifica un monitor dedicado.

**Limitación de entorno conocida — flapping del WebSocket auxiliar (observado 2026-06-15, Hito D-prev):**
En la máquina de desarrollo actual, el WebSocket `/public/ws` de Binance (QuoteBar + MarginInterestRate, la conexión de mayor tráfico) se desconecta y reconecta cada 30-90s. Los feeds críticos —trades (`/market/ws`, consume la consolidación de barras) y órdenes (`/private/ws`)— se mantienen estables, por lo que **no afecta señales ni ejecución**. La causa es la red local: la misma que bloquea NTP (UDP 123, ver ADR-043) corta la conexión de mayor ancho de banda. **No es un bug del sistema.** El dead-man's switch no se ve afectado porque mide liveness del feed de minuto (ADR-042), no esta conexión. **Resolución definitiva: migrar el hosting a un VPS estable con IP fija cerca de una región de Binance** antes de operar con tamaño real — elimina de raíz este flapping, el drift de reloj y la fragilidad del whitelist de IP. Hasta entonces, el flapping en los logs es ruido esperado.

---

## 3. Umbrales por estrategia

Esta es la sección que `StrategyHealthMonitor` (OPS-2) va a leer y aplicar runtime. Cada estrategia activa en el sistema tiene una entrada acá.

### 3.1 Plantilla por estrategia

Toda estrategia activa debe tener su entrada poblada antes de operar (paper o live). La entrada contiene:

```
Estrategia: <Nombre> / <Instrumento> / <Timeframe>
Estado: <pre-paper | paper | live | pausada>
Fecha inicio paper: <YYYY-MM-DD o N/A>
Fecha inicio live: <YYYY-MM-DD o N/A>
Trades acumulados en vivo: <int> (paper + live combinados)

Umbrales de apagado automático (cualquiera dispara → liquidación inmediata + pausa):

  U1. Drawdown absoluto desde ATH equity de la estrategia > 25%
  U2. Drawdown rolling 30 días > 15% sostenido 5 días consecutivos
  U3. Profit factor rolling 30 trades < 1.0 sostenido 10 trades consecutivos *
  U4. Expectancy rolling 30 trades < 0 sostenido 10 trades consecutivos *

  * U3 y U4 solo se arman tras 50 trades acumulados en vivo.
    Antes de 50 trades: solo U1 y U2 activos.

Acción al disparar cualquier umbral:
  - Liquidación inmediata de la posición abierta de esta estrategia (a mercado).
  - La estrategia queda excluida del flujo de generación de señales en strategies.json.
  - Evento RiskLimitBreachedEvent emitido con razón StrategyDegradation y umbral disparado.
  - Notificación al operador (log Critical + heartbeat marca degraded).

Reactivación: análisis escrito en DECISIONS.md/incidents/, reactivación manual en strategies.json.
                No se exige re-paper trading antes de reactivar en live.
```

### 3.2 Definiciones precisas de las métricas

Para que `StrategyHealthMonitor` y el operador hablen el mismo idioma:

- **ATH equity de la estrategia:** máximo histórico del valor acumulado de P&L cerrado de la estrategia desde su primer trade en vivo. Se calcula sumando el P&L realizado de cada `OrderFilledEvent` que cierra una posición (no incluye P&L no realizado de posiciones abiertas).
- **Drawdown desde ATH:** `(ATH - equity_actual) / ATH`, expresado como fracción positiva (15% = 0.15).
- **Drawdown rolling 30 días:** drawdown calculado sobre la ventana de los últimos 30 días de wall clock real, no de IClock.
- **Profit factor:** `gross_profit / gross_loss` sobre los últimos 30 trades cerrados. Indefinido (skip) si gross_loss == 0.
- **Expectancy:** `(win_rate * avg_win) - (loss_rate * avg_loss)`, donde `win_rate` y `loss_rate` se calculan sobre los últimos 30 trades cerrados, y `avg_win` / `avg_loss` son los promedios de los ganadores y perdedores en esa misma ventana.
- **"Sostenido N trades consecutivos" (U3, U4):** el umbral debe permanecer cruzado durante N trades cerrados sucesivos, sin que ninguna evaluación intermedia lo devuelva al lado sano. Si en el medio una evaluación da PF >= 1.0 (para U3) o expectancy >= 0 (para U4), el contador se resetea.
- **"Sostenido 5 días consecutivos" (U2):** evaluación diaria al cierre de cada día de wall clock real. 5 evaluaciones consecutivas con DD rolling 30 días > 15%.
- **"Trade":** ciclo completo de una posición, desde entry hasta cierre (por SL, TP, time exit, o liquidación). NO se cuentan órdenes individuales (un trade típico genera 3-4 órdenes).

### 3.3 Calibración de los umbrales

Los umbrales U1-U4 son **absolutos**, derivados del mandato de riesgo del operador, no de métricas históricas del backtest. Razón: el backtest actual de las estrategias del sistema fue construido para validar infraestructura, no como proceso de validación cuantitativa institucional. Calibrar umbrales sobre ese backtest equivaldría a calibrar el termómetro con un termómetro roto.

**Recalibración:** cuando exista walk-forward analysis (Hito G del ROADMAP), los umbrales se recalibran con base estadística (Conditional Value at Risk del drawdown, distribución empírica de la curva de equity en out-of-sample). Cada recalibración: entrada en `DECISIONS.md`.

Ver **ADR-022** para el racional completo de la calibración absoluta.

### 3.4 Mientras los umbrales rolling no están armados (< 50 trades)

Cuando una estrategia recién arranca paper trading, tiene < 50 trades acumulados. En ese período:

- **U1 y U2 (drawdown absoluto y rolling 30 días) están activos** desde el primer trade. Son insensibles al tamaño de muestra; un drawdown grande es un drawdown grande.
- **U3 y U4 (profit factor y expectancy rolling) NO se evalúan automáticamente.** Razón: < 30 trades no produce ventanas rolling válidas; entre 30 y 50, las ventanas son técnicamente computables pero estadísticamente ruidosas (un solo trade extremo puede mover PF de 1.5 a 0.6).
- **Reemplazo manual durante esta ventana:** el operador inspecciona PF y expectancy en la revisión semanal (sección 4) y usa **criterio humano** para decidir si la estrategia merece seguir operando. Si en los primeros 30-50 trades el comportamiento es claramente patológico (10 pérdidas consecutivas, drawdown rápido, divergencia obvia con el backtest), el operador pausa manualmente.

---

## 4. Cadencia de revisión humana

La automatización cubre los umbrales de las secciones 2 y 3. Lo que NO automatiza es la observación de patrones que el sistema no sabe ver: degradación lenta dentro de banda, anomalías en la microestructura, slippage anómalo, comportamiento del exchange, eventos del mundo que el sistema desconoce.

### 4.0 Principio: la cadencia escala con el portafolio activo

Las tareas listadas en 4.1–4.4 se ejecutan en cada ciclo independientemente de cuántas estrategias o timeframes haya activos. Lo que escala es el **tiempo necesario para ejecutarlas**, no la frecuencia ni la naturaleza de las tareas.

Los rangos de tiempo en cada sección son estimaciones para **una estrategia activa de baja densidad de trades** (orden de magnitud: una estrategia en 1h con ~1-3 trades por día). Si el portafolio activo es más complejo, multiplicar el tiempo de inspección por estrategia activa y por densidad de trades esperada.

**Regla operativa:** si el portafolio activo hace inviable cumplir la cadencia con honestidad en una sesión razonable, **el problema es el portafolio, no la cadencia escrita**. Las dos opciones honestas son: reducir el portafolio (pausar estrategias, agregar filtros más restrictivos) o aceptar mayor latencia de inspección documentando la decisión en `DECISIONS.md`. Lo que NO es aceptable es marcar la cadencia como cumplida sin haberla cumplido.

Esto es consistente con P4: el monitor automático cubre los umbrales duros; la inspección humana es la capa que detecta patrones que el monitor no ve. Si la inspección humana se vuelve teatro porque no hay tiempo de hacerla en serio, esa capa desaparece.

### 4.1 Diario

**Tareas (independientes del tamaño del portafolio):**
- Verificar que `heartbeat.json` tenga timestamp reciente (< 5 minutos).
- Verificar último ping a Healthchecks.io en el dashboard.
- Revisar el JSONL del día previo: ¿hubo eventos Critical?, ¿órdenes en estado raro?, ¿el kill switch se activó y no me enteré?
- Revisar fills del día por estrategia activa: ¿órdenes huérfanas?, ¿discrepancias entre estrategias?

**Tiempo estimado:** 2-5 minutos por estrategia activa. Si no se hace, la cadencia se quiebra silenciosamente.

### 4.2 Semanal

**Tareas (por estrategia activa):**
- Calcular y registrar manualmente: PF rolling 30 trades, expectancy rolling, DD desde ATH, DD rolling 30 días.
- Revisar trades del período: ¿alguno con slippage anómalo (>2x el promedio)?, ¿alguno que se cerró por time exit en lugar de SL/TP (puede indicar señal mal calibrada)?
- Anotar resultados en el cuaderno operativo (no en el repo del sistema; este cuaderno es del operador). Comparar con la semana previa: ¿hay tendencia de degradación que el monitor aún no detecta?

**Tareas (cross-estrategia, una vez por semana):**
- Cross-check contra calendario macro: ¿hay eventos de la sección 2.3 esta semana próxima que requieran pausa manual?
- Si hay múltiples estrategias activas: ¿están correlacionadas en P&L de forma que el sistema agregado tenga concentración de riesgo que ninguna por sí sola refleja?

**Tiempo estimado:** 15-25 minutos por estrategia activa + 5-10 minutos cross-estrategia.

### 4.3 Mensual

**Tareas (independientes del tamaño del portafolio):**
- Revisar `DECISIONS.md/incidents/` del mes: ¿qué incidentes ocurrieron?, ¿qué patrones aparecen?
- Revisar `ROADMAP.md`: ¿hay deudas (DEUDA-*) que ya se pueden cobrar?, ¿hay refactors postergados cuyo trigger se cumplió?
- Revisar si algún aprendizaje del mes amerita ADR nuevo.
- Backup del JSONL y `heartbeat.json` históricos a almacenamiento secundario.

**Tiempo estimado:** 1-2 horas. Escala poco con el portafolio (la revisión de roadmap y deudas es global; la de incidentes escala con la cantidad de incidentes, no de estrategias).

### 4.4 Trimestral / al cierre de cada hito

**Tareas:**
- **Revisión de la propia POLICY.md.** Los umbrales que pusiste hace 3 meses, ¿siguen calibrados a la luz de los datos reales acumulados? Si una estrategia operó 3 meses con PF rolling consistentemente en 1.1-1.3 y nunca tocó el umbral, ¿los umbrales son razonables o son tan laxos que nunca van a disparar? Al revés también: si una estrategia sana fue apagada y el post-mortem reveló que era ruido normal, los umbrales están demasiado estrictos.
- **Revisión de la cadencia (sección 4 misma).** ¿Los tiempos estimados aún reflejan la realidad del portafolio actual? ¿Estás cumpliendo la cadencia con honestidad o ya empezaste a saltearte pasos?
- Cada cambio a POLICY: entrada en `DECISIONS.md` justificando.

---

## 5. Procedimientos de emergencia

Runbooks cortos. La regla: **leerlos antes del incidente**, no durante.

### 5.1 Kill switch global se activó automáticamente

1. **No reactivar nada todavía.** El kill se activó por algo; entender qué antes de tocar.
2. Inspeccionar el JSONL del día: buscar los eventos `Critical` y `RiskLimitBreachedEvent`. Identificar cuál monitor disparó (`DrawdownMonitor`, `ConsecutiveLossesMonitor`, `StrategyHealthMonitor`).
3. Confirmar que las posiciones se liquidaron efectivamente: revisar el broker, no fiarse del log.
4. Decidir: ¿es degradación real?, ¿es bug del sistema (falso positivo)?, ¿es evento de mercado anómalo (gap, flash crash)? Documentar en `DECISIONS.md/incidents/`.
5. Solo después de los pasos 1-4: decisión de reactivar (con o sin ajustes) o pausar más tiempo.

### 5.2 Healthchecks.io disparó alerta de proceso caído

1. Verificar si la máquina está viva (ping, RDP, SSH).
2. Si la máquina está viva pero el proceso no: revisar logs del JSONL más reciente, buscar la última línea escrita. Buscar excepciones no manejadas.
3. Si la máquina está caída: priorizar reanudación. Una vez arriba, ejecutar reconciliación del paso 5.3 antes de reactivar el sistema.
4. Documentar el incidente en `DECISIONS.md/incidents/` con causa raíz si la identificaste.

### 5.3 Discrepancia entre estado interno del sistema y portfolio real del broker

1. **Stop manual del sistema** (matar el proceso si está vivo). No operar más.
2. Exportar el portfolio real del broker (CSV o screenshot timestamped).
3. Exportar el último `heartbeat.json` y los `OrderFilledEvent` recientes del JSONL.
4. Reconciliar manualmente: identificar qué posiciones faltan o sobran en cada lado, y por qué.
5. **No reanudar el sistema hasta que la reconciliación esté completa.** Documentar en `DECISIONS.md/incidents/`.
6. Si la causa fue un bug del sistema, abrir item en el ROADMAP antes de reanudar.

### 5.4 Performance anómala (resultado del día > 3σ del baseline)

1. Inspeccionar la operación específica: ¿slippage anómalo?, ¿llenado parcial?, ¿precio de ejecución muy alejado del precio de señal?
2. Comparar contra datos del exchange en el mismo período: ¿hubo flash crash, halt, evento que el sistema no ignoró?
3. Decidir: ¿es alpha legítimo (raro pero ocurre)?, ¿es bug (más probable)?, ¿es evento de mercado (revisar policy de eventos en sección 2.3)?
4. Anotar resultado en cuaderno operativo. Si > 3σ se repite con frecuencia, el baseline está mal calibrado.

### 5.5 El proceso no arranca: error -1021 de Binance (timestamp adelantado)

Síntoma en los logs durante la inicialización (antes del warmup):
`{"code":-1021,"msg":"Timestamp for this request was 1000ms ahead of the server's time."}`

Causa: el reloj de la máquina está adelantado >1000ms respecto al servidor de Binance.
Binance rechaza esos requests; `recvWindow` no ayuda (solo tolera timestamps atrasados).

1. Medir el drift: `Trading.Research/broker_validation/Sync-TradingClock.ps1 -CheckOnly`
   (no requiere admin). Reporta el offset local−Binance en ms.
2. Si supera el umbral, corregir el reloj. Con la tarea programada instalada corre sola;
   manualmente, en PowerShell **elevado**: `w32tm /resync /force` (o el worker sin `-CheckOnly`).
3. Confirmar offset < 500ms antes de reabrir F5.
4. **Prevención (una vez, como admin):** `Install-TradingClockSync.ps1` deja una tarea
   programada que resincroniza al inicio y cada 60 min como SYSTEM. Es el pre-flight de reloj
   del sistema live. Ver ADR-043.

---

## 6. Política de cambios al sistema en operación

Cuando el sistema está operando con capital real (o paper trading no trivial), los cambios al código y la configuración tienen reglas distintas que durante desarrollo.

### 6.1 Cambios al código

- Cambios a Domain o Application requieren: todos los tests pasan + backtest del último mes da resultados idénticos al pre-cambio (chequeo de no-regresión). Si los resultados difieren, la diferencia se explica en el commit message antes de mergear.
- Cambios a Strategies (adaptadores, host) requieren: tests pasan + backtest no-regresión + revisión visual de que no se introdujo dependencia indebida (cero `using QuantConnect` en Domain/Application sigue valiendo).
- Cambios a `strategies.json` (cambio de parámetros de una estrategia activa): ADR en `DECISIONS.md` justificando antes de aplicar.

### 6.2 Cambios a la POLICY misma

- **No se modifica durante un drawdown.** Si una estrategia está cerca de un umbral, no se relaja el umbral.
- Cambios sustantivos (umbrales, principios, procedimientos) requieren entrada en `DECISIONS.md`.
- Cambios a la sección 7 (estado de estrategias) son operativos y se hacen libremente, sin ADR, con commit message descriptivo.

### 6.3 Activación de una estrategia nueva

Para que una estrategia entre al sistema (paper o live):

1. Tiene sus tests de referencia y de comportamiento (regla del `AI.md`).
2. Tiene entrada poblada en la sección 7 de POLICY (estado, umbrales, fechas).
3. Tiene entrada en `DECISIONS.md` justificando la activación.
4. Si va a live (no solo paper): operó un mínimo de paper trading sin disparar umbrales. La cantidad mínima la define el operador caso por caso; recomendación: **50 trades o 30 días de wall clock, el que llegue después** (no antes). Razón: ambos criterios capturan dimensiones distintas — los trades dan significancia estadística, los días dan exposición a regímenes de mercado distintos. Una estrategia en 15m puede acumular 50 trades en una semana pero todavía no haber visto un cambio de régimen; una en 1d puede pasar 30 días y haber visto solo 10 trades. Esperar a que se cumplan ambos protege contra los dos sesgos.

---

## 7. Estado actual de las estrategias

Esta sección se actualiza con frecuencia operativa. Es la única sección que cambia sin requerir ADR.

### 7.1 EmaCrossStrategy / BTCUSDT / 15m

**Naturaleza de esta estrategia.** La `EmaCrossStrategy` se construyó como **estrategia de desarrollo**: su propósito original fue ejercitar la infraestructura del sistema (sizing, eventos, HMM, monitoring) en un backtest no trivial. No pasó por proceso de validación cuantitativa institucional (sin walk-forward analysis, sin purged k-fold cross-validation, sin Monte Carlo sobre la curva de equity). Ver ADR-022 (D2) para el racional de por qué los umbrales U1-U4 no se derivaron de su backtest.

**Decisión de no promoción a live (2026-05-23).** Esta estrategia **nunca opera capital real**, aun si en algún momento pasara por Hito G y mostrara métricas favorables. Razón: arrancar live con una estrategia explícitamente construida como andamio de desarrollo manda mala señal cultural al proyecto y abre la puerta a racionalizar "es chico, no importa" — exactamente lo que P4 busca prevenir. Si en el futuro hay interés genuino en una estrategia de tipo EMA cross, se implementa como entrada nueva en `strategies.json` con nombre distinto, sus propios tests, su propio walk-forward, y su propia entrada en esta sección 7.

```
Estado: paper (activa desde 2026-06-03)
Fecha inicio paper: 2026-06-03
Fecha inicio live: nunca (estrategia de desarrollo, no será promovida a live
                          aunque pase Hito G — ver bloque explicativo arriba)
Trades acumulados en vivo: 0

Propósito durante paper trading (Hito C):
  Validación operativa del SISTEMA, no de la estrategia.
  - Verificar heartbeat real cada 60s de wall-clock.
  - Verificar pings a Healthchecks.io.
  - Verificar disparo de alerta Telegram al caer el proceso.
  - Ejercitar U1-U4 con equity en movimiento real.
  - Entrenar al operador en la cadencia diaria/semanal de POLICY 4.
  - Detectar comportamientos solo observables fuera de backtest
    (DEUDA-2 en live, doble Initialize, etc).
  Que la estrategia pierda equity virtual durante el paper es esperado e
  irrelevante para esta evaluación.

Umbrales de apagado automático (cualquiera dispara → liquidación inmediata + pausa):

  U1. Drawdown absoluto desde ATH equity de la estrategia > 25%
  U2. Drawdown rolling 30 días > 15% sostenido 5 días consecutivos
  U3. Profit factor rolling 30 trades < 1.0 sostenido 10 trades consecutivos *
  U4. Expectancy rolling 30 trades < 0 sostenido 10 trades consecutivos *

  * U3 y U4 solo se arman tras 50 trades acumulados en vivo.
    Antes de 50 trades: solo U1 y U2 activos.

  Nota: dado que esta estrategia no opera live, "trades acumulados en vivo"
        cuenta exclusivamente trades de paper. Si paper termina antes de
        50 trades, U3 y U4 nunca se arman para esta estrategia.

Acción al disparar cualquier umbral:
  - Liquidación inmediata de la posición abierta de esta estrategia (a mercado).
  - La estrategia queda excluida del flujo de generación de señales en strategies.json.
  - RiskLimitBreachedEvent emitido con razón StrategyDegradation y umbral disparado.
  - Notificación al operador (log Critical + heartbeat marca degraded).
  (Implementado runtime por StrategyHealthMonitor — OPS-2, ADR-023, 2026-05-21.)

Reactivación (solo aplicable a reactivar paper, no live):
  Análisis escrito en DECISIONS.md/incidents/, reactivación manual en strategies.json.

Filtro de régimen: opera solo cuando AccordHmmClassifier clasifica el régimen 4h
                   de BTCUSDT como Trend (ver Hito B - Paso 3, ADR-017, ADR-019).

Riesgo por trade: 2.0% (campo RiskPerTradePercentage en strategies.json, ADR-002).
```

---

### 7.2 EmaCrossStrategy / ETHUSDT / 1h

**Naturaleza de esta estrategia.** Misma naturaleza que 7.1: estrategia de desarrollo
sin walk-forward. Construida para ejercitar el sistema con un segundo activo y segundo
timeframe simultáneo (validación multi-símbolo, ADR-028). No será promovida a live.

**Decisión de no promoción a live:** ídem 7.1.

```
Estado: paper (activa desde 2026-06-03)
Fecha inicio paper: 2026-06-03
Fecha inicio live: nunca (estrategia de desarrollo — ídem 7.1)
Trades acumulados en vivo: 0

Umbrales de apagado automático: ídem 7.1 (U1-U4, mismos valores).

Filtro de régimen: opera solo cuando AccordHmmClassifier clasifica el régimen 4h
                   de ETHUSDT como Trend.

Riesgo por trade: 2.0% (campo RiskPerTradePercentage en strategies.json).
```

---

### 7.3 EmaCrossStrategy / TRBUSDT / 4h

**Naturaleza de esta estrategia.** Misma naturaleza que 7.1: estrategia de desarrollo
sin walk-forward. Activo de baja capitalización relativa; el timeframe 4h reduce
la frecuencia de señales. No será promovida a live.

**Decisión de no promoción a live:** ídem 7.1.

```
Estado: paper (activa desde 2026-06-03)
Fecha inicio paper: 2026-06-03
Fecha inicio live: nunca (estrategia de desarrollo — ídem 7.1)
Trades acumulados en vivo: 0

Umbrales de apagado automático: ídem 7.1 (U1-U4, mismos valores).

Filtro de régimen: opera solo cuando AccordHmmClassifier clasifica el régimen 4h
                   de TRBUSDT como Trend.

Riesgo por trade: 2.0% (campo RiskPerTradePercentage en strategies.json).
```

---

### 7.4 TradeSizeInstitutionalStrategy / BTC-ETH-SOL / 1h

**Naturaleza de esta estrategia.** H5 del batch de microestructura (Hito E/G, 2026-06-11).
Completó el pipeline completo: M4 (3/3 activos, Sharpe medio +2.7) → QC IS 2021-2024
(Sharpe=3.985) → QC OOS 2025 (Sharpe=4.186, MaxDD=5.9%, CAGR=97%). Monte Carlo Gate 2:
P(Sharpe<0)=0%. Resultado extraordinario: OOS supera IS, señal robusta fuera de muestra.

**Hipótesis.** MeanTradeSize en P90 del historial reciente (SizeWindow=24) Y BuySellRatio >
1.02 → acumulación institucional silenciosa → precio sube. El filtro BSR previene entradas
durante pánico (BSR colapsa < 1 en crashes). Long-only.

```
Estado: paper (activa desde 2026-06-15)
Fecha inicio paper: 2026-06-15
Fecha inicio live: pendiente (mínimo 50 trades paper O 30 días, el que llegue después)
Trades acumulados en vivo: 0

Símbolos activos: BTCUSDT 1h, ETHUSDT 1h, SOLUSDT 1h
Parámetros (alineados con QC IS/OOS): SL=5%, TP=10%, MaxBars=8, Risk=1%

Umbrales de apagado automático:
  U1. Drawdown absoluto desde ATH equity de la estrategia > 25%
  U2. Drawdown rolling 30 días > 15% sostenido 5 días consecutivos
  U3. Profit factor rolling 30 trades < 1.0 sostenido 10 trades consecutivos *
  U4. Expectancy rolling 30 trades < 0 sostenido 10 trades consecutivos *

  * U3 y U4 solo se arman tras 50 trades acumulados en vivo.

Filtro de régimen: ninguno (la estrategia no declara CompatibleRegimes — opera
                   en todos los regímenes, consistent con la config del QC IS/OOS).

Riesgo por trade: 1.0% (campo RiskPerTradePercentage en strategies.json).
```

---

### 7.5 CvdSellExhaustionStrategy / BTC-ETH-SOL / 1h

**Naturaleza de esta estrategia.** H3 del batch de microestructura (Hito E/G, 2026-06-11).
Completó el pipeline completo: M4 (≥2/3 activos, BTC Sharpe=+2.178 IS en QC) → QC IS
2021-2024 (Sharpe=2.178) → QC OOS 2025 (Sharpe=1.718, CAGR=30.4%). Monte Carlo Gate 2:
P(Sharpe<0)=1%.

**Hipótesis.** close ≤ mínimo de las últimas 48 barras (mínimo local genuino) Y CvdDelta > 0
(compradores netos en la barra) → vendedores agotados → rebote inminente. El CvdDelta > 0
durante crashes donde dominan vendedores actúa como filtro natural. Long-only.

```
Estado: paper (activa desde 2026-06-15)
Fecha inicio paper: 2026-06-15
Fecha inicio live: pendiente (mínimo 50 trades paper O 30 días, el que llegue después)
Trades acumulados en vivo: 0

Símbolos activos: BTCUSDT 1h, ETHUSDT 1h, SOLUSDT 1h
Parámetros (alineados con QC IS/OOS): SL=5%, TP=10%, MaxBars=6, Risk=1%

Umbrales de apagado automático:
  U1. Drawdown absoluto desde ATH equity de la estrategia > 25%
  U2. Drawdown rolling 30 días > 15% sostenido 5 días consecutivos
  U3. Profit factor rolling 30 trades < 1.0 sostenido 10 trades consecutivos *
  U4. Expectancy rolling 30 trades < 0 sostenido 10 trades consecutivos *

  * U3 y U4 solo se arman tras 50 trades acumulados en vivo.

Filtro de régimen: ninguno (la estrategia no declara CompatibleRegimes — opera
                   en todos los regímenes, consistent con la config del QC IS/OOS).

Riesgo por trade: 1.0% (campo RiskPerTradePercentage en strategies.json).
```

---

## Apéndice A — Glosario de términos

- **ATH (All-Time High):** máximo histórico observado de una serie (equity, precio).
- **Drawdown:** distancia porcentual entre el ATH y el valor actual, expresada como fracción positiva.
- **Expectancy:** valor esperado de P&L por trade. Positivo = la estrategia tiene edge; negativo = pierde plata en valor esperado.
- **Haircut backtest → live:** degradación esperada de las métricas en vivo respecto al backtest, por slippage real, comisiones, latencia y overfitting silencioso. Típico 30-50%.
- **Kill switch:** mecanismo automatizado que liquida posiciones y pausa generación de señales nuevas ante condiciones de riesgo críticas.
- **Profit Factor:** ratio entre gross profit y gross loss. > 1.0 = estrategia ganadora en el período; < 1.0 = perdedora.
- **Rolling window:** ventana móvil de N trades o N días, recalculada en cada evaluación.
- **Sostenido N períodos:** el umbral debe permanecer cruzado durante N evaluaciones consecutivas sin que ninguna intermedia lo devuelva al lado sano.
- **Wall clock real:** `DateTime.UtcNow` del sistema operativo. Distinguir de `IClock.UtcNow` (clock simulado en backtest). Ver ADR-021.
