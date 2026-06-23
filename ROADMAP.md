# ROADMAP - Sistema de Trading

> **Propósito de este documento:** mantener visibilidad del plan completo entre sesiones de trabajo. Cualquier sesión con Claude Code (o cualquier desarrollador) debe leer este archivo primero para entender en qué punto está el proyecto.
>
> **Reglas:**
> - Cada refactor completado se marca con ✅ y fecha.
> - Cada refactor en curso se marca con 🔄.
> - Cada refactor pendiente se marca con ⬜.
> - Los refactors abortados o descartados se marcan con ❌ y se anota la razón.
> - La columna "Bloque" indica el hito al que pertenece (ver sección Plan general).
> - Cuando se complete un refactor, mover su descripción detallada al final del archivo, sección "Historial completado".

---

## Plan general (hitos del proyecto)

El proyecto está organizado en bloques de trabajo. Los refactors técnicos están agrupados por bloque según cuándo es necesario hacerlos.

**Principio de orden (López de Prado):** primero construir el motor (infraestructura + clasificación de régimen), luego validar manualmente con segunda estrategia, **después** automatizar el pipeline de research. Invertir este orden produce automatización de cosas equivocadas.

```
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 0 — Estado actual (refactors ya completados)         │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 1 — Antes del Hito A (Tests de referencia)           │
├─────────────────────────────────────────────────────────────┤
│ Refactor A2 — Logging estructurado con placeholders  ✅     │
│ Refactor B1 — Result<T> donde hay magic values       ✅     │
│ Refactor B3 — Eventos de dominio (OrderSubmitted/...) ✅    │
└─────────────────────────────────────────────────────────────┘
        ✅ BLOQUE 1 COMPLETO
                            ↓
              ✅ HITO A: Tests de referencia de
                  indicadores y estrategias
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 2 — Antes del Hito B (Regímenes de mercado)          │
├─────────────────────────────────────────────────────────────┤
│ Refactor #4 — Separar IRiskMonitor de IRiskAction     ✅    │
└─────────────────────────────────────────────────────────────┘
        ✅ BLOQUE 2 COMPLETO — Sistema listo para Hito B
                            ↓
                  ✅ HITO B: Clasificación de regímenes
                  de mercado (HMM con Accord.NET)
                  Paso 1: ✅ Pre-requisitos de Domain (OHLCV, CompatibleRegimes)
                  Paso 2: ✅ Abstracciones + filtro + classifier fake
                  Paso 3: ✅ HMM real + trainer offline + modelo entrenado
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 3 — Antes del Hito C (Paper trading)                 │
├─────────────────────────────────────────────────────────────┤
│ INFRA-1 — Path absoluto → AppContext.BaseDirectory   ✅     │
│ INFRA-2 — Monitoreo básico (alertas si algo se cae)  ✅     │
│ OPS-1 — Trading Policy Document (POLICY.md)          ✅     │
│ OPS-2 — StrategyHealthMonitor (POLICY 3.1, U1-U4)     ✅   │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ ✅ HITO C: Paper trading — COMPLETADO 2026-06-09            │
│ ✅ Infraestructura verificada (feed, heartbeat, pings)      │
│ ✅ Primer trade real 2026-06-09T00:30 UTC (BTCUSDT 15m)     │
│    Orden enviada 00:30 UTC, posición cerrada 04:36 UTC.     │
│    Ciclo completo U1→U4 validado en paper brokerage.        │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ ✅ HITO E: Segunda estrategia manual — COMPLETADO 2026-06-11  │
│ Batch 1 (OFI): OfiContrarianStrategy rechazada Hito G.      │
│ Batch 2 (microestructura): 10 hipótesis, 2 APROBADAS:       │
│   CvdSellExhaustionStrategy (IS=2.178 / OOS=1.718)          │
│   TradeSizeInstitutionalStrategy (IS=3.985 / OOS=4.186)     │
│ ADR-038, ADR-039. Historial: Trading.Research/strategy_experiments  │
│                                                             │
│ Sub-tareas de infraestructura completadas:                  │
│ ✅ E-INFRA-1: Descarga histórica AggTrades (BTC/ETH/SOL)    │
│    Script: Trading.Research/download_aggtrades.py                   │
│    47,664 barras 1h por símbolo (2021-01-01 → 2026-06-09)  │
│    CSVs en: F:\Mis Documentos\...\AggTrades\features\       │
│ ✅ E-INFRA-2: Custom data loader C# para features 1h        │
│    IMicrostructureProvider + MicrostructureRegistry          │
│    Path configurable vía MicrostructureDataPath en          │
│    strategies.json (sin copia al build output)              │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ ✅ HITO F: Strategy Scaffolder — COMPLETADO 2026-06-11      │
│ New-Strategy.ps1 en la raíz del repo.                       │
│ Uso: .\New-Strategy.ps1 -Name RsiMeanReversion              │
│ Genera: clase IStrategy skeleton + tests (3 stubs) +        │
│ snippet JSON para strategies.json + línea StrategyFactory.  │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ ✅ HITO G: IS/OOS + Monte Carlo — COMPLETADO 2026-06-11      │
│ Trading.Analytics (C#, strategy-agnostic). Lee CSV IS+OOS,  │
│ calcula 9 métricas institucionales, MC block bootstrap 10k. │
│ Gate 1: Trades≥50, NetProfit>0, Sharpe≥0.3, PF≥1.1.        │
│ Gate 2: P(Sharpe<0)≤20%, MedianMaxDD≤55%, P5 CAGR>-5%.     │
│ Validaciones: OFI rechazada; CvdSellExhaustion APROBADA     │
│ (OOS Sharpe=1.718, CAGR=30.4%, P(Sharpe<0)=1%);            │
│ TradeSizeInstitutional APROBADA (OOS Sharpe=4.186,          │
│ CAGR=97%, P(Sharpe<0)=0%). 2 candidatas activas.            │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ HITO H: Optimización de Hiperparámetros                     │
│ Búsqueda automatizada con cross-validation por régimen:     │
│ - Grid search / optimización bayesiana.                     │
│ - Criterio de selección robusto (no maximizar Sharpe puro). │
│ - Validación purged k-fold (López de Prado) para evitar     │
│   leakage temporal.                                         │
│ El rango de búsqueda y el criterio los define el operador,  │
│ NO se automatizan (sobreajuste).                            │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ HITO D-prev: Validación de broker real (sin estrategia)     │
│ Tareas que no requieren una estrategia corriendo y que      │
│ valen la pena hacer una sola vez, contra Binance live:      │
│ - Conexión, API keys, scopes/permissions, withdrawal lock.  │
│ - Órdenes manuales de tamaño mínimo: confirmar fill,        │
│   comisiones reales, slippage real medible.                 │
│ - Funding fees en perpetuals (si aplica).                   │
│ - Reconciliación portfolio interno vs portfolio del broker. │
│ Separado de Hito D para evitar que "live con capital chico" │
│ arranque solo porque el broker ya está conectado.           │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ HITO D: Live trading con capital chico                      │
│ REQUISITO: al menos UNA estrategia con walk-forward         │
│ aprobado en Hito G. La EmaCrossStrategy NO opera live       │
│ (ver POLICY 7.1).                                           │
│ Capital chico = orden de magnitud del riesgo psicológico    │
│ que el operador puede absorber sin que distorsione su       │
│ juicio operativo. NO es "tan poco que no importa" —         │
│ esa racionalización es exactamente lo que POLICY P4         │
│ busca prevenir.                                             │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ BLOQUE 4 — Cuando el sistema crezca (no urgente)            │
├─────────────────────────────────────────────────────────────┤
│ Value Objects Money/Price/Quantity (cuando haya 2do asset)  │
│ OrderNormalizer separado (cuando haya múltiples callers)    │
│ Jerarquía DomainException                                   │
│ Trading.TestSupport proyecto separado                       │
│ Auditor independiente en Python con TA-Lib (pre-live serio) │
└─────────────────────────────────────────────────────────────┘
```

**Resumen del flujo:** Bloques 1-3 + Hito A-C te llevan al sistema operando en paper sobre la estrategia de desarrollo. Hitos E-F-G-H son la fábrica de estrategias del futuro — se construyen sobre el sistema ya validado operativamente en paper. Recién después, Hito D-prev (broker real) y Hito D (live con capital chico, requiere estrategia con walk-forward aprobado).

**Cambio respecto a versiones anteriores del ROADMAP (2026-05-23):** el orden original tenía Hito D (live con capital chico) inmediatamente después de Hito C (paper), antes de Hitos E-H. Se invirtió porque la EmaCrossStrategy es estrategia de desarrollo sin walk-forward, y operar capital real — aunque chico — sobre una estrategia no validada cuantitativamente contradice el principio P1 de POLICY y el orden institucional (López de Prado: walk-forward antes de capital). Hito C se redefinió como validación operativa del sistema, no de la estrategia. Ver entrada de DECISIONS.md correspondiente.

---

## Refactors pendientes

### ✅ BLOQUE 1 — Completo

*(Todos los refactors del Bloque 1 están completados. Ver Historial completado.)*

### ✅ BLOQUE 2 — Completo

