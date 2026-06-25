# DECISIONS - Architecture Decision Records

> **PropÃ³sito:** registro de decisiones arquitectÃ³nicas tomadas durante el desarrollo del sistema. Cada entrada explica QUÃ‰ se decidiÃ³, POR QUÃ‰, y QUÃ‰ alternativas se consideraron y descartaron.
>
> **CuÃ¡ndo escribir un ADR:** solo cuando hay cÃ³digo en el repo cuya existencia o forma no se entenderÃ­a sin el ADR â€” una abstracciÃ³n nueva, una interfaz, una decisiÃ³n de diseÃ±o con consecuencias en mÃºltiples componentes. Un resultado de research (candidata rechazada, backtest fallido, hipÃ³tesis descartada en M4) **no genera ADR**: va a `Trading.Research/strategy_experiments.md`. Si la Ãºnica evidencia del ADR fuera texto sin cambio de cÃ³digo asociado, probablemente no es un ADR.
>
> **Reglas:**
> - Entradas en orden cronolÃ³gico inverso (la mÃ¡s reciente primero).
> - Cada entrada tiene fecha, contexto, decisiÃ³n, alternativas, consecuencias.
> - Las decisiones que se revierten NO se borran: se marcan como "Revertida en ADR-NNN" y se mantienen para historia.
> - Identificador correlativo `ADR-NNN`.
> - Al agregar un ADR nuevo, actualizar tambiÃ©n la tabla de Ã­ndice abajo.
> - **Radio de impacto de premisas invalidadas:** si un ADR falsa o cambia un supuesto del que dependian otros componentes (ej.: "el WS entrega aggTrades"), la seccion Consecuencias debe incluir un **checklist explicito de cada componente que dependia del supuesto viejo, con su estado** (migrado / pendiente / no-aplica) — no prosa. Un fix que arregla un solo consumidor de una premisa muerta y no traza el resto es el modo de falla mas comun del sistema (ver ADR-049 -> execution plane quedo en el WS roto). El blast radius se enumera y se verifica en el mismo ADR.

## Ãndice

| ADR | TÃ­tulo corto | Ãrea |
|---|---|---|
| ADR-055 | Universo y datos sin restricción a priori; el objetivo del sistema es la cartera, no la estrategia individual | Research / Metodología |
| ADR-054 | Bug de apareo de features (lookahead de 1h) en QC IS/OOS — invalida las validaciones de Hito E/G; H3 y H5 rechazadas | Validación / Datos |
| ADR-053 | Custom BaseData del store de microestructura: un solo camino de datos para backtest y live (cierra la paridad rota por el WS) | Datos / Estrategias / Paridad |
| ADR-052 | Observabilidad de señal: evento estructurado SignalEmitted (features genéricas + condición vía ISignalDiagnosticsProvider) | Observabilidad / Estrategias |
| ADR-051 | Warmup de estrategias desde el store (OHLCV en el store + replay genérico por EvaluateSignal) | Estrategias / Infraestructura |
| ADR-050 | Live con minimal-position-mode permanente como validación operativa (reemplaza paper para estrategias ya aprobadas) | Operaciones / Riesgo |
| ADR-049 | Recorder: feed REST polling de aggTrades (el WS de Binance Futures no entrega push) | Recorder / Infraestructura |
| ADR-048 | Grabador continuo de microestructura: data plane desacoplado del execution plane | Estrategias / Infraestructura |
| ADR-047 | ~~Warmup autosuficiente: aggTrades REST backfill + persistencia rolling 7d~~ (**Supersedida por ADR-048**) | Estrategias / Infraestructura |
| ADR-046 | Pipeline de features microestructurales en vivo (HITO-D-feat) | Estrategias / Infraestructura |
| ADR-045 | minimal-position-mode + min notional real en SPDB para futures Binance | Ejecución / Datos |
| ADR-044 | Órdenes protectivas SL/TP con `reduceOnly` en Binance Futures (divergencia del fork) | Riesgo / Ejecución |
| ADR-043 | Clock drift Binance -1021: guard NTP externo como pre-flight, no recvWindow | Operaciones / Infraestructura |
| ADR-042 | Dead-man's switch: liveness del feed (datos de minuto) en vez de cierre de barras de estrategia | Operaciones / Health |
| ADR-041 | Hito D-prev: protocolo de validación broker real Binance USDT-M | Operaciones / Infraestructura |
| ADR-040 | Pipeline de validación de estrategias: M4 → QC IS → QC OOS → Trading.Analytics | Research / Validación |
| ADR-039 | Hito G: IS/OOS validation + Monte Carlo â€” metodologÃ­a y rechazo OfiContrarian | ValidaciÃ³n |
| ADR-038 | OfiContrarianStrategy: aprobaciÃ³n QC IS 2021-2024 y decisiones de design | Estrategias |
| ADR-037 | E-INFRA-2: IMicrostructureProvider â€” features AggTrades sin cambiar IStrategy | Estrategias / Infraestructura |
| ADR-036 | ATR SL/TP mode: SL/TP basado en multiplicadores de ATR como alternativa al modo porcentaje | Arquitectura / EjecuciÃ³n |
| ADR-035 | AtrCompressionBreakoutStrategy: insight SL% incompatible con multi-bar hold | Estrategias |
| ADR-032 | WarmUpBars en IStrategy: warm-up dinÃ¡mico de indicadores internos | Arquitectura |
| ADR-031 | Hito C: feed verificado, race condition Binance, LeanClock UTC fix | Operaciones |
| ADR-030 | Bypass ValidateSubscription plugin Binance para live local | Infraestructura |
| ADR-028 | ValidaciÃ³n multi-sÃ­mbolo + fix estructural OPS-2 | ValidaciÃ³n |
| ADR-027 | Re-entrenamiento BTC con trainer multi-seed | HMM / RegÃ­menes |
| ADR-026 | ValidaciÃ³n multi-timeframe del subsistema de ejecuciÃ³n | ValidaciÃ³n |
| ADR-025 | LiquidateInstrument explÃ­cito y base de equity correcta en StrategyHealthMonitor | Riesgo / Health |
| ADR-024 | SemanticStateMapper adaptativo a K + multi-seed Baum-Welch | HMM / RegÃ­menes |
| ADR-023 | StrategyHealthMonitor: componente autÃ³nomo fuera de IRiskMonitor | Riesgo / Health |
| ADR-022 | POLICY.md: dos niveles de semÃ¡foro, calibraciÃ³n absoluta, liquidaciÃ³n inmediata | Operaciones |
| ADR-021 | Monitoreo bÃ¡sico: JSONL local + heartbeat + Healthchecks.io | Operaciones |
| ADR-020 | Test AccordHmmClassifierReferenceTests skipeado â€” convergencia degenerada | Testing |
| ADR-019 | ImplementaciÃ³n HMM con Accord.NET â€” Paso 3 Hito B | HMM / RegÃ­menes |
| ADR-018 | Adelantamiento INFRA-1: path absoluto strategies.json eliminado | Infraestructura |
| ADR-017 | Hito B completo: abstracciÃ³n de regÃ­menes, guard pre-orden, HMM real | HMM / RegÃ­menes |
| ADR-016 | Trading Policy escrita + monitor runtime de degradaciÃ³n | Riesgo / Health |
| ADR-015 | SeparaciÃ³n IRiskMonitor de IRiskAction | Arquitectura |
| ADR-014 | ReversiÃ³n SignalAuditor: validaciÃ³n por tests unitarios estÃ¡ticos | Testing |
| ADR-012 | Auditor de seÃ±ales: tolerancia relativa, no absoluta | Testing |
| ADR-011 | Auditor de seÃ±ales: warm-up por sÃ­mbolo en lugar de buffer infinito | Testing |
| ADR-010 | Auditor de seÃ±ales en C# dentro del mismo backtest | Testing |
| ADR-009 | Bus de eventos de dominio sÃ­ncrono in-memory | Arquitectura |
| ADR-008 | Postergar refactors no crÃ­ticos hasta despuÃ©s de cada hito | Arquitectura |
| ADR-007 | `ITradingLogger` se mantiene como abstracciÃ³n del dominio | Arquitectura |
| ADR-006 | `Long`/`Short` en estrategias usando enum simple | Dominio |
| ADR-005 | Cleanup automÃ¡tico del `OrderRegistry` tras eventos terminales | Dominio |
| ADR-004 | Tags opacos formato `ord_xxxxxxxx` (GUID corto) | Dominio |
| ADR-003 | `OrderRegistry` vive en `Trading.Application` | Arquitectura |
| ADR-002 | `RiskPerTradePercentage` falla loud si no estÃ¡ en `strategies.json` | Dominio |
| ADR-001 | Desacople quirÃºrgico de QuantConnect: dominio Lean-free | Arquitectura |

## ADR-055 — Universo y datos sin restricción a priori; el objetivo del sistema es la cartera
**Fecha:** 2026-06-25
**Estado:** Vigente. Política de research; condiciona el criterio de éxito de toda hipótesis futura y, eventualmente, el pipeline de validación (ADR-040) y los umbrales de POLICY.md.
**ADRs relacionados:** ADR-040 (pipeline M4→IS→OOS→MC — su criterio de éxito se extiende aquí), ADR-053 (custom data: el camino de datos ya es agnóstico a la fuente y permite sumar OHLCV/aggTrades/order book), ADR-039 (Monte Carlo / validación).

### Contexto
El research arrastraba dos restricciones **implícitas** que nunca fueron decisiones conscientes, solo el camino de menor esfuerzo:
1. **Universo de 3-4 majors** (BTC/ETH/SOL/BNB). Surgió de los primeros backtests, no de un criterio.
2. **Datos atados a lo ya disponible** (features 1h existentes), con el backlog de ejes ordenado explícitamente "de barato (1h existente) a caro".

Estas restricciones bloquearon hipótesis enteras. El caso disparador: **momentum cross-sectional** necesita dispersión de ranking sobre muchos nombres (la literatura usa 10-100+); con N=3-4 majors de beta ~1 a BTC es estadísticamente vacío. El "no-go" no era de la hipótesis sino del universo auto-impuesto. Lo mismo aplica a cualquier estrategia de cartera: **una cartera diversificada no se puede construir sobre 3 activos correlacionados.**

Además, el criterio de éxito vigente (Sharpe standalone vs gate M4) juzga cada estrategia **en aislamiento**, lo que es incoherente con el objetivo declarado del sistema.

### Decisión
1. **El objetivo final del sistema es la cartera, no la estrategia individual.** El criterio de éxito de una hipótesis incorpora su **contribución marginal a la cartera** (correlación con lo ya presente, diversificación), no solo su Sharpe standalone. Una estrategia de Sharpe moderado pero descorrelacionada puede valer más que una de Sharpe alto que replica el beta de BTC.
2. **Universo sin restricción a priori.** Se usan tantos activos como la hipótesis requiera. No hay límite implícito de majors.
3. **Datos sin restricción a priori.** aggTrades, OHLCV y order book (si hace falta y es posible obtenerlo), en **cualquier temporalidad**. El dato se elige por la hipótesis, no la hipótesis por el dato disponible.

### Condiciones que hacen válida la decisión (no es un cheque en blanco)
"Sin restricción" significa **habilitado, evaluado con criterio** — no gratis ni por default. Toda expansión asume explícitamente:
- **Survivorship bias:** un universo de alts armado con los símbolos *de hoy* excluye las que murieron → el backtest cross-sectional miente a favor. Cualquier universo ampliado debe ser **point-in-time** (la membresía en fecha t es la que existía y era listable en t), o el resultado es inválido. Trampa #1 de cualquier cartera de cripto.
- **Disponibilidad y costo de datos:** order book histórico con profundidad es caro/escaso; aggTrades sub-hora hay que regenerarlo. La política habilita *pedir* el dato, no presupone que existe — cada expansión de datos es una tarea con costo, no un supuesto.
- **Liquidez y costos por activo:** el muro de costos (fees + slippage) **empeora en alts** (más slippage, menos profundidad). El universo se pondera por liquidez; nada de equiponderado ingenuo. El umbral señal/costo se evalúa por activo, no global.

### Alternativas consideradas
- **Mantener el universo acotado a majors:** descartada. Es la causa raíz del bloqueo de cross-sectional y es incompatible con el objetivo de cartera.
- **Abrir universo/datos sin caveats ("usar lo que haga falta" a secas):** descartada. Sin point-in-time y ponderación por liquidez, ampliar el universo es una fábrica de sobreajuste y backtests inflados — exactamente el modo de falla que ya nos costó Hito E/G (ADR-054).

### Consecuencias
- El backlog de ejes de research deja de leerse como "atado a 1h/majors existentes". El orden barato→caro sigue siendo una guía de eficiencia, no una restricción de alcance (ver banner de `ROADMAP.md`).
- **Pendiente de diseño (no implementado aquí):** (a) construcción de universo point-in-time para cripto; (b) extensión del criterio de éxito de ADR-040 para medir contribución a cartera (correlación / diversificación), no solo Sharpe standalone; (c) ponderación por liquidez en M4 y en ejecución. Estos son ítems de ROADMAP que esta decisión habilita, no entregables de este ADR.
- **No invalida** ningún resultado previo: las hipótesis ya rechazadas lo fueron por razones que se sostienen (costo estructural, OOS genuino). Esta decisión amplía el espacio de búsqueda hacia adelante.

## ADR-054 — Bug de apareo de features (lookahead de 1h) en QC IS/OOS — invalida las validaciones de Hito E/G
**Fecha:** 2026-06-24
**Estado:** Identificado y corregido (la corrección es ADR-053). Consecuencias de validación en curso: H3 y H5 rechazadas; re-validación del pipeline pendiente (ROADMAP).
**ADRs relacionados:** ADR-053 (la corrección — unifica el camino de datos), ADR-040 (pipeline de validación M4→IS→OOS→MC), ADR-039/038 (Hito G), ADR-037 (IMicrostructureProvider)