*(Refactor #4 completado. Ver Historial completado.)*

### ✅ BLOQUE 3 — Completo

| Estado | ID | Refactor | Bloquea | Comentario |
|---|---|---|---|---|
| ✅ | INFRA-1 | Path absoluto de `strategies.json` a configuración inyectable | Hito C | **Completado 2026-05-17.** Ver historial. Adelantado del orden original del ROADMAP por causar fricción operativa en sesiones de Hito B. |
| ✅ | INFRA-2 | Monitoreo básico del sistema en producción | Hito C | **Completado 2026-05-20.** Ver historial completado. Tres piezas: persistencia de logs JSONL, heartbeat local, ping a Healthchecks.io. Ver ADR-021. Validación end-to-end del ping real pendiente para Hito C. |
| ✅ | OPS-1 | Trading Policy Document (`POLICY.md`) | Hito C, OPS-2 | **Completado 2026-05-21.** Documento operativo versionado en la raíz del repo. Define principios inquebrantables, umbrales a nivel sistema, umbrales por estrategia (2 niveles OK/Apagar, calibración absoluta no derivada de backtest), cadencia de revisión humana, runbooks de emergencia, política de cambios al sistema en operación, y estado actual por estrategia. Ver ADR-022 para racional de las decisiones operativas tomadas. |
| ✅ | OPS-2 | `StrategyHealthMonitor` — implementación runtime de `POLICY.md` sección 3 | Hito C | **Completado 2026-05-21.** Ver historial completado. Componente autónomo (NO implementa `IRiskMonitor`) que consume `OrderFilledEvent` del bus, mantiene métricas rolling por `ExecutorIdentifier` y evalúa U1-U4 de POLICY 3.1. Al disparar: liquida posición abierta + flag `degraded` + `RiskLimitBreachedEvent(StrategyDegradation)` + log `Critical`. Guard en `BarProcessingService` consulta `IStrategyHealthMonitor.IsExcluded`. Ver ADR-023. |
| ✅ | DEUDA-1 | Diagnóstico del test `AccordHmmClassifierReferenceTests` actualmente skipeado | Hito C | **Completado 2026-05-22.** Ver historial completado. Bug en `SemanticStateMapper` (cuartiles no adaptivos a K) + convergencia de Baum-Welch a óptimo local. Fix adaptativo a K + multi-seed. Validación cruzada del modelo de producción OK. Ver ADR-024. |
| ✅ | DEUDA-2 | `TradingAlgorithmHost.Initialize()` se ejecuta dos veces en backtest | Hito C (validar si aplica también en live) | **Cerrada 2026-05-22 como NO reproducible.** Ver historial completado. El diagnóstico instrumentado reveló que `Initialize()` se ejecuta una sola vez (consola Lean reporta `llamada #1` una vez; JSONL muestra cada mensaje de arranque una vez). NO se aplicó guard de idempotencia. Validación pendiente en Hito C: confirmar comportamiento también en modo Live. |
| ✅ | DEUDA-3 | Logs durante `Initialize()` tienen timestamp del epoch de QC (1997-12-31) | No bloquea Hito C | **Completado 2026-06-03.** `LeanClock.UtcNow` retorna `_algorithm.UtcTime` con fallback a `DateTime.UtcNow` cuando el valor es anterior al año 2000. Elimina los timestamps `1997-12-31T19:00:00` en `ProcessStartedUtc` y primeros logs del JSONL. Ver commit `2671694`. |

### ✅ HITO B — Completado

| Estado | Paso | Descripción | Comentario |
|---|---|---|---|
| ✅ | Paso 1 | Pre-requisitos arquitectónicos del Domain (OHLCV, CompatibleRegimes, RegimeIncompatibility, validación del loader) | Completado 2026-05-14. Ver historial completado. |
| ✅ | Paso 2 | Abstracciones de régimen (`IMarketRegimeClassifier`, `RegimeLabel`, `RegimeClassification`, `MarketRegimeRegistry`, `StrategyRegimeCompatibility`), classifier fake (`ConfigurableMarketRegimeClassifier`), filtro pre-orden en `BarProcessingService`, wiring en `TradingAlgorithmHost` con consolidator 4h dedicado | Completado 2026-05-15. Ver historial completado. |
| ✅ | Paso 3 | HMM real con Accord.NET, trainer offline standalone, modelo entrenado de BTCUSDT perpetual de Binance (ventana 2020-2024, K∈{2,3,4} por BIC, mapeo semántico de estados) | Completado 2026-05-19. K=4 elegido por BIC con margen amplio (57644 vs 65912 vs 72556). Mapeo: {0:HighVolatility, 1:Squeeze, 2:Trend, 3:Trend}. Ver ADR-019 y historial completado. |

### ⬜ Post-ADR-028 — Validaciones y deudas pendientes

#### ✅ Validación multi-símbolo + multi-timeframe simultáneos
**Bloque:** continuación de la validación del subsistema.
**Estado:** completado (fecha exacta no registrada — documentado retroactivamente).
**Descripción:** Validación de que el subsistema sigue agnóstico cuando los
tres símbolos operan en TFs distintos simultáneamente
(BTC-15m / ETH-1h / TRB-4h). Resultado: backtest OK, cero violaciones del
invariante OPS-2, wiring de consolidators independientes por símbolo + TF
sin interacciones inesperadas bajo concurrencia mixta.

#### ⬜ Allocator multi-estrategia
**Bloque:** hito propio.
**Estado:** pendiente.
**Descripción:** Hoy cada executor ve `InitialAccountCashUsdt = 100_000`
como suyo para calcular DD, cuando la cuenta es realmente compartida
entre todos los executors. Esto distorsiona las métricas de DD
per-monitor en operación multi-estrategia y multi-símbolo. El hito
introduce un allocator que asigne capital nominal a cada executor con
visión coherente de la cuenta total. Trabajo arquitectónico no trivial.
**Bloqueantes:** ninguno. Decisión del operador sobre cuándo abordarlo.

#### ✅ DEUDA-3 — Backfiller aggTrades: rate limiter global + warmupHours por estrategia
**Estado:** Cerrada (2026-06-18) — ya no aplica.
**Resolución:** La deuda fue eliminada por diseño con ADR-048. El `BinanceAggTradeBackfiller` ya no se usa en el camino de producción: el `Trading.Recorder` (proceso siempre encendido) mantiene el store de microestructura actualizado de forma continua. Lean pasa a puro consumidor en `Initialize()`: lee del store sin tocar ningún endpoint REST. La siembra de activos nuevos usa `BinanceVisionSeeder` (descarga de `data.binance.vision`, sin rate limits). El problema de escalado (N × 52h × 700ms) desaparece.

#### ✅ RECORDER-1 — Feed del recorder vía REST polling + gap-fill desde Vision
**Bloque:** data plane (continuación de HITO-D-feat / ADR-048).
**Estado:** completado 2026-06-20 — **validado en el VPS**. `ITradeFeed` + `BinanceAggTradeRestFeed` (REST default) y gap-fill de arranque desde Vision con puente al vivo (`StartupSeeder`): arranca con historia inmediata, sin esperar días ni paso manual. Log del VPS: 168 barras/símbolo sembradas (7d), cursor puenteado, REST cerrando barras de hoy contiguas con CVD continuo. 7 tests nuevos + smoke tests reales OK. **Check operativo:** `RECORDER_STORAGE_DIR` (recorder) debe coincidir con `MICROSTRUCTURE_STORE_DIR` (LeanPaper/LeanLive) para que Lean lea el store. Escala futura (IP de egreso propia a >~15 símbolos) sigue como nota abajo.

**Escala — IP de egreso propia (futuro):** `aggTrades` pesa 20, límite 2400/min por IP, sin endpoint batch (peso lineal con símbolos). A ~20 símbolos con cadencia 20-30s ronda ~1000-1300/min (~mitad del budget compartido con LeanLive); techo duro ~25-30. Cuando el recorder supere ~15 símbolos o se quiera cadencia <15s, darle una IP de egreso separada (proxy/box chico) para que el data-plane tenga su propio budget de 2400 y se desacople del execution-plane.
**Contexto (diagnóstico cerrado 2026-06-19):** el WebSocket de Binance **Futures** (`fstream.binance.com`) **no entrega el push de datos a ninguna red ni cliente del proyecto**: conecta, manda el ACK del SUBSCRIBE, y después silencio con el socket `Open`. Reproducido en VPS (Alemania) y PC dev (Argentina), con tres runtimes (.NET Framework, .NET 10, Python). En las mismas máquinas: **Spot WS funciona**, **futures REST funciona** (`/fapi/v1/aggTrades`, `time`), y el **trading real de futures funciona** (orden 0.09 SOL filleada desde el VPS). Descartado empíricamente: geo-restricción (Argentina falla igual + se opera futures desde el VPS), proxy/firewall (no hay), código (spot anda con el mismo código). **Túnel descartado**: como no es geo, reubicar el egreso no cambia nada. Ver ADR-049.
**Decisión:** el recorder migra de `BinanceAggTradeWebSocketClient` a **REST polling de `/fapi/v1/aggTrades` por `fromId`**. No es solución degradada: devuelve los mismos aggregate trades que el stream, sin gaps, y la latencia es irrelevante para barras de 1h. Se introduce el puerto `ITradeFeed` con dos adapters seleccionables por env var `RECORDER_FEED` (WS queda como adapter válido para redes donde fstream sí entrega — no es dead code; REST por default). El `TradeHandler` no cambia.
**Robustez del adapter REST:** cursor `fromId` persistido por símbolo; drenaje en picos de volumen (re-poll inmediato hasta página <1000 para no atrasarse); ante gap grande tras downtime, **invalidar las barras afectadas — nunca fabricar dato** (lección Brief C); idempotencia por `aggId` monótono; **`429`/`Retry-After` con backoff** (innegociable: es lo que evita el ban `418`); cadencia configurable; `limit=1000` siempre.
**Escala y techo de rate-limit:** `aggTrades` pesa **20**, límite **2400/min por IP**, sin endpoint batch (el peso crece lineal con la cantidad de símbolos). A 3 símbolos es trivial (~120-240/min). A **~20 símbolos** con cadencia 20-30s ronda **~1000-1300/min** — sobrevive sin ban si se respetan los `429`, pero ya consume ~mitad del budget de IP **compartido con LeanLive** (el acople data-plane/execution-plane deja de ser despreciable). Techo duro alrededor de ~25-30 símbolos. **Trigger de escala — IP de egreso propia para el recorder:** cuando supere ~15 símbolos o se quiera cadencia <15s, darle al recorder una **IP de egreso separada** (proxy / box chico) para que el data-plane tenga su propio budget de 2400 y quede totalmente desacoplado del execution-plane. Es más simple y barato que el túnel WS descartado. Requiere nota en AI.md al implementarlo.

#### ⬜ DEUDA-2 — `OrderListHash` no determinista
**Estado:** pendiente, no bloqueante.
**Descripción:** Ver detalle en `DECISIONS.md` (DEUDA-2). El campo
`OrderListHash` del summary del backtest no es bit-idéntico entre
corridas del mismo modelo con la misma configuración, aunque los order
events sean idénticos. Workaround actual: validar no-regresión por
comparación de `transaction-log.csv`. Fix consiste en identificar qué
campos no-deterministas entran al hash y excluirlos.

#### ⬜ Fix POLICY 7.1 título "1h" vs config "1h"
**Estado:** pendiente, deuda documental.
**Descripción:** Hallazgo de ADR-026 re-anotado en ADR-027 y ADR-028. La
entrada de la estrategia de referencia en `POLICY.md` sección 7.1 está
titulada como TF "1h" pero el sistema corre actualmente la
configuración Config A que está en 1h (con baselines de los runs
post-fix de ADR-028). Trabajo: alinear título de la entrada con la
config real, o reescribir la entrada para no aludir a un TF específico
dado que la estrategia se ha validado en 15m, 1h y 4h.

### ⬜ HITOS POSTERIORES — Planificados

| Estado | ID | Hito | Pre-requisito | Comentario |
|---|---|---|---|---|
| ✅ | HITO-C | Paper trading (validación operativa del sistema) | Bloque 3 ✅ | **Completado 2026-06-09.** Primer trade 2026-06-09T00:30 UTC (BTCUSDT 15m), posición cerrada 04:36 UTC. Ciclo completo U1→U4 validado. Ver historial completado. |
| ✅ | HITO-E | Segunda estrategia manual — COMPLETADO 2026-06-11 | Hito C ✅ | **Batch 1 (OFI):** 13 candidatas evaluadas, OfiContrarianStrategy aprobada IS (Sharpe=0.503) pero rechazada Hito G (OOS Sharpe=-0.703). ADR-038, ADR-039. **Batch 2 (microestructura, 2026-06-11):** 10 hipótesis evaluadas (H1-H10). 5 pasaron M4. 2 APROBADAS Hito G: `CvdSellExhaustionStrategy` (IS=2.178 / OOS=1.718) y `TradeSizeInstitutionalStrategy` (IS=3.985 / OOS=4.186). 3 RECHAZADAS IS (H1 VwapDeviation=-0.369, H2 TradeCountSpike=-1.553, H10 SellingClimax=-5.128). Ver `Trading.Research/strategy_experiments.md`. |
| ✅ | HITO-F | Strategy Scaffolder | Hito E | **Completado 2026-06-11.** `New-Strategy.ps1` en raíz del repo. Genera clase `IStrategy` + tests skeleton. Imprime snippet JSON y entrada `StrategyFactory`. Ver historial completado. |
| ✅ | HITO-G | IS/OOS Validation + Monte Carlo + Métricas | Hito F | **Completado 2026-06-11.** `Trading.Analytics` (C#, strategy-agnostic): lee transaction-log.csv IS+OOS, calcula 9 métricas institucionales, block bootstrap MC 10k sims. Gate 1: Trades≥50, NetProfit>0, Sharpe≥0.3, PF≥1.1. Gate 2: P(Sharpe<0)≤20%, MedianMaxDD≤55%, P5 CAGR>-5%. Validaciones: OfiContrarianStrategy RECHAZADA; CvdSellExhaustion APROBADA (OOS Sharpe=1.718, P(Sharpe<0)=1%); TradeSizeInstitutional APROBADA (OOS Sharpe=4.186, P(Sharpe<0)=0%). Estado: **2 candidatas activas** listas para Hito D-prev / Hito D. |
| ⬜ | HITO-H | Optimización de Hiperparámetros | Hito G | Grid search / bayesiana con purged k-fold cross-validation (López de Prado) para evitar leakage temporal. El rango de búsqueda y el criterio los define el operador. |
| ✅ | HITO-D-prev | Validación de broker real (sin estrategia) | Hito G ✅ | **Completado 2026-06-15.** Orden real de prueba (0.09 SOL, ~$6.65) filleada en Binance USDT-M (FillPrice 73.87, fee 0.0033 USDT); balance reconciliado en 5661.48. Tres bloqueos de config resueltos: -1021 (clock guard, ADR-043), -2015 (permiso Futures en la key), -4061 (cuenta a One-way Mode). Fixes de infra: dead-man's switch ahora mide feed-liveness (ADR-042). Hook de validación removido. Flapping de `/public/ws` documentado como limitación de entorno (POLICY 2.4 → resolver con VPS). Ver ADR-041 historial y DECISIONS. |
| ✅ | HITO-D-feat | Pipeline de features de microestructura en vivo | Infra de Hito D ✅ | **Completado 2026-06-17.** Pipeline live de aggTrades implementado (ADR-046): (1) Lean Binance extendido mínimamente para capturar `is_buyer_maker` del WebSocket aggTrade → `Tick.SaleCondition`; (2) `AggTradeBucket` acumula ticks por barra 1h; (3) `MicrostructureFeatureComputer` replica exacto `_agg_1h()` del Python (paridad garantizada por 8 tests); (4) `LiveMicrostructureProvider` combina cómputo en vivo + fallback CSV para warmup; (5) `TradingAlgorithmHost` suscribe `Resolution.Tick` en live + `TickConsolidator(1h)`, acumula ticks en `OnData`, computa features en `DataConsolidated` antes de `ProcessBar`. Bloqueante resuelto. Próximo: re-deploy a VPS y arrancar `LeanLive`. |
| 🔄 | HITO-D | Live trading con capital mínimo | Hito D-prev ✅ + estrategia walk-forward ✅ + HITO-D-feat ✅ | **LeanLive ARRANCADO en el VPS 2026-06-22** con las 2 estrategias de microestructura en `minimal-position-mode` permanente (ADR-050). Estado: proceso `Running` estable (un solo `Started`, sin crash loop), warmup completado, heartbeat fresco, `KillSwitchActive:false`, feed de minuto vivo (`DataFeedStalenessSeconds≈0`), `minimal-position-mode ACTIVO` confirmado en log. **Obstáculos de arranque resueltos (todos de entorno/config, ninguno de código de trading):** (a) `data\` vacío → faltaban `market-hours-database.json` + `symbol-properties-database.csv` (copiados de `C:\Lean\Paper\data`); (b) `-1021` reloj → `w32tm /resync` (offset 173ms); (c) `Sync-TradingClock.ps1` no forzaba TLS 1.2 en PS 5.1 del VPS → corregido (commit); (d) `-1022` firma inválida → `binance-api-secret` pegado duplicado (128 chars → 64); (e) NSSM sin `AppStdout/AppStderr` → sin logs. **Herramienta nueva:** `deploy/Start-LeanLive.ps1` (pre-flight idempotente que valida todo lo anterior con fail-fast). **Verificado en código:** `StrategyHealthMonitor` real incondicional (no Null); `minimal-position-mode` se lee de **config.json** (no strategies.json). **Warmup desde store (ADR-051, 2026-06-23):** se detectó que las estrategias no señalizaban porque su warmup interno no se llenaba — Lean warmea reproduciendo history de precio y en live la suscripción es `Resolution.Tick`, sin history (`Tick resolution not supported`), así que las colas se llenaban a 1 barra/hora (~1-2 días ciego por reinicio). Fix: el store ahora persiste OHLCV y el host warmea las estrategias reproduciendo las barras del store por `EvaluateSignal` (genérico, open-closed). Incluye el fix del piso de warmup de 400h (ahora condicional a que haya clasificador de régimen). 309 tests verdes. **Requiere re-seed del store + redeploy** (Recorder con OHLC → borrar `*_live.csv` → re-siembra de Vision → redeploy LeanLive). **Próximo:** re-seed/redeploy en el VPS; luego monitorear primera señal (SL/TP `reduceOnly`, sizing min notional) y cadencia POLICY §4. Caveat (P3, ADR-050): el live-mínimo NO valida slippage a escala. |

### ⬜ BLOQUE 4 — Postergado (no urgente)

| Estado | ID | Refactor | Comentario |
|---|---|---|---|
| ⬜ | A4/A5 | Value Objects `Money`, `Price`, `Quantity`, `Notional` | Hacer cuando se agregue un segundo asset class o cuando aparezca un bug por confusión `decimal` → `decimal`. |
| ⬜ | A6 | `OrderNormalizer` separado del `PositionSizer` | Hacer cuando exista un segundo caller del `IOrderRouter` que no pase por el `PositionSizer`. |
| ⬜ | B2 | Jerarquía `DomainException` base | Mejora ergonomía, no previene bugs. Hacer cuando la cantidad de excepciones de dominio justifique la base común. |
| ⬜ | B5 | Proyecto separado `Trading.TestSupport` para fakes compartidos | Hacer cuando exista una segunda suite de tests que necesite los fakes. |
| ⬜ | A3 | `IOrderIdGenerator` inyectable | Purismo: testabilidad determinista del registry. `Guid.NewGuid()` funciona y no afecta dinero. |
| ⬜ | AUDIT-1 | Auditor independiente en Python con TA-Lib | Para live trading serio: auditoría verdaderamente independiente del runtime de QC. El auditor actual en C# detecta bugs de flujo de control y estado interno, pero comparte motor de cálculo con QC. Python + TA-Lib provee independencia plena. |
| ⬜ | SYSREG-1 | Régimen sistémico (segunda capa de clasificación, agregada al mercado) | Extensión natural de Hito B. Hito B clasifica régimen **por activo** (cómo se comporta BTC ahora, cómo se comporta SOL ahora). El régimen sistémico clasifica el estado del mercado cripto **en agregado** (risk-on/risk-off, alta correlación entre activos vs baja, dominancia de BTC vs alts). Vive en una capa superior y se compone con el régimen por activo: el sistémico responde "¿hoy es día para operar?", el específico responde "¿qué estrategia opera en este activo ahora?". Decisión técnica abierta cuando se implemente: qué índice usar (BTC dominance, market-cap-weighted top N, equal-weighted top N), cómo componer las dos señales cuando discrepan, qué hacer cuando el sistémico cambia a hostil mientras hay posiciones abiertas. Trigger sugerido: operar después de Hito E (segunda estrategia) y antes de escalar a >3 activos, cuando la diversificación cross-asset empieza a tener peso real en la curva de equity. Requiere ADR al implementarlo. |
| ⬜ | NEURAL-1 | Investigación de redes neuronales para clasificación de régimen (candidata a justificar extracción de `Trading.Regimes` como proyecto separado) | Exploración futura: evaluar si modelos neuronales (LSTM, Transformer, change-point detection con redes) agregan valor sobre el HMM de Hito B para clasificación de régimen. La abstracción `IMarketRegimeClassifier` se diseñó desde Hito B para ser agnóstica del algoritmo: una implementación neuronal se enchufa al lado de `AccordHmmClassifier` sin tocar nada del contrato ni del orquestador (open-closed). Trigger sugerido para activar este hito: cuando aparezca al menos una de tres señales — (1) dependencias pesadas o conflictivas (TorchSharp/ONNX runtime ~500MB pesa demasiado para meter directo en `Trading.Strategies`); (2) el pipeline de entrenamiento se vuelve un sub-sistema en sí mismo (data loaders, purged k-fold, hyperparameter tuning, experiment tracking); (3) múltiples clasificadores en producción que requieren orquestación de ensemble. Cuando una de las tres aplique, la decisión de extraer `Trading.Regimes` como proyecto separado se toma con evidencia concreta. Requiere ADR al implementarlo. |
| ⬜ | OPS-3 | Persistencia del estado de `StrategyHealthMonitor` entre reinicios del proceso | Hoy las métricas viven in-memory desde el arranque del proceso (ADR-014 + ADR-023). Si el proceso reinicia, se pierde historial reciente; el monitor entra en warm-up y los rolling se re-arman tras los próximos 50 trades. Aceptable para paper. En live serio, una caída del proceso seguida de restart resetea la detección U3/U4 silenciosamente: si la estrategia venía generando alertas sostenidas que aún no llegaban a 10 trades consecutivos, el contador se borra. Fix esperado: serializar `HealthSnapshot`-equivalente por estrategia a `health/strategy-health-{executorIdentifier}.json` con flush atómico cada N trades cerrados; cargar al boot. Decisión técnica abierta: ¿qué pasa si el archivo está corrupto al cargar? (fail loud vs. arrancar warm). Requiere ADR propio. Trigger: antes de migrar a live serio (post Hito D). |
| ⬜ | EVCAL-1 | `EventCalendarMonitor` — automatización de la pausa ±30min en eventos macro programados | Automatización de la regla operativa documentada en `POLICY.md` sección 2.3 (FOMC, CPI USA, NFP, halvings de BTC, anuncios regulatorios). Hoy se cumple manualmente: el operador consulta calendario económico semanalmente y desactiva la(s) estrategia(s) en `strategies.json` antes del evento. Implementación futura: consultar un proveedor de calendario económico (ForexFactory scraping, Trading Economics API, FRED, o equivalente), exponer `IEventCalendar` en Domain, instrumentar `BarProcessingService` para consultar el calendario antes de generar señales nuevas, y bloquear entradas (no salidas, no gestión de abiertas) durante la ventana ±30min. Trigger sugerido para activarlo: cuando aparezca al menos una de tres señales — (1) segunda estrategia activa simultáneamente en producción (coordinar pausa manual de varias estrategias se vuelve error-prone); (2) sistema operando >7 días seguidos sin supervisión humana diaria; (3) un incidente concreto registrado en `DECISIONS.md/incidents/` de "se pasó pausar antes del evento y entró posición en mal momento". Decisión técnica abierta cuando se implemente: qué proveedor de calendario, cómo manejar caída del proveedor (fail-safe: ¿pausar todo o continuar?), cómo manejar timezone (eventos publicados en horarios locales del país emisor). Requiere ADR al implementarlo. |

---

## Historial completado

> Los refactors completados se mueven acá con su fecha y un resumen de qué cambió. Orden cronológico: más antiguo arriba.

### ✅ HITO D-prev — Validación de broker real Binance USDT-M
**Fecha:** 2026-06-15
**Resumen:** Se ejecutó el protocolo de ADR-041 contra Binance USDT-M live. Conectividad validada (balance 5661.48 USDT, suscripciones BTC/ETH/SOL, warmup, proceso estable +1h). Orden de prueba de 0.09 SOL (~$6.65) filleada correctamente (FillPrice 73.87, OrderFee 0.0033 USDT, BrokerId 220782487914); posición cerrada manualmente; balance reconciliado dentro del 0.5%. Se diagnosticaron y resolvieron tres bloqueos de configuración de cuenta/entorno, ninguno de código de trading: **-1021** (drift de reloj > 1000ms → guard NTP con fallback a server time de Binance, ADR-043), **-2015** (API key sin permiso de Futures trading → habilitado en Binance), **-4061** (cuenta en Hedge Mode vs brokerage QC One-way → cuenta cambiada a One-way Mode). Además se corrigieron dos bugs de infraestructura del host: el `DrawdownMonitor` se inicializaba con el balance default de Lean antes de cargar el broker (movido a `OnWarmupFinished`), y el dead-man's switch mataba el proceso post-warmup porque medía cierre de barras de estrategia (1h) en vez de liveness del feed de minuto (rediseñado, ADR-042). Hook `PlaceBrokerValidationOrderIfRequested` removido. Estrategias 7.4/7.5 promovidas a `paper` en POLICY. Limitación de entorno documentada: flapping del WebSocket auxiliar `/public/ws` por red local restrictiva (misma que bloquea NTP) — no afecta trades ni órdenes; se resuelve migrando a VPS (POLICY 2.4). Ver ADR-041, ADR-042, ADR-043.

### ✅ HITO G — IS/OOS Validation + Monte Carlo
**Fecha:** 2026-06-11
**Resumen:** Pipeline reproducible de validación de estrategias implementada como herramienta C# standalone `Trading.Analytics` (proyecto console, net10.0, strategy-agnostic). Lee `transaction-log.csv` generado por Lean para IS y OOS, reconstruye trades completos (FIFO pairing), calcula 9 métricas institucionales (Sharpe, Sortino, Calmar, Profit Factor, Expectancy, Win Rate, Max DD, CAGR, Recovery Factor), corre Monte Carlo con block bootstrap (bloque=5, overlapping, 10k simulaciones, seed=42) sobre trades OOS, y evalúa dos gates de aprobación. Gate 1 (métricas deterministas OOS): Trades≥50, NetProfit>0, Sharpe≥0.3, PF≥1.1, Expectancy>0. Gate 2 (distribución MC): P(Sharpe<0)≤20%, MedianMaxDD≤55%, P5 CAGR>-5%. Exit code 0 si pasa, 1 si falla. Genera reporte markdown con tabla comparativa IS vs OOS. Primera estrategia validada: `OfiContrarianStrategy` — IS Sharpe=0.564 (Gate 1: PASA), OOS Sharpe=-0.703 (Gate 1+2: FALLA). Diagnóstico: Win rate colapsó 44%→36%, P(Sharpe<0)=77%. Edge ligado a bull market 2021-2024, no generaliza. Estrategia eliminada del repo. Ver ADR-039.

### ✅ HITO F — Strategy Scaffolder
**Fecha:** 2026-06-11
**Resumen:** Script PowerShell `New-Strategy.ps1` en la raíz del repo. Uso: `.\New-Strategy.ps1 -Name RsiMeanReversion`. Genera dos archivos: (1) `Trading.Strategies/Implementations/{Name}Strategy.cs` con clase `sealed`, `IStrategy`, `WarmUpBars`, `EvaluateSignal` con `Dictionary<string, object>` por ticker y TODO comments; (2) `Trading.Application.Tests/Strategies/{Name}StrategyTests.cs` con tres tests stubs: `WarmUpBars_ReturnsExpectedValue`, `EvaluateSignal_DuringWarmUp_ReturnsFlat` y `EvaluateSignal_TODO_DescribeScenario`. Imprime en consola: línea de registro en `StrategyFactory.cs` y snippet JSON para `strategies.json`. Guard fail-loud si alguno de los dos archivos ya existe. Normalización del nombre: acepta con o sin sufijo "Strategy". Implementado en PowerShell puro (sin dependencias externas), ASCII-only para compatibilidad con PS 5.1 sin BOM. Motivación: con objetivo de cartera multi-estrategia/multi-activo, el scaffolder reduce el costo marginal de evaluar cada candidata en fase M4 y garantiza que todas las estrategias arrancan con la estructura y convenciones correctas.

### ✅ HITO C — Paper trading: validación operativa del sistema
**Fecha:** 2026-06-03 (inicio) → 2026-06-09 (cierre)
**Resumen:** Validación operativa completa del sistema bajo wall-clock real con paper brokerage de Lean y data feed real de Binance Futures USDM. El propósito del hito fue validar la infraestructura, no la estrategia (EmaCrossStrategy es estrategia de desarrollo sin walk-forward). **Infraestructura verificada** desde 2026-06-03: feed sano (`BarStalenessSeconds` ~143-320s en mercado activo), heartbeat actualizándose cada 60s, pings a Healthchecks.io cada 5 min, JSONL escribiendo correctamente. **Tres bugs corregidos durante el hito** (ADR-031): (1) LeanClock UTC offset (+4h) — `_algorithm.Time` → `_algorithm.UtcTime`; (2) auto-restart vía `Environment.Exit(1)` cuando staleness > 1200s (parche operativo para race condition del plugin Binance); (3) epoch QC 1997-12-31 en Initialize() — fallback a `DateTime.UtcNow` cuando `UtcTime < año 2000`. **Fix adicional durante el hito** (ADR-032): warm-up dinámico de indicadores internos de estrategia — `WarmUpBars` en `IStrategy`, `isWarmingUp` flag en `BarProcessingService`, cálculo dinámico de `SetWarmUp` como `max(HMM mínimo, max estrategias × timeframe)`. **Primer trade real** 2026-06-09T00:30 UTC (BTCUSDT 15m, señal de cruce de EMA), posición cerrada 2026-06-09T04:36 UTC (SL o TP). Ciclo completo U1→U4 validado con equity en movimiento. `KillSwitchActive: false` — riesgo dentro de parámetros en todo momento. Tests al cierre: 146 verdes (143 Application + 3 nuevos de warm-up). Ver ADR-031, ADR-032.

### ✅ Validación multi-símbolo + fix estructural OPS-2 (ADR-028)
**Fecha:** 2026-05-26 / 2026-05-27
**Resumen:** Cierre de la validación de agnosticismo del subsistema de
ejecución/monitoreo bajo operación concurrente real con tres símbolos
(BTCUSDT, ETHUSDT, TRBUSDT) en mismo TF (1h), con sus propios
clasificadores HMM independientes (Opción A — un HMM por símbolo).
Cambios principales: `HmmTrainer` parametrizado por instrumento vía
CLI (`--instrument`, `--data-dir`, `--output`), `MinimumRequiredBars`
ajustado de 10000 a 5000 (piso técnico defendible para HMM-GMM K=4
multi-seed), output default del trainer pasa a `Trading.Models/regime/staging/`
con promoción manual gateada por criterios uniformes (K∈{3,4}, al menos
un estado Trend, ningún estado <5% ni >70%, ningún label agregado >85%).
ETHUSDT entrenado (K=4, BIC 56707.84, Trend 52% / Squeeze 31% / HighVol
17%) y TRBUSDT entrenado (K=4, BIC 49814.19, Trend 47% / Squeeze 40% /
HighVol 13%) — ambos pasan los 6 criterios y promovidos a producción.
Modelo BTC re-entrenado con multi-seed en ADR-027 antes de esta
validación. **Hallazgo crítico durante el primer backtest paralelo:**
dos violaciones del invariante OPS-2 producidas por un bug estructural
latente del flujo `LiquidateAll` del kill switch global, que existía
desde antes pero no se manifestaba en single-symbol. Causa raíz:
`LeanBrokerageAdapter.LiquidateAll` llamaba `_algorithm.Liquidate()`
(helper de Lean) produciendo órdenes con `Tag = "Liquidated"` no
registradas en `OrderRegistry`, que `OrderEventMapper` descartaba y
dejaban a `StrategyHealthMonitor` desincronizado de Lean. Fix
estructural: `IOrderRouter.LiquidateAll()` eliminado del contrato,
`LeanBrokerageAdapter.LiquidateAll()` eliminado de la implementación,
`LiquidateAllRiskAction` refactorizado para recibir lista de
instrumentos activos por inyección e iterar con
`LiquidateInstrument(instrumentId, OrderPurpose.Liquidate,
"RiskOrchestrator_KillSwitch")` solo para los efectivamente invertidos.
`OrderPurpose.Liquidate` agregado al enum del dominio. Cambios
colaterales aceptados (con nota de proceso por desviación del brief
original): `OrderLifecycleService` broadcast del fill al executor del
instrumento cuando el `ExecutorIdentifier` sintético del kill switch no
matcha, condicionado a `Purpose==Liquidate && Status==Filled`;
`StrategyHealthMonitor` case nuevo `Liquidate` con guard de posición
abierta. **Backtest post-fix 2025-01-01 → 2026-03-31:** cero OPS-2
invariante violado, cero `OrderEventMapper: evento sin tag` durante
liquidación dirigida, 5/5 criterios cualitativos verdes en los 3
executors, 3 kill switches activados sin ejercitar el path del
broadcast por estado real (cobertura del path por 8 tests unitarios
nuevos). Test suite final: 132 verdes (11 nuevos: 4
`LiquidateAllRiskActionTests`, 5 `OrderLifecycleServiceLiquidateTests`,
3 `StrategyHealthMonitorTests`). Deudas que quedan abiertas: DEUDA-2
(`OrderListHash` no determinista), allocator multi-estrategia, POLICY 7.1
título "1h" vs "15m" actual, varianza numérica del trainer en dígitos
12+. EmaCrossStrategy sigue VETADA para live por POLICY P1. Ver ADR-028.

### ✅ ADR-027 — Re-entrenamiento de BTC con trainer multi-seed (alineación post-DEUDA-1)
**Fecha:** 2026-05-26
**Resumen:** Al abrir sesión multi-símbolo, la verificación de no-regresión del `HmmTrainer` parametrizado reveló que el modelo de producción BTC fue generado antes del commit `6f72dcc` (DEUDA-1, multi-seed Baum-Welch). Decisión del operador: re-entrenar BTC para tener flota consistente con los futuros modelos de ETH y TRB. Modelo preDEUDA1 conservado como `BTCUSDT-perp-binance.hmm.json.preDEUDA1`. Nuevo modelo: K=4, BIC=57643.8833, mapping `{0:Trend, 1:Trend, 2:Squeeze, 3:HighVolatility}`. Test granular ventana 3 (crash feb 2025): crash de Feb 3 sigue clasificado como `Trend` ✓. Backtest BTC-15m post-reentrenamiento: resultados bit-idénticos a ADR-026 (147 órdenes, End Equity 87148.16, DD 21.5%, Sharpe -1.288, U2 dispara 2025-02-06, OPS-2 invariante violado 0, evento sin tag 0). La invarianza semántica del modelo explica la identidad de resultados. Ver ADR-027.

### ✅ DEUDA-1 — SemanticStateMapper adaptativo a K + multi-seed Baum-Welch + validación cruzada del modelo HMM
**Fecha:** 2026-05-22
**Resumen:** Cierre de la deuda técnica documentada en ADR-020: el test `AccordHmmClassifierReferenceTests.Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente` estaba marcado `[Fact(Skip = "...")]` por convergencia degenerada del pipeline HMM con K=3 sobre serie sintética. **Fase 1 (diagnóstico):** ejecución del test instrumentado con `ITestOutputHelper` confirmó dos hipótesis simultáneas: (A) Baum-Welch convergía a óptimo local malo con seed=42 (dos estados colapsados a parámetros casi idénticos); (B) `SemanticStateMapper.Build` calculaba `topQuartileThreshold = Ceiling(K * 0.75)`, que con K=3 da 3, haciendo `positionInSorted >= 3` insatisfacible en array de 3 elementos — ningún estado se mapeaba a `HighVolatility`. **Fase 2 (fix):** (1) `SemanticStateMapper` refactorizado con umbrales adaptativos a K: K=2 binario, K=3 tercios, K≥4 cuartiles tradicionales; (2) `HmmTrainer/Program.cs` extendido con multi-seed Baum-Welch (10 seeds `42*i+17`, conserva el de mayor log-likelihood); (3) mismo fix de multi-seed aplicado al método auxiliar del test de referencia. **Fase 3 (revalidación):** test pasa verde. `SemanticStateMapperTests` extendido con 5 tests adicionales cubriendo K=2, K=3 (caso bug), K=3 con Squeeze, K=4, K=5. **Fase 4 (validación cruzada):** 5 ventanas históricas de BTCUSDT (2025-2026) inspeccionadas visualmente por el operador contra las clasificaciones del modelo de producción (K=4). Todas OK o AMBIGUAS (ninguna contradicción frontal). Ventana 3 (2025-01-26 → 2025-02-10) resuelta con consulta granular barra a barra vía `ProductionHmmGranularQueryTests.cs`: el crash de Feb 3 (caída direccional ~8%) fue clasificado como `Trend`, no `HighVolatility` — coherente con la definición del modelo (crashes direccionales = Trend; caos bidireccional = HighVolatility). **Fase 5:** NO re-entrenamiento. Modelo de producción válido, baseline de 6 órdenes (ADR-023) preservado. **Fase 6 (cierre):** instrumentación TEMP DEUDA-1 removida; `[Fact(Skip)]` → `[Fact]`; ADR-024 nuevo en DECISIONS.md; ADR-020 pasa a "Resuelta". `ProductionHmmGranularQueryTests.cs` y `briefs/DEUDA_1_ventana3_granular.md` commiteados como evidencia durable. Ver ADR-024.

### ✅ DEUDA-2 — `Initialize()` doble en backtest: NO reproducible al ejecutar diagnóstico
**Fecha:** 2026-05-22
**Resumen:** Diagnóstico ejecutado según brief `DEUDA_2_BRIEF.md` (Fase 1: instrumentación con contador atómico de invocaciones y log con hash de instancia). Resultado: `Initialize()` se ejecuta **UNA sola vez** en backtest. La consola de Lean reporta `llamada #1` una vez y el JSONL del run (`trading-2026-05-22.jsonl`, 6 líneas totales) muestra cada uno de los mensajes de arranque del host (`HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada`, `Heartbeat flush timer deshabilitado`) exactamente una vez. La deuda documentada al cierre de INFRA-2 (ADR-021) no es reproducible con el código actual. Causa probable: el diagnóstico original fue por inferencia (logs duplicados → ergo doble invocación), no por instrumentación directa; los duplicados observados al cierre de INFRA-2 pudieron tener otra causa que se resolvió incidentalmente con los cambios de OPS-1/OPS-2 al wiring del host (no se conserva el JSONL del cierre de INFRA-2 para confrontación directa). **NO se aplica guard de idempotencia:** fixes solo a problemas reproducidos (regla institucional, consistente con Riesgo 2 del brief `DEUDA_2_BRIEF.md`). La instrumentación temporal de Fase 1 (`_initializeCallCount` + logs de hash de instancia) fue revertida; el código de `TradingAlgorithmHost.cs` queda idéntico al estado pre-Fase 1. **Validación pendiente en Hito C:** al arrancar paper trading, inspeccionar el JSONL inicial para confirmar que el síntoma tampoco aparece en modo Live; si aparece, abrir nueva deuda con diagnóstico fresco. Sin cambios de código de producción. Sin ADR nuevo (decisión documentada en esta entrada del historial y nota al ADR-021).

### ✅ Refactor inicial — Naming consistente
**Fecha:** sesión 1
**Resumen:** todos los identificadores en inglés, campos privados con `_`, eliminación de abreviaturas. Comportamiento preservado.

### ✅ Refactor de RiskParameters como Value Object
**Fecha:** sesión 1
**Resumen:** creación de `RiskParameters` value object con invariantes (stop, take profit, riesgo por trade) verificadas en construcción. Eliminado fallback silencioso `if (stopLossPercentage <= 0) ...= 0.03m` del `PositionSizer`. Conversión `/100m` centralizada en `FromPercentages`. 21 tests xUnit.

### ✅ Refactor de desacople de QuantConnect
**Fecha:** sesión 1
**Resumen:** creación de `Trading.Application` como proyecto separado. Introducción de abstracciones del dominio: `IPortfolioState`, `IInstrumentMetadata`, `IOrderRouter`, `IOrderHandle`, `IClock`, `ITradingLogger`, `IPriceRounder`. `MarketBar` reemplaza `MarketData`. `InstrumentId` reemplaza `Symbol` de QC en el dominio. Adaptadores Lean creados en `Trading.Strategies/Adapters`. 12 tests adicionales del `KillSwitchManager` con fakes. Invariante: `Trading.Domain` y `Trading.Application` cero `using QuantConnect`.

### ✅ Unificación de carpetas `Interfaces/` → `Abstractions/`
**Fecha:** sesión 1
**Resumen:** eliminada la carpeta `Trading.Domain/Interfaces/`. Movidos `IStrategy` e `IStrategyConfigLoader` a `Trading.Domain/Abstractions/`.

### ✅ RiskPerTradePercentage por estrategia en JSON
**Fecha:** sesión 1
**Resumen:** campo `RiskPerTradePercentage` obligatorio en `strategies.json`. `StrategyConfigLoader` falla loud si está ausente (decimal nullable para distinguir "ausente" de "presente con valor 0"). Eliminado el default 2% hardcodeado en `TradingAlgorithmHost`.

### ✅ Eliminación de stringly-typed tags
**Fecha:** sesión 1
**Resumen:** enum `OrderPurpose { Entry, StopLoss, TakeProfit, TimeExit }` reemplaza strings ENTRY/SL/TP/TIME. `OrderRegistry` central en `Trading.Application` mapea tags opacos (`ord_xxxxxxxx`) a registraciones estructuradas. `IOrderRouter` cambia firma para recibir `OrderPurpose` + `executorIdentifier`. `OrderLifecycleEvent` expone `Purpose` y `ExecutorIdentifier` resueltos. Cleanup automático del registry tras eventos terminales. 9 tests adicionales del `OrderRegistry`.

### ✅ Fix eventos huérfanos y sobreescritura de tags
**Fecha:** sesión 2
**Resumen:** descubierto en log de operaciones que `OrderTicket.Cancel(reason)` de Lean sobreescribe el `Tag` del ticket. Fix en `LeanOrderHandle.Cancel`: ya no propaga el reason a Lean. `OrderEventMapper` distingue tag con nuestro prefijo (residual esperado, Debug) de tag externo (liquidación global, Debug con mensaje distinto). `OrderLifecycleService` loguea Info con motivo de cancelación antes de invocar `Cancel`. Logs limpios: 0 mensajes anómalos en backtest posterior.

### ✅ Habilitar Long y Short en estrategias
**Fecha:** sesión 2
**Resumen:** enum `SignalDirection { Flat, Long, Short }` reemplaza el `bool` de `IStrategy.EvaluateSignal`. `EmaCrossStrategy` ahora produce `Short` en cruces bajistas (antes los ignoraba). `BarProcessingService` aplica signo a la cantidad según dirección. `PositionSizer` sigue devolviendo magnitud positiva (sin cambios). Sin tests nuevos por decisión de minimalismo.

### ✅ Refactor B1 — Result<T> donde había magic values (alcance: PositionSizer)
**Fecha:** 2026-05-12
**Resumen:** Se crearon dos tipos genéricos en `Trading.Domain/Abstractions/`: `Result<TValue, TFailureReason>` y `Result<TFailureReason>` como `readonly record struct` para evitar allocations en el hot path. Se creó el enum `SizingFailureReason` con tres valores: `InvalidPrice`, `QuantityRoundsToZero`, `BelowMinimumNotional`. `PositionSizer.CalculateQuantity` cambió de retornar `decimal` (magic value `0m` ante error) a `Result<decimal, SizingFailureReason>`: ahora distingue explícitamente éxito, precio inválido y cantidad que redondea a cero. `PositionSizer.IsValidNotional` fue renombrado a `ValidateNotional` y retorna `Result<SizingFailureReason>`. `BarProcessingService` fue adaptado como caller: agrega `ITradingLogger` al constructor (también wireado en `TradingAlgorithmHost`) y loguea en Debug el motivo de skip al recibir un `Failure`. Se crearon `FakeInstrumentMetadata`, `FakeStrategy` en el proyecto de tests. Se añadieron 7 tests nuevos en `PositionSizerTests`. Total: 29 tests verdes (0 errores). Invariante arquitectónica preservada: cero `using QuantConnect` en Domain/Application/Tests.

### ✅ Refactor B3 — Eventos de dominio tipados
**Fecha:** 2026-05-12
**Resumen:** Se creó la marker interface `IDomainEvent` y cuatro eventos tipados en `Trading.Domain/Events/`: `OrderSubmittedEvent`, `OrderFilledEvent`, `OrderCanceledEvent` y `RiskLimitBreachedEvent` (con enum `RiskLimitBreachReason`). Se definió la interfaz `IDomainEventBus` en `Trading.Domain/Abstractions/` y se implementó `DomainEventBus` en `Trading.Application/Eventing/`: bus síncrono in-memory con snapshot de suscriptores bajo lock, aislamiento de fallos (un suscriptor que lanza loguea Error y el bus continúa). `KillSwitchManager`, `BarProcessingService` y `OrderLifecycleService` reciben `IDomainEventBus` e `IClock` por constructor y emiten el evento correspondiente en cada transición crítica. `TradingAlgorithmHost` construye el bus y lo inyecta en todos los servicios. Se agregaron `CapturingEventSubscriber<TEvent>` para tests, 7 tests nuevos en `DomainEventBusTests` y 1 test nuevo en `KillSwitchManagerTests`. Total: 37 tests verdes (0 errores). Bloque 1 completo; sistema listo para Hito A.

### ✅ Refactor A2 — Logging estructurado con placeholders nombrados
**Fecha:** sesión 3 (2026-05-11)
**Resumen:** `ITradingLogger` extendido a 5 niveles (`Debug`, `Info`, `Warning`, `Error`, `Critical`) con firma `(string messageTemplate, params object[] arguments)`. `LeanLogger` convierte placeholders nombrados a posicionales via regex antes de `string.Format`. `FakeTradingLogger` reemplaza las tres `List<string>` por `List<CapturedLogEntry>` con `Level`, `MessageTemplate` y `Arguments`. Migrados 10 call sites en `OrderLifecycleService`, `KillSwitchManager`, `PositionSizer` y `OrderEventMapper`: eliminada toda interpolación `$"..."`. `ActivateKillSwitch` sube de `Error` a `Critical`; `EvaluateCoolingOffPeriod` sube de `Debug` a `Info`. Eliminados prefijos manuales de timestamp. Test `ActivateKillSwitch_LiquidatesAndLogsError` actualizado a `CriticalEntries`. Logs parseables por herramientas de observabilidad (Seq, Datadog). Sin cambios de comportamiento funcional. 21 tests Domain + 20 Application = 41 verde.

### ❌ Fix — SignalAuditor: tolerancia relativa en lugar de absoluta (REVERTIDO)
**Fecha original:** 2026-05-12
**Fecha de reversión:** 2026-05-13
**Razón:** todo el enfoque del SignalAuditor fue eliminado. Ver ADR-014.

### ❌ Fix — SignalAuditor: eliminar falsos positivos en recálculo de EMA (REVERTIDO)
**Fecha original:** 2026-05-12
**Fecha de reversión:** 2026-05-13
**Razón:** todo el enfoque del SignalAuditor fue eliminado. Ver ADR-014.

### ❌ Hito A — Auditor de fidelidad de señales en backtest (REVERTIDO)
**Fecha original:** 2026-05-12
**Fecha de reversión:** 2026-05-13
**Razón:** diseño equivocado. Recalcular indicadores en vivo durante el backtest dentro del mismo proceso es duplicación, no auditoría. Tras cuatro fixes iterativos (buffer, warm-up, tolerancia, algoritmo) persistían ~33% de discrepancias sin causa raíz clara. Reemplazado por tests unitarios estáticos contra valores de referencia (baseline QC), que es el estándar institucional documentado por la propia QuantConnect. Ver ADR-014.

### ✅ Hito A (versión 2) — Tests de referencia de indicadores y estrategias
**Fecha:** 2026-05-13
**Resumen:** eliminado completamente el SignalAuditor y todo el código del enfoque anterior (9 archivos borrados, 4 modificados). Reemplazado por dos tipos de tests unitarios estándares institucionales: (1) tests de referencia que verifican que ExponentialMovingAverage de QC produce valores equivalentes al baseline QC sobre serie sintética conocida (QC valida internamente contra TA-Lib), (2) tests de comportamiento de EmaCrossStrategy con datos sintéticos diseñados para forzar cruces alcistas y bajistas. Cobertura institucional sin overhead runtime. 6 tests nuevos. Total verde: 43 tests. Sanity check final humano (verificación de 3-5 señales en TradingView antes de pasar a paper trading) queda como práctica recomendada, no automatizada.

### ✅ Refactor #4 — Separar IRiskMonitor de IRiskAction
**Fecha:** 2026-05-13
**Resumen:** `KillSwitchManager` (que mezclaba detección y acción) descompuesto en componentes con responsabilidad única: `IRiskMonitor` (detección) + `IRiskAction` (mitigación) + `RiskOrchestrator` (coordinación). Tres componentes de risk: `DrawdownMonitor`, `ConsecutiveLossesMonitor` (ambos `IRiskMonitor`) y `CoolingOffTracker` (componente separado porque señala desactivación, no activación). `LiquidateAllRiskAction` como única implementación de `IRiskAction`. El sistema queda preparado para Hito B: agregar `RegimeIncompatibilityMonitor` será crear una clase nueva sin modificar nada existente (open-closed). 14 tests nuevos. Backtest produce operaciones idénticas (162). Bloque 2 completo.

### ✅ Hito B — Paso 1: Pre-requisitos arquitectónicos del Domain
**Fecha:** 2026-05-14
**Resumen:** Tres extensiones al Domain para habilitar el resto del Hito B sin acoplamientos prematuros. (1) `MarketBar` extendido a OHLCV completo (`Open`, `High`, `Low`, `Close`, `Volume` como `decimal`), con constructor primario `(InstrumentId, decimal open, decimal high, decimal low, decimal close, decimal volume, DateTime)` y constructor legado `(InstrumentId, decimal close, DateTime)` marcado `[Obsolete]` para retrocompatibilidad temporal (delega al nuevo poblando OHL con close y volumen en 0). (2) `StrategyDefinition` gana propiedad `List<string>? CompatibleRegimes` nullable, modelada como `List<string>` concreto (no `IReadOnlyList`) por consistencia con `RootConfig.Timeframes` y para evitar fricción con la deserialización de Newtonsoft.Json. (3) `RiskLimitBreachReason` extendido con `RegimeIncompatibility` (no se emite todavía, queda definido en el vocabulario del dominio para uso futuro). El `MarketBarMapper` en `Trading.Strategies/Adapters/` actualizado para construir `MarketBar` con OHLCV completo desde el `TradeBar` de Lean. El `StrategyConfigLoader` valida que `CompatibleRegimes`, si está presente, no sea lista vacía (mensaje explícito: ausencia = compatible con todo, lista vacía = inválido). Tests nuevos: `MarketBarTests` (3 tests del constructor nuevo y el obsoleto) y tres tests del loader (`Load_FallaSiCompatibleRegimesEstaPresenteVacio`, `Load_AceptaSiCompatibleRegimesEstaAusente`, `Load_AceptaSiCompatibleRegimesTieneValores`). Compilación verde. Backtest sin cambios funcionales: produce los mismos resultados que pre-Paso-1. Ver ADR-017.

### ✅ Hito B — Paso 2: Abstracciones de régimen + filtro pre-orden con classifier fake
**Fecha:** 2026-05-15
**Resumen:** Implementación completa de la infraestructura de régimen sin acoplarse a ningún algoritmo concreto. **Domain (`Trading.Domain/Abstractions/Regimes/`):** `RegimeLabel` enum (`Unknown`, `Trend`, `MeanReverting`, `HighVolatility`, `Squeeze`) + `RegimeLabelParser.Parse(string)` con mensajes de error explícitos (rechaza `Unknown` como configuración explícita); `RegimeClassification` record con `Label`, `Probabilities` (distribución completa por `double`), `ClassifiedAtUtc` y constructor estático `UnknownFor`; `IMarketRegimeClassifier` contrato agnóstico del algoritmo (ningún método ni propiedad delata HMM o cualquier otro). **Application (`Trading.Application/Regimes/`):** `MarketRegimeRegistry` con mapa instrumento → classifier y cache de última clasificación (instrumento sin classifier → fail-safe a `Unknown`); `ConfigurableMarketRegimeClassifier` que devuelve siempre una etiqueta fija (útil para tests y validación de wiring); `StrategyRegimeCompatibility` con tres reglas fail-safe (null → compatible con todo, vacío → compatible con todo, `Unknown` siempre compatible). **Integración:** `BarProcessingService` recibe `MarketRegimeRegistry` y `IReadOnlyDictionary<string, StrategyRegimeCompatibility>` por constructor; el filtro se inserta como guard `continue` después del check de `KillSwitchActivated` y `SignalDirection.Flat`, antes de los checks de `IsInvested` y `HasOpenOrders`. **Wiring en `TradingAlgorithmHost`:** construcción del registry con `ConfigurableMarketRegimeClassifier(BTCUSDT, Trend)`, parseo de `CompatibleRegimes` de cada `StrategyDefinition` a `RegimeLabel`, **consolidator 4h dedicado e independiente** que alimenta al registry (separado de los consolidators de estrategias por separation of concerns — el régimen es un concepto ortogonal a las estrategias y vive en timeframe propio). Tests nuevos (~30): `RegimeLabelTests`, `RegimeClassificationTests`, `ConfigurableMarketRegimeClassifierTests`, `MarketRegimeRegistryTests`, `StrategyRegimeCompatibilityTests`, `BarProcessingServiceRegimeFilterTests` (6 escenarios end-to-end del filtro). Hallazgo arquitectónico: el filtro NO va por `RiskOrchestrator` (el patrón de guards `continue` en `BarProcessingService` es la abstracción correcta para un filtro pre-orden por contexto; el `RiskOrchestrator` queda para condiciones catastróficas que justifican liquidar todo). Pendiente: agregar `"CompatibleRegimes": ["Trend"]` al `strategies.json` para activar el filtro en runtime. Ver ADR-017.

### ✅ Hito B — Paso 3: HMM real con Accord.NET, trainer offline y modelo entrenado de BTCUSDT
**Fecha:** 2026-05-19
**Resumen:** Cierre del Hito B. Reemplazo del `ConfigurableMarketRegimeClassifier` (fake) por `AccordHmmClassifier` (HMM real con emisiones Multivariate Gaussian, topología ergódica, decodificación Viterbi + forward filtering). **Trading.Strategies/Regimes/** gana 7 archivos nuevos: `AccordHmmClassifier`, `AccordHmmClassifierFactory`, `PersistedHmmModel`, `HmmModelSerializer`, `SemanticStateMapper`, `BinanceKlinesParser`, `FeatureExtractor`, `FeatureScaler`. **Trading.Strategies/Tools/HmmTrainer/** es un proyecto de consola standalone (`net10.0`, Exe) que entrena el HMM offline con datos históricos de Binance Klines (ventana 2020-01-01 a 2024-12-31 UTC, 10912 features tras descarte de 50 barras de warm-up de SMA50), prueba K ∈ {2, 3, 4} y elige por BIC mínimo. Inicialización canónica HMM-GMM por k-means clustering de las observaciones para romper simetría inicial (sin esta inicialización BaumWelch no convergía). **K elegido: 4** con BIC=57643.94 (margen 12.5% sobre K=3, 20% sobre K=2). Mapeo semántico resultante: estado 0→HighVolatility, estado 1→Squeeze, estado 2→Trend, estado 3→Trend (dos estados Trend con bias positivo y negativo respectivamente; permitido por el brief y manejado por el classifier sumando probabilidades por etiqueta). El modelo se serializa a `Trading.Models/regime/BTCUSDT-perp-binance.hmm.json` (JSON indentado por System.Text.Json, legible en code review) y se commitea al repo como artefacto versionado. MSBuild lo copia a `{OutputDir}/Trading.Models/regime/` en cada build. **Refactor del wiring de TradingAlgorithmHost:** extracción dinámica de instrumentos únicos del `strategies.json` que tienen estrategias con `CompatibleRegimes`, carga del modelo correspondiente por convención de naming, fail-loud al boot si una estrategia depende del régimen pero el modelo no existe. Eliminación del hardcoding previo de `btcInstrumentId`. **Fix crítico del consolidator de régimen:** quitado el `if (IsWarmingUp) return;` del handler (irrelevante con el fake del Paso 2, bug con el HMM real que necesita procesar barras durante el warm-up para calentar su buffer interno de 100 features). **SetWarmUp** extendido de 1 día a 20 días de calendario para cubrir las 100 barras 4h del warm-up del HMM con margen. **Nuevo método** `MarketRegimeRegistry.GetRegisteredInstruments()` para wiring agnóstico. **Nuevo proyecto** `Trading.Strategies.Tests` con tres test fixtures: `AccordHmmClassifierReferenceTests` (pipeline completo sobre serie sintética con 3 regímenes — Trend alcista, HighVolatility, MeanReverting — verificando que K=3 minimiza el BIC, que las clasificaciones discriminan los tres segmentos y que IsWarmedUp toggea correctamente), `SemanticStateMapperTests` (5 escenarios de las reglas de mapeo: cuartil superior, cuartil inferior + alta persistencia, media significativa + persistencia, default MeanReverting, caso degenerado), `BinanceKlinesParserTests` (6 escenarios: fila válida, detección de header, timestamp ms→UTC, descompresión de zip mensual, fail loud ante datos inválidos, filtro por rango de fechas). Tests previos (Paso 1 y Paso 2, ~82 tests) sin cambios. ADR-017 pasa a estado "Aceptada". ADR-019 nuevo documenta los parámetros específicos del HMM, los BICs por candidato, el mapeo resultante y las alternativas consideradas durante la ejecución. Ver ADR-017 y ADR-019.

### ✅ INFRA-1 — Path absoluto del strategies.json eliminado y reconciliado con MSBuild
**Fecha:** 2026-05-17
**Resumen:** El `TradingAlgorithmHost.cs` hardcodeaba `F:\DesarrolloTrading\QuantConnect\Lean\Trading.Strategies\bin\Debug\net10.0\strategies.json` para cargar la configuración de estrategias, generando dos problemas: (a) no portable a otras máquinas, (b) dos copias paralelas del JSON sin sincronizar (una en `Trading.Strategies\strategies.json` versionada, otra en `bin\Debug\` que era la que el código leía efectivamente y que MSBuild no actualizaba al recompilar). El refactor reemplaza el path absoluto por `System.IO.Path.Combine(System.AppContext.BaseDirectory, "strategies.json")`, agrega `<Content Include="strategies.json" CopyToOutputDirectory="PreserveNewest" />` al `Trading.Strategies.csproj` para que MSBuild sincronice automáticamente fuente → bin en cada build, y reconcilia el contenido (la fuente versionada quedó con el contenido correcto `EmaCrossStrategy / BTCUSDT / 1h / RiskPerTradePercentage: 2.0`, la copia del bin eliminada para que MSBuild la regenere). Adelantamiento del refactor INFRA-1 del Bloque 3, que el ROADMAP planificaba antes del Hito C; se adelantó por causar fricción operativa concreta en dos sesiones de trabajo sobre Hito B (confusión sobre qué archivo era la fuente de verdad). Backtest sin cambios funcionales: el sistema sigue cargando la configuración correcta, ahora desde una sola ubicación clara. Ver ADR-018.

### ✅ INFRA-2 — Monitoreo básico del sistema en producción
**Fecha:** 2026-05-20
**Resumen:** Tres piezas que dotan al sistema de observabilidad mínima para paper trading, ejecutadas y validadas en orden estricto A → B → C con tres fixes correctivos durante el camino. **Pieza A — Persistencia de logs JSONL:** nueva interfaz `IStructuredLogSink` (en `Trading.Domain.Abstractions`) y enum `LogLevel` (espejo de los métodos de `ITradingLogger`, cero dependencias externas en Domain). Implementación `JsonlFileLogSink` (en `Trading.Strategies.Adapters`) que escribe una línea JSON por evento a `logs/trading-{wall-clock-date}.jsonl` con rotación diaria y retención de 30 días. Helper estático `LogTemplateRenderer` extrae la lógica de parseo de placeholders nombrados que estaba embebida en `LeanLogger`. El `LeanLogger` se refactorizó para recibir el sink por constructor e invocarlo en paralelo al `QCAlgorithm.Log/Debug/Error` sin cambiar firmas públicas de `ITradingLogger`. Sink thread-safe (lock interno), traga excepciones de I/O para no romper trading. **Pieza B — Heartbeat local:** nuevo evento `BarProcessedEvent` (emitido por `BarProcessingService` solo en el camino exitoso, no en early-returns); nuevo componente `HealthHeartbeatTracker` (en `Trading.Application.Health`) suscripto a `BarProcessedEvent`, `OrderSubmittedEvent`, `OrderFilledEvent` y `RiskLimitBreachedEvent`, manteniendo estado in-memory con lock; `HealthSnapshot` record inmutable; `HeartbeatFileWriter` (en `Trading.Strategies.Adapters`) serializa el snapshot a `health/heartbeat.json` con escritura atómica (`.tmp` + `File.Move` overwrite). Flush periódico vía `System.Threading.Timer` cada 60s **solo en `LiveMode`**. **Pieza C — Ping externo a Healthchecks.io:** `HealthchecksIoPinger` (en `Trading.Strategies.Adapters`) hace HTTP GET a una URL leída de la variable de entorno `HEALTHCHECKS_PING_URL`. Throttle interno de 5 minutos. Modo no-op con Warning una sola vez al arranque si la variable no está definida o el formato no matchea (graceful degradation). `HttpClient` long-lived, dispose en `OnEndOfAlgorithm`. Nunca propaga excepciones al caller. **Tres fixes correctivos durante la implementación, todos por el mismo error de fondo (confundir `IClock` con wall clock real en componentes de housekeeping):** (1) el `Schedule.On(TimeRules.Every(60s))` original del heartbeat se disparaba al ritmo del clock simulado del backtest, llevando el tiempo de ejecución de 1 minuto a 20+; reemplazado por `System.Threading.Timer` envuelto en `if (LiveMode)`; (2) la rotación y retención del JSONL usaban `_clock.UtcNow.Date` y eliminaban los propios logs del run; reemplazado por `DateTime.UtcNow.Date` para esas dos operaciones específicas, manteniendo `_clock.UtcNow` para el campo `timestamp` de cada evento (que sí debe reflejar el clock del sistema para correlacionar con órdenes); (3) los tests `Write_*` del sink fallaban con `IOException` al intentar leer el archivo mientras el sink lo tenía abierto en modo escritura; corregidos adoptando patrón `using` con disposición antes de la lectura. **Tests totales agregados:** ~35-40 entre los tres componentes (`JsonlFileLogSinkTests`, `LogTemplateRendererTests`, `HealthHeartbeatTrackerTests`, extensiones de `BarProcessingServiceTests` para verificar emisión de `BarProcessedEvent`, `HealthchecksIoPingerTests` con `HttpMessageHandler` mockeado). **Métricas del backtest idénticas al baseline** (225 órdenes, P&L, drawdown), tiempo de ejecución restaurado a ~100 segundos tras los fixes. **Hallazgos no funcionales documentados como deuda:** DEUDA-2 (`Initialize()` se ejecuta dos veces en backtest, revelado por logs duplicados en el JSONL) y DEUDA-3 (timestamps del epoch de QC durante `Initialize()`). **Validaciones pendientes para Hito C** documentadas en ADR-021: confirmar que `heartbeat.json` se actualiza en live, que los pings llegan a Healthchecks.io, que la alerta de Telegram dispara cuando el proceso muere, y si DEUDA-2 aplica también a live. ADR-021 nuevo cubre todas las decisiones de INFRA-2, alternativas descartadas (Seq/Datadog, Uptime Kuma, Pingdom), el criterio arquitectónico aprendido (wall clock vs `IClock` en componentes de observabilidad) y la validación pendiente. AI.md ampliado con la regla wall clock vs `IClock`, persistencia JSONL, heartbeat, y nueva sección "Variables de Entorno". Ver ADR-021.

### ✅ OPS-2 — `StrategyHealthMonitor` — implementación runtime de POLICY sección 3
**Fecha:** 2026-05-21
**Resumen:** Implementación completa de los umbrales U1-U4 de POLICY 3.1 como componente runtime. **Pieza A (cableado):** nueva interfaz `IStrategyHealthMonitor` en `Trading.Domain/Abstractions/` con un solo método `bool IsExcluded(string executorIdentifier)`. Nuevo valor `StrategyDegradation` al enum `RiskLimitBreachReason`. Guard en `BarProcessingService` posicionado entre el check de `IsKillSwitchActivated` y el filtro de régimen: si `_strategyHealthMonitor.IsExcluded(executorIdentifier)` → `continue`. `NullStrategyHealthMonitor` como placeholder de Pieza A. `FakeStrategyHealthMonitor` en el proyecto de tests. 2 tests nuevos del guard. **Pieza B (monitor completo):** `StrategyHealthThresholds` (POCO inmutable, factory `FromPolicyDefaults()` con los 10 valores literales de POLICY 3.1). `StrategyHealthMonitor` en `Trading.Application/Health/`: consume `OrderFilledEvent` del bus (suscripción en constructor), mantiene estado rolling por `ExecutorIdentifier` bajo lock interno (equity acumulado, ATH, ventana de 30 trades cerrados, ventana de 30 puntos diarios de equity, contadores de días/trades sostenidos para U2/U3/U4, flag `degraded`). Evalúa U1 (DD absoluto desde ATH > 25%) en cada cierre; U2 (DD rolling 30 días > 15% sostenido 5 días) al avanzar el día; U3 (PF rolling < 1.0 sostenido 10 trades) y U4 (expectancy rolling < 0 sostenido 10 trades) armados tras 50 trades acumulados. Al disparar: `LiquidateInstrument` (si hay posición abierta en ese instante — defensivo, en la práctica el breach ocurre al cerrar), flag `degraded = true`, `RiskLimitBreachedEvent(StrategyDegradation)`, log `Critical`. U3/U4 no implementan `IRiskMonitor` ni activan kill switch global (ver ADR-023). Wiring en `TradingAlgorithmHost` reemplaza el `NullStrategyHealthMonitor` por el monitor real. **Tests:** ~28 nuevos (`StrategyHealthMonitorTests`: trade lifecycle, U1/U2/U3/U4, multi-estrategia, exclusión, evento de breach; `StrategyHealthThresholdsTests`: 4 tests de validación y factory). Total Application.Tests: 121 tests verdes. Domain.Tests: 38 sin cambios. **Invariantes verificadas:** cero `using QuantConnect` en Domain/Application; cero `DateTime.UtcNow` en `Trading.Application/Health/`; cero `throw new Exception` ni `ApplicationException`; literales de POLICY solo en `StrategyHealthThresholds.cs`. **Backtest no-regresión:** monitor activo pero `EmaCrossStrategy` no alcanza condiciones de disparo en el baseline (~225 órdenes esperadas, a verificar manualmente). ADR-023 documenta la decisión de componente autónomo vs `IRiskMonitor`. OPS-3 (persistencia entre reinicios) agregada como deuda en Bloque 4 postergado. Bloque 3 completo; DEUDA-1/2/3 abiertas pero no bloquean Hito C.

### ✅ OPS-1 — Trading Policy Document (`POLICY.md`)
**Fecha:** 2026-05-21
**Resumen:** Documento operativo nuevo, versionado en la raíz del repo junto a `AI.md`, `DECISIONS.md` y `ROADMAP.md`. Codifica las reglas operativas inquebrantables que gobiernan cuándo una estrategia o el sistema completo pierden el derecho de operar. **Estructura en 7 secciones**: (1) Principios operativos inquebrantables (5 principios: validación antes de capital, kill switch no se desactiva en caliente, haircut backtest→live esperado 30-50%, gana el monitor cuando hay disenso con la intuición, cada cambio operativo deja huella); (2) umbrales a nivel sistema (drawdown global 25%, pérdidas consecutivas 5 trades con cooling off 24h, eventos macro ±30min en pausa manual hasta `EventCalendarMonitor`, anomalías de infraestructura); (3) umbrales por estrategia (plantilla con U1-U4: DD absoluto desde ATH > 25%, DD rolling 30 días > 15% sostenido 5 días, PF rolling 30 trades < 1.0 sostenido 10 trades, expectancy rolling 30 trades < 0 sostenido 10 trades; U3 y U4 solo armados tras 50 trades en vivo); (4) cadencia de revisión humana (diaria/semanal/mensual/trimestral); (5) runbooks de emergencia (kill switch activado, alerta de proceso caído, discrepancia con broker, performance anómala); (6) política de cambios al sistema en operación; (7) estado actual por estrategia (hoy solo `EmaCrossStrategy / BTCUSDT / 1h` en pre-paper). **Decisiones operativas tomadas y documentadas en ADR-022**: dos niveles de semáforo (OK/Apagar) en lugar de tres (Verde/Amarillo/Rojo/Negro); calibración absoluta de umbrales en lugar de derivada del backtest actual (porque el backtest se construyó para validar infraestructura, no como proceso de validación cuantitativa institucional); liquidación inmediata al disparar umbral en lugar de pause-only; reactivación con solo análisis escrito en `DECISIONS.md/incidents/` sin re-paper obligatorio. **Recalibración futura** de umbrales planificada para post-Hito G (cuando exista walk-forward analysis con base estadística). **Cambios colaterales al ROADMAP**: OPS-2 actualizado con referencia explícita a las métricas y umbrales de POLICY sección 3; nueva entrada `EVCAL-1` (`EventCalendarMonitor`) agregada al Bloque 4 postergado con trigger documentado. **Sin cambios de código en este paso**: OPS-1 es 100% documental. El componente runtime que consume POLICY es OPS-2 (próximo refactor del Bloque 3). Ver ADR-022.

---

## Cómo usar este archivo

### Al iniciar una sesión nueva (con Claude o solo):
1. Abrir `ROADMAP.md` y leer la sección **"Refactors pendientes"**.
2. Confirmar cuál es el próximo refactor (el primero marcado 🔄 o el primer ⬜ del bloque actual).
3. Leer `DECISIONS.md` para entender las decisiones arquitectónicas tomadas que afectan el refactor.
4. Leer `AI.md` para las reglas de estilo y arquitectura.

### Al completar un refactor:
1. Mover la fila del refactor a la sección **"Historial completado"** con fecha y resumen.
2. Si surgieron decisiones arquitectónicas nuevas, agregarlas a `DECISIONS.md`.
3. Si una decisión cambió una regla del proyecto (ej. cambia el contrato de logging), actualizar `AI.md`.
4. Commitear los tres archivos junto con el código del refactor.

### Si un refactor se aborta:
1. Marcarlo como ❌.
2. Agregar nota con la razón.
3. Si la decisión amerita registro, agregar entrada en `DECISIONS.md`.