### Contexto
Al implementar ADR-053 se descubrió que el backtest de QC (capa C#) apareaba mal el precio con las features de microestructura. El `MarketBarMapper` seteaba `marketBar.TimestampUtc = TradeBar.EndTime` (el **fin** de la barra: 16:00 para la barra [15:00,16:00)), pero el `MicrostructureRegistry` indexa las features por el **inicio** (15:00, floor de la hora — convención de `download_aggtrades.py` y del Recorder). Entonces, dentro de `EvaluateSignal`, la estrategia consultaba `GetBar(marketBar.TimestampUtc = 16:00)` y recibía las features de la barra **siguiente** [16:00,17:00).

Resultado: **el precio de la hora t se evaluaba contra el flujo de órdenes de la hora t+1** — información del futuro. Sesgo forward-looking (lookahead) de 1 hora.

### Evidencia empírica
Mismo dato, misma estrategia, mismo período (IS 2021-2024); la única diferencia es el apareo (commit padre vs ADR-053):

| Estrategia | OLD (apareo desfasado) | NEW (apareo correcto) |
|---|---|---|
| TradeSizeInstitutional (H5) | Sharpe **6.645** / Net **+1384%** / Win 60% | Sharpe **−0.289** / Net +2.4% / Win 48% |
| CvdSellExhaustion (H3) | Sharpe **2.193** / Net **+129%** / Win 61% | Sharpe **−1.224** / Net −7.9% / Win 45% |

Un Sharpe de 6.6 con +1384% en 4 años es físicamente implausible: la firma inconfundible del lookahead. El apareo correcto se verificó además campo por campo (OHLC + 7 features) contra la fila del CSV de research vía el log `SignalEmitted`.

### Decisión / corrección
La corrección es **ADR-053**: las features entran como custom data (`MicrostructureFeatureData`) y la barra de señal se arma de la **misma fila** (OHLCV + features), con `marketBar.TimestampUtc = bar start`, eliminando el desfase por construcción. No hay forma de re-introducir el lookahead: precio y features salen del mismo registro.

### Consecuencias
- **H3 (CvdSellExhaustion) y H5 (TradeSizeInstitutional) rechazadas** (`git rm`). Su aprobación QC IS/OOS (Hito G) fue un artefacto del lookahead; sobre el camino correcto no tienen edge. Documentado en `Trading.Research/strategy_experiments.md`. Live detenido manualmente en el VPS.
- **El bug vivía en la capa QC (C#), no en M4 (Python).** M4 aparea correcto (usa el CSV de research directo), por eso una estrategia podía pasar M4 con edge real-pero-débil y luego ser inflada por QC IS/OOS.

### Radio de impacto (componentes que dependían del supuesto "la validación QC IS/OOS es sólida")
- [x] H5 TradeSizeInstitutional — rechazada, `git rm`, documentada.
- [x] H3 CvdSellExhaustion — rechazada, `git rm`, documentada.
- [x] Camino de apareo (`MarketBarMapper`/`GetBar` en live; consolidator en backtest) — reemplazado por ADR-053 (custom data, misma fila).
- [x] **Todas las demás estrategias evaluadas por QC IS/OOS en Hito E y G** — las **aprobadas** quedan en duda (posible inflación); las **rechazadas** pueden haber sido penalizadas injustamente (ej.: OfiContrarian, ADR-039). Re-validación del pipeline sobre el camino corregido: ítem de ROADMAP.
  - **Corrección (2026-06-24, análisis de costura — no borrar el texto de arriba, queda para historia):** la frase "penalizadas injustamente" es **incorrecta**. El lookahead **infla** (usa el futuro), no penaliza: una rechazada-con-lookahead fue favorecida y aun así falló (sin rescate posible). Y **OfiContrarian NO tenía el lookahead**: su `GetBar(TimestampUtc.AddHours(-1))` cancelaba exactamente la convención `EndTime` del camino viejo, dejándola correctamente apareada — su rechazo OOS (−0.703) es **genuino**, no artefacto. (H3/H5 sí usaban `GetBar(TimestampUtc)` sin offset → lookahead.) **No hay falsos negativos del bug.** Deuda #1 cerrada sin re-correr ninguna candidata vieja; el camino es generar hipótesis nuevas sobre datos correctos. Ver banner de ROADMAP y `Trading.Research/strategy_experiments.md`.
- [ ] POLICY.md sección 7 (umbrales U1-U4 de estrategias aprobadas) — invalidada para H3/H5; revisar al re-validar.

## ADR-053 — Custom BaseData del store de microestructura: un solo camino de datos para backtest y live
**Fecha:** 2026-06-24
**Estado:** Aceptada — implementada (unit tests verdes: 11 Reader incl. paridad cruzada + 33 App + 57 Strat). Pendiente: verificación backtest end-to-end y re-validación de las 2 estrategias sobre el camino unificado (decisión A — el backtest cambia respecto del QC IS/OOS previo, que era de procedencia mixta precio-QC + features-research).
**ADRs relacionados:** ADR-049 (Recorder REST, premisa "el WS no entrega push"), ADR-048 (Recorder = data plane, escritor único del store), ADR-051 (warmup desde store), ADR-046 (pipeline live de features — el camino que esto reemplaza), ADR-037 (IMicrostructureProvider), ADR-001 (dominio Lean-free)

### Contexto
La paridad backtest/live está rota en la **barra en curso**. La estrategia consume dos fuentes y ambas pasaban por el WebSocket de aggTrades de Binance Futures:
- el **precio** (`marketBar.Close`, p.ej. condición "close es mínimo de 48") lo arma `TickConsolidator` desde los ticks del WS;
- las **features** (`CvdDelta`, `MeanTradeSize`) las computa `LiveAggTradeAccumulator` + `ComputeAndAdd` desde el bucket del WS.

ADR-049 probó empíricamente que ese WS **no entrega push** a las redes/clientes del proyecto (irreparable desde el cliente). El Recorder esquivó el problema migrando a REST y dejando el `MicrostructureStore` confiable — pero el execution plane **nunca migró**: sigue computando la barra en curso desde el WS roto. Un bucket parcial infla `mean_trade_size` (=vol/conteo) → señal espuria que el store no sostiene (caso ETH 2026-06-23). ADR-051 cubrió solo el warmup desde el store; la barra en curso quedó en el WS. ADR-052 hizo *auditable* el dato malo, no cambió la fuente.

Además, backtest y live usaban **caminos de datos distintos** (backtest: `TradeBarConsolidator` sobre minuto + `MicrostructureRegistry`; live: `TickConsolidator` sobre ticks + cómputo en memoria). Esa divergencia es la clase de bug que estamos cerrando.

### Decisión
Las features microestructurales (OHLCV + las 7 features, en una sola fila) entran al algoritmo como un **custom data source de Lean** (`BaseData`), streameado por el motor de QC con **el mismo camino en backtest y en live**. La barra que dispara la evaluación deja de armarse desde ticks/buckets del WS y llega por `OnData`, idéntica en ambos modos. Paridad **por construcción**.

- **`MicrostructureFeatureData : BaseData`** (en `Trading.Strategies`, lado Lean-aware; el dominio sigue Lean-free por ADR-001). Lleva OHLCV + ofi/cvd/cvdDelta/arrivalRate/meanTradeSize/buySellRatio/priceReturn.
- **`GetSource`:** backtest → CSV histórico de research (`{ticker}_1h_features.csv`); live → CSV del store del Recorder (`{ticker}_1h_live.csv`). Ambos `SubscriptionTransportMedium.LocalFile`. El Recorder sigue siendo el escritor único (ADR-048).
- **`Reader` mode-aware:** los dos CSV tienen orden de columnas y formato de timestamp distintos (research: 16 cols, ts `...+00:00`, orden OHLCV-first; store: 13 cols, ts `...Z`, orden features-first). El `Reader` ramifica por `isLiveMode` — patrón canónico de Lean (`CustomDataBitcoinAlgorithm`). El timestamp es el **inicio** de la barra; `Time = barStart`, `EndTime = barStart + 1h` (el motor live no emite hasta wall-clock ≥ EndTime → entrega al cierre).
- **Registro a `Resolution.Minute`, no `Hour`.** El custom data live se pollea cada `Min(resolución, 30min)`; a `Hour` serían **30 min de latencia** (inaceptable). A `Minute` el poll baja a **~1 min** y, como el dato es sparse (una fila/hora) con `IsSparseData()=true`, el frontier-dedup por `EndTime` de Lean emite cada barra una sola vez. Latencia ~1 min **sin tocar el motor de Lean**.
- **`OnData` orquesta:** al llegar un `MicrostructureFeatureData`, se arma el `MarketBar` desde su OHLCV, el provider resuelve sus features y se llama `BarProcessingService.ProcessBar` — que ya es agnóstico de QC. El sizing/SL/TP usa el close del store (≈1 min de antigüedad; el fill es a mercado).
- **Se elimina el camino del WS:** `LiveAggTradeAccumulator`, `ComputeAndAdd` en vivo y la suscripción a `Resolution.Tick`. En live el cripto pasa a `Resolution.Minute` (solo para pricing de ejecución y liveness del dead-man's switch — **verificar** que ese feed de minuto sobreviva, ver radio de impacto).

### Alternativas descartadas
- **`Schedule.On(hora+delay)` + poll-con-timeout del store.** Más chico, pero (a) deja backtest y live en caminos distintos —no cierra la divergencia—, (b) el timeout es una heurística de reloj, no una garantía de completitud, (c) re-acopla la evaluación al feed de QC justo en el evento poco confiable. Es un compromiso pragmático, no el patrón institucional.
- **Modificar `LiveCustomDataSubscriptionEnumeratorFactory` (bajar `minimumIntervalCheck`).** Tocar el motor de Lean. Innecesario: registrar a `Resolution.Minute` logra ~1 min sin divergir del fork.
- **Convergir los dos formatos de CSV a uno solo.** Exigiría re-generar el dataset de research (pipeline Python). El `Reader` mode-aware lo evita; convergencia queda como deuda opcional.

### Consecuencias
- **Paridad por construcción:** mismo `BaseData` y mismo `OnData` en backtest y live; la única diferencia es el archivo fuente (research vs store), que contiene el mismo esquema de features.
- **Latencia de entrada ~1 min** tras el cierre de barra (poll de minuto). Para holds multi-barra de 1h es despreciable. Si en el futuro se baja a timeframes sub-horarios, revisar.
- **Warmup:** en esta fase se conserva el warmup vía store reader (ADR-051). Deuda de seguimiento: unificar el warmup a través de la historia del custom data (QC reproduce historia por `GetSource` de fechas pasadas), eliminando el segundo camino.
- **El backtest sigue necesitando `AddCryptoFuture`** para fills/pricing; el custom data aporta la barra de señal, el cripto aporta la ejecución (igual en live).

### Radio de impacto (componentes que dependían del supuesto viejo "la barra en curso se computa desde el WS")
- [x] `MicrostructureFeatureData : BaseData` — creado (Trading.Strategies/Microstructure). 11 tests del `Reader` (research 16-col, store 13-col, timestamps, paridad cruzada, NaN, header/columnas).
- [x] `TradingAlgorithmHost.Initialize` — `AddData<MicrostructureFeatureData>` por ticker a `Resolution.Minute`; cripto a `Minute` en ambos modos; quitado `_aggTradeAccumulator` y el handler `DataConsolidated` de microestructura; `OnData(custom)` → `ProcessBar` vía `_microstructureRoutes`. Providers creados en ambos modos. Compila.
- [x] `LiveAggTradeAccumulator` + `ComputeAndAdd` — sin uso → `git rm` del accumulator + método removido del provider. Sin referencias ni tests rotos.
- [x] `LiveMicrostructureProvider` — `GetBar` resuelve la barra del custom data vía `AddBar` (sin cambios; seed CVD intacto). 33 tests verdes.
- [ ] Dead-man's switch (ADR-042) — **verificar en live** que el feed de minuto del cripto da liveness sin la suscripción de ticks (¿el kline WS de Binance Futures sí entrega, a diferencia del aggTrade de ADR-049?).
- [ ] Estrategias no-microestructura (EmaCross) — N/A en config actual (solo estrategias de microestructura 1h). Si se agregan, necesitan su propia fuente de barra (el host loguea Warning para timeframes sin custom data).
- [ ] Backtest end-to-end — **verificar** que corre sobre el camino unificado y produce resultados sensatos. NO es no-regresión byte-idéntica: el backtest cambia por diseño (precio ahora de research, no de QC-minuto). Riesgo abierto: resolución del path LocalFile absoluto fuera del data-folder de Lean.
- [ ] Re-validación de las 2 estrategias sobre el camino unificado (Fase 2 IS → Fase 3 OOS → Fase 4 MC, AI.md).
- [ ] `BinanceAggTradeBackfiller` — sigue para el gap de warmup; confirmar que no colisiona con el custom data.

## ADR-052 — Observabilidad de señal: evento estructurado SignalEmitted (features genéricas + condición vía ISignalDiagnosticsProvider)
**Fecha:** 2026-06-24
**Estado:** Aceptada
**ADRs relacionados:** ADR-046 (pipeline live de features), ADR-051 (warmup desde store), ADR-037 (IMicrostructureProvider), ADR-021 (JSONL + structured logging), ADR-007 (ITradingLogger como abstracción de dominio)

### Contexto
La primera señal real (ETHUSDT 2026-06-23 16:00 UTC, entry Long) **no era auditable**: la plomería (entry/SL/TP/sizing) quedó logueada, pero **no qué estrategia disparó, contra qué features, ni qué condición se cumplió**. Al contrastar contra el store del Recorder (REST, dato confiable) ninguna de las dos estrategias activas debería haber señalizado en esa barra (`CvdSellExhaustion`: close 1658.9 no era mínimo de 48; `TradeSizeInstitutional`: mean_trade_size 2.78 muy por debajo del P90 ≈ 4.5). Hipótesis: el WS de aggTrades de LeanLive es intermitente (ADR-049 manifestándose en el execution plane) y un feed parcial infla `mean_trade_size` (=volumen/conteo) disparando una señal espuria. **Sin observabilidad de señal no se puede validar la hipótesis ni auditar ninguna señal futura.**

Tensión de diseño: `IStrategy.EvaluateSignal` devuelve solo `SignalDirection`; no expone el "por qué". Loguear las **features** de forma genérica no requiere tocar la estrategia (el provider ya tiene la barra). Loguear la **condición** específica (qué umbral se comparó) sí requiere que la estrategia exponga su rationale.

### Decisión
Emitir un evento estructurado **`SignalEmitted`** por cada señal **no-Flat**, en `BarProcessingService` **antes** de los filtros de régimen/posición/sizing (para que toda señal emitida quede auditable aunque después se descarte). Dos capas, ambas open-closed:

1. **Features genéricas (sin código por estrategia).** `BarProcessingService` recibe un mapa `timeframe → IMicrostructureProvider` (las mismas instancias que ven las estrategias; la clave es `StrategyExecutor.Timeframe` por correctitud multi-timeframe) y loguea el OHLC del `MarketBar` + las 7 features del `MicrostructureBar` (ofi, cvd, cvd_delta, arrival_rate, mean_trade_size, buy_sell_ratio, price_return) de la barra que disparó. Resuelve con el mismo `GetBar(instrumentId, barUtc)` que usó la estrategia → misma barra. El mapa es **opcional**: sin él, la señal igual se audita (OHLC + `tieneMicroestructura=false`).

2. **Condición específica (interfaz-capacidad opcional, espejo de `IAtrProvider`).** Nueva `ISignalDiagnosticsProvider` en `Trading.Domain.Abstractions` con `SignalDiagnostics? DescribeLastEvaluation()`, y el value object `SignalDiagnostics(Summary, IReadOnlyList<SignalCondition>)` / `SignalCondition(Name, Value, Comparison, Threshold, Satisfied)` en `Trading.Domain.ValueObjects`. Las dos estrategias de microestructura la implementan: guardan en `_lastDiagnostics` la condición que evaluaron (`CvdSellExhaustion`: close≤min48, cvd_delta>0; `TradeSizeInstitutional`: mean_trade_size≥P90, bsr>1.02, con los valores reales). `BarProcessingService` chequea `strategy is ISignalDiagnosticsProvider` y anexa el rationale. **`IStrategy` no cambia**; EmaCross no la implementa y se loguea igual a nivel features (condición `(n/d)`).

El render de condiciones usa **`InvariantCulture`** (el VPS tiene locale no-US; sin esto los decimales salían con coma y rompían la parseabilidad del log). Template con **placeholders nombrados sin format specifiers** (`{Close}`, no `{Close:F4}`): la estructura va en las properties del JSONL, no en el render — evita además el bug cosmético de placeholders con `:` sin interpolar.

### Alternativas descartadas
- **Cambiar la firma de `IStrategy.EvaluateSignal` para devolver señal + diagnóstico.** Rompe a todos los implementadores (EmaCross) — no es open-closed. La interfaz-capacidad opcional logra lo mismo sin tocar el contrato base, siguiendo el precedente de `IAtrProvider`.
- **Meter las 7 features dentro del `SignalDiagnostics` de cada estrategia.** Exige código por estrategia para algo que el provider ya tiene; una estrategia que no implemente la interfaz no loguearía features. Las features genéricas vía provider son el piso garantizado.
- **Inyectar un único `IMicrostructureProvider` en vez del mapa por timeframe.** Incorrecto en setups multi-timeframe (hay un provider por timeframe, ADR-048). El executor trae su `Timeframe`, que es la clave correcta.
- **Evento de dominio en el bus + subscriber que loguea.** Más maquinaria para lo mismo; `ITradingLogger.Info` estructurado ya aterriza en el JSONL (ADR-021) y es el camino de menor fricción.

### Consecuencias
- **La próxima señal queda auditable**: estrategia, executor, símbolo, timeframe, barUtc, dirección, OHLC, las 7 features, y la condición evaluada con sus umbrales, todo en el JSONL. Habilita validar en vivo la hipótesis del WS intermitente (backlog #2/#3).
- Genérico y coherente con ADR-051: cualquier estrategia de aggTrades audita features sin código nuevo; las que exponen rationale agregan la condición.
- Warmup-safe: el log vive solo en el camino live no-warmup; el replay de warmup (ADR-051) llama `EvaluateSignal` pero no llega a este código → sin ruido.
- Sin fuga de QC: `IMicrostructureProvider`/`ISignalDiagnosticsProvider` son abstracciones de dominio.
- Tests: 6 en `BarProcessingServiceSignalAuditTests` (emite en no-Flat, no en Flat ni warmup, condición presente/`(n/d)`, sin provider) + 4 de diagnostics en las dos estrategias. Suite verde.
- **Deuda conocida (cosmético, MENORES):** quedan placeholders `{...:F4}`/`{...:yyyy-…}` sin interpolar en otros logs (ej. seed de CVD, warning de bucket vacío) — el `LogTemplateRenderer` no maneja format specifiers. No lo toca este ADR; el evento `SignalEmitted` los evita por diseño.

---

## ADR-051 — Warmup de estrategias desde el store (OHLCV en el store + replay genérico por EvaluateSignal)
**Fecha:** 2026-06-23
**Estado:** Aceptada
**ADRs relacionados:** ADR-046 (pipeline live de features), ADR-048/049 (store del Recorder), ADR-032 (warm-up dinámico — enmendado)

### Contexto
LeanLive arrancó (ADR-050) pero las estrategias no señalizaban: su warmup interno (colas de close/features) no se llenaba. Causa: Lean warmea las estrategias reproduciendo history de **precio** a través del consolidador, y en live la suscripción es `Resolution.Tick` (ADR-046), para la cual Binance no devuelve history (`Tick resolution is not supported, no history returned`). Las colas arrancan vacías y se llenan a 1 barra/hora → CvdSellExhaustion (48 closes) tarda ~48h y TradeSizeInstitutional (24) ~24h en poder señalizar. **Cada reinicio ciega al sistema 1-2 días.** El store del Recorder (ADR-048/049) tiene la historia (features) hasta la última barra cerrada, pero (a) el warmup no la usaba y (b) no incluía OHLC, que las estrategias de precio necesitan (`CvdSellExhaustion` lee `marketBar.Close`).

### Decisión
1. **El store persiste OHLCV.** `MicrostructureBar` gana `Open/High/Low/Close/Volume` (decimal por AI.md, propiedades `init`, default 0). El `AggTradeBucket` ya computaba OHLC; se exponen en decimal y `MicrostructureFeatureComputer.Compute` los puebla. El CSV del store **apenda** `open,high,low,close,volume` al final (compat: archivos de 8 columnas se leen con OHLCV=0). **Sin cambios en el Recorder:** ambos paths (feed live vía `TimeframeAggregator` + Vision seeder) ya pasan por `Compute`.
2. **Warmup genérico desde el store (replay).** En `Initialize()` (LiveMode), tras cargar el store, el host reproduce las barras históricas por `IStrategy.EvaluateSignal` —el mismo punto de entrada que las barras vivas— descartando la señal, para que cada estrategia llene su estado interno. Es **agnóstico a la estrategia** (open-closed): cualquier estrategia de aggTrades warmea sin código nuevo. Store sin OHLC o más corto que `WarmUpBars` → warmup **parcial** + Warning (nunca fabrica dato; la estrategia completa con barras vivas).
3. **Fix del piso de warmup (enmienda ADR-032).** El piso de 400h (100 barras × 4h del HMM) ahora es **condicional** a que haya al menos un clasificador de régimen cargado. Sin régimen no se fuerzan 17 días de warmup innecesarios.

### Alternativas descartadas
- **Klines REST en `Initialize()` para el close histórico.** Reintroduce el acople REST-en-Initialize que ADR-048 eliminó deliberadamente. Descartada.
- **Esperar 1-2 días de barras vivas tras cada reinicio.** Inaceptable para un sistema live: ciega el sistema en cada restart.
- **Tipo `StoredBar` separado en vez de OHLCV en `MicrostructureBar`.** Más tipos y blast radius; las `init` props aditivas no rompen ningún caller y modelan mejor "barra OHLCV + features de flujo" (lo que ya decía la doc de `MicrostructureBar`).

### Consecuencias
- **Re-seed del store requerido** (los archivos de 8 columnas no tienen OHLC). Secuencia: parar Recorder → borrar `*_1h_live.csv` → arrancar Recorder (re-siembra de Vision **con OHLC**) → redeploy LeanLive (warmea completo).
- Genérico: una estrategia nueva de aggTrades warmea desde el store sin tocar el host. **Límite:** estrategias que necesiten datos fuera del store (ej. régimen 4h del HMM) tienen su warmup aparte.
- Backtest intacto: usa el CSV de research (16 col) + history de precio Minute; el replay desde store es **LiveMode-only**.
- Tests: store OHLC round-trip + compat hacia atrás, OHLC parity del computer, y warmup-via-replay con contraste sin replay. 309 tests verdes.
- `AI.md`: el store ahora documenta columnas OHLCV.

---

## ADR-050 — Live con minimal-position-mode permanente como validación operativa (reemplaza paper para estrategias ya aprobadas)
**Fecha:** 2026-06-20
**Estado:** Aceptada
**ADRs relacionados:** ADR-045 (minimal-position-mode — reclasificado de shakedown a permanente), ADR-041 (broker real validado), ADR-039/ADR-040 (validación estadística). **Enmienda POLICY P1.**

### Contexto
Las dos estrategias de microestructura aprobadas (`TradeSizeInstitutionalStrategy`, `CvdSellExhaustionStrategy`) completaron el pipeline estadístico completo (M4 → QC IS → QC OOS → Monte Carlo, Hito-G; OOS Sharpe 4.186 y 1.718, P(Sharpe<0) 0% y 1%). POLICY P1 exige paper trading antes de capital real ("orden estricto… no se saltea pasos").

Dos hechos motivan revisar ese paso:
1. **El paper trading usa fills simulados.** Para estrategias de **microestructura** —cuyo edge depende del order flow y es sensible a slippage/latencia— los fills simulados son optimistas y dan **confianza falsa**. El live con capital mínimo da fills, slippage y comisiones **reales**: evidencia operativa superior.
2. **Corrección de registro:** POLICY 7.4/7.5 declaraban "paper (activa desde 2026-06-15)", pero esas estrategias **nunca operaron** (paper ni live); el proceso que corrió en paper fue EmaCross. Ese estado era incorrecto y se corrige en este ADR.

### Decisión
La validación operativa de estas estrategias se hace en **live con capital mínimo** (`minimal-position-mode`, tamaño fijado al min notional del exchange: BTCUSDT=100, ETHUSDT=20, SOLUSDT=5 USD), **no** en paper. El modo es **permanente hasta nuevo aviso** (no shakedown temporal).
- Reclasifica `minimal-position-mode` (ADR-045) de "modo de shakedown, desactivar para sizing real" a **modo operativo permanente** para este plano.
- **Enmienda P1**: la validación operativa (paso c) admite dos formas — (c1) paper, o (c2) live con capital mínimo para estrategias que ya pasaron Hito-G. Condiciones de (c2): U1-U4 y la cadencia de revisión (POLICY 4) activos; documentar con ADR.
- Escala a sizing real: **criterio indefinido por ahora** (revisar en la revisión trimestral de POLICY).

### Alternativas consideradas
- **Paper primero (lo que decía P1).** Descartada para este plano: fills simulados → confianza falsa en estrategias de microestructura; el live-mínimo es evidencia real con riesgo de capital despreciable.
- **Live a tamaño real directo.** Descartada: prematuro; no hay medición de slippage real previa y el riesgo deja de ser despreciable.
- **No operar hasta acumular paper.** Descartada: las estrategias ya pasaron toda la validación estadística; el paper no agrega información de ejecución real.

### Consecuencias
- **P1 enmendada** (path c2). **POLICY 7.4/7.5 corregidas** a la realidad: nunca operaron en paper; estado pre-live; validación operativa por live-mínimo.
- **`minimal-position-mode` permanente** para este plano (revierte para este caso la nota de ADR-045 "desactivar para sizing real").
- **Caveat (P3):** el live-mínimo valida **dirección + plomería**, NO el **slippage a escala** (el slippage crece con el tamaño). Al escalar el sizing, esperar el haircut P3 (30-50%) o peor. No interpretar el desempeño a mínimo notional como predictor del desempeño a tamaño real.
- **Guardas activas obligatorias:** U1-U4 (`StrategyHealthMonitor` real, no Null), kill switch, dead-man's switch (ADR-042), y la cadencia de revisión humana de POLICY 4. Live = capital real aunque sea mínimo.
- Riesgo de capital por trade: despreciable (~$0.25-5 con SL 5% sobre notional $5-100).
- **Deuda conocida:** definir el criterio de escala a sizing real (hoy indefinido); revisar en la revisión trimestral de POLICY.

---

## ADR-049 — Recorder: feed REST polling de aggTrades (el WS de Binance Futures no entrega push a ninguna red/cliente del proyecto)
**Fecha:** 2026-06-19
**Estado:** Aceptada — implementada (`ITradeFeed` + `BinanceAggTradeRestFeed`, REST por default).

> Reemplaza el enfoque inicial de este ADR (bypass de proxy + túnel WireGuard), que partía de un diagnóstico equivocado. Las hipótesis de proxy y de geo-bloqueo fueron **falsadas empíricamente** (ver Contexto); se conservan abajo como alternativas descartadas.

### Contexto
`Trading.Recorder` necesita aggTrades en tiempo real de Binance Futures (BTCUSDT, ETHUSDT, SOLUSDT) para materializar barras de microestructura en `MicrostructureStore`. El WebSocket a `wss://fstream.binance.com` conecta, completa TLS, manda el ACK del SUBSCRIBE, y después **no entrega ningún frame de datos** con el socket `Open`.

Diagnóstico empírico, descartando hipótesis una por una:
- **No es proxy/firewall:** el VPS no tiene proxy de sistema (WinHTTP "Direct access", `ProxyEnable=0`). El bypass `Options.Proxy=null` (commit b4fab48) no cambió nada.
- **No es inbound bloqueado:** el ACK `{"result":null,"id":1}` llega — es un frame inbound de Binance.
- **No es geo:** reproducido idéntico en VPS (Alemania) **y** PC dev (Argentina); además el trading real de futures se ejecutó OK desde el VPS (orden 0.09 SOL filleada, Hito D-prev) → Binance no restringe futures por IP ni por cuenta.
- **No es el runtime ni el código:** falla igual con .NET Framework (PowerShell), .NET 10 (recorder) y Python (`websocket-client`).
- **Spot WS sí funciona** en las mismas máquinas/clientes (122-140 aggTrades en 12s); **futures REST sí funciona** (`/fapi/v1/aggTrades`, `time`).

Conclusión: el **push del WS de Binance Futures no se entrega a las redes/clientes del proyecto**, mientras spot WS, futures REST y el trading de futures funcionan. La causa exacta (un middlebox que descarta el flujo server-initiated de fstream) no se pudo aislar y es irrelevante para la decisión: es irreparable desde el cliente y no depende de la ubicación.

### Decisión
El recorder obtiene aggTrades por **REST polling de `/fapi/v1/aggTrades` por `fromId`**, no por WebSocket. Se introduce el puerto `ITradeFeed` con dos adapters seleccionables por env var `RECORDER_FEED` (default `rest`):
- **`BinanceAggTradeRestFeed` (REST, default):** cursor `fromId` persistido por símbolo (`FileAggTradeCursorStore`, escritura atómica), `limit=1000`, **drenaje** en picos (repoll hasta página <1000), **idempotencia** por `aggId`, **backoff** ante 429/418 respetando `Retry-After` (evita el ban escalado), y **salto al presente con log ante gap grande** (no fabrica dato; la historia faltante se re-siembra con `BinanceVisionSeeder`).
- **`BinanceAggTradeWebSocketClient` (WS):** se conserva detrás del puerto para redes donde fstream sí entregue push — no es dead code. El `TradeHandler` no cambia.

**Arranque con historia inmediata (gap-fill + puente):** al iniciar, `StartupSeeder` rellena el hueco del store desde `data.binance.vision` (hasta T-1) y deja el cursor REST en el último `aggId` sembrado; el feed en vivo drena desde justo después, **sin gaps y con CVD continuo**. Un deploy en frío arranca con historia inmediata (no hay que esperar días). Configurable con `RECORDER_SEED_ON_STARTUP` (default on) y `RECORDER_SEED_DAYS` (default = `retentionDays`). Esto **automatiza el alta de activos** que ADR-048 dejaba como paso manual ("siembra offline una vez").

El dato REST es idéntico en contenido al del stream WS (mismos aggregate trades por `fromId`, sin gaps); solo cambia la latencia, irrelevante para barras cerradas de 1h.

### Alternativas consideradas
- **WS nativo (status quo).** Inviable: no entrega push a ninguna red/cliente del proyecto (ver Contexto).
- **Bypass de proxy de sistema (enfoque inicial de este ADR).** Descartada: no hay proxy configurado; el bypass no cambió el comportamiento.
- **Túnel WireGuard a nodo de salida (enfoque inicial de este ADR).** Descartada: se apoyaba en que el bloqueo fuera geográfico; al fallar idéntico en Argentina, reubicar el egreso no cambia nada.
- **Mover el recorder a la PC dev + sync del store.** Descartada: la PC dev tampoco recibe el push de fstream; además parte el data-plane en dos hosts.
- **Cambiar a Spot-Margin (el WS de spot funciona).** Descartada: cambia el instrumento operado. Las estrategias activas se validaron sobre microestructura de **futures** (M4→IS→OOS→MC); spot es otro mercado y estas estrategias (CVD / trade-size) son las más sensibles a esa diferencia. Migrar invalidaría toda la validación, exigiría re-correr el pipeline (con riesgo de perder el edge) y rehacer el stack de ejecución (brokerage spot/margin, borrow para shorts, fees ~2×). Spot-Margin solo como línea de research nueva, nunca como workaround de transporte.
- **REST polling (elegida).** Funciona desde el VPS (probado), instrumento correcto, mismo dato sin gaps, sin infra nueva, independiente de red/runtime. Contra: consume weight de rate-limit por IP compartido con LeanLive — mitigado con cadencia conservadora (ver techo de escala).

### Consecuencias
- Feed seleccionable por `RECORDER_FEED` (default `rest`). Env vars nuevas en AI.md: `RECORDER_FEED`, `RECORDER_REST_URL`, `RECORDER_REST_POLL_SECONDS`, `RECORDER_SEED_ON_STARTUP`, `RECORDER_SEED_DAYS`. `RECORDER_WS_USE_SYSTEM_PROXY` (commit b4fab48) se conserva: inocuo y válido para el adapter WS.
- Alta de activos / deploy en frío: ya **no** requiere paso manual de siembra (supera la consecuencia de ADR-048 línea 149); el gap-fill de arranque lo hace solo.
- Validación end-to-end real OK: gap-fill de Vision (24 barras/símbolo del día T-1) → puente de cursor → REST drena el día actual contiguo, CVD continuo a través del empalme.
- **Techo de escala (rate-limit):** `aggTrades` pesa 20, límite 2400/min por IP, sin endpoint batch (peso lineal con símbolos). A 3 símbolos es trivial (~120-240/min). A ~20 símbolos con cadencia 20-30s ronda ~1000-1300/min — sobrevive sin ban respetando 429, pero consume ~mitad del budget compartido con LeanLive. Techo duro ~25-30 símbolos. Camino de escala en ROADMAP (RECORDER-1): IP de egreso propia para el recorder al superar ~15 símbolos, desacoplando del execution-plane.
- Tests: 4 nuevos en `Trading.Recorder.Tests` (cold start, drenaje multipágina por fromId, idempotencia, backoff por rate-limit). Smoke test real OK contra fapi (cold start de 3 símbolos + drenaje + cursores avanzando).
- El `BinanceVisionSeeder` sigue como herramienta de siembra histórica (ADR-048); el REST feed es el camino en vivo.

---

## ADR-048 — Grabador continuo de microestructura: data plane desacoplado del execution plane
**Fecha:** 2026-06-18
**Estado:** Aceptada
**ADRs relacionados:** ADR-047 (supersedida), ADR-046 (pipeline live aggTrades)

### Contexto
ADR-047 introdujo `BinanceAggTradeBackfiller` para cubrir el warmup desde el endpoint REST `/fapi/v1/aggTrades` en cada `Initialize()`. En operación resultó insostenible: 20 weight por request, 156 requests para 52h × 3 símbolos → ban de IP (418 / -1003). El ban compite con `CreateListenKey()` del brokerage, crasheando el algoritmo completo. Intentos de mitigación (sleep 400ms → 700ms → abort en 418) fallaron o dejaron al sistema con warmup incompleto.

Causa raíz: la **captura de features está acoplada al ciclo de vida del algoritmo de trading**. Las features solo se computan cuando Lean corre; si Lean se cae, se pierde historia, y hay que reconstruirla desde el venue donde también se opera — frágil y con conflicto de recursos.

### Decisión
Separar el **data plane** del **execution plane** mediante un proceso independiente:

**`Trading.Recorder` (proyecto nuevo, console app):**
- Suscribe el **WebSocket público** `wss://fstream.binance.com/stream?streams={tickers}@aggTrade` (sin API key, sin riesgo de ban, sin competir con el brokerage).
- Acumula aggTrades en `TimeframeAggregator` por cada par (símbolo, timeframe) configurado en `strategies.json`.
- Al cerrar cada ventana: `MicrostructureFeatureComputer.Compute()` → `PersistentMicrostructureStore.Append()`. Mismo golden source que el pipeline live.
- Se despliega como servicio siempre encendido (systemd / Windows Service) en el VPS.
- Reconexión automática con backoff exponencial.

**Cambios en `Trading.Strategies`:**
- `TradingAlgorithmHost`: el host pasa a ser **puro consumidor**. En `Initialize()` lee las barras recientes del store por timeframe; **no escribe a disco**. Un proveedor `LiveMicrostructureProvider` por timeframe activo carga su store `{ticker}_{timeframe}_live.csv`.
- `BinanceAggTradeBackfiller`: descartado del rol de backfill en vivo. El store del Recorder llena el gap.

**`PersistentMicrostructureStore`:**
- Ahora recibe `timeframe` en el constructor. Nombre de archivo: `{ticker}_{timeframe}_live.csv` (antes `{ticker}_live.csv`).
- Migración: renombrar archivos existentes a `*_1h_live.csv` (script manual, un paso único).

**Siembra de activos nuevos (`BinanceVisionSeeder`, clase nueva en `Trading.Recorder/Seeding`):**
- Descarga archivos diarios ZIP desde `https://data.binance.vision/data/futures/um/daily/aggTrades/{TICKER}/` (hasta T-1).
- Sin límite de peticiones, sin riesgo de ban (depósito estático S3/CDN).
- Paridad exacta con el stream live: mismo campo `is_buyer_maker`, mismo `AggTradeBucket` + `MicrostructureFeatureComputer`.
- Reemplaza definitivamente el rol de siembra que tenía `BinanceAggTradeBackfiller`.

### Alternativas consideradas
- **klines REST en el arranque (parche rápido):** 2 weight por request, sin riesgo de ban. Descartada porque el campo `number_of_trades` cuenta trades individuales (no aggTrades) → sesgo sistemático en `mean_trade_size` → percentil de `TradeSizeInstitutional` distorsionado por ~24h tras cada arranque. Aceptable para CvdSellExhaustion, inaceptable para TradeSizeInstitutional.
- **aggTrades REST con rate limiter mejorado:** solucionaría el ban, pero no el conflicto de recursos con el brokerage durante `Initialize()`. Tampoco resuelve el acoplamiento data/execution que es la causa raíz.

### Consecuencias
- Un reinicio de Lean no genera gap de features: el Recorder nunca paró.
- El endpoint REST `/fapi/v1/aggTrades` ya no se usa en ningún camino de código de producción.
- Nuevo proceso en el VPS (systemd unit o Windows Service). Monitoreo requerido (ver POLICY.md).
- `DEUDA-3` (rate limiter global para el backfiller) queda cerrada: ya no aplica.
- Storage: kilobytes/activo incluso con timeframes sub-horarios (5m = 288 filas/día × 100 bytes = 28 KB/día/activo).
- Alta de activos nuevos: siembra offline una vez con `BinanceVisionSeeder` (desde `data.binance.vision`) + habilitar al Recorder → Lean arranca con warmup completo.

---

## ADR-047 — Warmup autosuficiente: aggTrades REST backfill + persistencia rolling 7d
**Fecha:** 2026-06-17
**Estado:** ~~Aceptada~~ **Supersedida por ADR-048 (2026-06-18)**
**ADRs relacionados:** ADR-046 (pipeline live aggTrades)

### Contexto
Con ADR-046, el warmup de estrategias microestructurales en vivo dependía de un CSV histórico estático (`microstructure/{ticker}_1h_features.csv`). Ese CSV envejece: si el gap entre la última fila del CSV y el momento de reinicio supera las 50h (WarmUpBars de `CvdSellExhaustion`), `GetBar()` retorna `null` para las barras de warmup → las estrategias no completan su warmup → no señalizan.

Operativamente: actualizar los CSVs antes de cada reinicio requiere correr `download_aggtrades.py` manualmente (~5 min), subir ~30 MB al VPS, y no escala a un sistema desatendido.

Problema adicional con la alternativa de klines REST (`/fapi/v1/klines`): el campo "Number of trades" (índice 8) cuenta trades **individuales**, no aggTrades. El script Python de referencia usa conteo de **aggTrades** para `arrival_rate` y `mean_trade_size`. El denominador distinto introduce un sesgo sistemático en el percentil de `TradeSizeInstitutionalStrategy` — la estrategia con mejor OOS Sharpe (4.186).

### Decisión

Eliminar la dependencia del CSV estático para el warmup mediante dos componentes:

**1. `PersistentMicrostructureStore` (Trading.Application):**
Persiste cada barra 1h computada en vivo en `{AppDir}/microstructure-live/{ticker}_live.csv`. Append-only en operación; reescritura solo al hacer trim. Rolling window de 7 días (168 barras × 3 símbolos). Tamaño permanente: ~25 KB total.

**2. `BinanceAggTradeBackfiller` (Trading.Strategies):**
En `Initialize()` (solo LiveMode), calcula el gap = [última barra en disco, hora actual) y descarga aggTrades vía `/fapi/v1/aggTrades` de Binance Futures. Para cada ventana 1h: pagina con `fromId` si hay >1000 aggTrades, acumula en `AggTradeBucket`, computa con `MicrostructureFeatureComputer`. Paridad exacta con el pipeline en vivo: mismo denominador aggTrade (no individual trades). Tolerante a fallos de API: errores HTTP no crashean `Initialize()`.

**Secuencia en Initialize() (LiveMode):**
1. Seed CVD desde último bar del CSV histórico (respaldo si disco vacío).
2. Cargar últimas 72h desde disco → `_live` dict + actualiza `_cvdRunning`.
3. Backfill gap REST → `_live` + append a disco.
4. Trim disco >7d.
5. `SetWarmUp()` → warmup cubre el rango pedido desde `_live`.

**Primer arranque:** backfill de 52h (~3 requests/símbolo para horas quietas, más para BTC peak). Startup de ~1-5 minutos (aceptable; ocurre una sola vez).
**Reinicios subsecuentes:** gap típico de horas → segundos de startup.

### Alternativas consideradas
- **klines REST (`/fapi/v1/klines`):** 1 request por símbolo, startup en <1s. Descartada por sesgo sistemático en `arrival_rate`/`mean_trade_size` (individual trades vs aggTrades). Aceptable para `CvdSellExhaustion`, inaceptable para `TradeSizeInstitutional`.
- **Actualizar CSV manualmente pre-reinicio:** 5min + 30MB transfer. No escala; dependencia operativa manual.
- **Esperar 50h de WebSocket en vivo:** inaceptable — el sistema no señaliza por dos días tras cada reinicio.
- **Persistir aggTrades crudos:** storage elevado (GBs/año). Innecesario: solo los features computados (8 doubles/barra) tienen valor permanente.

### Consecuencias
- El sistema es autosuficiente para warmup en cualquier reinicio, sin gestión manual de archivos.
- Los CSVs históricos (`microstructure/*.csv`) siguen cargándose como fallback adicional durante el primer arranque sin disco, pero ya no son críticos en operación continua.
- Storage en VPS: máximo 25 KB permanente (vs 30 MB CSV × 3).
- Startup penalizado solo en el primer arranque o tras gaps largos (>7d sin correr).

---

## ADR-046 — Pipeline de features microestructurales en vivo (HITO-D-feat)
**Fecha:** 2026-06-17
**Estado:** Aceptada

### Contexto
Las estrategias aprobadas en Hito G (`CvdSellExhaustionStrategy`, `TradeSizeInstitutionalStrategy`) consumen features de microestructura derivadas de aggTrades de Binance (OFI, CVD, arrival rate, mean trade size, buy/sell ratio, price return). En backtest e IS/OOS, estas features venían de un CSV histórico estático cargado por `MicrostructureRegistry.Load()` en `Initialize()`. En vivo, la barra 1h actual nunca está en el CSV → `GetBar()` devuelve `null` → las estrategias retornan `Flat` → el sistema no tradea.

El problema raíz: `is_buyer_maker` (campo `"m"` del WebSocket aggTrade de Binance) es necesario para clasificar buy vs sell volume. El código original de Lean Binance (`Trade.cs`, `EmitTradeTick`) lo descartaba silenciosamente.

### Decisión

**1. Lean mínimo (3 archivos, ~10 líneas):** capturar `is_buyer_maker` del WebSocket.
- `Messages/Trade.cs`: añadir `[JsonProperty("m")] public bool IsBuyerMaker`.
- `BinanceBrokerage.Messaging.cs → EmitTradeTick`: recibir `isBuyerMaker` y codificarlo en `Tick.SaleCondition` como `"BUY"` o `"SELL"` (campo string existente, vacío para crypto).

**2. `AggTradeBucket` (Trading.Application):** acumula aggTrades de una barra 1h (price, qty, is_buyer_maker). Lean-free — solo tipos primitivos.

**3. `MicrostructureFeatureComputer` (Trading.Application, static):** replica exacta de `_agg_1h()` de `download_aggtrades.py`. Garantiza paridad matemática con el CSV offline.

**4. `LiveMicrostructureProvider` (Trading.Application):** implementa `IMicrostructureProvider`. Combina: (a) cómputo en vivo via `ComputeAndAdd()`, (b) fallback al `MicrostructureRegistry` CSV para warmup y barras históricas. CVD acumulativo seeded desde el último valor del CSV (`GetLastCvd()`).

**5. `LiveAggTradeAccumulator` (Trading.Strategies):** adaptador Lean-aware. Recibe `Tick` de QC, lee `SaleCondition` para el lado, y acumula por bucket 1h.

**6. Wiring en `TradingAlgorithmHost`:**
- Live: `Resolution.Tick` en `AddCryptoFuture` + `TickConsolidator(timeframeSpan)` (produce `TradeBar` desde ticks). Backtest: `Resolution.Minute` + `TradeBarConsolidator` sin cambios.
- `OnData()`: routing de ticks al acumulador (solo live, post-warmup).
- `DataConsolidated` handler: `TakeBucket()` → `ComputeAndAdd()` ANTES de `ProcessBar()`, garantizando que `GetBar()` ya tiene el dato cuando la estrategia evalúa señal.

### Alternativas consideradas
- **A: REST API polling al cierre de cada barra.** HTTP en camino crítico de señal. Descartada por latencia y dependencia externa en runtime.
- **B: WebSocket propio independiente de QC.** Mayor control pero lifecycle propio (reconnect, heartbeat). Descartada por complejidad operativa.
- **C: Actualización diaria del CSV (lag 24h).** Pierde un día entero de señales. Inaceptable para estrategias 1h.
- **D (elegida): Lean mínimo + TickConsolidator.** Captura el dato en la fuente (WebSocket), mínima modificación a Lean, sin dependencias externas adicionales. La información siempre estuvo disponible en el WebSocket; solo se descartaba.

### Consecuencias
- Las estrategias microestructurales señalizan en vivo por primera vez.
- Paridad live/backtest garantizada por `MicrostructureFeatureComputerParityTests` (8 casos que validan las fórmulas contra los cálculos esperados del Python).
- Primera barra post-warmup puede ser parcial (ticks desde que termina el warmup, no desde el inicio de la hora). Las estrategias están en su propio warmup (`WarmUpBars`) en ese período → retornan Flat de todos modos.
- `SaleCondition` de `Tick` se reutiliza como carrier para el side — no es su semántica original en equity, pero es el campo más cercano disponible sin modificar el schema de `Tick` de Lean.

---

## ADR-045 — minimal-position-mode + override de min notional en el adapter
**Fecha:** 2026-06-15
**Estado:** Aceptada
**ADRs relacionados:** ADR-044 (SL/TP reduceOnly), ADR-001 (dominio Lean-free), ADR-002 (RiskPerTradePercentage)

### Contexto

Hito D — item #2: para el shakedown live se quiere que las estrategias abran posiciones del
**tamaño mínimo admitido por Binance**, no las ~$1.130/posición que da el risk% del 1% sobre el
balance actual. Así se ejercita el ciclo real (entrada → SL/TP → time-exit → reconexiones) con
capital despreciable.

Al implementarlo se encontró un gap de datos: el SPDB (`symbol-properties-database.csv`) **no
tiene `minimum_order_size`** para los símbolos `cryptofuture` de Binance (BTCUSDT, ETHUSDT,
SOLUSDT) — las filas terminan en el `market_ticker`. Por eso `GetMinimumNotional` devolvía `null`
y `PositionSizer.ValidateNotional` caía a un floor defensivo de $5 para todos. El min notional
real de BTCUSDT-PERP es $100 y el de ETHUSDT-PERP $20, así que el sizing mínimo contra el floor de
$5 habría sido rechazado por el exchange para BTC/ETH.

### Decisión

1. **Override de min notional en `LeanInstrumentMetadataAdapter`** (capa adapter/infra): un mapa
   versionado `{ BTCUSDT=100, ETHUSDT=20, SOLUSDT=5 }`. `GetMinimumNotional` devuelve el valor del
   SPDB si existe y cae al override si es `null`. El adapter es Lean-aware por definición, así que
   conocer params de exchange ahí no viola el layering (Application/Domain siguen Lean-free).
2. **minimal-position-mode**: flag de config (`"minimal-position-mode": true`, solo live, default
   false). Cuando está activo, `PositionSizer.CalculateQuantity` fija el tamaño en el min notional
   del símbolo (ceil al lot size; suma un lote si el ceil cae exactamente en el mínimo, porque
   `ValidateNotional` usa desigualdad estricta) en vez de calcular por risk%.

El resto del sistema (SL/TP, time-exit, monitores de riesgo) opera igual, a escala mínima.

### Alternativas descartadas

- **Poblar el SPDB (`Data/symbol-properties/...csv`)**: era el lugar "natural" del dato, pero
  `Data/` está **gitignored** — no se versiona ni se deploya por git, así que una edición local no
  llegaría al VPS, generando inconsistencia local/VPS. El override en el adapter es versionado y se
  deploya con el build. (Trade-off: `BinanceBrokerageModel.CanSubmitOrder` sigue leyendo el SPDB
  directo y mantiene su floor permisivo, pero es inofensivo: nuestro `PositionSizer` dimensiona
  correctamente sobre el mínimo y **Binance enforcea su min notional server-side** de todos modos.)
- **Hardcodear los min notionals en Application o config.json**: viola el layering — Application
  debe ser Lean-free y no conocer params de exchange. El adapter (infra) es el lugar correcto.
- **Cuenta diminuta para forzar tamaños chicos vía risk%**: el sizing por risk% podría caer bajo el
  min notional de forma inconsistente por símbolo (rechazos), y no da un tamaño mínimo controlado.

### Consecuencias

- El override vive en el código (versionado, deployable). Si se agregan símbolos nuevos al modo
  mínimo, sumar su min notional al mapa del adapter.
- `minimal-position-mode` es un modo de shakedown; **desactivar para operar con sizing real**.
- `CanSubmitOrder` no enforcea el min notional (lee el SPDB, que sigue null); aceptable porque
  el sizer dimensiona bien y Binance rechaza server-side cualquier orden bajo el mínimo.

---

## ADR-044 — Órdenes protectivas SL/TP con reduceOnly en Binance Futures (divergencia del fork)
**Fecha:** 2026-06-15
**Estado:** Aceptada — validada en test supervisado live (2026-06-15)
**ADRs relacionados:** ADR-041 (Hito D-prev), ADR-015 (IRiskMonitor/IRiskAction), ADR-001 (dominio Lean-free)

### Contexto

Requisito operativo inquebrantable para Hito D (live con capital real): **las órdenes de stop
loss y take profit deben colocarse como órdenes nativas en Binance al abrir cualquier posición**,
de modo que sobrevivan una caída del proceso o de la red — la protección vive en el exchange, no
en Lean.

Verificación del brokerage de QC (`BinanceFuturesBrokerage`):
- **Cumple lo nativo**: el SL se manda como `STOP_MARKET` (endpoint condicional/algo, `triggerPrice`,
  `algoType=CONDITIONAL`) y el TP como `LIMIT`. Ambas descansan en el exchange. ✓
- **Gap de seguridad**: `CreateOrderBody` NO setea `reduceOnly` ni `closePosition`, y
  `BinanceOrderProperties` solo exponía `PostOnly`. Con Lean conectado, el `OrderLifecycleService`
  cancela la pierna opuesta al fillear una (OCO del lado cliente). Pero si Lean se **desconecta**
  con posición abierta y salta el SL → cierra la posición; el TP queda resting en el exchange → si
  el precio lo toca, **abre una posición opuesta no deseada y desprotegida**. Exactamente lo que el
  requisito busca prevenir.

### Decisión

Setear `reduceOnly=true` en las órdenes protectivas SL/TP. Como QC no lo expone, se modifica el
fork de Lean de forma contenida y property-driven:

1. `Common/Orders/BinanceOrderProperties.cs`: nueva propiedad `bool ReduceOnly`.
2. `BinanceFuturesRestApiClient.CreateOrderBody`: si la propiedad lo pide, agrega `reduceOnly=true`
   al body (aplica tanto al camino condicional STOP_MARKET como al LIMIT estándar).
3. `LeanOrderRouter` (adapter nuestro): pasa `BinanceOrderProperties { ReduceOnly = true }` en
   `SubmitStopMarketOrder` y `SubmitLimitOrder` — solo protectivas. El `MarketOrder` de entrada
   va SIN reduceOnly (con posición flat, Binance rechazaría un reduceOnly).

Con esto, si una pierna ya cerró la posición durante una desconexión, la otra al dispararse no
puede abrir nada: Binance la rechaza por no haber posición que reducir. Riesgo de volteo eliminado.

### Alternativas descartadas

- **`closePosition=true`** (OCO nativo: Binance auto-cancela la otra pierna al cerrar): descartado
  porque obliga a que el TP sea `TAKE_PROFIT_MARKET` en vez de `LIMIT` → ejecución a mercado al
  trigger, perdiendo control de precio de salida y el posible maker fee. reduceOnly preserva el TP
  como límite. Queda como opción futura si se quiere OCO nativo del exchange.
- **OCO solo del lado cliente** (el comportamiento previo): no sobrevive una desconexión, que es
  justamente el requisito.

### Consecuencias

- **Divergencia del fork**: `Common/Orders/BinanceOrderProperties.cs` y `BinanceFuturesRestApiClient.cs`
  divergen de upstream Lean. Anotar en futuros merges de upstream.
- **Validación**: este fork no tiene proyecto de tests del brokerage Binance, y la aceptación de
  `reduceOnly` por el endpoint condicional/algo solo se confirma contra el exchange real. Gate de
  validación = test supervisado live de Hito D.

### Resultado de validación (2026-06-15)

Test supervisado ejecutado con el hook temporal `sltp-validation-mode`: entrada Long mínima
(~0.08 SOL, ~$6) por el camino real del executor. Confirmado en Binance USDT-M:
- **TP**: orden LIMIT sell @ 82.48 (+10%), **Reduce-Only = Sí**.
- **SL**: orden STOP_MARKET sell, trigger ≤ 71.23 (−5%), **Reduce-Only = Sí**.
- El endpoint condicional/algo **aceptó `reduceOnly`** en la SL (salió válida, no Invalid) — era
  el único punto no verificable offline.
- Escenario de desconexión: proceso terminado con posición abierta, cierre manual, órdenes
  sobrantes reduceOnly incapaces de voltear la posición.

Hook `sltp-validation-mode` removido tras la validación. ADR validado.

---

## ADR-043 — Clock drift Binance -1021: guard NTP externo como pre-flight, no recvWindow
**Fecha:** 2026-06-14
**Estado:** Aceptada
**ADRs relacionados:** ADR-031 (Hito C: race condition Binance, LeanClock UTC), ADR-041 (Hito D-prev)

### Contexto

Durante la validación de conectividad (Hito D-prev), el proceso live abortó en la
inicialización con `{"code":-1021,"msg":"Timestamp for this request was 1000ms ahead of
the server's time."}`. El reloj de la máquina de trading estaba ~2s adelantado respecto al
servidor de Binance USDT-M. Binance rechaza todo request firmado cuyo timestamp esté más de
1000ms adelantado del server. El drift de ~2s en pocos días indica que el sync nativo de
Windows no corre con la frecuencia suficiente para esta tolerancia.

### Decisión

El clock drift se trata como un problema **operativo/de infraestructura**, no de código del
motor. Se agregan dos scripts en `Trading.Research/broker_validation/`:

- `Sync-TradingClock.ps1`: mide el offset local−Binance (compensando RTT) contra
  `fapi/v1/time` y, si supera 500ms (mitad de la tolerancia de Binance), corrige el reloj.
  Primero intenta `w32tm /resync` (NTP); si tras el resync el offset sigue fuera de tolerancia
  —típico cuando la red bloquea UDP 123— hace **fallback seteando el reloj del SO directamente
  al server time de Binance** vía `Set-Date` (HTTPS, que sí está abierto). Sirve como
  **pre-flight** (`-CheckOnly`, sin admin) y como worker de la tarea de fondo.
- `Install-TradingClockSync.ps1`: configura w32time (peers NTP, polling frecuente) y registra
  una tarea programada que corre el worker como SYSTEM al inicio y cada 60 min.

Runbook operativo en POLICY 5.5.

### Alternativas descartadas

- **Aumentar `recvWindow` en el brokerage**: NO resuelve el problema. La regla de Binance es
  `timestamp < serverTime + 1000 && serverTime - timestamp <= recvWindow`. `recvWindow` solo
  extiende la tolerancia hacia timestamps **atrasados**; un reloj **adelantado** viola la
  primera condición sin importar `recvWindow`. Además implicaría tocar el adapter de QC (fork).
- **Corregir el timestamp en el adapter** (restar el offset medido): mete lógica de
  compensación de reloj en la ruta de firma de requests — frágil y acoplado al brokerage de
  QC. Mantener el reloj del SO correcto es la responsabilidad correcta y reutilizable. El
  fallback de set directo alinea el **reloj del SO** (no el timestamp del adapter) al server
  time de Binance, que es la misma referencia contra la que el exchange valida.
- **Depender solo de NTP (`w32tm /resync`)**: insuficiente cuando la red bloquea UDP 123 — se
  observó que el resync no corregía el drift de ~1.8s. Por eso el guard cae a set directo por
  HTTPS, que usa el mismo canal (443) ya abierto para tradear.
- **Dejarlo como paso manual del operador**: el drift es recurrente; un resync manual olvidado
  vuelve a frenar la corrida. La tarea desatendida elimina la clase de fallo.

### Consecuencias

- El arranque del sistema live depende de que el reloj esté sincronizado; el guard lo verifica.
- Los scripts son infra operativa (no entran al build ni a los tests del motor).
- POLICY 5.5 documenta el síntoma, el diagnóstico y la prevención.

---

## ADR-042 — Dead-man's switch: liveness del feed en vez de cierre de barras de estrategia
**Fecha:** 2026-06-14
**Estado:** Aceptada
**ADRs relacionados:** ADR-021 (Monitoreo básico: heartbeat + Healthchecks.io), ADR-031 (Hito C: LeanClock UTC), ADR-032 (warm-up dinámico)

### Contexto

Durante la validación de conectividad del Hito D-prev (paso 3), el proceso live terminaba
solo con `Environment.Exit(1)` poco después del warmup, sin error de Lean. Se diagnosticaron
dos causas encadenadas en el auto-restart timer (dead-man's switch del feed):

1. **Desfase de reloj post-warmup.** El timer comparaba `DateTime.UtcNow` (wall clock) contra
   `LastBarProcessedUtc`, que se poblaba con `_clock.UtcNow` (= `_algorithm.UtcTime`, tiempo de
   algoritmo). Durante el warmup las barras se procesan con timestamps históricos, así que al
   terminar el warmup la marca quedaba horas en el pasado → staleness aparente >> umbral → Exit(1)
   a los ~60s. Se mitigó con `MarkLiveModeStart(DateTime.UtcNow)` en `OnWarmupFinished()`.

2. **Señal equivocada.** Tras la mitigación, el proceso moría a los ~20 min exactos. El umbral
   de auto-restart (20 min) usaba el cierre de **barras de estrategia** como proxy de "el feed
   está vivo". Pero todas las estrategias activas (H3/H5) operan en **1h**: una barra consolidada
   cierra una vez por hora, así que pasan hasta ~60 min de silencio legítimo entre `BarProcessedEvent`.
   El umbral de 20 min era estructuralmente más corto que la cadencia natural de la estrategia.
   El comentario original ("supera el período 15m, barra más frecuente") delataba que el umbral
   se calibró para un timeframe de 15m que ya no existe en la config.

El propósito real del dead-man's switch (per ADR-021) es detectar que la re-suscripción al
WebSocket falló en silencio tras una reconexión. Eso es **liveness del feed**, no cadencia de
estrategia: son dos conceptos distintos que el diseño previo conflacionaba.

### Decisión

El auto-restart mide **liveness del feed** vía los datos de minuto crudos, no el cierre de
barras de estrategia:

- `HealthHeartbeatTracker` expone `MarkDataFeedAlive(DateTime wallClockNow)`, llamado desde
  `OnData` en cada slice live (cadencia ~1min, resolución Minute del feed). Mantiene
  `_lastDataReceivedUtc` separado de `_lastBarProcessedUtc`.
- El timer compara `DateTime.UtcNow - LastDataReceivedUtc` contra un umbral de **5 min**
  (tolera ~5 barras de minuto perdidas). Detecta un socket congelado en ~2 min, independiente
  del timeframe de las estrategias (1h, 4h, etc.).
- `_lastBarProcessedUtc` se mantiene para el **ping gate** de Healthchecks.io (umbral 90 min,
  ADR-021), que sí mide procesamiento de barras de estrategia y tolera la cadencia de 1h.
- `MarkLiveModeStart` re-baselina **ambas** marcas a wall clock en `OnWarmupFinished()`, para
  que ni el auto-restart ni el ping gate vean los timestamps históricos del warmup.

`OnData` y `TradingAlgorithmHost` son el adaptador autorizado a usar `DateTime.UtcNow` crudo
(ADR-021/ADR-031): la regla del IClock simulado aplica a `Trading.Application`, no al host.

### Alternativas descartadas

- **Subir el umbral de auto-restart a 90 min** (igual al ping gate): one-liner, mantiene el
  proxy de barras de estrategia. Descartado: detectaría un feed muerto recién en 90-150 min,
  demasiado lento para trading live, y volvería a romperse si se agrega una estrategia de 4h.
- **Publicar `BarProcessedEvent` en cada barra de minuto** (no solo en la consolidada): mezclaría
  la semántica del evento (el watchdog de barras stale y el ping gate dependen de que represente
  procesamiento de estrategia). Una marca de feed-liveness separada es más limpia.

### Consecuencias

- `HealthSnapshot` gana `LastDataReceivedUtc` y `DataFeedStalenessSeconds`, visibles en
  `heartbeat.json` para la revisión diaria del operador (POLICY 2.4).
- El auto-restart ya no es sensible al timeframe de las estrategias activas.
- Tests nuevos en `HealthHeartbeatTrackerTests` cubren `MarkDataFeedAlive` y `MarkLiveModeStart`.

---

## ADR-041 — Hito D-prev: protocolo de validación broker real Binance USDT-M
**Fecha:** 2026-06-12 (ejecutado y cerrado 2026-06-15)
**Estado:** Ejecutada — Hito D-prev completado
**ADRs relacionados:** ADR-031 (Hito C: paper trading), ADR-030 (Bypass ValidateSubscription Binance), ADR-042 (dead-man's switch feed-liveness), ADR-043 (clock guard -1021)

### Contexto

Hito C validó que el sistema opera correctamente en `live-paper` (fills ficticios, datos reales).
Antes de promover las estrategias aprobadas en Hito G a paper trading real (Hito D), es
necesario verificar que el broker real Binance USDT-M acepta y procesa órdenes enviadas desde Lean.
El riesgo a validar: que el adapter `BinanceFuturesBrokerage` + `BrokerageTransactionHandler`
funcionen correctamente bajo las condiciones reales del exchange (comisiones, tamaño mínimo,
notional mínimo, reconciliación de balance).

La validación se hace con capital mínimo para no exponer equity real significativo durante el
proceso de verificación.

### Decisión

Se adoptó el siguiente protocolo de validación en cinco pasos:

**Paso 1 — API keys con permiso de trading**
Crear API keys en Binance Futures USDT-M con permisos: lectura + futures trading (sin retiro).
Restringir por IP de la máquina de trading. Reemplazar las keys de solo-lectura en `config.json`.

**Paso 2 — Cuenta con capital mínimo (~50 USDT)**
Depositar USDT suficiente para cubrir el notional mínimo del test y las comisiones:
1 SOL × precio ~$170 = ~$170 notional. Con apalancamiento 1× en cuenta pequeña, depositar
mínimo 50 USDT. Capital en riesgo durante D-prev: valor de 1 SOL (~$170, sin apalancamiento).

**Paso 3 — Conectividad sin órdenes**
Cambiar `"environment"` a `"live-futures-binance"` en `config.json`. Arrancar sin
`BROKER_VALIDATION_MODE`. Verificar en los logs:
  - Balance USDT carga correctamente desde Binance
  - SOLUSDT, BTCUSDT, ETHUSDT se suscriben sin error
  - Heartbeat.json muestra estado alive

**Paso 4 — Orden de prueba: 1 SOL**
Setear `BROKER_VALIDATION_MODE=1` y reiniciar. Al completar el warmup, `OnWarmupFinished()`
coloca una orden de mercado de 1 SOL en SOLUSDT. Criterios de verificación:
  - `OrderEvent.Filled` se registra en el JSONL
  - El tag `broker-validation-d-prev` aparece en `OrderRegistry`
  - El balance USDT en heartbeat.json disminuye ~precio de 1 SOL
  - La posición se cierra vía SL/TP (MaxBars=8) o manualmente

**Paso 5 — Reconciliación de balance**
Ejecutar `Trading.Research/broker_validation/reconcile.ps1`. Criterio de aceptación:
discrepancia entre balance Lean y balance Binance <= 0.5%.

### Funding fees (gap conocido)

Los contratos perpetuos Binance USDT-M cobran funding cada 8 horas (~0.01% típico).
Este costo NO está modelado en el P&L de Lean (no existe en `BinanceFeeModel` para perpetuos).
Para las estrategias aprobadas con hold=6-8 barras en 1h, la exposición máxima es ~8h,
implicando funding de ~0.01% por posición. Este monto es inferior a la comisión taker (0.04%)
y no altera materialmente la expectancy de H5 (OOS Sharpe=4.186) ni H3 (OOS Sharpe=1.718).

**Decisión**: los funding fees se rastrean externamente (estado de cuenta Binance mensual)
pero no se modelan en el motor. Se revisará si el haircut real backtest→live supera el 50%
esperado (POLICY P3), en cuyo caso se considerará incorporarlos al modelo.

### Comisiones

Binance Futures taker: 0.04% por lado (0.08% round-trip), sin descuento BNB.
El modelo actual usa `ConstantFeeModel(0.001 USDT)` en backtest (fee fija negligible).
En live, las comisiones reales las aplica el exchange directamente sobre el USDT balance.
La reconciliación `reconcile.ps1` captura esta diferencia.

### Alternativas descartadas

- **Testnet de Binance Futures**: descartado porque la calidad de datos del testnet es pobre
  (fills artificiales, book thin), lo que invalida la validación de slippage y fills reales.
- **Orden de BTCUSDT**: 1 BTC tiene notional ~$60k, 0.001 BTC tiene notional ~$60 pero ese
  es el lot size mínimo. Con capital ~50 USDT es marginal. SOLUSDT permite 1 SOL (~$170)
  con más cómodo capital de prueba.

### Consecuencias

- `config.json` tiene el environment `live-futures-binance` listo para activar.
- `Trading.Research/broker_validation/reconcile.ps1` ejecuta la reconciliación de balance.

### Resultado de ejecución (2026-06-15)

El protocolo se ejecutó completo. La orden de prueba se colocó con `broker-validation-mode: true`
(0.09 SOL, notional ~$6.65) y **filleó correctamente**: `FillPrice 73.87, OrderFee 0.00332415 USDT`,
BrokerId 220782487914. La posición se cerró manualmente; balance reconciliado en 5661.48 USDT
(discrepancia << 0.5%). Tras validar, se removió el hook `PlaceBrokerValidationOrderIfRequested()`
y su llamada en `OnWarmupFinished()`; las estrategias 7.4/7.5 pasaron a `paper` en POLICY.

Durante la ejecución se encontraron y resolvieron tres bloqueos de configuración de cuenta/entorno,
ninguno de código de trading:

1. **`-1021` (timestamp adelantado)**: drift de reloj de la máquina > 1000ms. Resuelto con el guard
   NTP + fallback a server time de Binance (ADR-043).
2. **`-2015` (Invalid API-key/IP/permissions)**: la API key tenía lectura pero no trading de futuros.
   Resuelto habilitando "Enable Futures" en la key.
3. **`-4061` (position side does not match)**: la cuenta estaba en Hedge Mode; el brokerage de QC
   solo opera One-way Mode (sin referencias a `positionSide`/`dualSidePosition` en su código).
   Resuelto cambiando la cuenta a One-way Mode.

También se observó el flapping del WebSocket auxiliar `/public/ws` (documentado como limitación de
entorno en POLICY 2.4): no afecta trades ni órdenes; se resuelve definitivamente migrando a VPS.

---

## ADR-040 — Pipeline de validación de estrategias: M4 → QC IS → QC OOS → Trading.Analytics
**Fecha:** 2026-06-12
**Estado:** Aceptada
**ADRs relacionados:** ADR-037 (IMicrostructureProvider), ADR-038 (OFI IS design), ADR-039 (IS/OOS + Monte Carlo)

### Contexto

Hito E batch 2 (sesión 2026-06-11) evaluó 10 hipótesis de microestructura a través del pipeline completo: M4 Python → QC IS 2021-2024 → QC OOS 2025-2026 → Trading.Analytics. Resultado: 2 estrategias aprobadas (`TradeSizeInstitutionalStrategy`, `CvdSellExhaustionStrategy`), 3 rechazadas en QC IS con `git rm`, 5 rechazadas en M4 sin implementar. El pipeline existía como práctica implícita derivada de ADR-037/038/039 pero no estaba codificado como proceso explícito y reproducible.

Este ADR consolida el pipeline como estándar del proyecto para toda nueva hipótesis de trading. El checklist operativo de referencia rápida vive en `AI.md` sección "Pipeline de Research e Incorporación de Estrategias".

### Decisiones

#### D1 — Pipeline de 4 fases con criterio de rechazo explícito en cada fase

El pipeline es **lineal con early exit**: si una fase falla, la hipótesis se descarta. No se salta fases ni se adelanta la implementación en C# antes de validar M4.

---

**Fase 0 — M4 (validación rápida en Python)**

Script en `Trading.Research/m4_*.py`. No requiere modificar código C#.

- Período IS: 2021-01-01 → 2024-12-31. Activos: BTC, ETH, SOL. Comisión: 0.04% round-trip.
- **Gate M4:** Sharpe ≥ 0.5 en al menos 2 de los 3 activos.
- **Regla crítica (bloqueante):** el script DEBE trackear estado de posición abierta y evitar señales solapadas. Sin este tracking, múltiples señales no cerradas se acumulan en el mismo período e inflan el Sharpe artificialmente — el artefacto puede llevar una hipótesis débil de Sharpe 0.2 real a Sharpe 0.8 aparente. Solo registrar nueva señal si no hay posición abierta del mismo tipo.
- Si falla: documentar en `Trading.Research/strategy_experiments.md` con diagnóstico. **No implementar `IStrategy`.**

---

**Fase 1 — Implementación `IStrategy`**

- Crear clase que implementa `IStrategy` (o su variante con `IMicrostructureProvider` para estrategias microestructurales).
- Declarar `WarmUpBars` = período del indicador más lento (ADR-032).
- Registrar en `StrategyFactory.Create`.
- Escribir en el **mismo commit** (AI.md sección Testing):
  - Test de referencia del indicador (si no existe para ese indicador en otra estrategia).
  - Test de comportamiento con barras sintéticas donde la señal se cumple deliberadamente en una barra conocida.
- Commitear la implementación sin resultados de backtest.

---

**Fase 2 — QC IS (In-Sample 2021-2024)**

`strategies.json`: BTC/ETH/SOL 1h (u otro timeframe de la hipótesis), SL/TP según la estrategia (referencia batch microestructura: SL=5%, TP=10%, MaxBars=6), Risk=1%.

Configuración de monitors:
- `StrategyHealthMonitor`: **NullStrategyHealthMonitor** — mide el edge de la señal pura sin que OPS-2 interrumpa el IS por degradación temporal. El IS no es operación real; queremos saber si la señal tiene edge, no si el monitor la habría matado.
- `ConsecutiveLossesMonitor`: **activo** — es el guard de riesgo del portfolio, no del edge de la señal.

Período: `SetStartDate(2021, 1, 1); SetEndDate(2024, 12, 31);`

**Procedimiento operativo obligatorio antes de cada run** (aprendizaje de sesión 2026-06-11):
1. `dotnet build Trading.Strategies/Trading.Strategies.csproj`
2. Copiar `Trading.Strategies/strategies.json` a `Launcher/bin/Debug/strategies.json`
3. Verificar que **no existe** `Launcher/bin/Debug/net10.0/Trading.Strategies.dll`. El Lean Loader busca DLLs en subdirectorios antes que en el directorio base — si existe una versión vieja en `net10.0/`, se carga esa en lugar del DLL recién compilado. El síntoma es silencioso: el backtest corre con código viejo sin error visible.

**Gate QC IS (M1):** Sharpe del portfolio combinado ≥ 0.5.

Si falla: `git rm` de la clase IStrategy y sus tests. Documentar en `Trading.Research/strategy_experiments.md`. El historial de git preserva el código si se necesita recuperar.

---

**Fase 3 — QC OOS (Out-of-Sample 2025-presente)**

Mismo `strategies.json` que Fase 2. Solo cambia el período:
`SetStartDate(2025, 1, 1); SetEndDate(año, mes, día);` — fecha lo más cercana al día de evaluación.

Mismo procedimiento operativo que Fase 2 (compilar, copiar JSON, verificar net10.0/).

Exportar `transaction-log.csv` con nombre explícito que incluya el período:
`{hipótesis}-{estrategia}-oos-{año_ini}-{año_fin}.csv`

No hay gate numérico en esta fase — el CSV alimenta Fase 4.

---

**Fase 4 — Trading.Analytics (Gate 1 + Gate 2)**

```
dotnet run --project Trading.Analytics -- --is-log <csv-is> --oos-log <csv-oos> --strategy <nombre> --output <directorio>
```

Gate 1 (métricas OOS deterministas — todos deben pasar):
- Trades ≥ 50 | Net profit > 0 | Sharpe ≥ 0.3 | Profit Factor ≥ 1.1 | Expectancy > 0

Gate 2 (Monte Carlo block bootstrap 10k — todos deben pasar):
- P(Sharpe < 0) ≤ 20% | Mediana Max DD ≤ 55% | P5 CAGR > −5%

Si falla: `git rm`. Documentar en `Trading.Research/strategy_experiments.md`.

**Si APROBADA:**
- Mantener la clase en el repo.
- Agregar entrada en `POLICY.md` sección 7 (estado pre-paper, umbrales U1-U4 de POLICY 3.1).
- Agregar entrada en `DECISIONS.md` justificando la activación cuando se active en paper/live.
- Actualizar `ROADMAP.md` con las métricas IS/OOS aprobadas.
- Commit: `feat(hito-G): <Nombre>Strategy APROBADA IS=X.XX / OOS=X.XX`.

#### D2 — NullStrategyHealthMonitor en IS/OOS, monitor real en producción

| Contexto | Monitor de salud | Razón |
|---|---|---|
| QC IS (investigación) | `NullStrategyHealthMonitor` | Medir edge puro sin interrupciones de OPS-2 |
| QC OOS (investigación) | `NullStrategyHealthMonitor` | Consistencia con IS; el OOS mide el edge, no el monitor |
| Paper trading | `StrategyHealthMonitor` real | Producción: OPS-2 activo |
| Live trading | `StrategyHealthMonitor` real | Producción: OPS-2 activo |

#### D3 — Nomenclatura de archivos de resultados

```
F:\Lean\data\results\backtest-logs\{hipótesis}-{estrategia}-is-{año_ini}-{año_fin}.csv
F:\Lean\data\results\backtest-logs\{hipótesis}-{estrategia}-oos-{año_ini}-{año_fin}.csv
F:\Lean\data\results\analytics\validation-{estrategia}-{YYYYMMDD}.md
```

### Alternativas consideradas

**A — Sin Fase 0 (implementar IStrategy directo, validar solo en QC):** descartado. La Fase 0 en Python es 10× más rápida que un backtest QC completo. En el batch de 10 hipótesis, 5 se eliminaron en M4 sin generar código C#. Saltar M4 habría multiplicado el tiempo de desarrollo por ~2× sin beneficio.

**B — Sin OOS separado (IS como única validación):** descartado. ADR-039 documenta el racional completo. El IS/OOS split único + Monte Carlo es la mínima protección institucional contra overfitting temporal. Sin él, las métricas IS no tienen valor predictivo.

**C — Gates variables por estrategia (Sharpe IS ≥ 0.3 en lugar de 0.5 para ciertas estrategias):** descartado. Los gates son el contrato de entrada al capital real; relajarlos caso por caso es p-hacking de criterios. Si el Sharpe IS cae entre 0.3 y 0.5, la estrategia es candidata a investigación adicional (cambio de parámetros, timeframe distinto), no a relajar el umbral.

**D — NullStrategyHealthMonitor también en paper/live:** descartado. El monitor real (OPS-2) es condición de operación en producción según POLICY 3.1. Usarlo en IS/OOS es una excepción metodológica documentada, no una práctica general.

### Consecuencias

- Toda nueva hipótesis tiene trazabilidad completa: M4 en `Trading.Research/`, implementación en `Trading.Strategies/`, resultados IS/OOS en `F:\Lean\data\results\backtest-logs\`, reporte de Analytics en `F:\Lean\data\results\analytics\`.
- Las estrategias rechazadas no dejan código huérfano (`git rm` es parte del proceso, el historial de git preserva el código si se necesita recuperar).
- El pipeline es reproducible: cualquier sesión futura puede re-ejecutar las 4 fases con los mismos archivos y obtener resultados equivalentes.
- Estrategias que completaron el pipeline satisfactoriamente: `TradeSizeInstitutionalStrategy` (IS=3.985 / OOS=4.186) y `CvdSellExhaustionStrategy` (IS=2.178 / OOS=1.718), batch microestructura 2026-06-11.
- La deuda del allocator multi-estrategia (cada executor ve $100k completos) distorsiona el MaxDD en IS/OOS con múltiples activos (ADR-028). El Sharpe no se ve afectado. Esta distorsión no altera los criterios de gate actuales pero debe tenerse en cuenta al interpretar los valores de DD en los reportes.

---

## ADR-039 â€” Hito G: IS/OOS validation + block bootstrap Monte Carlo
**Fecha:** 2026-06-11
**Estado:** Aceptada
**ADRs relacionados:** ADR-038 (OfiContrarianStrategy diseÃ±o IS), ADR-037 (E-INFRA-2)

### Contexto

Hito G requerÃ­a una pipeline reproducible de validaciÃ³n de estrategias: construirla una vez y reutilizarla para cualquier candidata futura. El objetivo de este hito era doble: (1) construir la herramienta `Trading.Analytics`, (2) validar `OfiContrarianStrategy` como primera estrategia a travÃ©s del pipeline.

**TerminologÃ­a**: en la literatura "walk-forward analysis" implica mÃºltiples ventanas IS/OOS con re-optimizaciÃ³n de parÃ¡metros en cada ventana. Para estrategias de parÃ¡metros fijos (no re-optimizados), el enfoque correcto es IS/OOS split Ãºnico + Monte Carlo sobre el OOS. El true WFA con re-optimizaciÃ³n pertenece a Hito H.

### Decisiones

**D1 â€” Trading.Analytics: herramienta C# standalone, strategy-agnostic.**
Proyecto `Trading.Analytics` (console, net10.0). Lee `transaction-log.csv` de Lean como fuente de verdad. No depende de ningÃºn modelo de dominio del sistema â€” opera sobre filas CSV con PnL. Reusable para cualquier estrategia futura sin modificaciÃ³n.

Pipeline: `LeanTransactionLogParser` â†’ `TradePairer` (FIFO por sÃ­mbolo) â†’ `MetricsCalculator` â†’ `MonteCarloEngine` â†’ `ValidationGates` â†’ `ValidationReportWriter`.

Uso: `Trading.Analytics --is-log <path> --oos-log <path> --strategy <name> --output <dir>`. Exit code 0 = ambos gates pasan; 1 = alguno falla.

**D2 â€” FIFO trade pairing; Sells sin Buy previo se descartan.**
Las Ã³rdenes del CSV son fills individuales (Entry, SL, TP). Se reconstruyen trades completos: Buy = apertura, Sell = cierre del mismo sÃ­mbolo (FIFO). Sells sin Buy previo en el OOS (posiciones carry-over del IS aÃºn abiertas al inicio de la ventana) se descartan silenciosamente: son artefactos de la ventana, no trades completos del OOS.

**D3 â€” Equity curve desde PnL diario; Sharpe anualizado por sqrt(252).**
La equity curve agrupa PnL por fecha de cierre con carry-forward. Los retornos diarios alimentan el Sharpe (sqrt(252) Ã— mean/std) y Sortino (solo retornos negativos en el denominador, MAR=0). Calmar = CAGR / MaxDD. Recovery Factor = NetProfit / MaxDD.

**D4 â€” Monte Carlo: block bootstrap, bloque=5 (overlapping), 10,000 simulaciones, seed=42.**
El block bootstrap preserva la autocorrelaciÃ³n de corto plazo entre retornos consecutivos de trades. Ventanas solapadas de 5 trades. 10,000 simulaciones. Se reportan P5/P50/P95 de Sharpe, MaxDD y CAGR sobre el OOS, mÃ¡s P(Sharpe<0) y P(CAGR<0).

**D5 â€” Gate 1 (mÃ©tricas OOS deterministas) y Gate 2 (distribuciÃ³n Monte Carlo).**

Gate 1 â€” todos deben pasar:
- Trades â‰¥ 50 (muestra mÃ­nima estadÃ­sticamente razonable)
- Net profit > 0
- Sharpe â‰¥ 0.3
- Profit Factor â‰¥ 1.1
- Expectancy > 0

Gate 2 â€” todos deben pasar:
- P(Sharpe < 0) â‰¤ 20%
- Mediana Max DD â‰¤ 55%
- P5 CAGR > âˆ’5%

Los criterios son estrictos por diseÃ±o: el gate debe ser difÃ­cil de pasar, no fÃ¡cil de ajustar post-hoc. Cualquier estrategia que falle un criterio por poco debe considerarse seÃ±al de fragilidad, no candidata a relajar el umbral.

### Resultado: OfiContrarianStrategy RECHAZADA

**IS 2021-2024** (184 trades):
Sharpe=0.564, PF=1.17, Expectancy=+0.52%, Win=44%, Net=+73.6%. Gate 1: PASA.

**OOS 2025-2026Q1** (133 trades, 2025-01-01 â†’ 2026-03-31):
Sharpe=-0.703, PF=0.84, Expectancy=-0.28%, Win=36%, Net=-12.7%, MaxDD=22.9%. Gate 1: FALLA (Sharpe, PF, Expectancy, Net profit).

**Monte Carlo OOS** (10k sims, block=5):
P(Sharpe<0)=77%, MedianMaxDD=62.5%, P5 CAGR=-28%. Gate 2: FALLA los 3 criterios.

**DiagnÃ³stico del fallo:** Win rate colapsÃ³ de 44% (IS) a 36% (OOS) â€” 8pp de caÃ­da consistente a lo largo del perÃ­odo OOS. El edge de OFI percentile como seÃ±al contrarian estaba ligado al contexto de mercado alcista de 2021-2024, donde la presiÃ³n vendedora se absorbÃ­a rÃ¡pidamente por demanda estructural. En 2025, con dinÃ¡micas de mercado diferentes, el rebote post-sellers no materializa con la misma frecuencia. La seÃ±al es real (M4 positivo, IS positivo) pero no es robusta a cambios de rÃ©gimen macro. El modelo fue correcto para el perÃ­odo de entrenamiento; el edge no generaliza.

**Acciones tomadas:**
- `OfiContrarianStrategy.cs` y `OfiContrarianStrategyTests.cs`: eliminados con `git rm`.
- `StrategyFactory.cs`: entrada OFI removida.
- `strategies.json`: 3 entradas 1h removidas.
- `Trading.Research/strategy_experiments.md`: actualizado con resultado OOS y diagnÃ³stico.
- Resultado completo: `F:\Lean\data\results\analytics\validation-oficontrarianstrategy-20260611.md`.

### Alternativas consideradas

**A â€” Relajar gates para admitir la estrategia con ajustes.** Descartado: los gates son el contrato de entrada a capital real. Relajarlos post-hoc sobre la primera estrategia que los falla es p-hacking de criterios, exactamente lo que los gates buscan prevenir.

**B â€” Re-optimizar parÃ¡metros sobre el OOS.** Descartado: pertenece a Hito H. El punto del IS/OOS split es medir la robustez del edge SIN re-optimizaciÃ³n. Re-optimizar sobre OOS convierte el OOS en IS.

**C â€” Extender el OOS para capturar mÃ¡s datos.** Descartado: 15 meses y 133 trades es muestra estadÃ­sticamente suficiente. El problema es la direcciÃ³n del fallo (Win rate consistentemente mÃ¡s bajo), no el ruido.

### Consecuencias

- `Trading.Analytics` estÃ¡ listo y reusable para cualquier estrategia futura. El pipeline completo ejecuta en < 2 minutos.
- Pipeline de estrategias: sin candidata activa. Hito D sigue bloqueado (POLICY P1).
- PrÃ³xima acciÃ³n: nueva candidata vÃ­a proceso de research (Hito E), scaffolder (Hito F), luego validaciÃ³n con Hito G.
- Los criterios de los gates (D5) quedan documentados en este ADR como referencia permanente para futuras validaciones.

---

## ADR-038 â€” OfiContrarianStrategy: aprobaciÃ³n QC IS 2021-2024 y decisiones de design
**Fecha:** 2026-06-11

**Contexto:**
Hito E requerÃ­a una segunda estrategia manual con edge validado en M4 y QC IS 2021-2024. DespuÃ©s de 12 candidatas rechazadas, `OfiContrarianStrategy` pasÃ³ ambas fases.

**HipÃ³tesis:** OFI (Order Flow Imbalance) normalizado en el rango [-1,1] es mean-reverting en 1h. Cuando el OFI estÃ¡ en el percentil inferior de su historial reciente (vendedores agresivos dominan), el precio tiende a rebotar porque los vendedores se agotan. SeÃ±al long-only: `ofi_percentile < (1 - threshold)`.

**Resultados M4 (Python, IS 2021-2024, 0.04% round-trip, grid 27 configs):**
- BTC +0.869, ETH +1.475, SOL +1.367 con window=24, threshold=0.85, hold=8h
- 25/27 configs pasan Sharpe â‰¥ 0.5 (93%)
- Win rate 50-52%, expectancy positiva en los 3 activos
- Lado Short (high OFI â†’ price drops) no tiene edge estable: descartado. Long-only.

**Resultados QC IS 2021-2024 (SL=10%, TP=15%, MaxBars=8, Risk=1%, NullStrategyHealthMonitor):**
- Sharpe portfolio combinado: **0.503** (umbral M1 = 0.5 â†’ PASA)
- CAGR: 11.69%, Net Profit: +55.7% ($100k â†’ $155,663)
- Sortino: 0.702, Probabilistic Sharpe: 21.44%
- Max Drawdown: **41.1%** â€” alto, esperado con 3 activos cripto altamente correlacionados
- Win Rate: 44%, Avg Win: +1.48%, Avg Loss: -1.01%, P/L Ratio: 1.46, Expectancy: 0.078
- 640 trades cerrados en 4 aÃ±os, 1946 Ã³rdenes totales
- Kill switch ConsecutiveLossesMonitor: 1 activaciÃ³n en 2024-08-05 (8 pÃ©rdidas consecutivas), cooling-off 1 dÃ­a, luego recovered.
- Backtest completado exitosamente (Status: Completed, sin RuntimeError).
- Slippage: 0.2% round-trip embedido en fill prices (ConstantSlippageModel 0.1% por lado). MÃS conservador que M4 (0.04% round-trip). La diferencia de Sharpe entre M4 (~1.2) y QC IS (0.503) se explica por: position sizing conservador (10% equity por trade con SL=10%/Risk=1%), SL/TP early exits (win rate cae de 50% en M4 a 44% en QC), portfolio effects (3 activos correlacionados compartiendo capital).

**Decisiones de design:**

**A. Rolling percentile via Queue<double>, no array circular propio.**
Se eligiÃ³ `Queue<double>` del BCL (queue simple): `Enqueue` nueva observaciÃ³n, `Dequeue` cuando `Count > lookback`. Lectura con `ToArray()` en cada bar â€” no es O(n) el hotpath que importa. Alternativa: array circular + puntero de escritura (O(1) amortizado, mÃ¡s complejo). La Queue es suficiente para 24-96 elementos y el hotpath de un bar 1h no requiere optimizaciÃ³n sub-ms.

**B. Timestamp alignment: `marketBar.TimestampUtc.AddHours(-1)` para lookup de microBar.**
Los `MarketBar` de QC reportan el cierre de la barra (e.g., 01:00 UTC para la barra 00:00-01:00). Las filas del CSV de microestructura estÃ¡n keyed por inicio de barra (00:00 UTC). La estrategia hace `AddHours(-1)` para mapear el cierre al inicio y encontrar el microBar correspondiente. Alternativa: almacenar filas keyed por cierre en el MicrostructureRegistry. Se eligiÃ³ mantener el registry keyed por inicio (formato nativo del CSV) y ajustar en la estrategia â€” la convenciÃ³n es mÃ¡s limpia y evita ambigÃ¼edad sobre quÃ© timestamp representa la fila.

**C. Long-only: Side Short descartado.**
M4 confirmÃ³ que el lado Short (high OFI â†’ price drops) no tiene edge estable en 2021-2024 en ninguno de los 3 activos. El contexto es que los mercados cripto tienen sesgo alcista estructural en ese perÃ­odo: la venta agresiva se absorbe rÃ¡pidamente y el precio rebota. Una estrategia Long+Short habrÃ­a degradado el Sharpe cross-asset. El lado Short no se implementÃ³ â€” no hay constante en la clase ni en la config para activarlo.

**D. NullStrategyHealthMonitor para QC IS.**
El IS test bypasea el OPS-2 monitor (StrategyHealthMonitor) para medir la seÃ±al pura sin interrupciÃ³n por degradaciÃ³n detectada. El kill switch ConsecutiveLossesMonitor (riesgo global, NO OPS-2) sigue activo â€” es el guard de riesgo del portafolio que se debe mantener incluso en IS. En paper/live se restaura el StrategyHealthMonitor. Esta decisiÃ³n estÃ¡ documentada en el cÃ³digo como comentario en la sesiÃ³n IS, pero el archivo de producciÃ³n (post-aprobaciÃ³n QC IS) usa el monitor real.

**Consecuencias:**
- `OfiContrarianStrategy` es la segunda estrategia de producciÃ³n del sistema (primera: `EmaCrossStrategy` como estrategia de validaciÃ³n infra, vetada para live por POLICY P1).
- El Max DD de 41.1% en IS implica que el OPS-2 dispararÃ¡ durante bear markets (2022-Q2 tÃ­picamente). Esto es **correcto y esperado** â€” el monitor pausarÃ¡ la estrategia durante la detecciÃ³n de degradaciÃ³n, no antes.
- La diferencia M4 Sharpe (1.2) vs QC IS Sharpe (0.503) es estructural: M4 mide el edge de la seÃ±al pura, QC IS mide el performance del sistema completo con position sizing, SL/TP y portfolio effects. Ambas mÃ©tricas son vÃ¡lidas para sus propÃ³sitos.
- PrÃ³ximo hito: Hito F (Strategy Scaffolder). La existencia de dos estrategias manuales (EmaCross + OfiContrarian) permite identificar quÃ© generalizar.

---

## ADR-037 â€” E-INFRA-2: IMicrostructureProvider â€” features AggTrades sin cambiar IStrategy
**Fecha:** 2026-06-10

**Contexto:**
Hito E requiere investigar estrategias basadas en features microestructurales derivadas de AggTrades
de Binance (OFI, CVD, arrival rate, mean trade size, buy/sell ratio, price return). Los datos se
generan offline con `Trading.Research/download_aggtrades.py` y se persisten como CSV por sÃ­mbolo.
El sistema necesita que estas features estÃ©n disponibles en el backtester sin romper la
infraestructura existente de estrategias OHLCV-only (EmaCrossStrategy y futuras).

**DecisiÃ³n:**
OpciÃ³n A â€” inyecciÃ³n por constructor en la estrategia, sin modificar `IStrategy`.

- `MicrostructureBar` (Domain/Models): value object inmutable con las 7 features + InstrumentId + BarUtc.
- `IMicrostructureProvider` (Domain/Abstractions): contrato de solo lectura con `GetBar(InstrumentId, DateTime)`.
- `MicrostructureRegistry` (Application/Microstructure): implementa `IMicrostructureProvider`. Carga el CSV al boot en un `Dictionary<(InstrumentId, DateTime), MicrostructureBar>`, lookup O(1). Si el CSV no existe, loguea Warning y retorna null para ese instrumento â€” las estrategias OHLCV-only no se ven afectadas.
- `StrategyFactory.Create` acepta `IMicrostructureProvider` como parÃ¡metro opcional. Las estrategias microestructurales lo reciben por constructor; las OHLCV-only lo ignoran.
- `TradingAlgorithmHost`: construye el registry, carga CSVs para todos los sÃ­mbolos activos, pasa el registry al factory.
- CSV cargado desde la ruta configurada en `strategies.json` â†’ `MicrostructureDataPath`. Si el campo es null, fallback a `{BaseDirectory}/microstructure/{SYMBOL}_1h_features.csv`. Esto evita copiar los CSVs (~17 MB total) al build output cuando ya residen en un directorio de datos externo.

**Alternativas consideradas:**

- **OpciÃ³n B â€” extender `IStrategy` con `BarContext { MarketBar, MicrostructureBar? }`**: mÃ¡s limpio a largo plazo pero toca la interfaz y todos los tests existentes. Diferida a Hito F. Ver nota de deuda abajo.
- **Lean `AddData<>`**: requiere implementar `BaseData`, adaptar el formato al sistema de archivos de Lean, y gestionar la sincronizaciÃ³n temporal dentro del motor. Complejidad injustificada para un lookup simple de features pre-computadas.
- **Parquet directo en C#**: requerirÃ­a aÃ±adir `Parquet.Net` como dependencia al proyecto de producciÃ³n. El CSV es suficiente y no aÃ±ade dependencias.

**Consecuencias:**
- Las estrategias microestructurales deben manejar `null` gracefully (degradar a `Flat` si no hay datos).
- El CSV debe estar en `{BaseDirectory}/microstructure/` antes de correr el backtest. Si no estÃ¡, el sistema arranca igual con Warning (no fail-fast, por diseÃ±o â€” las estrategias OHLCV-only no lo necesitan).
- `StrategyFactory` recibe el provider aunque no todas las estrategias lo usen â€” acoplamiento mÃ­nimo y aceptable.

**Deuda hacia Hito F:**
Cuando el Scaffolder generalice la creaciÃ³n de estrategias, evaluar migrar a `BarContext { MarketBar Bar, MicrostructureBar? Micro }` como Ãºnico parÃ¡metro de `IStrategy.EvaluateSignal`. Esto eliminarÃ­a la inyecciÃ³n por constructor y harÃ­a el acceso a features uniforme para todas las estrategias. El debate estÃ¡ registrado aquÃ­ para no repetirlo desde cero en Hito F.

---

## ADR-036 â€” ATR SL/TP mode: SL/TP basado en multiplicadores de ATR
**Fecha:** 2026-06-09
**Estado:** Vigente
**ADRs relacionados:** ADR-035 (AtrCompressionBreakoutStrategy), ADR-009 (bus de eventos)

### Contexto

H2 (AtrCompressionBreakoutStrategy) fallÃ³ el backtest con Sharpe -0.922, DD 30.3%, kill switch 2025-03-19. DiagnÃ³stico post-mortem: la seÃ±al tiene edge genuino (M4 pasado 6/9 BTC, 5/9 ETH, 7/9 BNB), pero el SL fijo de 2% es inadecuado para una estrategia de breakout de compresiÃ³n de volatilidad. La distancia correcta del SL debe ser proporcional al ATR en el momento de la seÃ±al, no un porcentaje fijo del precio.

### DecisiÃ³n

**Agregar soporte config-driven para SL/TP basado en multiplicadores de ATR**, sin modificar `IStrategy`. El modo se selecciona en `strategies.json` via `StopTakeMode: "Atr"`.

Puntos clave de diseÃ±o:

1. **SeparaciÃ³n temporal seÃ±al / fill**: el ATR se captura en `BarProcessingService` al momento de la seÃ±al (barra actual), no al momento del fill (barra siguiente). Se almacena en `StrategyExecutor.PendingStopLossPrice` / `PendingTakeProfitPrice`.

2. **Sin cambio a IStrategy**: la estrategia expone su ATR vÃ­a la interfaz opcional `IAtrProvider.GetLastAtr(ticker)`. `BarProcessingService` usa type check (`strategy is IAtrProvider`) para activar el modo ATR. Las estrategias que no implementen `IAtrProvider` siguen usando el modo porcentaje.

3. **Compatibilidad con PositionSizer**: `StopLossPercentage` se mantiene en el JSON como aproximaciÃ³n estÃ¡tica para el sizing (PositionSizer no sabe de ATR). Con 2.5Ã—ATR â‰ˆ 3.5% para BTC 4h, la aproximaciÃ³n es razonable.

4. **StopLossPercentage no debe ser 0 en modo ATR**: si es 0, `RiskParameters.FromPercentages` lanzarÃ­a una excepciÃ³n (invariante). Se mantiene el valor como placeholder para el sizing.

### Alternativas descartadas

- **Cambiar IStrategy**: requerirÃ­a cambiar el contrato de todas las estrategias; over-engineering para una feature que quizÃ¡s solo use una estrategia.
- **Computar ATR en el fill (bar+1)**: el ATR cambia entre la seÃ±al y el fill. Usando el ATR de la barra del fill se romperÃ­a la lÃ³gica de "colocar SL a NÃ—ATR del precio de seÃ±al".
- **SL dinÃ¡mico por trailing stop**: out of scope para Hito E; requerirÃ­a gestiÃ³n activa durante la posiciÃ³n.

### Consecuencias

- Un nuevo `StopTakeMode` en `StrategyDefinition` rompe la config de estrategias que no incluyan el campo (serializaciÃ³n JSON nullable por default).
- `PendingStopLossPrice` es nullable: si el ATR es 0 (indicador no ready) o la estrategia no implementa `IAtrProvider`, se usa el modo porcentaje como fallback.
- El test de compresiÃ³n del ATR tiene un artefacto: si el breakout bar tiene un gap grande (TR alto), el ATR se dispara y puede salir del rango de compresiÃ³n. Esto es **comportamiento correcto** â€” una barra con gap extremo no debe clasificarse como breakout de compresiÃ³n. Los tests usan breakouts moderados (10% vs 20%) para evitar este artefacto.

---

## ADR-035 â€” SL% fijo es incompatible con estrategias de multi-bar hold
**Fecha:** 2026-06-09
**Estado:** Rechazada â€” estrategia eliminada. Ver ADR-036 para la soluciÃ³n.
**ADRs relacionados:** ADR-036 (ATR SL/TP mode), ADR-032 (WarmUpBars)

### Contexto

`AtrCompressionBreakoutStrategy` (hold=3 barras 4h = 12h) pasÃ³ M4 pero fallÃ³ backtest OOS con SL 2% fijo: Sharpe -0.922, Win 37%, DD 30.3%. La misma estrategia con SL ATR 2.5Ã— mejorÃ³ el DD (18.3%) y Sharpe (-0.779) pero la Win Rate permaneciÃ³ en 37%.

### Insight arquitectÃ³nico

**Un SL porcentual fijo destruye el edge de estrategias con holding period largo.** El SL se activa sistemÃ¡ticamente durante la volatilidad intraday del recorrido antes de que el movimiento direccional se materialice. El edge existe en la seÃ±al (M4 positivo) pero la gestiÃ³n del riesgo lo cancela. Ver `Trading.Research/strategy_experiments.md` para detalles completos.

Consecuencia directa: la infraestructura ATR SL/TP (`StopTakeMode: "Atr"`, `IAtrProvider`, pending prices en `StrategyExecutor`) fue construida para resolver este problema. Ver ADR-036.

---

## ADR-032 â€” WarmUpBars en IStrategy: warm-up dinÃ¡mico de indicadores internos de estrategia
**Fecha:** 2026-06-07
**Estado:** Aceptada
**ADRs relacionados:** ADR-031 (Hito C, operaciones VPS)

### Contexto

Durante el Hito C se detectÃ³ que las estrategias arrancaban con sus indicadores internos en frÃ­o, incluso despuÃ©s de que Lean completaba el warm-up. La causa: el consolidador de estrategias tenÃ­a un `if (IsWarmingUp) return` que impedÃ­a que `BarProcessingService` recibiera barras histÃ³ricas. El warm-up de Lean (20 dÃ­as fijos, dimensionado para el HMM) calentaba el clasificador de rÃ©gimen correctamente â€”que tiene su propio consolidador sin esa guardaâ€” pero no llegaba a las EMAs internas de `EmaCrossStrategy`.

Consecuencia prÃ¡ctica: en la corrida del VPS activa desde 2026-06-03, `EmaCrossStrategy` necesitÃ³ tiempo adicional en live para calentar sus propias EMAs (EMA 30 y EMA 60), equivalente a 60 barras Ã— el timeframe de cada instancia. Para TRBUSDT 4h eso era 10 dÃ­as adicionales en vivo sin posibilidad de seÃ±al.

El problema tambiÃ©n era estructural: el valor `SetWarmUp(TimeSpan.FromDays(20))` era una constante hardcodeada sin relaciÃ³n con los requerimientos reales de las estrategias. Al agregar una estrategia con indicadores de perÃ­odo largo (ej. EMA 200 en 4h = 33 dÃ­as), el warm-up serÃ­a insuficiente sin que hubiera ningÃºn error visible.

### DecisiÃ³n

**Tres cambios coordinados:**

**D1 â€” `int WarmUpBars` en `IStrategy`.** Cada estrategia declara la cantidad de barras necesarias para calentar su indicador mÃ¡s lento. `EmaCrossStrategy` retorna 60 (perÃ­odo de la EMA lenta). El contrato garantiza que cuando `EvaluateSignal` reciba el primer bar real, los indicadores internos ya tienen historia suficiente.

**D2 â€” `isWarmingUp` en `BarProcessingService.ProcessBar`.** Se elimina la guarda `if (IsWarmingUp) return` del consolidador y se pasa el flag de Lean al service. Durante warm-up: se llama `EvaluateSignal` en cada estrategia (calienta indicadores) pero se retorna antes de toda la lÃ³gica de Ã³rdenes. `BarProcessedEvent` se publica siempre â€”warm-up y liveâ€” para que `LastBarProcessedUtc` refleje actividad real durante el warm-up.

**D3 â€” CÃ¡lculo dinÃ¡mico de `SetWarmUp`.** El host calcula el warm-up como `max(100 barras Ã— 4h, max(executor.Strategy.WarmUpBars Ã— timeframeSpan))` iterando todos los executors construidos. El mÃ­nimo garantiza que el HMM siempre tenga los 100 perÃ­odos 4h que necesita; el mÃ¡ximo sobre estrategias garantiza que ningÃºn indicador arranque en frÃ­o.

### Alternativas consideradas

- **A: Warm-up fijo con documentaciÃ³n manual.** Requerir que cada autor de estrategia recuerde ajustar la constante de 20 dÃ­as. Descartado: frÃ¡gil, no escala, el error es silencioso.
- **B: Consolidador dedicado de warm-up por estrategia.** Un segundo consolidador (separado del de seÃ±ales) que solo alimenta los indicadores durante warm-up. Descartado: duplicaciÃ³n de lÃ³gica de consolidaciÃ³n, frÃ¡gil al agregar timeframes o estrategias nuevas.
- **C (elegida): `WarmUpBars` en la interfaz + flag `isWarmingUp`.** El contrato queda en la abstracciÃ³n correcta: la estrategia sabe quÃ© necesita, el host respeta ese requerimiento automÃ¡ticamente. Sin duplicaciÃ³n.

### Consecuencias

**Positivas:**
- Cualquier estrategia nueva que declare `WarmUpBars` correctamente tiene sus indicadores calentados al inicio del trading real, sin configuraciÃ³n adicional.
- El warm-up del VPS que antes tardaba 10 dÃ­as en live (TRBUSDT 4h) ahora se resuelve en el replay histÃ³rico (~8 minutos de wall clock en el VPS).
- `LastBarProcessedUtc` se actualiza durante el warm-up, mejorando la observabilidad del arranque en el heartbeat.

**RestricciÃ³n activa:**
- `WarmUpBars` debe reflejar el perÃ­odo del indicador mÃ¡s lento de la estrategia. Si una estrategia subestima este valor, arranca con historia parcial sin error visible. Es responsabilidad del autor de cada `IStrategy`.

**Deuda futura:**
- Para estrategias con indicadores de perÃ­odo muy largo (ej. EMA 200 en 4h = 33 dÃ­as), el warm-up dinÃ¡mico puede superar los 20 dÃ­as actuales, aumentando el tiempo de arranque del proceso. Aceptable por ahora; si se vuelve un problema operativo, considerar precalentar desde una snapshot de estado persistida.

---

## ADR-031 â€” Hito C: infraestructura de feed verificada; deuda de race condition en plugin de Binance
**Fecha:** 2026-06-03
**Estado:** Aceptada
**ADRs relacionados:** ADR-021 (INFRA-2 monitoreo), ADR-030 (bypass ValidateSubscription)

### Contexto

Al arrancar Hito C (paper trading en VPS Windows con NSSM) surgieron tres problemas encadenados que impidieron la validaciÃ³n operativa plena:

**Problema 1 â€” Race condition en `BrokerageMultiWebSocketSubscriptionManager.OnOpen()`.**
El plugin de Binance tiene un bug en el vendored code: `OnOpen` re-suscribe en el callback de reconexiÃ³n WebSocket pero no espera a que los streams previos se cierren antes de suscribirse de nuevo. Bajo ciertas condiciones de red, el feed queda en estado stall permanente: el WebSocket reporta `Connected` pero no entrega barras. El proceso puede mantenerse en ese estado indefinidamente sin seÃ±al de error observable.

**Problema 2 â€” Watchdog disparando cada ~15 min con `staleness=14400s`.**
El watchdog implementado en `TradingAlgorithmHost` comparaba `DateTime.UtcNow` (UTC real) contra `LastBarProcessedUtc` que provenÃ­a de `BarProcessingService` vÃ­a `_clock.UtcNow`. El bug: `LeanClock.UtcNow` retornaba `_algorithm.Time` en lugar de `_algorithm.UtcTime`. En operaciÃ³n live de junio (EDT=UTC-4), `_algorithm.Time` devuelve hora local del algoritmo (11:01 EDT cuando el wall clock era 15:01 UTC). Diferencia = 4h = 14400 segundos >> umbral de 1200s â†’ el watchdog disparaba restart inmediatamente despuÃ©s del primer bar consolidado.

**Problema 3 â€” Timestamps `1997-12-31T19:00:00` durante `Initialize()` (DEUDA-3).**
`_algorithm.UtcTime` durante la fase `Initialize()` retorna el epoch de QC (`1997-12-31T19:00:00 UTC`) antes de que el motor inicialice su reloj interno. Los primeros eventos JSONL y `ProcessStartedUtc` quedaban con ese timestamp inÃºtil.

### Decisiones

**D1 â€” Patch operativo para el race condition (stall del feed): auto-restart vÃ­a `Environment.Exit(1)`.**
El watchdog en `TradingAlgorithmHost` llama `Environment.Exit(1)` cuando `BarStalenessSeconds > 1200s`. NSSM estÃ¡ configurado con `AppExit Default Restart` y re-levanta el proceso automÃ¡ticamente. SoluciÃ³n operacional robusta mientras el bug vendored no se resuelve. Umbral: 20 minutos de silencio de barras antes de restart (umbral conservador para absorber perÃ­odos normales de baja actividad de mercado, incluyendo fines de semana). Commit `ee76d65`.

**D2 â€” Fix de `LeanClock`: `_algorithm.Time` â†’ `_algorithm.UtcTime`.**
La causa raÃ­z de los falsos positivos del watchdog. `_algorithm.UtcTime` siempre devuelve UTC real (tiempo simulado en backtest, tiempo UTC del exchange en live). `_algorithm.Time` devuelve hora en el timezone del algoritmo (que en este deployment es EDT=UTC-4), rompiendo cualquier comparaciÃ³n contra `DateTime.UtcNow`. Commit `8789061`. Regla permanente documentada en AI.md.

**D3 â€” Fix de Ã©poca QC en `LeanClock` (DEUDA-3): fallback a `DateTime.UtcNow` cuando `UtcTime < aÃ±o 2000`.**
Si `_algorithm.UtcTime` es anterior al aÃ±o 2000, `LeanClock.UtcNow` retorna `DateTime.UtcNow` (wall clock real). Esto garantiza que `ProcessStartedUtc` y los primeros eventos JSONL de `Initialize()` tengan timestamps reales. Umbral elegido como `new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)`. Commit `2671694`.

### Alternativas consideradas

**D1 alternativas:**
- **A: Fix directo del race condition en el plugin vendored.** Deseable como soluciÃ³n permanente, pero requiere debugging profundo de cÃ³digo vendored de Lean y re-testing de integraciÃ³n. Postergado como DEUDA tÃ©cnica activa (ver consecuencias).
- **B: Timeout de watchdog mÃ¡s corto.** Descartado: umbral < 20 min genera falsos positivos en perÃ­odos legÃ­timos de baja actividad (fine de semana, gaps nocturnos).
- **C (elegida): Auto-restart.** Operacionalmente probado durante Hito C. El proceso se levanta en segundos, warmup completa rÃ¡pido (~30s), sin intervenciÃ³n humana.

**D2 alternativas:**
- **A: Usar `DateTime.UtcNow` directamente en `BarProcessingService`** para el evento. Descartado: viola el criterio `IClock` del dominio y hace el cÃ³digo no determinista en backtest.
- **B (elegida): Corregir `LeanClock`.** El contrato de `IClock.UtcNow` es "UTC real"; `LeanClock` no lo cumplÃ­a. La correcciÃ³n es en la fuente.

### Consecuencias

**Positivas:**
- Sistema corriendo estable en VPS desde 2026-06-03. Verificado >2h continuas con pings cada 5 min a Healthchecks.io, heartbeat actualizado correctamente, `BarStalenessSeconds` en rango normal (300-600s en mercado activo).
- `LeanClock.UtcNow` cumple su contrato en todos los contextos: backtest (UtcTime simulado), live (UtcTime UTC real), Initialize (fallback a wall clock real).
- DEUDA-3 cerrada: logs del JSONL tienen timestamps reales desde el arranque.

**Deuda tÃ©cnica abierta:**
- Race condition en `BrokerageMultiWebSocketSubscriptionManager.OnOpen()` (plugin vendored de Lean). El auto-restart es parche operativo; la cura requiere sincronizaciÃ³n de re-suscripciÃ³n. Postergado a Bloque 4 o como mejora al plugin. Mientras el parche sea efectivo, la prioridad es baja.

**Modelo de deployment VPS documentado en AI.md:**
- Servicio NSSM `LeanPaper` en Windows Server.
- Binario en `C:\Lean\Paper\`.
- Variable de entorno `HEALTHCHECKS_PING_URL` inyectada via NSSM `AppEnvironmentExtra`.

**Hito C pendiente de cierre pleno:** la infraestructura operativa estÃ¡ verificada. El cierre definitivo del Hito C requiere al menos un trade real (live con paper brokerage) para validar el ciclo completo U1-U4, el kill switch con equity en movimiento, y el comportamiento del JSONL bajo Ã³rdenes reales.

---

## ADR-030 â€” Bypass de ValidateSubscription en el plugin de Binance para operaciÃ³n en live local sin suscripciÃ³n QuantConnect
**Fecha:** 2026-05-29
**Estado:** Aceptada
**ADRs relacionados:** ADR-029 (lecciÃ³n de verificaciÃ³n de binario), ADR-021 (monitoreo bÃ¡sico para paper trading)

### Contexto

El arranque de Hito C requerÃ­a que el sistema corriera en `LiveMode == true`
con Paper Brokerage de Lean + data feed real de Binance Futures USDM. Al
lanzar paper trading el sistema fallÃ³ durante la inicializaciÃ³n del
brokerage:

```
ValidateSubscription(): Failed during validation, shutting down.
Error : Invalid api user id or token, cannot authenticate subscription.
```

VerificaciÃ³n posterior en el portal de QuantConnect confirmÃ³: "To request an
access token, you must belong to a paid organization." La cuenta gratuita
no genera token vÃ¡lido. Tarifas externas estiman el plan mÃ¡s barato que
habilita live trading entre USD 20-60/mes (no confirmado en pricing
logueado).

**UbicaciÃ³n real de la validaciÃ³n (verificada en cÃ³digo).** La rutina
`ValidateSubscription()` NO vive en el motor de Lean. Vive dentro del
plugin de Binance, en
`Brokerages.Binance/QuantConnect.BinanceBrokerage/BinanceBrokerage.cs:909`,
y es invocada desde `Initialize(...)` del propio brokerage. La funciÃ³n
arma un `ApiConnection` con credenciales de `Globals`, hace POST a
`modules/license/read` en los servidores de QC, decripta la respuesta con
AES, valida que la licencia no estÃ© expirada, y ante cualquier error llama
`Environment.Exit(1)`.

Esto importa para entender el alcance del fork: el motor de Lean (Engine,
Common, Algorithm, etc.) permanece sin modificaciones y puede actualizarse
libremente. El gate de suscripciÃ³n estÃ¡ focalizado en los plugins
oficiales de brokerages. PatrÃ³n "open core" acotado a los conectores
comerciales, no al motor â€” legal con Apache 2.0, Ã©ticamente ambiguo
porque el material de marketing presenta a LEAN como "open source" sin
distinguir entre motor (genuinamente abierto) y plugins oficiales (con
gating).

### DecisiÃ³n

Parchar `ValidateSubscription` en la copia vendored del plugin de Binance
para que retorne inmediatamente, sin contactar a los servidores de QC.
ImplementaciÃ³n con retorno temprano mÃ¡s una cadena `static readonly`
(`_adr030BinaryMarker`) que persiste como `ADR-030-BYPASS-VALIDATE-SUBSCRIPTION`
en el binario, permitiendo verificaciÃ³n fÃ­sica por bÃºsqueda UTF-16 LE
sobre el `.dll` desplegado (lecciÃ³n ADR-029).

RazÃ³n principal: el sistema estÃ¡ en validaciÃ³n pre-rentabilidad. Un costo
recurrente de USD 240-720/aÃ±o por una capacidad que solo se usa mientras
el sistema aÃºn no demostrÃ³ generar ingresos no es defendible
financieramente con el contexto actual del operador. La decisiÃ³n es
revisable cuando ese contexto cambie (ver Trigger de revisiÃ³n).

### Alternativas consideradas

**A: Pagar el plan mÃ¡s barato de QuantConnect.** Comodidad mÃ¡xima:
mantenimiento delegado del plugin de Binance, soporte oficial, posibilidad
de usar features pagas adicionales (cloud deployment, datasets premium).
Descartada por costo recurrente sin retorno demostrado y por crear
dependencia comercial con QC que persiste mientras el sistema opere en
live, paper o real. Re-evaluable cuando el sistema cruce los triggers de
revisiÃ³n.

**B: Binance Testnet completo.** Sin costo. Evita pasar por el gate de
QC porque conecta directamente al testnet del exchange. Descartada porque
degrada el valor de la validaciÃ³n de Hito C: el feed del testnet es
sintÃ©tico, con liquidez y microestructura que no replican fielmente al
mercado real. El Hito C valida infraestructura del sistema bajo
wall-clock real; un feed sintÃ©tico introduce variables que confunden esa
validaciÃ³n. Sigue siendo opciÃ³n vÃ¡lida para validaciones futuras donde el
feed sintÃ©tico no sea limitante.

**C (elegida): Parche local en el plugin de Binance.** Sin costo, feed
real de producciÃ³n, fills ficticios vÃ­a PaperBrokerage. Preserva la
calidad del feed que el Hito C requiere. El precio es asumir
mantenimiento manual del fork del plugin (no del motor).

**B' (variante mixta descartada):** modificar el plugin para apuntar el
feed a producciÃ³n de Binance y las Ã³rdenes al testnet. TÃ©cnicamente
posible pero requiere intervenciÃ³n mÃ¡s invasiva al plugin (desacoplar
URLs de feed y de transactions, que hoy estÃ¡n controladas por un Ãºnico
flag). Complejidad innecesaria para Hito C; archivable si en el futuro
se necesita un setup mÃ¡s fino que combine ambos mundos.

### Consecuencias

**Positivas:**
- Hito C desbloqueado. El sistema arrancÃ³ en live-paper el 2026-05-29 a
  las 16:51 sin errores de autenticaciÃ³n. ValidaciÃ³n funcional descrita
  abajo.
- Costo recurrente cero. La decisiÃ³n preserva el principio operativo del
  operador de no asumir gastos fijos antes de demostrar rentabilidad.
- Motor de Lean intacto y libremente actualizable.

**Neutras / aceptadas:**
- Mantenimiento delegado a uno mismo, **acotado al plugin de Binance**.
  Cada actualizaciÃ³n del plugin va a requerir re-aplicar el parche. El
  motor de Lean y los demÃ¡s componentes no cargan ese trabajo.
- Aislamiento del ecosistema comercial QC: sin soporte oficial, sin foro
  para mostrar cÃ³digo modificado en caso de bug.
- Si en el futuro se agregan otros plugins oficiales (Coinbase, IB, etc.),
  cada uno traerÃ¡ su propia `ValidateSubscription` y serÃ¡ una decisiÃ³n
  separada si parchar tambiÃ©n esos plugins. La presente decisiÃ³n NO se
  extiende implÃ­citamente.

**Negativas:**
- Zona gris Ã©tica (no legal). Apache 2.0 permite la modificaciÃ³n pero el
  parche elude el modelo de negocio del vendor. Esto se acepta como
  trade-off consciente, no se minimiza.
- Riesgo de drift: QC puede endurecer la validaciÃ³n en versiones futuras
  del plugin (mÃ¡s sitios de validaciÃ³n, validaciÃ³n cruzada, ofuscaciÃ³n
  del check). El parche actual podrÃ­a dejar de ser suficiente. El trigger
  "actualizaciÃ³n del plugin falla" en secciÃ³n de revisiÃ³n cubre este
  escenario.

### ValidaciÃ³n funcional

El sistema arrancÃ³ en `live-paper` el 2026-05-29 a las 16:51 (wall-clock).
El log de arranque NO contiene la lÃ­nea de error
`Invalid api user id or token`. WebSocket de Binance Futures producciÃ³n
(`wss://fstream.binance.com/...`) conectÃ³ correctamente. Warmup completÃ³
con los tres sÃ­mbolos suscriptos (BTCUSDT, ETHUSDT, TRBUSDT). Heartbeat
flush timer arrancÃ³. Pings a Healthchecks.io confirmados por integraciÃ³n
de Telegram (notificaciones UP/DOWN recibidas durante el ciclo
arranque â†’ hibernaciÃ³n accidental â†’ reanudaciÃ³n). El parche cumple su
propÃ³sito.

### Trigger de revisiÃ³n

Esta decisiÃ³n se revisa cuando se cumpla CUALQUIERA de:

- **Primer trade rentable real** (no paper, no testnet) operado por el
  sistema.
- **6 meses corridos** de sistema operando estable en VPS sin caÃ­das
  significativas.
- **Una actualizaciÃ³n del plugin de Binance falla** porque el parche no
  se puede re-aplicar limpio sobre la versiÃ³n nueva.

En la revisiÃ³n, re-evaluar los tres caminos con el contexto financiero y
operativo de ese momento.

### Reversibilidad

Alta. Quitar el `return` temprano, la lÃ­nea `_ = _adr030BinaryMarker;` y
eliminar el campo estÃ¡tico `_adr030BinaryMarker` restaura el
comportamiento original. Sin efectos colaterales sobre backtest, sobre la
estrategia, ni sobre el motor de Lean (que nunca fue tocado).

### ImplementaciÃ³n

Commit `bb4ae62` â€” `fix(engine): bypass ValidateSubscription for local live mode (ADR-030)`.

Cambio puntual en
`Brokerages.Binance/QuantConnect.BinanceBrokerage/BinanceBrokerage.cs:909`:
retorno temprano + campo estÃ¡tico con la cadena de trazabilidad.

VerificaciÃ³n fÃ­sica: cadena `ADR-030-BYPASS-VALIDATE-SUBSCRIPTION`
encontrada en `QuantConnect.Brokerages.Binance.dll` por bÃºsqueda UTF-16 LE
en offset 67586.

Suite completa de tests: 211/211 verdes. El parche no rompe ningÃºn test
existente.

### Pendiente operativo

ADR-029 quedÃ³ pendiente de redacciÃ³n en el momento en que se cerrÃ³ (no
estÃ¡ ni en DECISIONS.md ni como archivo suelto). Esta omisiÃ³n deberÃ­a
remediarse en algÃºn momento del cierre del Hito C, reconstruyendo el ADR
desde los commits y conversaciÃ³n de la sesiÃ³n correspondiente. No es
urgente pero la deuda crece con el tiempo.

## ADR-028 â€” ValidaciÃ³n multi-sÃ­mbolo + fix estructural OPS-2
**Fecha:** 2026-05-26 / 2026-05-27
**Estado:** Aceptada
**ADRs relacionados:** ADR-026 (validaciÃ³n multi-timeframe BTCUSDT), ADR-027 (re-entrenamiento BTC multi-seed)

### Contexto

ADR-026 habÃ­a validado el subsistema de ejecuciÃ³n/monitoreo como agnÃ³stico al
timeframe sobre BTCUSDT mediante tres backtests secuenciales (15m, 1h, 4h).
Quedaba pendiente validar agnosticismo al sÃ­mbolo en operaciÃ³n concurrente
real, lo que requerÃ­a sumar al menos dos sÃ­mbolos mÃ¡s con sus propios
clasificadores de rÃ©gimen.

Esta sesiÃ³n cubre:
1. Entrenamiento de modelos HMM para ETHUSDT y TRBUSDT bajo OpciÃ³n A
   (un HMM por sÃ­mbolo).
2. ConfiguraciÃ³n multi-sÃ­mbolo en `strategies.json` con los tres sÃ­mbolos
   en TF 1h (decisiÃ³n de aislar sÃ­mbolo como Ãºnica variable de validaciÃ³n,
   habiendo ya cubierto TF en ADR-026).
3. ValidaciÃ³n del subsistema sobre el backtest paralelo 2025-01-01 â†’
   2026-03-31.

Durante la validaciÃ³n apareciÃ³ un bug estructural latente preexistente del
flujo `LiquidateAll` del kill switch global, que no se manifestaba en
configuraciones single-symbol y que causÃ³ dos violaciones del invariante
OPS-2 en el primer backtest multi-sÃ­mbolo. La sesiÃ³n se extendiÃ³ para
arreglar la causa raÃ­z.

### Decisiones tomadas

#### DecisiÃ³n 1 â€” `MinimumRequiredBars` del trainer baja de 10000 a 5000

Calibrado al piso tÃ©cnico defendible para HMM-GMM con K=4 y D=7 features
multi-seed (rule of thumb ~10-50 obs/parÃ¡metro sobre ~155 parÃ¡metros del
modelo). El threshold previo de 10000 no estaba calibrado â€” era un valor
genÃ©rico conservador. 5000 deja margen claro y permite incluir activos
con historia de listado posterior a 2020-01 (TRB, listado 2020-09).

Criterio asociado: **el sistema no se acomoda al activo**. Los thresholds
de promociÃ³n de modelo son uniformes; los activos que no cumplen se
rechazan, no se acomodan. La elasticidad legÃ­tima por activo vive en
`strategies.json` (SL/TP/Risk paramÃ©tricos), no en el modelo de rÃ©gimen.

#### DecisiÃ³n 2 â€” Default de output del trainer pasa al staging

`Trading.Models/regime/staging/{ticker}-perp-binance.hmm.json` por defecto. La
promociÃ³n a `Trading.Models/regime/{ticker}-perp-binance.hmm.json` es manual y
gateada por criterios de inspecciÃ³n uniformes (K âˆˆ {3,4}, al menos un
estado Trend, ningÃºn estado decodificado <5% ni >70%, ningÃºn label
agregado >85%).

Este cambio aplica tambiÃ©n a BTC. Cierra la deuda implÃ­cita "el archivo
en `Trading.Models/regime/` se trataba como artefacto histÃ³rico congelado en
lugar de output regenerable del trainer", que el incidente del `.bak`
preDEUDA1 revelÃ³ (ADR-027).

#### DecisiÃ³n 3 â€” Criterios de promociÃ³n de modelo uniformes

ETHUSDT y TRBUSDT entrenados con el trainer multi-seed y evaluados
contra los criterios uniformes. **Ambos pasaron los 6 criterios:**

- **ETHUSDT (K=4, BIC 56707.84):** Trend 52.07% / Squeeze 31.10% /
  HighVolatility 16.83%. Mapping: state 0â†’Trend, 1â†’Trend, 2â†’HighVol,
  3â†’Squeeze.
- **TRBUSDT (K=4, BIC 49814.19, 9405 barras post warm-up):** Trend
  46.66% / Squeeze 40.49% / HighVolatility 12.85%. Mapping: state
  0â†’Squeeze, 1â†’Trend, 2â†’HighVol, 3â†’Trend.

Ambos promovidos a `Trading.Models/regime/`. La flota de modelos en producciÃ³n
al cierre de sesiÃ³n es: BTCUSDT (re-entrenado multi-seed en ADR-027),
ETHUSDT, TRBUSDT.

#### DecisiÃ³n 4 â€” Eliminar `IOrderRouter.LiquidateAll()` (fix estructural OPS-2)

Bug detectado: `LeanBrokerageAdapter.LiquidateAll()` llamaba
`_algorithm.Liquidate()` (helper global de Lean). Las Ã³rdenes resultantes
llevaban `Tag = "Liquidated"`, ese tag no proviene de `OrderRegistry`, y
`OrderEventMapper` las descartaba como "liquidaciÃ³n global ignorada".
`StrategyHealthMonitor` nunca recibÃ­a el close. Resultado:
desincronizaciÃ³n entre Lean (posiciÃ³n cerrada fÃ­sicamente) y el monitor
(estado "posiciÃ³n abierta"). DÃ­as despuÃ©s, una nueva seÃ±al compatible
pasaba el filtro pre-orden de `BarProcessingService` (porque
`IPortfolioState.IsInvested` consultaba Lean directamente y reportaba
`false`), hacÃ­a fill, y el monitor lanzaba OPS-2 invariante violado.

El comentario explÃ­cito en el cÃ³digo del propio `LiquidateAll()` admitÃ­a
el problema: *"NO se registra (no hay executor Ãºnico). Los eventos
resultantes serÃ¡n ignorados por OrderEventMapper con log de
advertencia."* Esa "advertencia" era exactamente la causa de OPS-2 en
multi-sÃ­mbolo.

**Fix aplicado:**

- `IOrderRouter.LiquidateAll()` eliminado del contrato del dominio.
- `LeanBrokerageAdapter.LiquidateAll()` eliminado de la implementaciÃ³n.
- `LiquidateAllRiskAction` refactorizado: recibe la lista de instrumentos
  activos por inyecciÃ³n, itera sobre ellos, consulta
  `IPortfolioState.IsInvested(instrumentId)`, y emite
  `LiquidateInstrument` (que ya usaba la disciplina correcta de
  `OrderRegistry` y tags propios) solo para los invertidos.
- `OrderPurpose.Liquidate` agregado al enum del dominio.
- `ExecutorIdentifier` para Ã³rdenes del kill switch global:
  `"RiskOrchestrator_KillSwitch"` (identificador sintÃ©tico, no
  corresponde a una estrategia). Discutido al diseÃ±ar el fix; se aceptÃ³
  romper la convenciÃ³n `{Strategy}_{Symbol}_{Timeframe}` para que en logs
  sea obvio que el cierre provino del kill switch global, no de una
  estrategia.
- `TradingAlgorithmHost.ExtractActiveInstruments` agregado para construir
  la lista de instrumentos Ãºnicos desde `strategies.json` durante el
  wiring del orchestrator.
- `IPortfolioState` **NO se extendiÃ³**. La lista de instrumentos activos
  proviene del wiring, no del dominio. DecisiÃ³n deliberada: el dominio
  no debe contaminarse con mÃ©todos para resolver un problema de
  infraestructura cuando la informaciÃ³n ya estÃ¡ disponible en el callsite.

**`OrderEventMapper` no se tocÃ³.** Su lÃ³gica defensiva de ignorar tags
no registrados sigue siendo correcta para Ã³rdenes genuinamente externas
(operador manual en producciÃ³n, ajustes del broker, etc). El bug no era
el receptor â€” era que el sistema emitÃ­a Ã³rdenes que no se hacÃ­a cargo de
registrar.

#### DecisiÃ³n 5 â€” Aceptar dos cambios tÃ©cnicos fuera del brief original

Durante la implementaciÃ³n del fix de DecisiÃ³n 4, Claude Code descubriÃ³
que el identificador sintÃ©tico `RiskOrchestrator_KillSwitch` no era
reconocido por `OrderLifecycleService` como executor existente y los
fills se descartaban. Sin reportar al operador, agregÃ³ dos cambios:

- **`OrderLifecycleService.cs`**: cuando llega un evento con
  `Purpose == OrderPurpose.Liquidate` y `Status == OrderEventStatus.Filled`
  cuyo `ExecutorIdentifier` no se encuentra entre los executors
  registrados, broadcast del evento a **todos los executors del mismo
  instrumento**. Cualquier otra combinaciÃ³n de purpose/status con executor
  desconocido sigue al `_logger.Error` original.
- **`StrategyHealthMonitor.cs`**: case nuevo en el switch de
  `OnOrderFilled` para `OrderPurpose.Liquidate`. Si el monitor tiene
  posiciÃ³n abierta para ese executor, llama `ProcessTradeClose`. Si no,
  no-op. El `default` del switch sigue lanzando para purposes
  desconocidos.

**AuditorÃ­a de los diffs confirmÃ³ que los cambios son la soluciÃ³n correcta
y mÃ­nima** del problema real:
- El broadcast estÃ¡ doble-condicionado a `Liquidate + Filled` y no
  cambia la semÃ¡ntica de flujos normales (SL/TP/TimeExit).
- El case del monitor estÃ¡ guardado con `_openPositions[id] is not null`
  y preserva la invariante "purpose desconocido = bug".

Los cambios se aceptan. La nota de proceso queda registrada:
**Claude Code debiÃ³ haber pausado y consultado al operador antes de
extender el brief.** El brief original especificaba "detener y reportar
si el `ExecutorIdentifier` sintÃ©tico no pasa alguna validaciÃ³n en
`OrderRegistry` o `StrategyHealthMonitor`". La falla ocurriÃ³ en
`OrderLifecycleService`, que no estaba listado explÃ­citamente â€”
interpretaciÃ³n literal cuando el espÃ­ritu del brief era "ante adyacencias
no triviales, pausar". HeurÃ­stica futura: cuando un brief especifica
"detener si X" y aparece un caso adyacente con soluciÃ³n no obvia, el
default es pausar. Bajo costo de pausar (un mensaje), alto costo de
proceder sin consultar (deuda escondida, cambios no documentados).

#### DecisiÃ³n 6 â€” DEUDA-2 registrada (OrderListHash no determinista)

Durante la verificaciÃ³n de equivalencia conductual del modelo BTC
re-entrenado (ADR-027), se descubriÃ³ que `OrderListHash` no es
determinista entre corridas del mismo modelo y misma configuraciÃ³n.
La verificaciÃ³n se hizo entonces por comparaciÃ³n directa de
`transaction-log.csv` (147 order events idÃ©nticos en orderId, timestamp,
fill price y fill quantity entre modelo preDEUDA1 y modelo nuevo). DEUDA-2
queda registrada en `DECISIONS.md` con workaround documentado. No
bloqueante.

### Resultados de la validaciÃ³n multi-sÃ­mbolo

Backtest paralelo 2025-01-01 â†’ 2026-03-31, `strategies.json` con tres
entradas en TF 1h:

- `BTCUSDT 1h`: SL 1.0%, TP 2.0%, Risk 2.0%, MaxBars 20, CompatibleRegimes [Trend].
- `ETHUSDT 1h`: SL 1.2%, TP 2.4%, Risk 2.0%, MaxBars 20, CompatibleRegimes [Trend].
- `TRBUSDT 1h`: SL 2.0%, TP 4.0%, Risk 2.0%, MaxBars 20, CompatibleRegimes [Trend].

**MÃ©tricas portfolio (run post-fix, 2026-05-27 15:35):**

| MÃ©trica            | Valor          |
|--------------------|----------------|
| Start Equity       | 100,000 USDT   |
| End Equity         | 63,131.87 USDT |
| Net Profit         | âˆ’36.87%        |
| Max Drawdown       | 47.0%          |
| Sharpe             | âˆ’1.096         |
| Sortino            | âˆ’1.082         |
| Win Rate           | 33% (40/122)   |
| P/L Ratio          | 1.56           |
| Total Orders       | 377            |
| Total Round Trips  | 122            |
| Avg Trade Duration | 6h 44m         |

**Trades por executor (post-fix):**

| Executor                       | Round trips | Primer cierre    | Ãšltimo cierre    |
|--------------------------------|-------------|------------------|------------------|
| EmaCrossStrategy_BTCUSDT_1h    | 24          | 2025-01-08 12:04 | 2025-05-20 08:00 |
| EmaCrossStrategy_ETHUSDT_1h    | 58          | 2025-01-07 17:02 | 2025-08-29 10:07 |
| EmaCrossStrategy_TRBUSDT_1h    | 38          | 2025-02-09 08:51 | 2026-02-18 13:44 |

BTC deja de operar el 2025-05-20 por degradaciÃ³n U2 (DD rolling 30d
21.48% sostenido 5 dÃ­as). ETH deja de operar el 2025-08-29 por
degradaciÃ³n U2 (DD rolling 30d 15.81% sostenido 5 dÃ­as) y U3/U4 armados
en 2025-08-17. TRB nunca alcanzÃ³ 50 trades acumulados para armar U3/U4.

**Kill switches (3 activaciones):**

| Fecha/hora          | Monitor                  | RazÃ³n                   | LiquidateInstrument calls       |
|---------------------|--------------------------|-------------------------|---------------------------------|
| 2025-04-17 09:40    | DrawdownMonitor          | Drawdown 25.09% â‰¥ 25%   | 0 (ningÃºn instrumento invertido)|
| 2025-05-26 09:57    | ConsecutiveLossesMonitor | 8 pÃ©rdidas consecutivas | 0 (ningÃºn instrumento invertido)|
| 2025-08-28 08:26    | DrawdownMonitor          | Drawdown 25.00% â‰¥ 25%   | 0 (ningÃºn instrumento invertido)|

En los tres kill switches del run post-fix, las posiciones estaban
cerradas antes del breach por SL/TP normales 1-24h antes. El path de
broadcast del fix (DecisiÃ³n 5) no se ejercitÃ³ por el backtest pero **sÃ­
queda cubierto por 8 tests unitarios** (4 en `OrderLifecycleServiceLiquidateTests`
+ uno agregado durante el ciclo: `LiquidateCanceled_ExecutorDesconocido_LoguaError_NoBroadcast`;
3 en `StrategyHealthMonitorTests`). La confianza en el fix viene del
anÃ¡lisis estructural + cobertura unitaria, no de "tuvimos suerte de
ejercitar el path en este run".

**Criterios cualitativos del subsistema (5Ã—3):**

| Criterio                                                          | BTC | ETH | TRB |
|-------------------------------------------------------------------|-----|-----|-----|
| Cero OPS-2 invariante violado                                     | âœ…  | âœ…  | âœ…  |
| Cero `OrderEventMapper: evento sin tag` durante TimeExit/Liquidate| âœ…  | âœ…  | âœ…  |
| U1/U2 disparan con DD coherente (no por bug)                      | âœ…  | âœ…  | âœ…  |
| `ExecutorIdentifier` Ãºnico bien etiquetado                        | âœ…  | âœ…  | âœ…  |
| OperaciÃ³n independiente entre executors                           | âœ…  | âœ…  | âœ…  |

**15/15 criterios cualitativos verdes.** Subsistema validado como
agnÃ³stico al sÃ­mbolo bajo operaciÃ³n concurrente real.

### Hallazgos secundarios (no actuados)

1. **Trades fantasma del bug previo**: el run del Brief 3 pre-fix produjo
   159 trades; el run post-fix produjo 122. La diferencia (~37 trades)
   son entradas que el bug del `LiquidateAll` permitÃ­a bajo estado
   inconsistente. El fix no solo cumple el invariante formal â€” tambiÃ©n
   elimina trades que no debieron existir.

2. **Net Profit âˆ’36.87% y Max Drawdown 47.0%** son nÃºmeros feos. NO son
   criterio de validaciÃ³n de esta sesiÃ³n (el objetivo era validar
   agnosticismo del subsistema, no rentabilidad de la estrategia).
   EmaCrossStrategy sigue VETADA para live por POLICY P1 (sin
   walk-forward). Su uso actual sigue siendo exclusivamente como
   instrumento de validaciÃ³n del subsistema.

3. **Varianza numÃ©rica del trainer en dÃ­gitos 12+**: el trainer
   multi-seed produce JSONs que difieren en dÃ­gitos 12-15 de los doubles
   serializados entre corridas (con K y mapping idÃ©nticos). Causa
   probable: orden de iteraciÃ³n de Dictionary interno o reducciones
   parallelas en Accord. Benign â€” las clasificaciones que el modelo
   produce sobre cualquier barra son indistinguibles. Aislable por
   inspecciÃ³n visual del modelo antes de promover (POLICY 7). No
   bloqueante.

4. **Test suite final: 132 verdes** (de 121 originales + 11 nuevos: 4
   `LiquidateAllRiskActionTests` (3 inicialmente + 1 borde), 4
   `OrderLifecycleServiceLiquidateTests` + 1 agregado en cobertura del
   ciclo, 3 `StrategyHealthMonitorTests`).

### Deudas que quedan abiertas al cierre

- **DEUDA-2**: `OrderListHash` no determinista entre corridas. Workaround
  documentado en `DECISIONS.md`. Pendiente de fix separado.
- **Allocator multi-estrategia**: cada executor ve `InitialAccountCashUsdt =
  100_000` como suyo para calcular DD, cuando la cuenta es compartida.
  DistorsiÃ³n per-monitor aceptada provisoriamente. Hito propio futuro.
- **POLICY 7.1 tÃ­tulo "1h" vs config "1h" actual**: hallazgo de ADR-026,
  re-anotado. Fix separado.
- **EmaCrossStrategy vetada para live** (POLICY P1): sin walk-forward
  analysis. Su uso queda como instrumento de validaciÃ³n del subsistema.

### Consecuencias

**Positivas:**
- Subsistema de ejecuciÃ³n/monitoreo formalmente validado como agnÃ³stico
  al sÃ­mbolo y al timeframe (ADR-026 + ADR-028).
- Bug estructural del `LiquidateAll` resuelto. El sistema mantiene la
  invariante "toda orden emitida por el sistema pasa por `OrderRegistry`".
- Flota de tres modelos HMM consistente, todos generados por el mismo
  pipeline multi-seed, todos en `Trading.Models/regime/` con criterios uniformes
  de promociÃ³n.
- Test suite expandida en 11 tests con cobertura especÃ­fica del nuevo
  flujo de liquidaciÃ³n dirigida por instrumento.

**Neutras / aceptadas:**
- El fix de `LiquidateAll` cambiÃ³ mÃ©tricas y comportamiento del backtest
  vs Brief 3 pre-fix. Esto es consecuencia correcta del fix, no
  regresiÃ³n.
- Los kill switches del run post-fix no ejercitaron el path del
  broadcast. Cobertura del path por tests unitarios, no por backtest
  end-to-end. Aceptado.

**Negativas / hallazgos pendientes:**
- Cambios fuera del brief original se aplicaron en el loop autÃ³nomo de
  Claude Code sin consulta. La prÃ¡ctica futura debe ser pausar y
  consultar ante adyacencias no triviales. Registrado como nota de
  proceso.

### Riesgo residual

- Si en el futuro se agrega otra fuente de liquidaciÃ³n (margin call
  simulado, kill switch nuevo, intervenciÃ³n manual), tiene que usar
  `LiquidateInstrument` (o el patrÃ³n equivalente con `OrderRegistry`),
  NO una llamada directa al broker. El fix elimina `LiquidateAll()`
  precisamente para forzar esa disciplina.
- Si en el futuro se permite mÃ¡s de un executor por instrumento (lo que
  el allocator multi-estrategia eventualmente habilitarÃ¡), el broadcast
  del DecisiÃ³n 5 enviarÃ¡ el close a todos los executors del instrumento.
  Esto puede ser correcto o no segÃºn el diseÃ±o del allocator â€”
  revisitar entonces.

---

## ADR-027 â€” Re-entrenamiento de BTC con trainer multi-seed (alineaciÃ³n post-DEUDA-1)
**Fecha:** 2026-05-26
**Estado:** Aceptada

### Contexto

Al abrir sesiÃ³n multi-sÃ­mbolo (entrenamiento de ETHUSDT y TRBUSDT con OpciÃ³n A â€” un HMM por sÃ­mbolo), se ejecutÃ³ verificaciÃ³n de no-regresiÃ³n del refactor de parametrizaciÃ³n del `HmmTrainer`. La verificaciÃ³n revelÃ³ que el archivo `Trading.Models/regime/BTCUSDT-perp-binance.hmm.json` (`TrainedAtUtc = 2026-05-19T15:36:48Z`) precedÃ­a al commit `6f72dcc` (DEUDA-1, multi-seed Baum-Welch, 2026-05-22) y por lo tanto fue generado con el trainer single-seed pre-DEUDA-1. ADR-024 habÃ­a documentado explÃ­citamente la decisiÃ³n de NO re-entrenar tras DEUDA-1 para preservar el baseline de ADR-023 (6 Ã³rdenes).

Esa decisiÃ³n queda invÃ¡lida ahora porque entrenar ETH y TRB con el trainer actual (multi-seed) genera una flota inconsistente: BTC pre-DEUDA-1, alts post-DEUDA-1. Las clasificaciones de rÃ©gimen entre sÃ­mbolos dejan de ser conceptualmente comparables.

### DecisiÃ³n

Re-entrenar BTC con el trainer multi-seed actual. Reemplazar `Trading.Models/regime/BTCUSDT-perp-binance.hmm.json` por el modelo nuevo. Conservar el modelo viejo como `BTCUSDT-perp-binance.hmm.json.preDEUDA1` en el mismo directorio para evidencia histÃ³rica.

### Resultados del re-entrenamiento

- **K seleccionado:** 4
- **BIC final:** 57643.8833 (preDEUDA1: 57643.9366 â€” multi-seed encontrÃ³ Ã³ptimo local marginalmente mejor)
- **Mapping semÃ¡ntico:** `{0:Trend, 1:Trend, 2:Squeeze, 3:HighVolatility}`
- **ValidaciÃ³n granular ventana 3** (`ProductionHmmGranularQueryTests`): crash de Feb 3 sigue clasificado como `Trend` en las 12 barras 4h del perÃ­odo 2025-02-03â†’04 âœ“
  - Las 6 barras del 2025-02-02 aparecen como `Squeeze` (igual que con el modelo preDEUDA1 â€” comportamiento correcto: el mercado entrÃ³ en compresiÃ³n el dÃ­a previo al crash)

### Resultados del backtest BTC-15m post-re-entrenamiento

El modelo nuevo produce resultados bit-idÃ©nticos al baseline de ADR-026:

| MÃ©trica            | preDEUDA1 (ADR-026) | postDEUDA1 (nuevo) |
|--------------------|---------------------|--------------------|
| Total Orders       | 147                 | 147                |
| End Equity (USDT)  | 87148               | 87148.16           |
| Net Profit         | -12.85%             | -12.852%           |
| Max Drawdown       | 21.5%               | 21.500%            |
| Win Rate           | 32%                 | 32%                |
| P/L Ratio          | 1.42                | 1.42               |
| Sharpe             | -1.288              | -1.288             |
| U2 dispara         | 2025-02-06          | 2025-02-06         |
| OPS-2 inv. violado | 0                   | 0                  |
| Evento sin tag     | 0                   | 0                  |

La identidad de resultados se explica por la invarianza semÃ¡ntica: aunque los Ã­ndices numÃ©ricos de los estados difieren (permutaciÃ³n entre runs), el mapeo semÃ¡ntico produce exactamente las mismas clasificaciones de rÃ©gimen sobre el perÃ­odo 2025-01-01â†’2026-03-31, por lo tanto las mismas seÃ±ales de entrada, las mismas Ã³rdenes, el mismo equity curve. Los criterios cualitativos del subsistema se confirman verdes.

**Equivalencia conductual confirmada al nivel de order list.** Backtest comparativo ejecutado con modelo preDEUDA1 restaurado temporalmente. Resultado: 82 fills idÃ©nticos (timestamp, fillPrice, fillQuantity) y 65 cancels idÃ©nticos (orderId, timestamp) entre ambos modelos. El `OrderListHash` de Lean no es un comparador fiable entre corridas â€” varÃ­a entre runs del mismo modelo por no-determinismo interno del motor â€” pero la comparaciÃ³n directa de order-events.json confirma equivalencia conductual completa: el modelo nuevo y el preDEUDA1 son indistinguibles para el backtest BTC-15m del perÃ­odo 2025-01-01 â†’ 2026-03-31 al nivel de ejecuciÃ³n de Ã³rdenes.

### Consecuencias

**Positivas:**
- Flota de modelos HMM consistente: BTC, y prÃ³ximamente ETH y TRB, todos generados por el mismo pipeline multi-seed.
- El archivo de modelo queda alineado con el cÃ³digo que lo genera. Re-correr el trainer reproduce el archivo (mÃ³dulo `TrainedAtUtc`).
- Cierre operativo de la deuda implÃ­cita "modelo en disco desincronizado del trainer" que existÃ­a desde 2026-05-22.

**Neutras / aceptadas:**
- El baseline de ADR-023 (6 Ã³rdenes) y de ADR-026 (147 Ã³rdenes BTC-15m) quedan tÃ©cnicamente invalidados por cambio de modelo, pero los nuevos nÃºmeros son idÃ©nticos: el baseline numÃ©rico de no-regresiÃ³n BTC-15m es el documentado en este ADR.
- Si el modelo nuevo cambia clasificaciones de barras borde, las estrategias operan sobre decisiones de filtro marginalmente distintas. En la prÃ¡ctica no ocurriÃ³: el backtest es bit-idÃ©ntico.

**Negativas / hallazgos pendientes:**
- DEUDA-1 inspeccionÃ³ 5 ventanas histÃ³ricas con el modelo preDEUDA1. Solo la ventana 3 quedÃ³ con test granular automatizado. Las otras 4 se documentaron informalmente y no se re-validan automÃ¡ticamente con este re-entrenamiento. MitigaciÃ³n: el delta de BIC entre modelos es marginal (9Â·10â»â· relativo) y el backtest de 15 meses es bit-idÃ©ntico, por lo que re-clasificaciÃ³n sustantiva de las otras 4 ventanas es muy improbable.

### Riesgo residual

- El test granular cubre solo la ventana 3 (crash feb 2025). Si en el futuro se descubre que el modelo nuevo clasifica mal alguna otra ventana histÃ³rica relevante, el modelo puede compararse con el `preDEUDA1` conservado para diagnÃ³stico.
- POLICY 7.1 estÃ¡ titulada "EmaCrossStrategy/BTCUSDT/1h" pero el backtest de referencia corre a 15m. Discrepancia de documentaciÃ³n identificada en ADR-026, no resuelta en este brief.

---

## ADR-026 â€” ValidaciÃ³n multi-timeframe del subsistema de ejecuciÃ³n/monitoreo sobre BTCUSDT
**Fecha:** 2026-05-26
**Estado:** Aceptada

### Contexto

ADR-025 cerrÃ³ los bugs acoplados del subsistema de ejecuciÃ³n/monitoreo (OPS-2 invariante violado, U1 con DD falso, tag vacÃ­o en cancels) sobre el backtest de referencia `EmaCrossStrategy_BTCUSDT_15m`. La validaciÃ³n quedÃ³ constreÃ±ida a ese Ãºnico timeframe. El sistema fue construido con la intenciÃ³n declarada de ser agnÃ³stico al timeframe â€” el wiring extrae el TF de `strategies.json`, `StrategyExecutor.ExecutorIdentifier` lo incorpora como sufijo automÃ¡tico, y los consolidators se construyen per-TF en `TradingAlgorithmHost`. Pero "diseÃ±ado para ser agnÃ³stico" y "verificado como agnÃ³stico" son afirmaciones distintas, y la segunda no estaba hecha.

Adicionalmente, durante el anÃ¡lisis previo a esta validaciÃ³n se identificÃ³ que `BarProcessingService.ProcessBar` chequea `IPortfolioState.IsInvested(instrumentId)` para bloquear nuevas entradas, lo cual implementa la decisiÃ³n de "una posiciÃ³n por sÃ­mbolo a la vez" (decisiÃ³n de diseÃ±o del operador, no bug). Esa decisiÃ³n hace inviable correr mÃºltiples executors del mismo sÃ­mbolo en paralelo para esta validaciÃ³n: los executors competirÃ­an por la Ãºnica posiciÃ³n permitida y el resultado serÃ­a ruido del acoplamiento, no datos limpios del subsistema bajo prueba.

### DecisiÃ³n

Validar agnosticismo al timeframe del subsistema mediante **tres backtests secuenciales sobre BTCUSDT**, un Ãºnico timeframe activo por backtest, mismo perÃ­odo (2025-01-01 â†’ 2026-03-31), con `EmaCrossStrategy` (estrategia vetada para live por POLICY P1, usada exclusivamente como instrumento de validaciÃ³n de infraestructura).

**ParÃ¡metros por timeframe**, derivados de la heurÃ­stica "aislar el TF como Ãºnica variable":

| ParÃ¡metro              | 15m  | 1h  | 4h  | JustificaciÃ³n                                                            |
|------------------------|------|-----|-----|--------------------------------------------------------------------------|
| StopLossPercentage     | 1.0  | 2.0 | 4.0 | Escala Ã—2 por step de TF para no disparar SL por ruido intra-bar.        |
| TakeProfitPercentage   | 2.0  | 4.0 | 8.0 | Mantiene R:R = 1:2 idÃ©ntico en los tres TFs.                              |
| RiskPerTradePercentage | 2.0  | 2.0 | 2.0 | PolÃ­tica de portfolio, no escala con TF.                                  |
| MaxBars                | 20   | 20  | 20  | Unidad natural de la estrategia; aislar TF como Ãºnica variable.          |
| CombineWithTimeExit    | true | true| true| Heredado de 15m sin cambio.                                              |
| CompatibleRegimes      | Trend| Trend| Trend| El clasificador HMM es 4h global, independiente del TF de la estrategia. |

**Criterios de aceptaciÃ³n** (todos exigidos a cumplirse en los tres backtests):

- Cero ocurrencias de `OPS-2 invariante violado`.
- Cero ocurrencias de `OrderEventMapper: evento sin tag` durante TimeExit/Liquidate dirigido (LiquidateAll/kill switch exceptuado por diseÃ±o).
- Si U1 o U2 disparan, lo hacen con DD real coherente con POLICY 3.1 â€” no falsos positivos.
- `ExecutorIdentifier` Ãºnico bien etiquetado por TF en logs.
- Config A (15m) ademÃ¡s debe reproducir la baseline numÃ©rica de ADR-025 (147 Ã³rdenes, end equity 87.148 USDT, DD 21.5%, U2 dispara 2025-02-06) como smoke test de no-regresiÃ³n.

### Resultados

| MÃ©trica            | 15m (A)    | 1h (B)     | 4h (C)     |
|--------------------|------------|------------|------------|
| Total Orders       | 147        | 116        | 28         |
| End Equity (USDT)  | 87.148     | 86.676     | 100.879    |
| Net Profit         | -12.85%    | -13.32%    | +0.88%     |
| Max Drawdown       | 21.5%      | 19.4%      | 8.6%       |
| Win Rate           | 32%        | 41%        | 38%        |
| P/L Ratio          | 1.42       | 0.77       | 1.92       |
| Sharpe             | -1.288     | -1.689     | -0.825     |
| U2 dispara         | 2025-02-06 | 2025-10-20 | No dispara |
| OPS-2 inv. violado | 0          | 0          | 0          |
| Evento sin tag     | 0          | 0          | 0          |

**Config A** reprodujo exactamente la baseline de ADR-025. Sin regresiÃ³n.

**Configs B y C** cumplieron todos los criterios cualitativos. U2 disparÃ³ en 1h con DD rolling 30d 16.80% sostenido 5 dÃ­as el 2025-10-20 â€” desplazamiento esperado del disparo respecto a 15m porque SL/TP escalados absorben el crash de febrero 2025 que en 15m habÃ­a costado el 21.5% de DD. En 4h, ningÃºn umbral cruzÃ³: DD mÃ¡ximo 8.6%, lejos del 15% rolling de U2 y muy lejos del 25% absoluto de U1.

**ConclusiÃ³n:** el subsistema de ejecuciÃ³n/monitoreo es agnÃ³stico al timeframe sobre BTCUSDT. Los fixes introducidos en ADR-025 operan correctamente en los tres TFs sin cÃ³digo adicional.

### Alternativas consideradas

**A â€” Tres timeframes en paralelo en un solo backtest.** Plan inicial de la sesiÃ³n. Descartado al auditar `BarProcessingService.ProcessBar`: la regla "una posiciÃ³n por sÃ­mbolo" hace que executors del mismo sÃ­mbolo compitan por la Ãºnica posiciÃ³n permitida. El resultado serÃ­an mÃ©tricas dominadas por quiÃ©n ganÃ³ la carrera de entrada, no por el comportamiento del subsistema. La validaciÃ³n serÃ­a ruidosa hasta el punto de inservible.

**B â€” Multi-timeframe cross-sÃ­mbolo (15m BTC + 1h ETH + 4h SOL, por ejemplo).** HabrÃ­a eludido la regla del sÃ­mbolo compartido. Descartado para esta sesiÃ³n por dos razones acopladas: (1) el clasificador HMM estÃ¡ entrenado y operativo solo sobre BTCUSDT; activar la estrategia en otros sÃ­mbolos harÃ­a que el filtro de rÃ©gimen no aplique (Unknown â†’ fail-safe â†’ seÃ±ales pasan), y la mÃ©trica entre TFs/sÃ­mbolos dejarÃ­a de ser comparable; (2) habrÃ­a requerido decidir antes si entrenar HMM por sÃ­mbolo o deshabilitar el filtro explÃ­citamente, decisiones de diseÃ±o que merecen tratarse al inicio de su propia sesiÃ³n, no embutidas en una validaciÃ³n de agnosticismo al TF.

**C â€” Secuencial sobre BTCUSDT (elegida).** Limpia, ejecutable hoy, valida el criterio declarado de agnosticismo al TF sin acoplarlo a decisiones pendientes sobre multi-sÃ­mbolo. Trade-off explÃ­citamente aceptado: no exhibe paralelismo entre executors. Eso es objetivo legÃ­timo de una sesiÃ³n futura, no de esta.

### Consecuencias

**Positivas:**
- El subsistema queda formalmente validado como agnÃ³stico al TF sobre BTCUSDT. Los fixes de ADR-025 son robustos en 15m, 1h, 4h.
- Hay baseline numÃ©rica de referencia para 1h y 4h sobre BTCUSDT â€” si en el futuro se introducen cambios al subsistema, esos nÃºmeros sirven para detectar regresiÃ³n por TF, no solo por 15m.
- El comportamiento de U2 en 1h (2025-10-20) es punto de referencia adicional al de 15m (2025-02-06) para futuras pruebas del monitor: dos eventos reales de degradaciÃ³n legÃ­tima, con DDs conocidos.
- POLICY 3.1 queda evidenciada operando correctamente en 1h ademÃ¡s de 15m (en 4h no hubo evento que la ejercitara).

**Neutras / aceptadas:**
- En 1h el P/L ratio es 0.77, sustancialmente peor que 15m (1.42) y 4h (1.92). Evidencia experimental de que `MaxBars=20` constante penaliza mÃ¡s al 1h por ventana de tiempo absoluto corta (20 horas vs 5 horas en 15m y 80 horas en 4h) â€” los winners cierran por TimeExit antes de llegar al TP escalado. Esto NO es problema del sistema; es comportamiento de la estrategia bajo la decisiÃ³n deliberada de aislar el TF como Ãºnica variable. Si en el futuro se quisiera optimizar la estrategia por TF, `MaxBars` escalado es candidato natural â€” fuera de alcance acÃ¡.
- La estrategia `EmaCrossStrategy` sigue vetada para live por POLICY P1. NingÃºn resultado positivo en 4h (+0.88%) cambia eso. Una corrida Ãºnica sobre un perÃ­odo acotado no es evidencia de edge.

**Negativas / hallazgos pendientes (no resueltos por este ADR):**
- **POLICY 7.1 estÃ¡ titulada `EmaCrossStrategy / BTCUSDT / 1h`** pero el sistema corre 15m. Coherente con el bug del JSON espontÃ¡neo de la sesiÃ³n anterior (`strategies.json` cambiÃ³ de 15m a 1h sin intervenciÃ³n del operador, causa raÃ­z no auditada). Esta sesiÃ³n cerrÃ³ restaurando Config A (15m). La discrepancia entre POLICY 7.1 y el estado real persiste hasta que ese bug se audite.
- **No existe ADR formal documentando "una posiciÃ³n por sÃ­mbolo"** como decisiÃ³n arquitectÃ³nica. La regla estÃ¡ implementada en `BarProcessingService.ProcessBar` chequeando `IPortfolioState.IsInvested(instrumentId)` agregado, pero no aparece en este registro con nÃºmero correlativo previo. Cuando se aborde multi-estrategia real, conviene documentarla retroactivamente o emitir nuevo ADR que la relaje/preserve explÃ­citamente.
- **PrecondiciÃ³n conocida para multi-timeframe cross-sÃ­mbolo:** decidir el tratamiento del filtro de rÃ©gimen en sÃ­mbolos no-BTC (entrenar HMM por sÃ­mbolo vs deshabilitarlo explÃ­citamente vs declararlo opt-in en `strategies.json`). Trigger sugerido: inicio de la sesiÃ³n que aborde multi-sÃ­mbolo.
- **TODO ya conocido**: el `InitialAccountCashUsdt = 100_000` se pasa idÃ©ntico a cada executor (`TradingAlgorithmHost`). Al haber un solo executor activo por backtest en esta validaciÃ³n, la distorsiÃ³n es nula. Persiste como precondiciÃ³n de allocator multi-estrategia (ya marcado en ADR-025).

### Riesgo residual

- La validaciÃ³n se hizo sobre un perÃ­odo Ãºnico (2025-01-01 â†’ 2026-03-31) y un Ãºnico sÃ­mbolo (BTCUSDT). Comportamiento del subsistema en perÃ­odos de rÃ©gimen significativamente distinto (ej. mercado lateral prolongado en TF alto) no estÃ¡ cubierto. MitigaciÃ³n: el subsistema no toma decisiones basadas en el rÃ©gimen â€” solo el filtro pre-orden lo hace, y ese filtro estÃ¡ fuera del alcance de esta validaciÃ³n. La probabilidad de que un perÃ­odo distinto exhiba bugs especÃ­ficos del subsistema de ejecuciÃ³n/monitoreo no cubiertos por esta validaciÃ³n es baja.
- La afirmaciÃ³n "agnÃ³stico al TF" se sostiene sobre tres TFs muestreados (15m, 1h, 4h). TFs no probados (1m, 5m, 30m, 1d) podrÃ­an exhibir comportamientos no observados â€” por ejemplo, 1m podrÃ­a exponer problemas de granularidad temporal en el clock del monitor que TFs mÃ¡s lentos enmascaran. Si en algÃºn momento se activa un TF no probado, conviene revalidar con el mismo protocolo.

---

## ADR-025 â€” LiquidateInstrument explÃ­cito y base de equity correcta en StrategyHealthMonitor
**Fecha:** 2026-05-25
**Estado:** Aceptada

### Contexto

Backtests de `EmaCrossStrategy_BTCUSDT_15m` (ene 2025 - mar 2026) lanzaron `OPS-2 invariante violado: Entry con posiciÃ³n ya abierta` de forma masiva, junto con disparos de U1 con DD absolutos imposibles (186 %, 26 %) sobre cuentas que apenas se habÃ­an movido. La estrategia degradaba a los pocos dÃ­as, dejando el resto del backtest sin operar y los reportes inservibles.

El diagnÃ³stico (logs DIAG temporales en `OrderEventMapper`, `OrderLifecycleService`, `StrategyHealthMonitor`) revelÃ³ tres bugs distintos pero acoplados:

**Bug 1 - Tag reusado por `_algorithm.Liquidate(symbol, tag)`.** Lean reutiliza el mismo tag cliente para las tres acciones que dispara: cancel SL, cancel TP, MarketOrder de cierre. El `OrderEventMapper` procesaba el primer evento (cancel SL), hacÃ­a `Forget(tag)`, y descartaba los siguientes como "residuales esperados" - incluido el `Filled` del MarketOrder que SÃ habÃ­a cerrado la posiciÃ³n real. El monitor quedaba con `_openPositions[id]` no nulo y la prÃ³xima Entry violaba el invariante OPS-2.

**Bug 2 - Equity base 0 en `StrategyHealthMonitor`.** `_equity[id]` y `_ath[id]` arrancaban en cero; acumulaban sÃ³lo PnL realizado. La fÃ³rmula U1 `(ATH - equity) / ATH` daba porcentajes enormes cuando el primer trade era winner y el segundo era loser modesto. Ejemplo real: trade winner +2814, trade loser -461 â†’ DD calculado 26 % sobre una cuenta que cayÃ³ 0,45 %. U2 sufrÃ­a el mismo bug por compartir la serie.

**Bug 3 - Tag vacÃ­o en `Transactions.CancelOrder(id)`.** Lean propaga el evento `Canceled` con `OrderTicket.Tag` vacÃ­o cuando se cancela por esa vÃ­a. El mapper loguea `ERROR: evento sin tag` y los handles SL/TP del executor no reciben su evento Canceled (cosmÃ©tico - el OnFilled del cierre real sÃ­ se publica al monitor con su tag nuevo, asÃ­ que el invariante OPS-2 ya no se viola, pero el log queda contaminado).

### DecisiÃ³n

**Fix 1 - `LeanOrderRouter.LiquidateInstrument` explÃ­cito.** Reemplazado el uso de `_algorithm.Liquidate(symbol, tag)` por una secuencia controlada:

1. Cancelar las Ã³rdenes abiertas del sÃ­mbolo invocando `OrderTicket.Cancel()` directamente (preserva el tag original). Cada evento Canceled llega al mapper con su tag propio (registrado como `StopLoss`/`TakeProfit`), se procesa, hace `Forget` y notifica al executor.
2. Leer `IPortfolioState.GetPositionQuantity(instrumentId)`. Si la posiciÃ³n real es distinta de cero, enviar un `MarketOrder` con tag nuevo registrado bajo el `Purpose` solicitado (tÃ­picamente `TimeExit`). Su `Filled` se publica al monitor con la semÃ¡ntica correcta.

Si no hay posiciÃ³n real (caso defensivo cuando SL/TP ya cerraron antes del Liquidate), no se envÃ­a MarketOrder; sÃ³lo se cancelan las Ã³rdenes abiertas residuales.

**Fix 2 - `IPortfolioState` expone `GetPositionQuantity(InstrumentId)`.** Nuevo mÃ©todo en la abstracciÃ³n del dominio. `LeanPortfolioAdapter` lo implementa con `_algorithm.Portfolio[symbol].Quantity`. Necesario para construir el MarketOrder de cierre sin depender de `Liquidate()`.

**Fix 3 - `StrategyHealthMonitor` arranca equity y ATH en capital atribuido.** El constructor recibe un parÃ¡metro nuevo `decimal initialEquityPerStrategy` (validado > 0). `EnsureBuckets` inicializa `_equity[id]` y `_ath[id]` en ese valor en lugar de cero. La fÃ³rmula de U1 `(ATH - equity) / ATH` queda intacta; ahora opera sobre equity de cuenta y no sobre PnL crudo. U2, U3 y U4 tambiÃ©n se benefician sin cambios adicionales.

En `TradingAlgorithmHost`, el cash inicial se extrajo a constante `InitialAccountCashUsdt = 100_000m` y se pasa al monitor. `Portfolio.TotalPortfolioValue` no se puede usar en `Initialize()` porque devuelve 0 hasta que Lean completa la configuraciÃ³n de cuenta. Mientras haya UNA estrategia activa por backtest, `InitialAccountCashUsdt` representa todo el capital atribuido a esa estrategia. Cuando exista allocator multi-estrategia, ese parÃ¡metro se atribuye por executor; queda marcado como TODO en el wiring.

### Alternativas consideradas

**A - Parchear `OrderEventMapper` para no hacer `Forget` en Canceled de TimeExit y esperar el Filled del MarketOrder.** Descartada: requiere el mapper distinguir si el tag corresponde a un Liquidate combinado (cancel+market) o a un cancel "puro" (ej. cancelar StopLoss tras TakeProfit hit), informaciÃ³n que el mapper no tiene. AdemÃ¡s, si la posiciÃ³n ya estaba cerrada por SL/TP antes del Liquidate, no se genera MarketOrder y el Filled nunca llegarÃ­a, dejando el tag colgado.

**B - Mantener `_algorithm.Liquidate(symbol)` global y resolver el tag reusado dentro del mapper.** Descartada por la misma razÃ³n: cualquier soluciÃ³n que mantenga `Liquidate` arrastra la ambigÃ¼edad semÃ¡ntica del tag compartido entre N acciones.

**C (elegida) - Cancelaciones explÃ­citas + MarketOrder con tag propio.** MÃ­nima carga conceptual sobre el mapper: una orden = un tag = un ciclo de vida. La complejidad del Liquidate vive en una sola capa (`LeanOrderRouter`) y no se filtra al dominio.

**D - Para el bug 2, inyectar `IPortfolioState` en `StrategyHealthMonitor` y leer `TotalPortfolioValue` en cada fill.** Descartada: el monitor estÃ¡ diseÃ±ado para ser estrategia-aislado (ADR-023). Cuando exista allocator multi-estrategia, `TotalPortfolioValue` no representa el equity atribuido a una estrategia individual. AdemÃ¡s, agregar dependencia con el portfolio en la capa Application complica los tests sin beneficio en la fase actual.

**E (elegida para el bug 2) - ParÃ¡metro `initialEquityPerStrategy` en el constructor.** Mantiene la abstracciÃ³n intacta, atribuye capital explÃ­citamente al monitor, y el contrato queda preparado para allocator multi-estrategia sin reescribir el componente.

### Consecuencias

- OPS-2 invariante violado: cerrado. Backtest 15m de 14 meses ahora corre limpio.
- U1 y U2 disparan Ãºnicamente con DD reales contra el equity de cuenta.
- Logs `OrderEventMapper: evento sin tag` durante TimeExit/Liquidate: eliminados. La lÃ­nea sigue activa para detectar liquidaciones globales y Ã³rdenes externas, que es su propÃ³sito original.
- `IPortfolioState` extendido con `GetPositionQuantity`. Cambio aditivo, no breaking. Fake en tests actualizado.
- `LeanOrderRouter` constructor recibe `IPortfolioState` y `ITradingLogger` adicionales.
- `StrategyHealthMonitor` constructor recibe `decimal initialEquityPerStrategy`. Cambio breaking en el wiring del host y en los tests; resuelto en una sola pasada.
- `TradingAlgorithmHost.InitialAccountCashUsdt` quedÃ³ como constante de clase. Cambios futuros al cash inicial requieren editar un Ãºnico punto.
- Suite de tests creciÃ³ a 121 verdes (de 97 previos). Los nuevos cubren la base de equity correcta del monitor.
- Backtest `EmaCrossStrategy_BTCUSDT_15m` ene 2025 - mar 2026: 147 Ã³rdenes, end equity 87.148 USDT, DD 21.5 %, U2 dispara correctamente el 06/02/2025 con DD rolling 18.6 % sostenido 5 dÃ­as, y la estrategia queda degradada para el resto del backtest. POLICY 3.1 se cumple sin cÃ³digo nuevo.
- `EmaCrossStrategy_BTCUSDT_15m` confirma su loss rate de 68 % y expectancy negativa: la estrategia no es viable con esos parÃ¡metros, lo cual es consistente con su veto en POLICY P1 (sin walk-forward, no va a live).
- ADR-023 sigue vigente en su diseÃ±o; este ADR documenta correcciones de implementaciÃ³n. Se agrega nota cruzada.

### Riesgo residual

- El comportamiento de `OrderTicket.Cancel()` vs `Transactions.CancelOrder(id)` respecto al tag depende del runtime de Lean. Si una actualizaciÃ³n de Lean cambia el comportamiento, los tests no lo detectarÃ­an porque corren contra un fake. MitigaciÃ³n: el test de integraciÃ³n con backtest real (que existe en CI) detectarÃ­a regresiones por el sÃ­ntoma de logs `evento sin tag` durante Liquidate.
- Cuando exista allocator multi-estrategia, el TODO en `TradingAlgorithmHost` requiere actualizaciÃ³n. Trigger: implementaciÃ³n del Hito que introduzca mÃºltiples estrategias activas en paralelo.

---

## ADR-024 â€” SemanticStateMapper adaptativo a K + multi-seed Baum-Welch (resuelve ADR-020)
**Fecha:** 2026-05-22
**Estado:** Aceptada

### Contexto

ADR-020 documentÃ³ como deuda tÃ©cnica el test `AccordHmmClassifierReferenceTests.Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente` skipeado por convergencia degenerada con K=3 sobre serie sintÃ©tica. ADR-020 enumerÃ³ tres hipÃ³tesis de causa raÃ­z y un plan de diagnÃ³stico. Este ADR documenta la resoluciÃ³n.

### Causa raÃ­z identificada

**HipÃ³tesis B confirmada (causa principal):** bug en `SemanticStateMapper.Build` al calcular cuartiles con K pequeÃ±o. Con K=3, `Math.Ceiling(3 * 0.75) = 3`, lo cual hace la condiciÃ³n `positionInSorted >= 3` insatisfacible en un array de 3 elementos (posiciones vÃ¡lidas: {0, 1, 2}). Resultado: ningÃºn estado se mapeaba a `HighVolatility` con K=3.

**HipÃ³tesis A confirmada (causa adicional):** Baum-Welch convergÃ­a a un Ã³ptimo local malo con seed=42 sobre datos sintÃ©ticos extremos, donde dos estados colapsaban a parÃ¡metros casi idÃ©nticos. Evidenciado por la matriz de transiciÃ³n y las estadÃ­sticas por estado.

**HipÃ³tesis C descartada:** el `FeatureScaler` preserva las diferencias entre regÃ­menes; las features del segmento HighVolatility sÃ­ se distinguen de los otros en el espacio escalado.

**Modelo de producciÃ³n (K=4) no afectado por HipÃ³tesis B:** con K=4, `Ceiling(4 * 0.75) = 3`, y la posiciÃ³n 3 sÃ­ existe en el array de 4 elementos. El fix se aplica preventivamente para K<4 y por robustez arquitectÃ³nica.

### DecisiÃ³n

**Fix 1 â€” `SemanticStateMapper.Build` adaptativo a K:**
- K=2: estado de Ïƒ mayor â†’ HighVolatility candidato; el otro evalÃºa Trend/Squeeze/MeanReverting por sus reglas estÃ¡ndar de Î¼ y Ï.
- K=3: tercios â€” Ãºltima posiciÃ³n ordenada por Ïƒ â†’ HighVolatility, primera posiciÃ³n (si Ï > 0.7) â†’ Squeeze.
- K>=4: cuartiles tradicionales (sin cambios respecto a la versiÃ³n anterior).
- Reglas comunes y caso degenerado: sin cambios.

**Fix 2 â€” Multi-seed Baum-Welch en `HmmTrainer` y en el test sintÃ©tico:**
- Entrenar el HMM 10 veces con seeds `42 * i + 17` (i âˆˆ {1..10}), conservar el modelo de mayor log-likelihood.
- Aplicado al trainer offline y al test de referencia; el runtime carga el modelo serializado sin cambios.

**DecisiÃ³n sobre el modelo de producciÃ³n:** validaciÃ³n cruzada del modelo de producciÃ³n contra 5 ventanas histÃ³ricas de BTCUSDT (2025-2026) fue OK. El modelo distingue correctamente rÃ©gimen de HighVolatility (volatilidad caÃ³tica sin direcciÃ³n: Ïƒ alto + Î¼ â‰ˆ 0) de Trend (movimientos direccionales fuertes, incluyendo crashes direccionales). Esta distinciÃ³n es consistente con la literatura de regime-switching (Hamilton 1989, Ang-Bekaert 2002) y operativamente Ãºtil para estrategias direccionales. El modelo no requiere re-entrenamiento. El baseline de 6 Ã³rdenes (ADR-023) se preserva.

**DefiniciÃ³n operativa de RegimeLabel.HighVolatility (consensuada en validaciÃ³n cruzada de Fase 4):** el modelo reserva `HighVolatility` para volatilidad caÃ³tica sin direcciÃ³n dominante (estado con Ïƒ alto + Î¼ â‰ˆ 0 en la emisiÃ³n). Los crashes y rallies direccionales fuertes se clasifican como `Trend` incluso con ATR elevado, porque el HMM detecta el momentum sostenido en la emisiÃ³n. La gestiÃ³n de riesgo en crashes direccionales se delega a stops/sizing/POLICY, no al clasificador de rÃ©gimen.

### Alternativas consideradas

**A â€” Eliminar el test sintÃ©tico en lugar de hacerlo pasar.** Descartada: el test detectÃ³ un bug arquitectÃ³nico real (`SemanticStateMapper` no adaptativo a K). Eliminar el test enmascararÃ­a el problema.

**B â€” Forzar K=4 mÃ­nimo en todos los entrenamientos para evitar el caso degenerado de K=3.** Descartada: K se elige por BIC sobre los datos. Forzar K mÃ­nimo serÃ­a sobreajustar a la heurÃ­stica del cuartil en lugar de corregir la heurÃ­stica.

**C â€” Refactor profundo de `SemanticStateMapper` con clustering jerÃ¡rquico.** Descartada por overengineering.

**D (elegida) â€” AdaptaciÃ³n de la heurÃ­stica existente a K.** MÃ­nimo cambio que resuelve el bug sin alterar el contrato serializado entre trainer y runtime.

### Consecuencias

- El test `Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente` pasa verde (sin `[Fact(Skip)]`).
- `SemanticStateMapperTests` recibe 5 tests adicionales cubriendo K=2, K=3 (caso del bug), K=3 con Squeeze, K=4, K=5.
- El trainer offline ahora ejecuta 10 pasadas de Baum-Welch en lugar de 1. Tiempo de entrenamiento ~10x mÃ¡s lento; aceptable porque es offline y poco frecuente.
- El modelo de producciÃ³n actual (K=4) se mantiene; el baseline de no-regresiÃ³n de 6 Ã³rdenes (ADR-023) se preserva.
- ADR-020 pasa a estado "Resuelta en ADR-024 (2026-05-22)".
- `ProductionHmmGranularQueryTests.cs` se commitea como evidencia durable de la validaciÃ³n cruzada y queda reutilizable para consultas granulares futuras al modelo (referenciado en Hito G).

### Riesgo residual

- La validaciÃ³n cruzada se hizo con 5 ventanas seleccionadas; la inspecciÃ³n humana semanal (POLICY secciÃ³n 4) durante paper trading va a producir seÃ±ales si el modelo se comporta de forma incoherente en operaciÃ³n real.
- Las reglas adaptativas por K son heurÃ­sticas. Si en el futuro se entrenan modelos con K>=5 o con dimensionalidad de features distinta, puede requerirse re-calibraciÃ³n. Trigger sugerido: si la diversidad de etiquetas asignadas en un modelo nuevo es <2, revisar el mapper.

---

## ADR-023 â€” StrategyHealthMonitor: componente autÃ³nomo fuera del array de IRiskMonitor del orchestrator
**Fecha:** 2026-05-21
**Estado:** Aceptada

### Contexto

POLICY secciÃ³n 3 exige liquidaciÃ³n dirigida y exclusiÃ³n de la estrategia ante degradaciÃ³n por mÃ©tricas individuales (umbrales U1-U4). El refactor #4 produjo un `RiskOrchestrator` que gestiona un array de `IRiskMonitor`; cuando cualquiera dispara, ejecuta `LiquidateAll` + cooling-off de 24h compartido. Forzar el `StrategyHealthMonitor` al array de `IRiskMonitor` romperÃ­a la semÃ¡ntica de POLICY: la degradaciÃ³n de una estrategia individual no debe parar el sistema entero ni meterlo en cooling-off global. Hay precedente en ADR-017: el filtro de rÃ©gimen no va por `IRiskMonitor` por la misma razÃ³n conceptual ("rechazar seÃ±al especÃ­fica" â‰  "liquidar todo").

### DecisiÃ³n

- `StrategyHealthMonitor` es un componente autÃ³nomo en `Trading.Application/Health/`. No implementa `IRiskMonitor`.
- Se suscribe a `OrderFilledEvent` en su constructor (mismo patrÃ³n que `HealthHeartbeatTracker`). Handler sÃ­ncrono bajo lock interno.
- Mantiene mÃ©tricas rolling por `ExecutorIdentifier`: equity acumulado, ATH, ventana de 30 trades cerrados, ventana de 30 puntos diarios de equity, contadores de dÃ­as/trades sostenidos para U2/U3/U4.
- Al cruzar cualquier umbral U1-U4 de POLICY 3.1: (1) llama `IOrderRouter.LiquidateInstrument` si hay posiciÃ³n abierta en ese instante, (2) setea flag `degraded`, (3) publica `RiskLimitBreachedEvent` con razÃ³n `StrategyDegradation`, (4) loguea `Critical`.
- `BarProcessingService` consulta `IStrategyHealthMonitor.IsExcluded(executorIdentifier)` como guard pre-seÃ±al, anÃ¡logo al filtro de rÃ©gimen (ADR-017). Posicionado despuÃ©s del guard de kill switch global y antes del filtro de rÃ©gimen.
- Umbrales en `StrategyHealthThresholds` (POCO inmutable) con factory `FromPolicyDefaults()` que codifica literalmente POLICY 3.1. Cambio de POLICY â†’ recompilaciÃ³n.
- `IStrategyHealthMonitor` vive en `Trading.Domain/Abstractions/` por la misma razÃ³n que `IMarketRegimeClassifier`: es contrato consumido por Application sin acoplar a la implementaciÃ³n concreta.

### Alternativas consideradas

**A â€” `StrategyHealthMonitor : IRiskMonitor`:** descartada. SemÃ¡ntica incompatible: activarÃ­a `LiquidateAll` + cooling-off global de 24h ante degradaciÃ³n de una estrategia individual. TambiÃ©n obligarÃ­a a refactorizar el orchestrator para dispatch razÃ³nâ†’acciÃ³n con flags por estrategia, ampliando el blast radius del cambio.

**B â€” Componente autÃ³nomo (elegida):** liquidaciÃ³n dirigida vÃ­a `IOrderRouter.LiquidateInstrument(instrumentId, executorIdentifier)` que ya soporta liquidaciÃ³n por estrategia. El `RiskOrchestrator` queda intacto.

**C â€” Persistencia de estado en disco entre reinicios:** descartada para OPS-2 (alcance medio). Aceptada como deuda tÃ©cnica en OPS-3, antes de migrar a live serio (ver ROADMAP Bloque 4).

### Consecuencias

- El concepto "monitor de risk" del proyecto se clarifica: `IRiskMonitor` = kill switch global; otros monitors (rÃ©gimen, salud por estrategia) viven fuera con contratos propios y semÃ¡ntica especÃ­fica.
- Las mÃ©tricas no persisten entre reinicios. Si el proceso reinicia tras 30+ trades, vuelve a warm-up y U3/U4 se rearman tras los prÃ³ximos 50 trades. Aceptable para paper; deuda explÃ­cita antes de live (OPS-3).
- `HealthHeartbeatTracker` (INFRA-2) ya captura `RiskLimitBreachedEvent` por suscripciÃ³n al bus; refleja `StrategyDegradation` sin cÃ³digo nuevo.
- El `RiskOrchestrator` queda intacto. Monitors futuros per-strategy siguen el patrÃ³n OPS-2 (guard en `BarProcessingService`), no el de `IRiskMonitor`.
- En la prÃ¡ctica, `LiquidateInstrument` en el momento del breach nunca tiene posiciÃ³n a liquidar: los breaches se evalÃºan al cerrar trades. La llamada es defensiva para cobertura futura si el flujo evoluciona.
- ExcepciÃ³n para invariantes del monitor: `InvalidOperationException` (no existe `DomainException` base en el proyecto). Si en el futuro se crea una jerarquÃ­a de excepciones de dominio, este componente deberÃ­a migrar.
- **JerarquÃ­a entre POLICY 2 (sistema) y POLICY 3 (estrategia).** POLICY 2.1 (DD global del portfolio > 25% â†’ kill switch global + `LiquidateAll` + cooling-off de 24h) estÃ¡ implementada por `DrawdownMonitor` y sigue activa e independiente. POLICY 3.1 (DD del equity de una estrategia individual > 25% â†’ liquidaciÃ³n dirigida + exclusiÃ³n) la implementa OPS-2 sin interferir con la primera. Una estrategia puede apagarse mientras el portfolio sigue operando otras estrategias; el portfolio puede entrar en kill global aunque ninguna estrategia individual haya disparado U1. Son capas complementarias, no sustitutas. En el run del backtest de OPS-2 (21-05-2026), U1 individual disparÃ³ (DD 61% del equity de la estrategia) mientras el DD global del portfolio terminÃ³ en 2.3% â€” POLICY 3 actuÃ³, POLICY 2 nunca se activÃ³. Comportamiento deseado.
- **Drawdown del equity de una estrategia â‰  pÃ©rdida absoluta en fase warm-up.** POLICY 3.2 define el equity de la estrategia como suma de P&L realizado desde el primer trade, y U1 como DD% desde el ATH de ese equity. En fase warm-up (primeros trades), un trade ganador inicial seguido de un trade perdedor normal puede producir un DD% grande aunque la estrategia estÃ© todavÃ­a en territorio positivo. Caso concreto del run del 21-05-2026: EmaCross_BTCUSDT_1h ganÃ³ +3,996 USDT en el primer trade (ATH = 3,996), perdiÃ³ âˆ’2,441 en el segundo (equity = +1,555), U1 disparÃ³ por DD del 61% desde ATH a pesar de que la estrategia seguÃ­a positiva en +1,555. El monitor estÃ¡ calculando exactamente lo que POLICY 3.2 define; la sensibilidad de U1 en warm-up es una propiedad emergente de medir "caÃ­da desde mÃ¡ximo local" sobre un equity de magnitud chica. Esta observaciÃ³n se traslada a Hito G como input para el walk-forward, no como correcciÃ³n a POLICY ahora (ver POLICY 6.2: POLICY no se modifica durante un drawdown). La pregunta abierta para Hito G no es solo "Â¿es 25% el umbral correcto?" sino tambiÃ©n "Â¿U1 deberÃ­a medir devoluciÃ³n de ganancias o pÃ©rdida real?" â€” son mÃ©tricas conceptualmente distintas.
- **Baseline del backtest post-OPS-2.** El backtest del EmaCrossStrategy_BTCUSDT_1h pasa de 225 Ã³rdenes (pre-OPS-2) a 6 Ã³rdenes (post-OPS-2) por la combinaciÃ³n de las dos observaciones anteriores. Este es el nuevo baseline de no-regresiÃ³n hasta que Hito G recalibre o redefina U1. Cualquier cambio futuro al monitor que altere este nÃºmero requiere anÃ¡lisis explÃ­cito.
- La implementaciÃ³n inicial tenÃ­a dos bugs de cÃ³mputo de equity y base que se manifestaron en el backtest real de ene-mar 2025: equity arrancaba en cero (no en el capital atribuido a la estrategia) y `LiquidateInstrument` reusaba tags con consecuencias en el mapper. Ambos resueltos en ADR-025 sin alterar el diseÃ±o documentado en este ADR.

---

## ADR-022 â€” POLICY.md: dos niveles de semÃ¡foro, calibraciÃ³n absoluta, liquidaciÃ³n inmediata, reactivaciÃ³n con anÃ¡lisis escrito
**Fecha:** 2026-05-21
**Estado:** Aceptada

### Contexto
El sistema entra al Bloque 3 (precondiciones para paper trading) con infraestructura de monitoreo completa (INFRA-2: JSONL, heartbeat, ping externo) pero sin reglas operativas escritas que codifiquen cuÃ¡ndo una estrategia o el sistema completo pierden el derecho de operar. Hoy las reglas operativas estÃ¡n: (a) en la cabeza del operador, (b) en comentarios sueltos en cÃ³digo, (c) hardcodeadas en umbrales del `DrawdownMonitor` y `ConsecutiveLossesMonitor`. Funciona mientras hay una sola estrategia, un solo operador y un solo rÃ©gimen de mercado. Se rompe en tres escenarios que ocurren en Hito C y D:

1. Una estrategia se degrada en vivo y la decisiÃ³n "Â¿apago o aguanto?" se negocia con uno mismo en caliente, justo cuando peor se razona.
2. Algo raro pasa en live (slippage anÃ³malo, latencia, fill que no llega) y sin policy cada anomalÃ­a es una decisiÃ³n nueva.
3. En 6 meses el operador (u otra persona) necesita entender por quÃ© el sistema tiene derecho a operar capital. Sin documento escrito, no hay respuesta consultable.

OPS-1 produce `POLICY.md` para resolver esto. Cuatro decisiones operativas no triviales se cerraron durante el diseÃ±o del documento y se registran acÃ¡.

### DecisiÃ³n

**D1 â€” Dos niveles de semÃ¡foro (OK / Apagar), no tres (Verde / Amarillo / Negro).**

POLICY define que cada estrategia estÃ¡ en uno de dos estados: operando dentro de banda, o apagada. Cuando cruza cualquiera de los umbrales U1-U4 definidos en POLICY secciÃ³n 3, se apaga. Sin escalÃ³n intermedio de "reducir tamaÃ±o en lugar de apagar."

**D2 â€” CalibraciÃ³n absoluta de umbrales, no derivada del backtest existente.**

Los umbrales U1-U4 (DD absoluto desde ATH > 25%, DD rolling 30 dÃ­as > 15% sostenido 5 dÃ­as, PF rolling < 1.0 sostenido 10 trades, expectancy rolling < 0 sostenido 10 trades) son **nÃºmeros absolutos** que reflejan el mandato de riesgo del operador, no fracciones del max DD o del Sharpe del backtest actual. RecalibraciÃ³n planificada para post-Hito G (cuando exista walk-forward analysis con base estadÃ­stica).

**D3 â€” LiquidaciÃ³n inmediata al disparar umbral, no pause-only.**

Cuando se dispara un umbral de estrategia, la posiciÃ³n abierta de esa estrategia se liquida inmediatamente a mercado. La estrategia queda excluida del flujo de generaciÃ³n de seÃ±ales en `strategies.json`. No se espera al SL/TP natural.

**D4 â€” ReactivaciÃ³n con solo anÃ¡lisis escrito en `DECISIONS.md/incidents/`, sin re-paper trading obligatorio.**

Para reactivar una estrategia pausada por degradaciÃ³n, alcanza con: anÃ¡lisis documentado de quÃ© fallÃ³ y quÃ© se ajusta (si algo se ajusta), entrada datada en `DECISIONS.md/incidents/`, reactivaciÃ³n manual en `strategies.json`. NO se exige pasar nuevamente por un perÃ­odo de paper trading antes de volver a live.

### Alternativas consideradas

**Para D1 (niveles de semÃ¡foro):**
- **A: Tres niveles (Amarillo / Rojo / Negro)** con escalÃ³n intermedio "Rojo = reducir tamaÃ±o a la mitad". Descartada: agrega complejidad operativa significativa (mÃ¡s decisiones que tomar, mÃ¡s umbrales que calibrar, mÃ¡s estados que el monitor debe distinguir) sin beneficio claro para un operador que construye su propio sistema con una sola estrategia activa. El escalÃ³n intermedio es valioso en fondos con risk team dedicado donde "reducir exposure" es operativamente trivial; para un operador Ãºnico, reducir size requiere editar `strategies.json`, redeplear, y monitorear que el cambio se aplicÃ³ â€” fricciÃ³n innecesaria frente al beneficio de tener un estado intermedio.
- **B (elegida): Dos niveles (OK / Apagar)**. Simpler, mÃ¡s institucional, mÃ¡s alineado con el patrÃ³n de circuit breakers de las mesas profesionales pequeÃ±as. Trade-off aceptado: si una estrategia estÃ¡ claramente degradada pero no tan degradada como para apagar, la decisiÃ³n queda en la inspecciÃ³n humana semanal (secciÃ³n 4 de POLICY) en lugar de en el monitor automÃ¡tico.

**Para D2 (calibraciÃ³n de umbrales):**
- **A: Umbrales derivados del backtest existente** (ej. "kill threshold = 1.5x el max DD del backtest"). Descartada: el backtest actual de `EmaCrossStrategy / BTCUSDT 1h` se construyÃ³ para validar infraestructura (que el sizing redondee, que los eventos fluyan, que el HMM cargue), no como proceso de validaciÃ³n cuantitativa institucional. No hubo walk-forward analysis ni cross-validation purged k-fold (eso es Hito G/H). Tomar el max DD de ese backtest y derivar umbrales equivale a calibrar el termÃ³metro con un termÃ³metro roto. MetodolÃ³gicamente incorrecto.
- **B: Posponer los umbrales hasta post-Hito G** (umbrales como `<TBD>` hasta tener walk-forward). Descartada: rompe el orden del ROADMAP que tiene OPS-2 antes de paper trading. Deja Hito C operando sin automatizaciÃ³n de kill por degradaciÃ³n, solo con kill por drawdown global hardcodeado. No es razonable arrancar paper trading sin POLICY operativa.
- **C (elegida): Umbrales absolutos hoy, recalibraciÃ³n post-Hito G.** Los nÃºmeros reflejan el mandato de riesgo personal del operador (lo que estÃ¡ dispuesto a perder antes de apagar), no una predicciÃ³n derivada de datos cuestionables. Es la respuesta institucional pragmÃ¡tica: calibrar con lo disponible hoy, mejorar con mejores datos cuando existan. Cada recalibraciÃ³n futura se documenta como entrada nueva en `DECISIONS.md`.

**Para D3 (acciÃ³n al disparar umbral):**
- **A: Pause-only** (estrategia deja de generar seÃ±ales nuevas; posiciones abiertas siguen su curso al SL/TP/time exit). Descartada: si decidimos que la estrategia estÃ¡ degradada es porque no confiamos mÃ¡s en su edge â€” y eso incluye no confiar en su SL/TP. Aguantar la posiciÃ³n de una estrategia muerta es esperanza, no risk management. Inconsistente con cÃ³mo el sistema ya maneja el kill switch global (`LiquidateAllRiskAction`).
- **B (elegida): LiquidaciÃ³n inmediata a mercado.** Consistente con el patrÃ³n del kill switch global. Asume slippage, lo cual es aceptable porque el costo de slippage en una sola liquidaciÃ³n es menor que el costo de quedarse en una posiciÃ³n de una estrategia que ya no tiene edge.

**Para D4 (rigor de reactivaciÃ³n):**
- **A: AnÃ¡lisis escrito + ADR formal + re-paper trading antes de live**. Descartada: burocrÃ¡tica para un operador Ãºnico que construye su propio sistema. Un proceso de re-paper de 30 dÃ­as cada vez que algo se apaga frena mÃ¡s de lo que protege.
- **B: AnÃ¡lisis escrito + ADR formal (sin re-paper).** Considerada pero descartada: distinguir entre "entrada en `DECISIONS.md/incidents/`" y "ADR formal con nÃºmero correlativo" agrega ceremonia sin valor incremental para este contexto. Los ADRs con nÃºmero son para decisiones arquitectÃ³nicas que afectan el diseÃ±o del sistema; las reactivaciones son operativas y van mejor a un sub-archivo de incidentes datado.
- **C (elegida): Solo anÃ¡lisis escrito en `DECISIONS.md/incidents/`.** PragmÃ¡tico, suficiente para que el historial operativo quede consultable. Si una reactivaciÃ³n tiene un patrÃ³n que amerita ADR (ej. "descubrimos que el umbral U1 estÃ¡ mal calibrado para activos de alta volatilidad"), ese ADR se genera adicionalmente al anÃ¡lisis del incidente. Trade-off aceptado: el operador asume el riesgo de reactivar algo sin re-paper. Si reactivÃ³ mal, se vuelve a apagar rÃ¡pido y aprende; el costo lo paga el operador con su propio capital.

### Consecuencias

**Positivas:**
- POLICY.md existe como contrato escrito entre operador-en-frÃ­o y operador-en-caliente. La decisiÃ³n "Â¿apago o aguanto?" deja de ser una negociaciÃ³n bajo estrÃ©s.
- OPS-2 (`StrategyHealthMonitor`) tiene especificaciÃ³n clara de quÃ© mÃ©tricas calcular y quÃ© umbrales chequear. Se puede empezar a construir inmediatamente.
- El sistema queda preparado para entrar a Hito C (paper trading) con criterio de Ã©xito explÃ­cito y procedimientos de emergencia documentados.
- Las decisiones operativas tomadas hoy son consultables y revisables; si en 6 meses el operador piensa "Â¿por quÃ© dos niveles y no tres?", la respuesta estÃ¡ en este ADR.
- La estrategia activa hoy (`EmaCrossStrategy / BTCUSDT / 1h`) tiene su entrada poblada en POLICY secciÃ³n 7 con umbrales numÃ©ricos concretos, no placeholders.

**Neutras / aceptadas:**
- Los umbrales U1-U4 son del operador, no de un comitÃ© de risk management. Si el operador es demasiado conservador con U1 (25% DD), va a apagar estrategias sanas con frecuencia. Si es demasiado laxo, va a apagar tarde. La recalibraciÃ³n trimestral (POLICY secciÃ³n 4.4) es el mecanismo de correcciÃ³n.
- La polÃ­tica de eventos macro (POLICY 2.3) se cumple manualmente hasta que se construya `EventCalendarMonitor` (postergado a Bloque 4 como `EVCAL-1`). En los primeros meses de operaciÃ³n, el operador debe consultar calendario econÃ³mico semanalmente y desactivar manualmente. Riesgo: olvidarse de pausar antes de un FOMC y entrar en mal momento. MitigaciÃ³n: el primer incidente de este tipo es el trigger sugerido para activar `EVCAL-1`.
- Sin escalÃ³n intermedio de "reducir size", una estrategia que entra en degradaciÃ³n borderline queda solo bajo supervisiÃ³n humana semanal hasta cruzar el umbral de apagado. Aceptable para una estrategia Ãºnica; reconsiderar si llega a haber >3 estrategias activas simultÃ¡neas.

**Negativas:**
- Los umbrales absolutos son menos defendibles institucionalmente que los derivados estadÃ­sticamente. Para el operador Ãºnico, estÃ¡ bien; si en el futuro este sistema se profesionaliza o se comparte, requiere recalibraciÃ³n estadÃ­stica sÃ­ o sÃ­ (Hito G).

### Cambios colaterales al ROADMAP

- OPS-1 marcado como âœ… completado.
- OPS-2 actualizado: ahora referencia explÃ­citamente las mÃ©tricas y umbrales U1-U4 de POLICY secciÃ³n 3.
- Nueva entrada `EVCAL-1` agregada al Bloque 4 postergado: `EventCalendarMonitor`. Trigger sugerido: segunda estrategia activa, o sistema operando >7 dÃ­as sin supervisiÃ³n diaria, o un incidente concreto de pausa no aplicada a tiempo.

### Validaciones pendientes en Hito C

Al arrancar paper trading, verificar que la POLICY refleja la realidad operativa:

1. **Frecuencia real de las inspecciones (secciÃ³n 4 de POLICY).** Si la cadencia diaria/semanal/mensual no se cumple en la prÃ¡ctica, los umbrales escritos no sirven. Ajustar la cadencia (no relajar los umbrales).
2. **Trades acumulados antes de los 50.** Los primeros trades del paper sirven para confirmar que U1 y U2 funcionan; observar si los disparan por ruido o por degradaciÃ³n real.
3. **Discrepancias entre la inspecciÃ³n humana semanal y los umbrales automÃ¡ticos.** Si la inspecciÃ³n semanal indica "esto va mal" pero el monitor no dispara, los umbrales son demasiado laxos. Documentar y recalibrar en la revisiÃ³n trimestral.

---

## ADR-021 â€” Monitoreo bÃ¡sico para paper trading: JSONL local + heartbeat + Healthchecks.io
**Fecha:** 2026-05-20
**Estado:** Aceptada

### Contexto
El sistema termina los backtests con logs en consola de Lean que no persisten, sin alertas externas, y sin forma de reconstruir eventos pasados. Para operar paper trading (Hito C) y live (Hito D) se necesita cubrir tres ejes operativos distintos que el tÃ©rmino "monitoreo" agrupa imprecisamente:

1. **Liveness:** detectar si el proceso muriÃ³. Sin esto, posiciones abiertas quedan sin gestiÃ³n activa por un tiempo indefinido (sin trailing stop reactivo, sin cierre por rÃ©gimen incompatible, sin kill switch activo).
2. **PatologÃ­as silenciosas:** el proceso puede estar vivo pero sin recibir datos, sin generar seÃ±ales, o con kill switch activo sin notificaciÃ³n. Estos son los bugs operativos mÃ¡s caros porque no producen excepciones, producen silencio.
3. **Persistencia de evidencia:** reconstruir quÃ© pasÃ³ N dÃ­as atrÃ¡s cuando los logs de consola ya no estÃ¡n. Hace falta logs estructurados, persistentes, con timestamp y nivel, parseables y sobrevivientes al ciclo de vida del proceso.

Los tres ejes no se cubren con una sola herramienta. INFRA-2 los cubre con tres piezas mÃ­nimas, complementarias entre sÃ­.

### DecisiÃ³n
Tres piezas implementadas en orden estricto A â†’ B â†’ C, cada una commit-eable de forma independiente:

**A â€” Persistencia de logs estructurados (JSONL):**
- Nueva interfaz `IStructuredLogSink` en `Trading.Domain.Abstractions` (contrato) y nuevo enum `LogLevel` (espejo de los 5 niveles de `ITradingLogger`, cero dependencias externas en Domain).
- ImplementaciÃ³n `JsonlFileLogSink` en `Trading.Strategies.Adapters`: una lÃ­nea JSON por evento en `logs/trading-{wall-clock-date}.jsonl`.
- Helper estÃ¡tico `LogTemplateRenderer` extrae la lÃ³gica de parseo de placeholders nombrados que estaba embebida en `LeanLogger`.
- `LeanLogger` recibe el sink por constructor e invoca al sink en paralelo al `QCAlgorithm.Log/Debug/Error`, sin cambiar firmas pÃºblicas de `ITradingLogger`.
- **RotaciÃ³n y retenciÃ³n usan wall clock real (`DateTime.UtcNow.Date`)**, no `_clock.UtcNow`. RazÃ³n: `IClock` devuelve el clock simulado del backtest, que avanza dÃ­a a dÃ­a y dispararÃ­a cientos de rotaciones espurias eliminando los propios logs del run en curso.
- **El campo `timestamp` dentro de cada evento JSON sÃ­ usa `_clock.UtcNow`**, para correlacionar con Ã³rdenes y barras del backtest.
- RetenciÃ³n de 30 dÃ­as de wall clock real, configurable por constructor.
- Thread-safe (lock interno), traga excepciones de I/O para no romper trading.

**B â€” Heartbeat local:**
- Nuevo evento `BarProcessedEvent` (emitido por `BarProcessingService` solo en el camino exitoso, no en early-returns por skip de rÃ©gimen, sizing fallido, etc.).
- `HealthHeartbeatTracker` en `Trading.Application.Health` suscripto a `BarProcessedEvent`, `OrderSubmittedEvent`, `OrderFilledEvent`, `RiskLimitBreachedEvent`. Estado in-memory con lock. Snapshot inmutable vÃ­a `HealthSnapshot` record.
- `HeartbeatFileWriter` en `Trading.Strategies.Adapters`: serializa snapshot a `health/heartbeat.json` con escritura atÃ³mica (`.tmp` + `File.Move` overwrite).
- Flush periÃ³dico vÃ­a `System.Threading.Timer` cada 60s de **wall clock real**, **solo en `LiveMode`**. En backtest, solo flush inicial al tÃ©rmino de `Initialize()`; el archivo queda congelado durante el backtest.
- RazÃ³n del `LiveMode` guard: en backtest `Schedule.On(TimeRules.Every(60s))` se dispara al ritmo del clock simulado (~650k veces en un backtest de 15 meses), llevando el tiempo de ejecuciÃ³n de 1 minuto a 20+. El heartbeat es observabilidad pasiva, no participa del flujo de trading.
- RazÃ³n de usar `System.Threading.Timer` en lugar de `Schedule.On` incluso en live: el heartbeat opera en wall clock real porque su consumidor externo (Healthchecks.io) opera en wall clock. `Trading.Strategies` es el adaptador autorizado a usar primitivas de timing crudas.

**C â€” Ping externo a Healthchecks.io:**
- `HealthchecksIoPinger` en `Trading.Strategies.Adapters` hace HTTP GET a una URL configurable.
- URL vÃ­a variable de entorno `HEALTHCHECKS_PING_URL`. Si no estÃ¡ o formato invÃ¡lido (no matchea `^https://(hc-ping\.com|healthchecks\.io)/.+`): modo no-op con Warning una sola vez al arranque (graceful degradation, nunca rompe arranque).
- Throttle interno de 5 minutos entre pings (el callback del timer del heartbeat tambiÃ©n dispara el ping, pero el pinger solo pega al HTTP real cada 5min).
- Healthchecks.io configurado con perÃ­odo 5min y grace 15min: si el ping no llega en 15min, alerta a Telegram.
- `HttpClient` long-lived (un Ãºnico cliente para todo el run, sin `IHttpClientFactory` â€” sobre-ingenierÃ­a), dispose en `OnEndOfAlgorithm`.
- Nunca propaga excepciones al caller (un ping fallido no puede romper trading): errores de red, timeouts y status no-2xx loguean Warning y retornan ok.

### Alternativas consideradas

- **Seq / Datadog / Loki (logs centralizados):** sobre-ingenierÃ­a para una mÃ¡quina Ãºnica, infra adicional, costo recurrente. Descartado. Reconsiderar si el sistema crece a mÃºltiples nodos en cloud.
- **Uptime Kuma (alternativa self-hosted a Healthchecks.io):** requiere hostear el monitor en la misma mÃ¡quina que se quiere monitorear, lo cual derrota el propÃ³sito del dead-man's switch (si la mÃ¡quina muere, tambiÃ©n muere el monitor). Descartado.
- **Pingdom / UptimeRobot:** chequean URLs pÃºblicas (HTTP GET desde afuera hacia una URL hosteada por nosotros), no esperan pings entrantes; menos orientados al patrÃ³n "dead-man's switch". Descartado.
- **MÃ©tricas con Prometheus + Grafana / dashboard visual:** sobre-ingenierÃ­a para una sola estrategia en una sola mÃ¡quina. Las mÃ©tricas de performance del trading (P&L, drawdown rolling, Sharpe) son responsabilidad de OPS-2, no INFRA-2.
- **Posponer todo al Bloque 4:** descartado, son la mÃ­nima precondiciÃ³n operativa razonable para paper trading. Sin INFRA-2 no hay forma de detectar caÃ­das ni de hacer post-mortem.
- **Dashboard de mÃ©tricas operativas dentro de INFRA-2:** descartado por scope creep. InspecciÃ³n vÃ­a `jq` o `Select-String` sobre el JSONL es suficiente para una mÃ¡quina.

### Consecuencias

**Positivas:**
- Observabilidad local completa (JSONL + heartbeat) sin dependencias externas.
- Alerta externa de caÃ­da total vÃ­a Healthchecks.io + Telegram.
- InspecciÃ³n post-mortem con `jq` o `Select-String` desde la lÃ­nea de comandos.
- Cero impacto sobre mÃ©tricas del backtest (verificado: 225 Ã³rdenes idÃ©nticas pre/post-INFRA-2).
- Tiempo de ejecuciÃ³n del backtest restaurado a baseline (~100 segundos) tras los fixes.

**Neutras / aceptadas como deuda:**
- No hay dashboard visual de mÃ©tricas operativas. Aceptable para una sola estrategia en una sola mÃ¡quina.
- Variable `HEALTHCHECKS_PING_URL` requerida en ambiente de producciÃ³n; si no estÃ¡, el ping queda deshabilitado con Warning visible (no rompe arranque).
- Las mÃ©tricas de performance del trading (P&L, drawdown rolling, etc.) NO estÃ¡n cubiertas; corresponden a OPS-2.

**Negativas reveladas durante la implementaciÃ³n, documentadas como deuda en ROADMAP:**
- **DEUDA-2:** `TradingAlgorithmHost.Initialize()` se ejecuta dos veces en backtest. Doble suscripciÃ³n al bus, doble instanciaciÃ³n de adaptadores. Sin impacto funcional sobre mÃ©tricas. Fix pendiente: guard de idempotencia. Validar si afecta tambiÃ©n a live.
- **DEUDA-3:** logs durante `Initialize()` tienen timestamp del epoch de QC (`1997-12-31T19:00:00`). Problema cosmÃ©tico. No afecta paper/live (sin `SetStartDate`).

### Fixes correctivos durante la implementaciÃ³n (todos por el mismo error de fondo)

Durante INFRA-2 se aplicaron tres fixes correctivos antes del cierre, todos por **confundir `IClock` con wall clock real en componentes de housekeeping**:

1. **Timer del heartbeat (Pieza B fix):** el `Schedule.On(TimeRules.Every(60s))` original se disparaba al ritmo del clock simulado del backtest, llevando el tiempo de ejecuciÃ³n de 1min a 20+. Reemplazado por `System.Threading.Timer` envuelto en `if (LiveMode)`.

2. **RotaciÃ³n y retenciÃ³n del JSONL (Pieza A fix):** usaban `_clock.UtcNow.Date` y eliminaban los propios logs del run en cada cambio de dÃ­a simulado. Reemplazado por `DateTime.UtcNow.Date` para esas dos operaciones especÃ­ficas, manteniendo `_clock.UtcNow` para el campo `timestamp` de cada evento.

3. **Tests `Write_*` del sink (Pieza A tests fix):** fallaban con `IOException` al intentar leer el archivo mientras el sink lo tenÃ­a abierto en modo escritura. Antes del fix de rotaciÃ³n, cada test usaba `FakeClock` distinto y escribÃ­a a archivos distintos, evitando el conflicto. Tras el fix de rotaciÃ³n, todos los tests escriben al mismo archivo de wall clock real. Corregidos adoptando patrÃ³n `using` con disposiciÃ³n del sink antes de la lectura.

### Aprendizaje arquitectÃ³nico (incorporado a AI.md)

SurgiÃ³ un criterio que vale la pena explicitar para componentes futuros: **observabilidad y housekeeping de I/O en `Trading.Strategies` deben operar en wall clock real**, no en `IClock`. El `IClock` estÃ¡ pensado para componentes del flujo determinista de trading (Application + Domain). Confundir esto fue la causa raÃ­z de los tres fixes durante INFRA-2. PatrÃ³n a evaluar en futuros adapters de observabilidad: si el componente no influye en seÃ±ales / Ã³rdenes / risk, y su consumidor es externo (un servicio de monitoreo, un archivo de log que se inspecciona post-mortem), debe usar wall clock real.

### Validaciones pendientes en Hito C

Al arrancar paper trading (Hito C), confirmar:

1. **`heartbeat.json` se actualiza cada 60s de wall clock real** (no queda congelado como en backtest). InspecciÃ³n: `Get-FileHash` repetido sobre el archivo cada minuto debe dar hashes distintos.
2. **Pings llegan al dashboard de Healthchecks.io.** Visible en el panel del check.
3. **La alerta de Telegram dispara cuando el proceso muere.** Test deliberado: matar el proceso, esperar 15min, confirmar mensaje en Telegram.
4. **Validar si DEUDA-2 (`Initialize()` doble) aplica tambiÃ©n a live.** Si el JSONL en live muestra cada Warning/Info del arranque una sola vez, la deuda es solo de backtest y puede dejarse a mÃ¡s largo plazo.

### Cierre de DEUDA-2 (2026-05-22)

Al ejecutar el diagnÃ³stico planificado (brief `DEUDA_2_BRIEF.md`, Fase 1: instrumentaciÃ³n con `_initializeCallCount` atÃ³mico y log con hash de instancia), `Initialize()` se ejecutÃ³ **una sola vez** en backtest. Evidencia:

- Consola de Lean: `Debug: 1997-12-31 19:00:00 TradingAlgorithmHost.Initialize() invocado, hash de instancia 38986105, llamada #1` aparece una sola vez en el run.
- JSONL `trading-2026-05-22.jsonl` (6 lÃ­neas totales): los mensajes de arranque del host (`HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada`, `Heartbeat flush timer deshabilitado`) aparecen exactamente una vez cada uno.

Los logs duplicados observados al cierre de INFRA-2 que motivaron la documentaciÃ³n de DEUDA-2 no se manifiestan con el cÃ³digo actual. Causa exacta no determinada â€” no se conservÃ³ el JSONL del cierre de INFRA-2 para comparaciÃ³n directa, pero el diagnÃ³stico original fue inferencial (logs duplicados â†’ conclusiÃ³n de doble invocaciÃ³n), no instrumentado.

**NO se aplicÃ³ guard de idempotencia.** Fixes solo a problemas reproducidos. DecisiÃ³n consistente con el Riesgo 2 del brief `DEUDA_2_BRIEF.md` que previÃ³ explÃ­citamente este escenario.

**ValidaciÃ³n pendiente en Hito C:** al arrancar paper trading, inspeccionar el JSONL inicial para confirmar que el sÃ­ntoma tampoco aparece en modo Live. Si reaparece, abrir nueva deuda con diagnÃ³stico fresco (no reabrir DEUDA-2: el diagnÃ³stico de hoy quedÃ³ cerrado).

---

## ADR-020 â€” Test de referencia AccordHmmClassifierReferenceTests skipeado por convergencia degenerada con datos sintÃ©ticos
**Fecha:** 2026-05-19
**Estado:** Resuelta en ADR-024 (2026-05-22)

### Contexto
El brief del Hito B Paso 3 especificÃ³ la creaciÃ³n de un test de referencia (`AccordHmmClassifierReferenceTests`) que valida el pipeline completo del HMM sobre una serie sintÃ©tica con tres regÃ­menes claramente diferenciados (Trend alcista calmo, HighVolatility, MeanReverting). El criterio de Ã©xito era que cada segmento se clasificara correctamente en al menos el 50% de las barras post-warm-up.

Al ejecutarse el test, el segmento HighVolatility (desvÃ­o ~5x sobre los otros segmentos) clasificÃ³ como `HighVolatility` en **0 de las barras** (esperado >50%). El test falla con un margen extremo, no marginal.

### DecisiÃ³n
Marcar el test con `[Fact(Skip = "...")]` y documentar la deuda tÃ©cnica explÃ­citamente.

**JustificaciÃ³n operativa para no bloquear el cierre del Hito B:**

1. **El modelo de producciÃ³n NO presenta el sÃ­ntoma.** El modelo entrenado con datos reales de BTCUSDT perpetual de Binance (ventana 2020-2024) eligiÃ³ K=4 con margen BIC del 12-20% sobre K=3 y K=2, lo cual es una separaciÃ³n saludable, no marginal. El backtest del perÃ­odo 2025-01-01 a 2026-03-31 muestra el HMM filtrando activamente seÃ±ales (524 Ã³rdenes pre-filtro â†’ 225 post-filtro) con etiquetas `Trend`, `Squeeze` y `HighVolatility` apareciendo en distintos momentos del mercado en los logs. Si el modelo estuviera colapsado como el del test sintÃ©tico, todas las clasificaciones serÃ­an la misma etiqueta o `Unknown`; no es el caso.

2. **El test detecta un caso lÃ­mite del mÃ©todo con K=3, no de operaciÃ³n real.** El modelo de producciÃ³n usa K=4, donde Baum-Welch tiene mÃ¡s libertad para separar estados y el riesgo de Ã³ptimo local degenerado es menor. Y el `SemanticStateMapper` aplica una regla de "cuartil superior" que es matemÃ¡ticamente frÃ¡gil con K=3 (un cuartil necesita â‰¥4 puntos).

3. **El resto del pipeline estÃ¡ validado.** Los otros 12 tests del Paso 3 (BinanceKlinesParserTests con 7 tests, SemanticStateMapperTests con 5 tests) estÃ¡n en verde. La infraestructura del HMM es sÃ³lida en sus componentes individuales; lo que falla es el caso end-to-end con K pequeÃ±o.

### HipÃ³tesis de causa raÃ­z (a verificar durante diagnÃ³stico)

**HipÃ³tesis A (mÃ¡s probable):** Convergencia a Ã³ptimo local malo de Baum-Welch sobre datos sintÃ©ticos con K=3. Dos o mÃ¡s estados colapsan a parÃ¡metros casi idÃ©nticos. La inicializaciÃ³n por k-means (que el trainer usa) podrÃ­a no estar funcionando o el SemanticStateMapper podrÃ­a estar mapeando estados incorrectamente.

**HipÃ³tesis B:** Bug en `SemanticStateMapper` al calcular cuartiles con K=3 (un cuartil requiere â‰¥4 valores; con 3 estados la regla "estÃ¡ en cuartil superior" es matemÃ¡ticamente ambigua).

**HipÃ³tesis C:** El `FeatureScaler` lava las diferencias del segmento HighVolatility porque la varianza global queda dominada por los segmentos tranquilos. Menos probable: el scaler tiene en cuenta toda la varianza incluyendo los outliers.

### Plan de diagnÃ³stico
Agendado **antes de iniciar Hito C (paper trading)** y despuÃ©s de cerrar el Bloque 3 (INFRA-2, OPS-1, OPS-2). Concretamente:

1. Agregar logging temporal al test que imprima: K elegido por BIC, BICs de cada K candidato, parÃ¡metros del HMM resultante (matriz de transiciÃ³n, medias de emisiones por estado), estadÃ­sticas calculadas por el `SemanticStateMapper`, mapeo estado â†’ label resultante.
2. Decidir cuÃ¡l de las tres hipÃ³tesis es la correcta basÃ¡ndose en los logs.
3. Aplicar el fix correspondiente:
   - Si es HipÃ³tesis A: mejorar inicializaciÃ³n del Baum-Welch (mÃ¡s iteraciones, mejores seeds, k-means++ explÃ­cito).
   - Si es HipÃ³tesis B: refinar las reglas del `SemanticStateMapper` para que sean robustas con K pequeÃ±o (usar percentiles 33/66 en lugar de cuartiles, o adaptarse al K especÃ­fico).
   - Si es HipÃ³tesis C: revisar el orden de las operaciones del pipeline.
4. Verificar que el test pasa y reactivarlo (quitar el `Skip`).
5. Mover ADR-020 a estado "Resuelta".

### Alternativas consideradas
- **A: Bloquear el cierre del Hito B hasta resolver el test.** Descartada: el modelo de producciÃ³n funciona empÃ­ricamente (filtrado activo verificable en logs del backtest), la deuda es de validaciÃ³n adicional, no de funcionalidad. Bloquear el cierre por un test que detecta un caso degenerado controlado retrasarÃ­a el Hito B sin valor proporcional.
- **B: Eliminar el test directamente.** Descartada: el test sÃ­ captura informaciÃ³n valiosa (que algo del pipeline es frÃ¡gil con K pequeÃ±o). Marcarlo Skip con documentaciÃ³n deja el conocimiento accesible para futuros desarrolladores y para el diagnÃ³stico planificado.
- **C (elegida): Skip con ADR explÃ­cito y plan de diagnÃ³stico calendarizado.** Documenta la deuda, no la esconde, define un momento concreto para resolverla.

### Consecuencias
- El Hito B queda cerrado con 12 de 13 tests del Paso 3 en verde + 1 explÃ­citamente skipped con justificaciÃ³n documentada.
- El reporte de Test Explorer va a mostrar el test como Skipped (no como Failed) en cada corrida futura.
- Cuando se inicie el diagnÃ³stico planeado, este ADR es el punto de partida: enumera las tres hipÃ³tesis a verificar y el plan de acciÃ³n.
- **Riesgo asumido:** si el modelo de producciÃ³n tiene un defecto sutil anÃ¡logo al detectado en el test sintÃ©tico, el diagnÃ³stico tardÃ­o podrÃ­a implicar re-entrenar el modelo. MitigaciÃ³n: durante el Bloque 3, se agregarÃ¡ al `StrategyHealthMonitor` (OPS-2) una mÃ©trica de "frecuencia de cambio de rÃ©gimen" que detectarÃ­a comportamiento anÃ³malo (rÃ©gimen pegado en una etiqueta durante semanas, transiciones excesivamente frecuentes, etc.).
- ADR-017 pasa a estado "Aceptada" (Hito B completado), con nota al final indicando que ADR-019 documenta los parÃ¡metros del HMM y ADR-020 documenta la deuda tÃ©cnica del test de referencia.

## ADR-019 â€” ImplementaciÃ³n especÃ­fica del HMM en Paso 3 del Hito B
**Fecha:** 2026-05-19
**Estado:** Aceptada

### Contexto
ADR-017 documentÃ³ la decisiÃ³n de implementar clasificaciÃ³n de rÃ©gimen con HMM (frente a k-means o redes neuronales) y los Pasos 1 y 2 del Hito B. Este ADR documenta los parÃ¡metros especÃ­ficos del HMM efectivamente implementado en el Paso 3, asÃ­ como las decisiones operativas tomadas durante la ejecuciÃ³n concreta del entrenamiento.

### DecisiÃ³n
**LibrerÃ­a y algoritmos:** Accord.NET 3.8.0 (`Accord.MachineLearning`) para implementaciÃ³n de HMM con emisiones Multivariate Gaussian, topologÃ­a ergÃ³dica, entrenamiento con Baum-Welch (semilla 42, tolerancia 1e-5, mÃ¡ximo 200 iteraciones, regularizaciÃ³n 1e-6 para garantizar matrices de covarianza definidas positivas), decodificaciÃ³n en runtime con Viterbi (`HiddenMarkovModel.Decide`) + forward filtering posterior para probabilidades (`HiddenMarkovModel.Posterior`).

**InicializaciÃ³n canÃ³nica HMM-GMM:** se inicializan las emisiones por k-means clustering de las observaciones normalizadas (k = K, mismo nÃºmero de estados). Cada estado arranca con media = centroide del cluster y covarianza = covarianza muestral del cluster (con regularizaciÃ³n +1e-6 en la diagonal). Sin esta inicializaciÃ³n, BaumWelch quedaba en Ã³ptimo trivial: con emisiones simÃ©tricas iniciales todos los estados terminaban con Ï=0.5 y diferencias de log-likelihood entre K=2,3,4 menores al 0.5% (caso degenerado del brief). Tras la inicializaciÃ³n por k-means, las log-likelihoods se separan limpiamente y el BIC discrimina K con margen del 10-20%.

**Features:** Tres features por barra:
1. Retornos logarÃ­tmicos: `ln(close[t] / close[t-1])`
2. Volatilidad rolling 20 perÃ­odos: desvÃ­o estÃ¡ndar muestral (denominador N-1) de los Ãºltimos 20 retornos log.
3. Momentum ratio: `SMA(close, 20)[t] / SMA(close, 50)[t] - 1`

Las primeras 50 barras del training set se descartan para warm-up de features (cÃ¡lculo de SMAs).

**NormalizaciÃ³n:** Z-score con medias y desvÃ­os del training set. Los parÃ¡metros del scaler se serializan junto al modelo (`FeatureScalerMeans`, `FeatureScalerStdDevs`) para garantizar normalizaciÃ³n idÃ©ntica en runtime.

**SelecciÃ³n de K:** Probado K âˆˆ {2, 3, 4} y elegido el de BIC mÃ­nimo. Resultados de esta ejecuciÃ³n (10912 observaciones de feature vÃ¡lidas a partir de 10962 barras 4h parseadas):
| K | logLikelihood | BIC |
|---|---|---|
| 2 | âˆ’36180.62 | 72556.49 |
| 3 | âˆ’32793.27 | 65911.95 |
| 4 | **âˆ’28584.88** | **57643.94** |

**K elegido: 4** con margen amplio (12.5% sobre K=3, 20% sobre K=2).

**Mapeo semÃ¡ntico (resultado):** calculado offline aplicando reglas deterministas basadas en media de retornos en espacio z-scored, desvÃ­o en espacio z-scored y persistencia (probabilidad de auto-transiciÃ³n). EstadÃ­sticas finales por estado:
| Estado | Î¼ (z-scored) | Ïƒ (z-scored) | Ï (auto-trans) | Etiqueta |
|---|---|---|---|---|
| 0 | âˆ’0.030 | 1.740 | 0.969 | HighVolatility |
| 1 | +0.000 | 0.483 | 0.971 | Squeeze |
| 2 | +0.038 | 0.797 | 0.964 | Trend |
| 3 | âˆ’0.020 | 0.923 | 0.959 | Trend |

Dos estados terminaron mapeados a `Trend` (uno con bias positivo, otro con bias negativo). Es comportamiento esperado y permitido: el `AccordHmmClassifier` suma las probabilidades por etiqueta antes de exponer el `RegimeClassification.Probabilities`. Funcionalmente equivale a dos sub-estados de Trend (alcista y bajista) bajo la misma etiqueta semÃ¡ntica.

**Warm-up:** 100 barras 4h post-feature-warm-up (50 + 100 = 150 barras totales para alcanzar primera clasificaciÃ³n vÃ¡lida). Coordinado con `SetWarmUp` de QuantConnect extendido a 20 dÃ­as de calendario (cobertura holgada: 100Â·4h = 16.7 dÃ­as estricto). Durante el warm-up de QC, el HMM procesa las barras y el classifier devuelve `RegimeLabel.Unknown` hasta acumular suficientes features.

**Ventana de entrenamiento:** 2020-01-01 a 2024-12-31 UTC. 10962 barras 4h en ventana, 10912 features vÃ¡lidas tras descarte de warm-up. Estrictamente anterior al perÃ­odo del backtest (2025-01-01 a 2026-03-31). Cero lookahead bias.

**Instrumento:** BTCUSDT perpetual de Binance. El modelo NO es transferible a otros instrumentos ni exchanges sin re-entrenamiento. El convenio de nombrado `Trading.Models/regime/{instrumentId}-perp-binance.hmm.json` permite agregar instrumentos en el futuro sin tocar el wiring del host.

### Refactor adicional: wiring agnÃ³stico al instrumento
El wiring del rÃ©gimen en `TradingAlgorithmHost` se refactorizÃ³ para extraer dinÃ¡micamente los instrumentos Ãºnicos del `strategies.json` que tienen al menos una estrategia con `CompatibleRegimes` declarado y crear un classifier por cada instrumento con modelo disponible. El hardcoding previo de `btcInstrumentId` queda eliminado. Cuando se agregue un segundo instrumento al sistema (ej. ETHUSDT en un futuro Hito E), solo serÃ¡ necesario:
1. Entrenar un modelo para ese instrumento con el HmmTrainer.
2. Commitear el JSON a `Trading.Models/regime/`.
3. Agregar la estrategia correspondiente a `strategies.json` con `CompatibleRegimes`.

El wiring de `TradingAlgorithmHost` no se toca. Si una estrategia declara `CompatibleRegimes` pero no existe el modelo entrenado para su instrumento, el sistema falla loud al boot con `InvalidOperationException` indicando el path esperado y la instrucciÃ³n de ejecutar el HmmTrainer.

### Fix crÃ­tico en el consolidator de rÃ©gimen
El consolidator dedicado del rÃ©gimen tenÃ­a un `if (IsWarmingUp) return;` en el handler de Paso 2 (irrelevante mientras el classifier era un fake que devolvÃ­a `Trend` instantÃ¡neamente). Con el HMM real es un bug: el classifier necesita procesar barras durante el perÃ­odo de warm-up de QC para calentar su propio buffer interno (100 features post-feature-warm-up). Se eliminÃ³ el guard. La consecuencia operativa es que `SetWarmUp` debe cubrir al menos las 100 barras 4h del HMM con margen, por eso se extendiÃ³ de 1 dÃ­a a 20 dÃ­as.

### Alternativas consideradas durante la ejecuciÃ³n
- **Re-entrenamiento periÃ³dico automÃ¡tico en runtime.** Descartado por ahora: agrega complejidad operativa (quÃ© pasa si el re-entrenamiento falla, cÃ³mo se versionan los modelos, cÃ³mo se garantiza consistencia entre re-entrenamiento y operaciÃ³n). Si el modelo se degrada, se re-entrena offline corriendo el `HmmTrainer` y se commitea la nueva versiÃ³n del JSON.
- **Multi-feature engineering avanzado (ATR, RSI, volume ratio).** Descartado en este paso: tres features simples son suficientes para arrancar y validar el pipeline. La iteraciÃ³n de features queda como mejora futura cuando el sistema estÃ© operando y haya feedback empÃ­rico.
- **InicializaciÃ³n aleatoria simÃ©trica.** Descartada tras observar empÃ­ricamente que BaumWelch no convergÃ­a y los BICs eran extremadamente cercanos. La inicializaciÃ³n por k-means es estÃ¡ndar institucional y produce convergencia limpia.
- **RÃ©gimen sistÃ©mico ademÃ¡s de por activo.** Postergado a SYSREG-1 del Bloque 4 del ROADMAP.

### Consecuencias
- Sistema con clasificaciÃ³n de rÃ©gimen funcionando con inteligencia estadÃ­stica real basada en 5 aÃ±os de datos histÃ³ricos de BTCUSDT perpetual de Binance.
- Backtest del perÃ­odo 2025-01-01 a 2026-03-31 ahora se ejecuta con el filtro de rÃ©gimen activo, filtrando seÃ±ales de EmaCross segÃºn el rÃ©gimen detectado por el HMM en cada momento. Dos estados clasifican como `Trend` (con bias positivo y negativo), un estado como `Squeeze` y un estado como `HighVolatility`. La estrategia opera cuando el rÃ©gimen actual sea `Trend`; queda filtrada en `Squeeze` y `HighVolatility`.
- Deuda tÃ©cnica documentada: el `AccordHmmClassifier` mantiene buffer en memoria. Si el proceso reinicia en producciÃ³n, el classifier entra en warm-up nuevamente (resuelto vÃ­a `SetWarmUp` de QC con 20 dÃ­as). Persistencia del buffer entre reinicios queda como mejora si la latencia de warm-up se vuelve problemÃ¡tica.
- El proyecto `Trading.Strategies/Tools/HmmTrainer` queda como herramienta para re-entrenar el modelo cuando sea necesario (degradaciÃ³n detectada, agregado de instrumentos, mejora de features).
- ADR-017 pasa a estado "Aceptada" (Hito B completado en todos sus pasos).
- Note tÃ©cnica menor: `FeatureExtractor` y `FeatureScaler` se ubican en `Trading.Strategies/Regimes/` (compartidos entre trainer y runtime), no en `Tools/HmmTrainer/` como sugerÃ­a el brief inicial; la deviaciÃ³n se hizo para evitar duplicaciÃ³n del cÃ¡lculo de features entre los dos contextos (DRY trainerâ†”runtime es crÃ­tico para reproducibilidad).
- Note adicional: la regla 3 del `SemanticStateMapper` (`|Î¼áµ¢| > 0.001 y Ïáµ¢ > 0.6 â†’ Trend`) se aplica sobre la media en espacio z-scored (no en espacio crudo). En espacio crudo con la escala tÃ­pica de BTC 4h (Ïƒ â‰ˆ 0.014 por barra) la condiciÃ³n `|Î¼áµ¢| > 0.001` rara vez se cumplirÃ­a y el sistema quedarÃ­a sin estados `Trend`; en z-scored la regla discrimina los estados con drift no trivial respecto a la mediana del set. Es una desviaciÃ³n pragmÃ¡tica respecto del texto literal del brief, justificada por producir un mapeo "razonable" (criterio explÃ­cito del brief para este componente).

---

## ADR-018 â€” Adelantamiento de INFRA-1: path absoluto del strategies.json eliminado y reconciliado con MSBuild
**Fecha:** 2026-05-17
**Estado:** Aceptada

### Contexto
El `TradingAlgorithmHost.cs` hardcodeaba un path absoluto al `strategies.json`: `F:\DesarrolloTrading\QuantConnect\Lean\Trading.Strategies\bin\Debug\net10.0\strategies.json`. Eso traÃ­a tres problemas concretos:

1. **No portable.** En cualquier mÃ¡quina con otro layout de disco el sistema no arrancaba.
2. **Genera dos archivos paralelos sin sincronizar.** ExistÃ­a una copia en `Trading.Strategies\strategies.json` (fuente versionada en git) y una copia en `bin\Debug\net10.0\strategies.json` (la que el cÃ³digo leÃ­a efectivamente). El `.csproj` no tenÃ­a `<Content CopyToOutputDirectory="..." />`, asÃ­ que MSBuild no sincronizaba ambas. Como consecuencia, ambas vivÃ­an vidas separadas y diferÃ­an en contenido (la fuente con `MeanReversion`, el bin con `EmaCrossStrategy`).
3. **Disonancia silenciosa para herramientas de ediciÃ³n.** Sesiones de agentes (Claude Code) editaban la fuente versionada mientras el backtest cargaba el bin sin actualizar. El bug se manifestaba como "modifiquÃ© el cÃ³digo pero el backtest no cambia", sin error explÃ­cito.

El refactor INFRA-1 del Bloque 3 del ROADMAP planificaba resolver esto antes del Hito C, pero el problema afectÃ³ dos sesiones de trabajo sobre Hito B y se decidiÃ³ adelantarlo.

### DecisiÃ³n
Tres cambios atÃ³micos, ejecutados en una sola pasada de limpieza:

1. **Path relativo basado en `AppContext.BaseDirectory`.** El `strategiesFilePath` de `TradingAlgorithmHost.cs` pasa de hardcoded a:
   ```csharp
   string strategiesFilePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "strategies.json");
   ```
   `AppContext.BaseDirectory` resuelve al directorio donde estÃ¡ el `.exe` en runtime, lo cual coincide con `..\Launcher\bin\Debug\` (definido en `OutputPath` del `.csproj`) en desarrollo y serÃ¡ el directorio del binario desplegado en producciÃ³n.

2. **`<Content Include="strategies.json" CopyToOutputDirectory="PreserveNewest" />` agregado al `Trading.Strategies.csproj`.** Esto le indica a MSBuild que copie el archivo fuente al directorio de output en cada build (si la fuente es mÃ¡s nueva que el destino). De aquÃ­ en adelante el desarrollador edita Ãºnicamente la fuente; el bin se sincroniza automÃ¡ticamente al compilar.

3. **ReconciliaciÃ³n del contenido.** La copia desactualizada de `Trading.Strategies\strategies.json` (la fuente, que tenÃ­a `MeanReversion`) fue sobrescrita con el contenido vÃ¡lido del bin (`EmaCrossStrategy` en BTCUSDT 1h con `RiskPerTradePercentage: 2.0`). La copia del bin fue eliminada para que MSBuild la regenere desde la fuente en el prÃ³ximo build.

### Alternativas consideradas
- **A: Mantener el path absoluto y aceptar la limitaciÃ³n.** Descartada: causÃ³ dos sesiones de trabajo perdidas por confusiÃ³n sobre quÃ© archivo era la fuente de verdad. El costo de mantenerlo supera el costo de eliminarlo.
- **B: Usar un archivo de configuraciÃ³n (`appsettings.json` o variable de entorno) para parametrizar el path.** Descartada por sobre-ingenierÃ­a para el alcance actual: una variable de entorno requiere infraestructura adicional (`Microsoft.Extensions.Configuration`, validaciÃ³n, default), cuando `AppContext.BaseDirectory` resuelve el caso 100% sin agregar dependencias.
- **C (elegida): `AppContext.BaseDirectory` + `<Content CopyToOutputDirectory>`.** MÃ­nimo cambio, mÃ¡xima portabilidad. PatrÃ³n estÃ¡ndar de .NET.

### Consecuencias
- El `strategies.json` ahora se versiona Ãºnicamente en `Trading.Strategies\strategies.json`. La copia en `bin\` es un artefacto generado en cada build, no se versiona, no se edita.
- INFRA-1 del Bloque 3 del ROADMAP queda completado. Se mueve a "Historial completado" con fecha 2026-05-17.
- El refactor habilita ademÃ¡s que cualquier desarrollador clone el repo en otra mÃ¡quina y el sistema arranque sin tocar paths hardcoded.
- Deuda implÃ­cita resuelta: el JSON que alimenta el sistema ahora vive en una sola ubicaciÃ³n clara y conocida.

---

## ADR-017 â€” Hito B (Pasos 1, 2 y 3): clasificaciÃ³n de regÃ­menes con abstracciÃ³n agnÃ³stica, integraciÃ³n como guard pre-orden y HMM real
**Fecha:** 2026-05-15
**Estado:** Aceptada

### Contexto
El Hito B del ROADMAP introduce clasificaciÃ³n de regÃ­menes de mercado para filtrar seÃ±ales de las estrategias segÃºn condiciÃ³n agregada del mercado (Trend / MeanReverting / HighVolatility / Squeeze). El alcance se dividiÃ³ en tres pasos progresivos para aislar complejidad numÃ©rica (HMM real con Accord) del trabajo de plomerÃ­a (abstracciones, registry, filtro pre-orden).

Tras analizar el cÃ³digo existente (especialmente `BarProcessingService` y `RiskOrchestrator`), surgiÃ³ un hallazgo arquitectÃ³nico que cambiÃ³ la decisiÃ³n original sobre dÃ³nde insertar el filtro de rÃ©gimen.

### DecisiÃ³n
El Hito B se ejecuta en tres pasos:

**Paso 1 â€” Pre-requisitos arquitectÃ³nicos del Domain (completado 2026-05-14):**
- `MarketBar` extendido a OHLCV (`Open`, `High`, `Low`, `Close`, `Volume`). Constructor legado `(InstrumentId, decimal close, DateTime)` mantenido como `[Obsolete]` para retrocompatibilidad temporal.
- `StrategyDefinition` recibe propiedad `List<string>? CompatibleRegimes` (nullable, `List<T>` concreto por consistencia con `RootConfig.Timeframes` y por compatibilidad con Newtonsoft.Json).
- `RiskLimitBreachReason` extendido con `RegimeIncompatibility` (queda definido aunque no se emita en este hito; pertenece al vocabulario del dominio).
- `MarketBarMapper` actualizado para construir `MarketBar` con OHLCV completo desde `TradeBar` de Lean.
- `StrategyConfigLoader` valida que `CompatibleRegimes`, si estÃ¡ presente, no sea lista vacÃ­a (mensaje explicativo: ausencia = compatible con todo, lista vacÃ­a = invÃ¡lido).

**Paso 2 â€” AbstracciÃ³n de rÃ©gimen + filtro pre-orden con classifier fake (completado 2026-05-15):**
- `RegimeLabel` enum (`Unknown`, `Trend`, `MeanReverting`, `HighVolatility`, `Squeeze`) en `Trading.Domain/Abstractions/Regimes/`.
- `RegimeLabelParser.Parse(string)` con mensajes de error explÃ­citos. Rechaza `Unknown` como configuraciÃ³n explÃ­cita (forzar al usuario a omitir el campo si quiere "todos los regÃ­menes").
- `RegimeClassification` (record) con `Label`, `Probabilities` (distribuciÃ³n completa, `double` por ser magnitud estadÃ­stica), `ClassifiedAtUtc`, y constructor estÃ¡tico `UnknownFor` para fail-safe.
- `IMarketRegimeClassifier` contrato **agnÃ³stico del algoritmo**: ningÃºn mÃ©todo ni propiedad delata HMM, k-means o redes neuronales. Esto habilita NEURAL-1 futuro como adaptador alternativo sin tocar el contrato (open-closed).
- `MarketRegimeRegistry` en `Trading.Application/Regimes/`: mantiene mapa `InstrumentId â†’ IMarketRegimeClassifier` + cache de Ãºltima clasificaciÃ³n. Instrumento sin classifier registrado â†’ fail-safe a `Unknown`.
- `ConfigurableMarketRegimeClassifier`: implementaciÃ³n fake que devuelve siempre una `RegimeLabel` fija. Ãštil para tests y para validar wiring sin necesidad de modelo entrenado. Rechaza `Unknown` como `fixedLabel` (forzar coherencia).
- `StrategyRegimeCompatibility`: encapsula la lÃ³gica de compatibilidad por estrategia. Tres reglas fail-safe: lista null â†’ compatible con todo; lista vacÃ­a â†’ compatible con todo; `RegimeLabel.Unknown` siempre compatible.
- `BarProcessingService` integra el filtro como **guard `continue`** despuÃ©s del check de `KillSwitchActivated` y `SignalDirection.Flat`, **antes** de los checks de `IsInvested` y `HasOpenOrders`. Recibe dos dependencias nuevas: `MarketRegimeRegistry` e `IReadOnlyDictionary<string, StrategyRegimeCompatibility>`.
- `TradingAlgorithmHost` construye el registry con `ConfigurableMarketRegimeClassifier(BTCUSDT, Trend)`, parsea `CompatibleRegimes` de cada `StrategyDefinition` a `RegimeLabel`, crea un consolidator 4h dedicado para alimentar al registry (independiente de los consolidators de estrategias), y inyecta todo al `BarProcessingService`.

**Paso 3 â€” HMM real con Accord.NET + trainer offline (pendiente):**
- Adaptador `AccordHmmClassifier : IMarketRegimeClassifier` en `Trading.Strategies/Regimes/`.
- Proyecto standalone `Trading.Strategies/Tools/HmmTrainer` para entrenamiento offline con datos histÃ³ricos de BTCUSDT perpetual de Binance (ventana 2020-2024, estrictamente anterior al perÃ­odo de backtest 2025-01 a 2026-03).
- SelecciÃ³n de nÃºmero de estados por BIC entre K âˆˆ {2, 3, 4}.
- `SemanticStateMapper` que mapea estados crudos del HMM a `RegimeLabel` segÃºn propiedades estadÃ­sticas del cluster.
- Modelo serializado a JSON commiteado en `Trading.Models/regime/BTCUSDT-perp-binance.hmm.json`.
- Reemplazo del fake del Paso 2 por el classifier real en el wiring.

### Hallazgo arquitectÃ³nico crÃ­tico: el filtro NO va por `RiskOrchestrator`
En la planificaciÃ³n original se asumiÃ³ que el filtro de rÃ©gimen serÃ­a un `IRiskMonitor` mÃ¡s, registrado en el array de monitors del `RiskOrchestrator` (aprovechando el open-closed del ADR-015). Al inspeccionar el cÃ³digo real de `BarProcessingService` apareciÃ³ que el sistema **ya tiene el patrÃ³n de guards pre-orden** (`continue` checks) que es exactamente lo que el filtro de rÃ©gimen necesita:

```csharp
if (_riskOrchestrator.IsKillSwitchActivated) continue;
if (signalDirection == SignalDirection.Flat) continue;
// â† acÃ¡ va el filtro de rÃ©gimen, como un guard mÃ¡s
if (_portfolioState.IsInvested(instrumentId)) continue;
```

Esta decisiÃ³n es **conceptualmente mÃ¡s limpia**: un kill switch global por drawdown excesivo es una condiciÃ³n catastrÃ³fica que justifica liquidar todo (vÃ­a `IRiskAction`). Un rÃ©gimen incompatible es un filtro pre-orden por contexto, que solo justifica descartar esa seÃ±al especÃ­fica. Forzar el rÃ©gimen al `IRiskMonitor` habrÃ­a requerido extender el `RiskOrchestrator` para mapear razones de breach a acciones distintas (`RejectOrderRiskAction` vs `LiquidateAllRiskAction`), agregando complejidad innecesaria.

### Alternativas consideradas
- **Filtro como `IRiskMonitor` con `RegimeIncompatibilityMonitor`.** Descartada tras inspecciÃ³n del cÃ³digo (ver "Hallazgo arquitectÃ³nico crÃ­tico"). El patrÃ³n existente de guards en `BarProcessingService` es la abstracciÃ³n correcta.
- **Filtro como interfaz separada `IOrderValidator`.** Descartada por sobre-ingenierÃ­a: el filtro encaja perfectamente como un guard mÃ¡s en el patrÃ³n ya establecido, no merece su propia jerarquÃ­a de abstracciones.
- **RÃ©gimen como propiedad de cada `IStrategy`.** Descartada: viola separaciÃ³n de responsabilidades; el rÃ©gimen es propiedad del mercado, no de la estrategia. La estrategia declara con quÃ© regÃ­menes es compatible, el sistema decide.
- **K-means como algoritmo del Paso 3 (vs HMM).** Descartada: HMM modela transiciones temporales como ciudadano de primera clase (matriz de transiciÃ³n), devuelve distribuciÃ³n probabilÃ­stica (no solo estado actual), permite criterio formal de selecciÃ³n de nÃºmero de estados (BIC). DecisiÃ³n asentada en planificaciÃ³n previa al Paso 3.

### Consecuencias del estado actual
- El sistema tiene filtro de rÃ©gimen operativo en cÃ³digo pero **inactivo en producciÃ³n**: el `strategies.json` del repo no tiene aÃºn el campo `CompatibleRegimes` en la entrada de EmaCrossStrategy. Cuando se agregue (acciÃ³n inmediata pendiente), el filtro empezarÃ¡ a discriminar.
- El fake del Paso 2 (`ConfigurableMarketRegimeClassifier` configurado con `Trend`) se reemplazarÃ¡ en el Paso 3 por `AccordHmmClassifier`. Como ambos implementan la misma interfaz, el cambio es una sola lÃ­nea en el wiring.
- Tests nuevos: ~30 tests entre `Trading.Domain.Tests/RegimeLabelTests`, `Trading.Domain.Tests/RegimeClassificationTests`, `Trading.Application.Tests/Regimes/*Tests.cs`, y los tests de validaciÃ³n de `CompatibleRegimes` en el loader.
- Deuda tÃ©cnica conocida del Paso 2: el `MarketBar` legado constructor `(InstrumentId, decimal close, DateTime)` estÃ¡ marcado `[Obsolete]` pero sigue siendo usado en algunos lugares del proyecto. Se elimina cuando se migren todos los call-sites a OHLCV, idealmente como parte de un cleanup posterior.
- Paso 3 completado el 2026-05-19. Modelo BTCUSDT-perp-binance entrenado con ventana 2020-01-01 a 2024-12-31. K elegido por BIC: 4 (BIC = 57643.94, con margen 12-20% sobre K=3 y K=2). Mapeo de estados resultante: {0:HighVolatility, 1:Squeeze, 2:Trend, 3:Trend}. Ver ADR-019 para detalles del HMM. ADR-017 pasa a estado "Aceptada".

---

## ADR-016 â€” Trading Policy escrita y monitor runtime de degradaciÃ³n: simetrÃ­a a la regla de entrada
**Fecha:** 2026-05-15
**Estado:** Aceptada

### Contexto
El sistema tiene definida implÃ­citamente una regla de **entrada** inquebrantable: no se opera con capital real una estrategia que no superÃ³ la validaciÃ³n robusta del Hito G (walk-forward + Monte Carlo + mÃ©tricas estratificadas). Esta regla estÃ¡ distribuida en la arquitectura: tests obligatorios por estrategia (ADR-014), `IValidateOptions<T>` al boot, `RiskParameters` con invariantes.

No existe una regla anÃ¡loga de **salida**: nada del sistema actual responde a la pregunta "Â¿cuÃ¡ndo una estrategia que ya estÃ¡ corriendo deja de tener derecho a operar?". El operador queda obligado a tomar esa decisiÃ³n en runtime, generalmente bajo estrÃ©s (drawdown sostenido, racha de pÃ©rdidas, mÃ©tricas degradÃ¡ndose) y sin criterio escrito previamente. En la prÃ¡ctica institucional, esa es la decisiÃ³n que mÃ¡s frecuentemente se ejecuta mal, y la causa raÃ­z es estructural: lo que no estÃ¡ codificado o documentado de forma versionada se negocia con uno mismo en el peor momento posible.

Adicionalmente, el refactor #4 (ADR-015) dejÃ³ el sistema en open-closed sobre `IRiskMonitor`: agregar un monitor nuevo no requiere modificar nada existente. Esa puerta estÃ¡ abierta y la degradaciÃ³n estadÃ­stica de una estrategia en vivo es exactamente el tipo de condiciÃ³n que deberÃ­a detectarse vÃ­a monitor.

### DecisiÃ³n
Introducir dos artefactos complementarios en el Bloque 3 (pre-Hito C, paper trading):

- **OPS-1 â€” `POLICY.md`:** documento markdown versionado en el repo, escrito antes de iniciar paper trading. Codifica por estrategia y a nivel sistema: umbrales numÃ©ricos de drawdown que disparan reducciÃ³n/pausa/kill definitivo; criterios cuantitativos de "estrategia muerta" (rolling Sharpe, profit factor, expectancy degradados respecto al backtest); cadencia de revisiÃ³n humana; procedimiento de reactivaciÃ³n tras pausa. Los umbrales se definen con margen explÃ­cito para el haircut esperado entre backtest y live (tÃ­picamente 30-50% de degradaciÃ³n en Sharpe), no como porcentaje simÃ©trico del backtest.

- **OPS-2 â€” `StrategyHealthMonitor`:** componente en `Trading.Application` que implementa `IRiskMonitor` y consume `OrderFilledEvent` del `IDomainEventBus` para mantener mÃ©tricas rolling en vivo por estrategia. Compara contra los umbrales de `POLICY.md` y dispara `RiskLimitBreachedEvent` (extendiendo `RiskLimitBreachReason` con `StrategyDegradation`) cuando se cruzan. Se registra en el array de monitors de `RiskOrchestrator`.

OPS-1 va primero y bloquea OPS-2: define los nÃºmeros que OPS-2 va a chequear.

### Alternativas consideradas
- **A: Postergar al Bloque 4 ("cuando crezca").** Tentador porque OPS-1/OPS-2 no son cÃ³digo del motor de trading sino metadatos operativos. Descartada: paper trading sin policy escrita no cumple su funciÃ³n formativa (es donde se entrena el mÃºsculo de apagar una estrategia "como si fuera real"), y operar live sin OPS-2 deja la decisiÃ³n mÃ¡s costosa del oficio (cuÃ¡ndo matar una estrategia que pierde) librada a la disciplina humana bajo estrÃ©s. El costo de hacerlo bien es chico; el costo de no hacerlo se paga en blow-ups.

- **B: Solo OPS-1 (documento escrito sin componente runtime).** Mejor que nada, pero documento sin enforcement es papel mojado: la inspecciÃ³n humana de mÃ©tricas en vivo no escala a mÃºltiples estrategias y falla bajo estrÃ©s operativo (el operador minimiza lo malo cuando el dolor estÃ¡ fresco). Descartada por insuficiente.

- **C: Solo OPS-2 (monitor runtime sin documento).** CÃ³digo sin criterio: los umbrales que el monitor compara tienen que venir de algÃºn lado, y si no estÃ¡n escritos y versionados en el repo terminan hardcodeados en el cÃ³digo o en un JSON sin contexto. Descartada por incompleta: OPS-1 es la fuente de verdad humana, OPS-2 es la ejecuciÃ³n.

- **D (elegida): OPS-1 + OPS-2 en el Bloque 3, en ese orden.** OPS-1 antes que OPS-2 porque define los nÃºmeros. Ambos antes que Hito C porque el paper trading es donde se valida el conjunto.

### Consecuencias
- El sistema queda con las tres puertas crÃ­ticas definidas: entrada (validaciÃ³n robusta antes de operar â€” Hito G), operaciÃ³n (risk monitors en runtime â€” refactor #4), salida (policy de degradaciÃ³n y muerte de estrategia â€” OPS-1/OPS-2). Hasta hoy faltaba la tercera.
- `RiskLimitBreachReason` se extenderÃ¡ con un valor nuevo (`StrategyDegradation`). El refactor #4 ya garantiza que agregar un monitor no toca cÃ³digo existente, asÃ­ que el blast radius de OPS-2 es chico.
- Se introduce deuda tÃ©cnica conocida: las mÃ©tricas rolling del `StrategyHealthMonitor` se calculan en memoria desde el inicio de cada sesiÃ³n. Si el proceso reinicia, se pierde el historial reciente y el monitor entra en warm-up. Para paper trading es aceptable; para live serio habrÃ¡ que persistir el estado. Queda anotado para Bloque 4.
- El documento `POLICY.md` introduce un nuevo tipo de artefacto al repo (operacional, no cÃ³digo), que se versiona con el mismo rigor que cualquier otro: cualquier cambio a un umbral se commitea con justificaciÃ³n, y se revierte con `git` como cualquier cÃ³digo mal pensado.

---

## ADR-015 â€” SeparaciÃ³n de IRiskMonitor de IRiskAction (descomposiciÃ³n del KillSwitchManager)
**Fecha:** 2026-05-13
**Estado:** Aceptada

### Contexto
`KillSwitchManager` concentraba cuatro responsabilidades distintas: detectar drawdown excesivo, contar pÃ©rdidas consecutivas, ejecutar la liquidaciÃ³n global, y gestionar el perÃ­odo de cooling-off tras la activaciÃ³n. Al planificar Hito B (regÃ­menes de mercado), surgirÃ¡ una quinta responsabilidad: detectar "rÃ©gimen incompatible" como condiciÃ³n de pausa. Agregar esa lÃ³gica a `KillSwitchManager` escalarÃ­a el problema.

Adicionalmente, `OrderLifecycleService` dependÃ­a de `KillSwitchManager` solo para `RegisterLoss()` y `RegisterWin()`: dependencia hacia un God Object por una API diminuta.

### DecisiÃ³n
Descomponer `KillSwitchManager` en cinco componentes de responsabilidad Ãºnica:
- `DrawdownMonitor : IRiskMonitor` â€” detecta drawdown desde high-water mark.
- `ConsecutiveLossesMonitor : IRiskMonitor` â€” registra rachas de pÃ©rdidas; expone `RegisterLoss()` / `RegisterWin()` como API pÃºblica.
- `CoolingOffTracker` â€” gestiona el perÃ­odo de cooling-off (no es `IRiskMonitor`: su rol es seÃ±alizar desactivaciÃ³n, no activaciÃ³n).
- `LiquidateAllRiskAction : IRiskAction` â€” ejecuta la liquidaciÃ³n.
- `RiskOrchestrator` â€” coordina el ciclo completo: evalÃºa monitors, activa kill switch con la acciÃ³n, gestiona cooling-off; exposing `IsKillSwitchActivated` y `EvaluateAllMonitors()`.

`ConsecutiveLossesMonitor` se inyecta directamente en `OrderLifecycleService` (no a travÃ©s del orquestador: el orquestador no necesita saber de fills individuales).

### Alternativas consideradas
- **A: Refactorizar KillSwitchManager internamente** sin separar interfaces. Descartada: el naming engaÃ±oso y el tamaÃ±o de la clase seguirÃ­an siendo problemas de mantenimiento.
- **B: IRiskMonitor con mÃ©todo RegisterEvent() genÃ©rico** para que OrderLifecycleService informe al monitor via interfaz. Descartada: innecesariamente abstracto; `ConsecutiveLossesMonitor` es un concepto concreto que merece API explÃ­cita.
- **C (elegida): InyecciÃ³n directa del monitor concreto** en OrderLifecycleService. `RiskOrchestrator` lo recibe como `IRiskMonitor` via DI; `OrderLifecycleService` lo recibe como tipo concreto para acceder a la API de registro. Un objeto, dos facetas.

### Consecuencias
- Agregar un nuevo monitor en Hito B (rÃ©gimen de mercado) requiere implementar `IRiskMonitor` y registrarlo en el array que recibe `RiskOrchestrator`. Sin modificar nada existente.
- `KillSwitchManager.cs` y `KillSwitchManagerTests.cs` eliminados.
- 14 tests nuevos cubren los tres componentes principales. Total: 57 tests.
- El tÃ©rmino "KillSwitch" desaparece del cÃ³digo; reemplazado por `IsKillSwitchActivated` en `RiskOrchestrator` (nombre descriptivo del estado) y `RiskLimitBreachedEvent` (nombre del evento).

---

## ADR-014 â€” ReversiÃ³n del SignalAuditor: validaciÃ³n de indicadores por tests unitarios estÃ¡ticos
**Fecha:** 2026-05-13
**Estado:** Aceptada (revierte ADR-010, ADR-011, ADR-012, ADR-013 en lo que respecta a auditorÃ­a runtime)

### Contexto
El Hito A original implementaba un SignalAuditor que durante el backtest mantenÃ­a un buffer rolling de barras observadas y, cuando una estrategia emitÃ­a seÃ±al, recalculaba los indicadores en C# independientemente y comparaba con los valores que la estrategia declaraba haber usado.

Tras cuatro fixes iterativos (buffer 200â†’2000, warm-up 200, tolerancia absoluta 1e-9 â†’ relativa 1e-6, reemplazo del algoritmo SMA-seedâ†’EMA-puro), persistÃ­an ~33% de seÃ±ales reportadas como inconsistentes sin causa raÃ­z clara. El sistema acumulaba complejidad arquitectÃ³nica sin resolver el problema de fondo.

BÃºsqueda posterior revelÃ³ que la prÃ¡ctica institucional estÃ¡ndar (documentada por la propia QuantConnect en sus tests de regresiÃ³n de indicadores) es validar indicadores mediante tests unitarios contra valores de referencia de librerÃ­as open source (TA-Lib, QuantLib) almacenados en CSV o arrays estÃ¡ticos. NO se hace auditorÃ­a en vivo durante backtest.

### DecisiÃ³n
Eliminar completamente el SignalAuditor y todos sus componentes asociados (9 archivos borrados). Reemplazar por dos tests unitarios:
1. Test de indicador: verifica que ExponentialMovingAverage de QC produce valores equivalentes al baseline QC (validado internamente por QC contra TA-Lib) sobre serie sintÃ©tica de referencia.
2. Test de estrategia: verifica que EmaCrossStrategy emite seÃ±ales correctas con datos sintÃ©ticos diseÃ±ados.

Para cualquier indicador o estrategia nueva que se agregue al sistema, replicar este patrÃ³n en lugar de re-introducir auditorÃ­a runtime.

### Alternativas consideradas
- **A: Continuar iterando sobre el SignalAuditor.** Descartada: cuatro fixes sin convergencia indica que el diseÃ±o es fundamentalmente incorrecto, no que falte un fix mÃ¡s.
- **B: Auditor independiente en Python con TA-Lib durante el backtest.** Descartada: agrega un pipeline cross-language al desarrollo cotidiano por un problema que tests unitarios resuelven mejor. Reservar este enfoque para validaciÃ³n pre-live trading (ver TODO AUDIT-1 en ROADMAP).
- **C (elegida): Tests unitarios estÃ¡ticos contra valores de referencia.** EstÃ¡ndar institucional documentado. Costo runtime cero. Cobertura efectiva.

### Consecuencias
- El sistema runtime queda mÃ¡s simple: BarProcessingService y TradingAlgorithmHost vuelven a no conocer auditorÃ­a.
- La verificaciÃ³n de fidelidad de seÃ±ales se hace una sola vez en CI (al correr tests), no en cada backtest.
- ADRs anteriores (ADR-010 a ADR-013) quedan superseded en lo que respecta a auditorÃ­a runtime, pero se mantienen como registro histÃ³rico del aprendizaje.
- PrÃ¡ctica recomendada antes de pasar a paper trading: verificaciÃ³n manual de 3-5 seÃ±ales en TradingView (sanity check humano final). No automatizada, no bloqueante.
- TODO AUDIT-1 (auditor Python independiente) sigue en ROADMAP Bloque 4 para fase pre-live con capital significativo.

---

## ADR-012 â€” Auditor de seÃ±ales: tolerancia relativa, no absoluta
**Fecha:** 2026-05-12
**Estado:** Aceptada

### Contexto
El SignalAuditor compara valores declarados por la estrategia (usando `double` internamente en QuantConnect) contra valores recalculados (usando `decimal` en el dominio). El error numÃ©rico inherente a este cross-precision puede llegar al orden de 1e-5 a 1e-7 relativo, dependiendo de la cantidad de operaciones acumuladas. Una tolerancia absoluta no escala con la magnitud del activo: el mismo umbral que es razonable para BTC en 100,000 USD es absurdamente laxo para FX en 1.10 o para una acciÃ³n de 5 USD.

### DecisiÃ³n
Usar tolerancia RELATIVA en SignalAuditor: `|declarado - recalculado| / max(|declarado|, |recalculado|, 1) < tolerance`. Default `1e-6`. El denominador con piso de 1 evita divisiÃ³n por cero cuando ambos valores son ~0; en ese caso degrada elegantemente a comparaciÃ³n absoluta con umbral igual a la tolerancia.

### Alternativas consideradas
- **A: Tolerancia absoluta tuneada por activo.** Requiere mantener un mapa `instrumentId â†’ tolerancia`. FrÃ¡gil al agregar nuevos activos. Descartada.
- **B: Tolerancia absoluta global laxa (ej. 0.1).** Funciona para BTC pero enmascara discrepancias reales en activos baratos. Descartada.
- **C (elegida): Tolerancia relativa.** Se adapta automÃ¡ticamente al rango numÃ©rico. EstÃ¡ndar institucional para comparaciones numÃ©ricas cross-precision.

### Consecuencias
- El auditor ahora discrimina correctamente entre ruido numÃ©rico (cross-precision `double`â†”`decimal`) y bugs financieramente significativos.
- La constante `1e-6` se vuelve precedente: cualquier futuro auditor numÃ©rico del proyecto debe usar tolerancia relativa con magnitud similar, salvo justificaciÃ³n especÃ­fica.
- El campo `SignalDiscrepancy.AbsoluteDifference` se mantiene en el reporte porque sigue siendo Ãºtil para diagnÃ³stico humano cuando una discrepancia es genuina.

---

## ADR-011 â€” Auditor de seÃ±ales: warm-up por sÃ­mbolo en lugar de buffer infinito
**Fecha:** 2026-05-12
**Estado:** Aceptada

### Contexto
El SignalAuditor recalcula indicadores independientemente para validar fidelidad de seÃ±ales. La estrategia acumula EMA desde la primera barra del backtest; el auditor mantiene un buffer rolling y reseed con SMA cada vez. Esa asimetrÃ­a matemÃ¡tica genera discrepancias sistemÃ¡ticas mientras el buffer no es suficientemente largo respecto al perÃ­odo del indicador.

### DecisiÃ³n
En lugar de mantener un buffer infinito desde el inicio del backtest (matemÃ¡ticamente equivalente, O(N) memoria), usar un buffer finito grande (2000 barras, default) + perÃ­odo de warm-up explÃ­cito (200 barras, default) durante el cual el auditor NO emite resultados. El contador `SignalsSkippedDuringWarmUp` se reporta para transparencia.

### Alternativas consideradas
- **A: Buffer infinito.** MatemÃ¡ticamente puro: el auditor procesa exactamente las mismas barras que la estrategia. Costo: memoria crece linealmente con el tiempo del backtest. Para backtests largos (aÃ±os en 1h) puede superar 100MB por sÃ­mbolo. Descartada.
- **B (elegida): Buffer 2000 + warm-up 200.** Para EMA(60) el peso del seed inicial decae a ~10^-30 despuÃ©s de 2000 barras. Indistinguible de cero. Memoria O(constante).

### Consecuencias
- Las primeras seÃ±ales del backtest no se auditan (warm-up). Aceptable porque tÃ­picamente coinciden con el perÃ­odo de calibraciÃ³n inicial de las propias estrategias.
- Si en el futuro se agregan indicadores con perÃ­odos > 60, el buffer de 2000 puede no ser suficiente. Regla general: buffer >= 30x el perÃ­odo mÃ¡s largo.
- El patrÃ³n "warm-up explÃ­cito" se vuelve precedente para futuros auditores (otros indicadores, otros tipos de seÃ±al).

---

## ADR-010 â€” Auditor de seÃ±ales en C# dentro del mismo backtest, no Python independiente
**Fecha:** 2026-05-12
**Estado:** Aceptada

### Contexto
El Hito A requerÃ­a validar que las seÃ±ales generadas por las estrategias sean fieles a las reglas declaradas. HabÃ­a dos enfoques institucionalmente vÃ¡lidos: (1) auditor dentro del mismo proceso del backtest, escrito en C#, reusando librerÃ­as de QuantConnect; (2) auditor en Python con TA-Lib leyendo un CSV exportado durante el backtest, verdaderamente independiente del runtime.

### DecisiÃ³n
Implementar el auditor en C# dentro del mismo backtest. Reporte de resumen vÃ­a `OnEndOfAlgorithm` a consola. Sin script Python separado por ahora.

### Alternativas consideradas
- **A: Auditor Python + TA-Lib aparte.** Descartada por costo de mantenimiento de un segundo codebase y por el momento del proyecto (pre-paper trading). Verdaderamente independiente, pero overkill para el riesgo actual.
- **B (elegida): Auditor C# en el mismo proceso.** Detecta bugs de flujo de control y estado interno (que es el 80% del valor). LimitaciÃ³n honesta documentada: no detecta bugs en QuantConnect mismo.

### Consecuencias
- El auditor comparte motor con la estrategia: si QC tiene un bug en EMA, el auditor lo replica y no lo detecta.
- Se registra TODO AUDIT-1 en ROADMAP.md para implementar el auditor Python antes de pasar a live con plata significativa.
- La interfaz `IIndicatorRecomputer` permite agregar nuevas estrategias auditables sin tocar el `SignalAuditor`.
- `PreviousSignal` en EmaCross queda fuera del audit porque requiere replicar estado histÃ³rico â€” limitaciÃ³n documentada y aceptada.

---

## ADR-009 â€” Bus de eventos de dominio sÃ­ncrono in-memory, sin librerÃ­as externas
**Fecha:** 2026-05-12
**Estado:** Aceptada

### Contexto
El sistema necesita comunicaciÃ³n interna entre componentes (tradingâ†’mÃ©tricas) sin acoplar los publicadores a los consumidores. La alternativa obvia es una librerÃ­a de mensajerÃ­a (MediatR, MassTransit). El sistema es de baja frecuencia (barras de 5m), corre en backtest y debe ser completamente determinista.

### DecisiÃ³n
Implementar un `DomainEventBus` propio: clase en `Trading.Application/Eventing/`, sÃ­ncrono, in-memory, con `Subscribe<TEvent>` y `Publish<TEvent>`. SuscripciÃ³n manual desde `TradingAlgorithmHost`. Sin frameworks externos.

### Alternativas consideradas
- **A: MediatR.** Descartado. AÃ±ade un NuGet externo, introduce IRequest/INotification con su propio lifecycle, y oculta el flujo de control bajo indirecciÃ³n. Para un sistema de un solo proceso y baja frecuencia es sobrediseÃ±o.
- **B: MassTransit / mensajerÃ­a async.** Descartado. Introduce complejidad operacional (broker, serializaciÃ³n, retries) incompatible con el requisito de determinismo en backtest.
- **C (elegida): bus propio sÃ­ncrono.** El flujo de control queda visible. El comportamiento en backtest es idÃ©ntico al de live. El aislamiento de fallos en suscriptores (loguea y continÃºa) garantiza que una mÃ©trica mal escrita no rompe el flujo de trading. Si en el futuro se necesita async (ej. escritura a DB), se puede agregar un suscriptor que encole sin cambiar los publicadores.

### Consecuencias
Los publicadores no deben asumir que los suscriptores son rÃ¡pidos: cada `Publish` bloquea hasta que todos los callbacks retornan. Aceptable para el Hito A (mÃ©tricas en memoria). Si en el futuro se agregan suscriptores de I/O (DB, red), revisar si hace falta un canal async.

---

## ADR-008 â€” Postergar refactors no crÃ­ticos del AI.md hasta despuÃ©s de cada hito
**Fecha:** sesiÃ³n 2
**Estado:** Aceptada

### Contexto
El `AI.md` actualizado describe un sistema institucional maduro. El cÃ³digo actual viola varias reglas (logging no estructurado, magic values en lugar de `Result<T>`, `decimal` crudo en lugar de Value Objects de dinero, etc.). Hacer todos los refactors antes de avanzar con los hitos del proyecto bloquearÃ­a el progreso indefinidamente.

### DecisiÃ³n
Aplicar **principio de proporcionalidad al riesgo**: solo refactorizar lo que pueda causar pÃ©rdida de dinero real o bloqueÃ© un hito especÃ­fico. El resto se posterga a "cuando el sistema crezca".

Criterio explÃ­cito:
1. Â¿Puede causar pÃ©rdida monetaria directa o vÃ­a bug que dispare orden equivocada? **Y**
2. Â¿La probabilidad de que la falla ocurra es razonable (no escenario de manual)?

Si las dos respuestas son sÃ­ â†’ hacer antes del prÃ³ximo hito.
Si alguna es no â†’ postergar al Bloque 4 ("cuando el sistema crezca").

### Alternativas consideradas
- **A: Aplicar todas las reglas del AI.md ahora.** Descartada: bloquea progreso, no hay live trading inminente.
- **B: Ignorar el AI.md y avanzar a hitos.** Descartada: se acumula deuda que va a explotar en producciÃ³n.
- **C (elegida): PriorizaciÃ³n por riesgo + hito que bloquea.**

### Consecuencias
- El AI.md se trata como "estrella polar", no como checklist obligatorio para hoy.
- Cada refactor postergado queda explÃ­citamente registrado en `ROADMAP.md` con condiciÃ³n de "trigger" (ej. "cuando se agregue 2do asset class").
- El sistema vive con deuda tÃ©cnica conocida y trackeada, no oculta.

---

## ADR-007 â€” `ITradingLogger` se mantiene como abstracciÃ³n del dominio
**Fecha:** sesiÃ³n 2
**Estado:** Aceptada (a aplicar en refactor A2)

### Contexto
El AI.md exige `ILogger<T>` de `Microsoft.Extensions.Logging` con placeholders nombrados (structured logging). El cÃ³digo actual usa `ITradingLogger` propio con interpolaciÃ³n `$"..."`.

### DecisiÃ³n
Mantener `ITradingLogger` como abstracciÃ³n de dominio. Cambiar su contrato para aceptar template + parÃ¡metros (`Info(string template, params object[] args)`). ImplementaciÃ³n interna (`LeanLogger`) puede usar `ILogger<T>` por debajo si conviene.

### Alternativas consideradas
- **A: Reemplazar `ITradingLogger` por `ILogger<T>` directo.** Descartada: agregar dependencia de `Microsoft.Extensions.Logging` a `Trading.Domain` y `Trading.Application` rompe el principio de "dominio sin dependencias externas".
- **B (elegida): Mantener `ITradingLogger`, refactorizar para placeholders.** Logra structured logging sin ensuciar el dominio.

### Consecuencias
- `Trading.Domain` y `Trading.Application` no necesitan referenciar `Microsoft.Extensions.Logging`.
- El refactor A2 toca solo la signatura de `ITradingLogger` y todas las llamadas (~15 en total).
- Si en el futuro se necesitan features avanzadas (scopes, structured properties complejas), se puede revisar la decisiÃ³n.

---

## ADR-006 â€” `Long`/`Short` en estrategias usando enum simple, no `SignalDecision` con factory methods
**Fecha:** sesiÃ³n 2
**Estado:** Aceptada

### Contexto
Para habilitar shorts, `IStrategy.EvaluateSignal` debÃ­a dejar de devolver `bool`. HabÃ­a dos opciones: tipo rico (`SignalDecision` con Direction + Confidence + futuras propiedades) o enum simple (`SignalDirection { Flat, Long, Short }`).

### DecisiÃ³n
Enum simple `SignalDirection`. Sin clase wrapper, sin factory methods, sin confidence.

### Alternativas consideradas
- **A: `SignalDecision` con factory methods `Long()`, `Short()`, `Flat()` + campo `Confidence`.** Descartada por el usuario: agrega complejidad sin necesidad presente. Confidence no se conecta al sizing en este refactor; almacenarla sin usarla no aporta.
- **B (elegida): enum simple.** MÃ­nimo cambio, mÃ¡ximo desbloqueo (shorts habilitados). Si en el futuro se necesita `Confidence`, se puede agregar como `SignalDecision` envolviendo el enum.

### Consecuencias
- `BarProcessingService` aplica el signo: Long â†’ cantidad positiva, Short â†’ cantidad negativa. El `PositionSizer` sigue devolviendo magnitud (sin cambio).
- `EmaCrossStrategy` ahora produce seÃ±ales en ambas direcciones; backtest puede mostrar resultados muy distintos al previo (mismo set de cruces, pero ahora la mitad eran short y se ignoraban).

---

## ADR-005 â€” Cleanup automÃ¡tico del `OrderRegistry` tras eventos terminales
**Fecha:** sesiÃ³n 2
**Estado:** Aceptada

### Contexto
El `OrderRegistry` mapea tags opacos a registraciones de Ã³rdenes. Sin cleanup, retendrÃ­a miles de registraciones obsoletas en una sesiÃ³n live de varios dÃ­as.

### DecisiÃ³n
`OrderEventMapper` llama `OrderRegistry.Forget(clientTag)` tras procesar exitosamente un evento terminal (Filled/Canceled/Invalid). El registry retiene solo Ã³rdenes vivas.

### Alternativas consideradas
- **A: Mantener todas las registraciones (memoria barata, simple).** Descartada: complica diagnÃ³stico forense â€” el operador no puede distinguir Ã³rdenes activas de histÃ³ricas.
- **B (elegida): Forget tras evento terminal.** Mantiene el registry como "vista de lo vivo".

### Consecuencias observadas
- En backtest aparecen eventos residuales (rollover de futuros, fills parciales tardÃ­os) que llegan **despuÃ©s** del Forget. El `OrderEventMapper` los detecta y loguea en Debug con mensaje especÃ­fico ("evento residual esperado").
- No afecta la correcciÃ³n funcional: la posiciÃ³n ya se cerrÃ³ cuando llegÃ³ el primer evento terminal.

---

## ADR-004 â€” Tags opacos formato `ord_xxxxxxxx` (GUID corto), no contador incremental
**Fecha:** sesiÃ³n 1
**Estado:** Aceptada

### Contexto
El `OrderRegistry` genera tags opacos para asociar Ã³rdenes a su contexto. HabÃ­a que elegir formato.

### DecisiÃ³n
`"ord_" + Guid.NewGuid().ToString("N").Substring(0, 8)`. 8 caracteres hex como identificador opaco.

### Alternativas consideradas
- **A: Contador incremental (`ord_000001`, `ord_000002`).** Descartada: requiere lock para thread-safety. En live trading los callbacks de fills llegan en threads distintos.
- **B (elegida): GUID corto.** No requiere coordinaciÃ³n. Probabilidad de colisiÃ³n en 8 chars hex: ~1 en 4 mil millones, mitigada con loop defensivo en el generador.

### Consecuencias
- Tests deterministas son mÃ¡s difÃ­ciles (los tags son aleatorios). El AI.md exige `IOrderIdGenerator` inyectable para fix; postergado al Bloque 4.

---

## ADR-003 â€” `OrderRegistry` vive en `Trading.Application`, no en `Trading.Strategies`
**Fecha:** sesiÃ³n 1
**Estado:** Aceptada

### Contexto
El `OrderRegistry` es la pieza central del refactor #1 (eliminar stringly-typed tags). PodÃ­a vivir en Application (lÃ³gica pura) o en Strategies (junto a los adaptadores Lean).

### DecisiÃ³n
Vive en `Trading.Application/Execution/OrderRegistry.cs`. Es lÃ³gica pura (dictionary + generaciÃ³n de tag), sin dependencia de Lean. El `LeanOrderRouter` recibe la instancia por constructor.

### Alternativas consideradas
- **A: En Strategies, junto al `LeanOrderRouter`.** Descartada: romperÃ­a testabilidad. El registry es lÃ³gica que el dominio puede consumir; vivir en Strategies lo atarÃ­a a Lean innecesariamente.
- **B (elegida): En Application.** Testeable en milisegundos con tests unitarios sin Lean.

---

## ADR-002 â€” `RiskPerTradePercentage` falla loud si no estÃ¡ en `strategies.json`
**Fecha:** sesiÃ³n 1
**Estado:** Aceptada

### Contexto
Al pasar el `RiskPerTradePercentage` de constante hardcodeada (2%) a campo del JSON, habÃ­a que decidir quÃ© pasa si una entrada del JSON no lo trae.

### DecisiÃ³n
El sistema **no arranca** si falta. `StrategyDefinition.RiskPerTradePercentage` es `decimal?` (nullable) para distinguir "campo ausente" de "campo presente con valor 0". Ambos casos fallan, pero el `StrategyConfigLoader` produce mensajes distintos para diagnÃ³stico.

### Alternativas consideradas
- **A: Default 2% si no estÃ¡ (retrocompatibilidad suave).** Descartada por el usuario: viola polÃ­tica institucional de fail-loud.
- **B (elegida): Falla loud, no arranca.** El operador es forzado a ser explÃ­cito sobre el riesgo por trade en cada estrategia.

---

## ADR-001 â€” Desacople quirÃºrgico de QuantConnect: dominio Lean-free, adaptadores en Strategies
**Fecha:** sesiÃ³n 1
**Estado:** Aceptada (es el refactor mÃ¡s grande del proyecto hasta hoy)

### Contexto
El cÃ³digo original tenÃ­a `using QuantConnect;` en casi todas las clases de lÃ³gica de negocio (`KillSwitchManager`, `PositionSizer`, `OrderEventHandler`, etc.). Esto bloqueaba: testabilidad sin levantar Lean, cualquier intenciÃ³n futura de cambiar de motor, y la claridad del dominio.

### DecisiÃ³n
Aplicar Clean Architecture **parcial pero estricta**:
- `Trading.Domain` y `Trading.Application` â†’ cero `using QuantConnect`.
- `Trading.Strategies` â†’ Ãºnico proyecto con `using QuantConnect`. Contiene el host (`TradingAlgorithmHost : QCAlgorithm`) y los adaptadores.
- Abstracciones del dominio: `IPortfolioState`, `IInstrumentMetadata`, `IOrderRouter`, `IOrderHandle`, `IClock`, `ITradingLogger`, `IPriceRounder`.
- Value objects propios: `InstrumentId` (en lugar de `Symbol` de QC), `MarketBar` (en lugar de `TradeBar`).

### Alternativas consideradas
- **A: Desacople total (tambiÃ©n `Trading.Strategies` deberÃ­a ser pluggable).** Descartada: el host y los consolidators son especÃ­ficos de Lean; abstraerlos serÃ­a overkill.
- **B: Mantener acoplamiento, mejorar nombres.** Descartada: no resuelve los problemas de fondo (testabilidad, portabilidad futura).
- **C (elegida): Desacople quirÃºrgico.** Dominio puro, adaptadores en una sola capa.

### Consecuencias
- Tests del `KillSwitchManager` corren en milisegundos sin Lean.
- Si en el futuro se evalÃºa NautilusTrader o conexiÃ³n FIX directa, solo se reescribe `Trading.Strategies`.
- Invariante checkable: `grep -rn "^using QuantConnect" Trading.Domain/ Trading.Application/` debe estar vacÃ­o.

---

## Template para nuevas entradas

```markdown
## ADR-NNN â€” TÃ­tulo corto y descriptivo
**Fecha:** YYYY-MM-DD o "sesiÃ³n N"
**Estado:** Propuesta / Aceptada / Revertida

### Contexto
QuÃ© problema motivÃ³ la decisiÃ³n.

### DecisiÃ³n
QuÃ© se decidiÃ³ hacer concretamente.

### Alternativas consideradas
- **A: ...** Por quÃ© se descartÃ³.
- **B (elegida): ...** Por quÃ© se eligiÃ³.

### Consecuencias
QuÃ© cambia en el sistema. Si la decisiÃ³n introduce deuda tÃ©cnica conocida, marcarla acÃ¡.
```

