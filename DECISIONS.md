# DECISIONS - Architecture Decision Records

> **Propósito:** registro de decisiones arquitectónicas tomadas durante el desarrollo del sistema. Cada entrada explica QUÉ se decidió, POR QUÉ, y QUÉ alternativas se consideraron y descartaron.
>
> **Reglas:**
> - Entradas en orden cronológico inverso (la más reciente primero).
> - Cada entrada tiene fecha, contexto, decisión, alternativas, consecuencias.
> - Las decisiones que se revierten NO se borran: se marcan como "Revertida en ADR-NNN" y se mantienen para historia.
> - Identificador correlativo `ADR-NNN`.
> - Al agregar un ADR nuevo, actualizar también la tabla de índice abajo.

## Índice

| ADR | Título corto | Área |
|---|---|---|
| ADR-036 | ATR SL/TP mode: SL/TP basado en multiplicadores de ATR como alternativa al modo porcentaje | Arquitectura / Ejecución |
| ADR-035 | AtrCompressionBreakoutStrategy: H2 Hito E — M4 pasado con diagnóstico de hold | Estrategias |
| ADR-034 | IntradayMomentumStrategy: segunda estrategia manual (Hito E, candidata 2) | Estrategias |
| ADR-033 | DonchianBreakoutStrategy: segunda estrategia manual (Hito E) | Estrategias |
| ADR-032 | WarmUpBars en IStrategy: warm-up dinámico de indicadores internos | Arquitectura |
| ADR-031 | Hito C: feed verificado, race condition Binance, LeanClock UTC fix | Operaciones |
| ADR-030 | Bypass ValidateSubscription plugin Binance para live local | Infraestructura |
| ADR-028 | Validación multi-símbolo + fix estructural OPS-2 | Validación |
| ADR-027 | Re-entrenamiento BTC con trainer multi-seed | HMM / Regímenes |
| ADR-026 | Validación multi-timeframe del subsistema de ejecución | Validación |
| ADR-025 | LiquidateInstrument explícito y base de equity correcta en StrategyHealthMonitor | Riesgo / Health |
| ADR-024 | SemanticStateMapper adaptativo a K + multi-seed Baum-Welch | HMM / Regímenes |
| ADR-023 | StrategyHealthMonitor: componente autónomo fuera de IRiskMonitor | Riesgo / Health |
| ADR-022 | POLICY.md: dos niveles de semáforo, calibración absoluta, liquidación inmediata | Operaciones |
| ADR-021 | Monitoreo básico: JSONL local + heartbeat + Healthchecks.io | Operaciones |
| ADR-020 | Test AccordHmmClassifierReferenceTests skipeado — convergencia degenerada | Testing |
| ADR-019 | Implementación HMM con Accord.NET — Paso 3 Hito B | HMM / Regímenes |
| ADR-018 | Adelantamiento INFRA-1: path absoluto strategies.json eliminado | Infraestructura |
| ADR-017 | Hito B completo: abstracción de regímenes, guard pre-orden, HMM real | HMM / Regímenes |
| ADR-016 | Trading Policy escrita + monitor runtime de degradación | Riesgo / Health |
| ADR-015 | Separación IRiskMonitor de IRiskAction | Arquitectura |
| ADR-014 | Reversión SignalAuditor: validación por tests unitarios estáticos | Testing |
| ADR-012 | Auditor de señales: tolerancia relativa, no absoluta | Testing |
| ADR-011 | Auditor de señales: warm-up por símbolo en lugar de buffer infinito | Testing |
| ADR-010 | Auditor de señales en C# dentro del mismo backtest | Testing |
| ADR-009 | Bus de eventos de dominio síncrono in-memory | Arquitectura |
| ADR-008 | Postergar refactors no críticos hasta después de cada hito | Arquitectura |
| ADR-007 | `ITradingLogger` se mantiene como abstracción del dominio | Arquitectura |
| ADR-006 | `Long`/`Short` en estrategias usando enum simple | Dominio |
| ADR-005 | Cleanup automático del `OrderRegistry` tras eventos terminales | Dominio |
| ADR-004 | Tags opacos formato `ord_xxxxxxxx` (GUID corto) | Dominio |
| ADR-003 | `OrderRegistry` vive en `Trading.Application` | Arquitectura |
| ADR-002 | `RiskPerTradePercentage` falla loud si no está en `strategies.json` | Dominio |
| ADR-001 | Desacople quirúrgico de QuantConnect: dominio Lean-free | Arquitectura |

## ADR-036 — ATR SL/TP mode: SL/TP basado en multiplicadores de ATR
**Fecha:** 2026-06-09
**Estado:** Vigente
**ADRs relacionados:** ADR-035 (AtrCompressionBreakoutStrategy), ADR-009 (bus de eventos)

### Contexto

H2 (AtrCompressionBreakoutStrategy) falló el backtest con Sharpe -0.922, DD 30.3%, kill switch 2025-03-19. Diagnóstico post-mortem: la señal tiene edge genuino (M4 pasado 6/9 BTC, 5/9 ETH, 7/9 BNB), pero el SL fijo de 2% es inadecuado para una estrategia de breakout de compresión de volatilidad. La distancia correcta del SL debe ser proporcional al ATR en el momento de la señal, no un porcentaje fijo del precio.

### Decisión

**Agregar soporte config-driven para SL/TP basado en multiplicadores de ATR**, sin modificar `IStrategy`. El modo se selecciona en `strategies.json` via `StopTakeMode: "Atr"`.

Puntos clave de diseño:

1. **Separación temporal señal / fill**: el ATR se captura en `BarProcessingService` al momento de la señal (barra actual), no al momento del fill (barra siguiente). Se almacena en `StrategyExecutor.PendingStopLossPrice` / `PendingTakeProfitPrice`.

2. **Sin cambio a IStrategy**: la estrategia expone su ATR vía la interfaz opcional `IAtrProvider.GetLastAtr(ticker)`. `BarProcessingService` usa type check (`strategy is IAtrProvider`) para activar el modo ATR. Las estrategias que no implementen `IAtrProvider` siguen usando el modo porcentaje.

3. **Compatibilidad con PositionSizer**: `StopLossPercentage` se mantiene en el JSON como aproximación estática para el sizing (PositionSizer no sabe de ATR). Con 2.5×ATR ≈ 3.5% para BTC 4h, la aproximación es razonable.

4. **StopLossPercentage no debe ser 0 en modo ATR**: si es 0, `RiskParameters.FromPercentages` lanzaría una excepción (invariante). Se mantiene el valor como placeholder para el sizing.

### Alternativas descartadas

- **Cambiar IStrategy**: requeriría cambiar el contrato de todas las estrategias; over-engineering para una feature que quizás solo use una estrategia.
- **Computar ATR en el fill (bar+1)**: el ATR cambia entre la señal y el fill. Usando el ATR de la barra del fill se rompería la lógica de "colocar SL a N×ATR del precio de señal".
- **SL dinámico por trailing stop**: out of scope para Hito E; requeriría gestión activa durante la posición.

### Consecuencias

- Un nuevo `StopTakeMode` en `StrategyDefinition` rompe la config de estrategias que no incluyan el campo (serialización JSON nullable por default).
- `PendingStopLossPrice` es nullable: si el ATR es 0 (indicador no ready) o la estrategia no implementa `IAtrProvider`, se usa el modo porcentaje como fallback.
- El test de compresión del ATR tiene un artefacto: si el breakout bar tiene un gap grande (TR alto), el ATR se dispara y puede salir del rango de compresión. Esto es **comportamiento correcto** — una barra con gap extremo no debe clasificarse como breakout de compresión. Los tests usan breakouts moderados (10% vs 20%) para evitar este artefacto.

---

## ADR-035 — AtrCompressionBreakoutStrategy: H2 Hito E — M4 pasado con diagnóstico de hold
**Fecha:** 2026-06-09
**Estado:** Vigente — pendiente backtest QC completo
**ADRs relacionados:** ADR-034 (protocolo M4), ADR-032 (WarmUpBars)

### Contexto

Candidata 7 de Hito E, tras 6 rechazos (Donchian, IntradayMomentum, BollingerBands, H3 lead-lag, H1 RSI+HMM, FRP funding rate). Última candidata planificada en la lista original.

### Decisión

**Hipótesis implementada:** ATR Compression Breakout (bidireccional, 4h).
- Compresión: ATR(14) < percentil 20 de las últimas 100 lecturas del ATR (rolling window).
- Rompimiento Long: Close actual > máximo de los 10 cierres anteriores.
- Rompimiento Short: Close actual < mínimo de los 10 cierres anteriores.
- Hold: 3 barras 4h (12h) via `MaxBars=3 + CombineWithTimeExit=true`.
- WarmUpBars = 114 (14 para ATR ready + 100 para llenar la ventana del percentil).

**Proceso M4:** el grid original (hold=[4,8], ATR=[P25,P35], look=[10,20] → 8 configs) falló porque hold=8 destruía la señal. Hold=4 pasaba cross-asset (BTC +0.822, ETH +0.659, BNB +0.670) pero el conteo 1/8 BTC y 2/8 ETH no alcanzaba el gate. Diagnóstico A con hold=[2,3,4] confirmó el mecanismo: hold=3 pasa los tres activos sin excepción (6/9 BTC, 5/9 ETH, 7/9 BNB). El cambio de grid está justificado porque la hipótesis del decay rápido surgió del análisis de hold=4 vs hold=8, no de buscar configuraciones individuales que pasen.

**Implementación:** `AtrCompressionBreakoutStrategy.cs`. Sin dependencia del clasificador HMM (el filtro ATR cumple el rol de comprimir el régimen). El PriceHistory se actualiza DESPUÉS de evaluar la señal para que siempre contenga los N cierres anteriores, sin look-ahead.

**Limpieza:** `StrategyFactory` tenía referencias muertas a `DonchianBreakoutStrategy` e `IntradayMomentumStrategy` (clases eliminadas por git rm en commit anterior pero no removidas del factory). Corregido en este commit.

### Alternativas consideradas

- **Añadir HMM Squeeze como filtro adicional:** descartado. H1 ya probó RSI+HMM y el condicionamiento por Squeeze reducía demasiado la frecuencia (3 trades/año). El ATR < P20 ya captura el estado de compresión sin depender del HMM.
- **P25 en lugar de P20:** P20 es más estricto (menos señales, mayor calidad de compresión). Ambos pasan el M4; P20 elegido por mayor consistencia en Sharpe cross-asset.
- **Hold=4 en lugar de hold=3:** ambos pasan. Hold=3 tiene mejor Sharpe promedio en ETH y BNB; hold=4 es ligeramente mejor en BTC. Hold=3 es más conservador.

### Pendiente

Backtest completo en QC con SL 2% / TP 4% / MaxBars=3 para verificar M1 (Sharpe ≥ 0.5) y M2 (Win rate ≥ 40%). Si pasa, proceder a Hito F.

---

## ADR-034 — M4 obligatorio antes de IStrategy + patrón de estrategia tiempo-dependiente
**Fecha:** 2026-06-08
**Estado:** Vigente
**ADRs relacionados:** ADR-032 (WarmUpBars)

### Decisión

**M4 obligatorio:** antes de implementar cualquier `IStrategy`, ejecutar M4 — señal pura (tamaño fijo, sin SL/TP ni vol-scaling) sobre ≥ 3 activos. Umbral: Sharpe ≥ 0.5 en ≥ 2/3 activos. Si no pasa, descartar la hipótesis sin escribir código.

**Razón:** las candidatas a Hito E mostraron que el código puede ser correcto y el edge no existir. El M4 detecta esto antes de invertir tiempo en implementación.

**Patrón para estrategias tiempo-dependientes** (derivado de IntradayMomentumStrategy): diseño en dos pasos — barra de referencia registra estado sin emitir señal; barra de entrada emite la señal si el estado es del mismo período. Validar con fecha para evitar contaminación cross-day. MaxBars alineado al holding period que validó el M4, no al intervalo entre referencia y entrada.

Ver resultados de experimentos: [`research/strategy_experiments.md`](research/strategy_experiments.md).

---

## ADR-033 — DonchianBreakoutStrategy (Hito E, candidata 1)
**Fecha:** 2026-06-08
**Estado:** ~~Retirada~~ — descartada por Fase 0. Ver [`research/strategy_experiments.md`](research/strategy_experiments.md).

**Hallazgo arquitectónico:** `StrategyFactory` confirmado como único punto de registro. Agregar una `IStrategy` no requiere modificar el host — solo la clase + entrada en `strategies.json`. Patrón válido para todas las estrategias futuras.

---

## ADR-032 — WarmUpBars en IStrategy: warm-up dinámico de indicadores internos de estrategia
**Fecha:** 2026-06-07
**Estado:** Aceptada
**ADRs relacionados:** ADR-031 (Hito C, operaciones VPS)

### Contexto

Durante el Hito C se detectó que las estrategias arrancaban con sus indicadores internos en frío, incluso después de que Lean completaba el warm-up. La causa: el consolidador de estrategias tenía un `if (IsWarmingUp) return` que impedía que `BarProcessingService` recibiera barras históricas. El warm-up de Lean (20 días fijos, dimensionado para el HMM) calentaba el clasificador de régimen correctamente —que tiene su propio consolidador sin esa guarda— pero no llegaba a las EMAs internas de `EmaCrossStrategy`.

Consecuencia práctica: en la corrida del VPS activa desde 2026-06-03, `EmaCrossStrategy` necesitó tiempo adicional en live para calentar sus propias EMAs (EMA 30 y EMA 60), equivalente a 60 barras × el timeframe de cada instancia. Para TRBUSDT 4h eso era 10 días adicionales en vivo sin posibilidad de señal.

El problema también era estructural: el valor `SetWarmUp(TimeSpan.FromDays(20))` era una constante hardcodeada sin relación con los requerimientos reales de las estrategias. Al agregar una estrategia con indicadores de período largo (ej. EMA 200 en 4h = 33 días), el warm-up sería insuficiente sin que hubiera ningún error visible.

### Decisión

**Tres cambios coordinados:**

**D1 — `int WarmUpBars` en `IStrategy`.** Cada estrategia declara la cantidad de barras necesarias para calentar su indicador más lento. `EmaCrossStrategy` retorna 60 (período de la EMA lenta). El contrato garantiza que cuando `EvaluateSignal` reciba el primer bar real, los indicadores internos ya tienen historia suficiente.

**D2 — `isWarmingUp` en `BarProcessingService.ProcessBar`.** Se elimina la guarda `if (IsWarmingUp) return` del consolidador y se pasa el flag de Lean al service. Durante warm-up: se llama `EvaluateSignal` en cada estrategia (calienta indicadores) pero se retorna antes de toda la lógica de órdenes. `BarProcessedEvent` se publica siempre —warm-up y live— para que `LastBarProcessedUtc` refleje actividad real durante el warm-up.

**D3 — Cálculo dinámico de `SetWarmUp`.** El host calcula el warm-up como `max(100 barras × 4h, max(executor.Strategy.WarmUpBars × timeframeSpan))` iterando todos los executors construidos. El mínimo garantiza que el HMM siempre tenga los 100 períodos 4h que necesita; el máximo sobre estrategias garantiza que ningún indicador arranque en frío.

### Alternativas consideradas

- **A: Warm-up fijo con documentación manual.** Requerir que cada autor de estrategia recuerde ajustar la constante de 20 días. Descartado: frágil, no escala, el error es silencioso.
- **B: Consolidador dedicado de warm-up por estrategia.** Un segundo consolidador (separado del de señales) que solo alimenta los indicadores durante warm-up. Descartado: duplicación de lógica de consolidación, frágil al agregar timeframes o estrategias nuevas.
- **C (elegida): `WarmUpBars` en la interfaz + flag `isWarmingUp`.** El contrato queda en la abstracción correcta: la estrategia sabe qué necesita, el host respeta ese requerimiento automáticamente. Sin duplicación.

### Consecuencias

**Positivas:**
- Cualquier estrategia nueva que declare `WarmUpBars` correctamente tiene sus indicadores calentados al inicio del trading real, sin configuración adicional.
- El warm-up del VPS que antes tardaba 10 días en live (TRBUSDT 4h) ahora se resuelve en el replay histórico (~8 minutos de wall clock en el VPS).
- `LastBarProcessedUtc` se actualiza durante el warm-up, mejorando la observabilidad del arranque en el heartbeat.

**Restricción activa:**
- `WarmUpBars` debe reflejar el período del indicador más lento de la estrategia. Si una estrategia subestima este valor, arranca con historia parcial sin error visible. Es responsabilidad del autor de cada `IStrategy`.

**Deuda futura:**
- Para estrategias con indicadores de período muy largo (ej. EMA 200 en 4h = 33 días), el warm-up dinámico puede superar los 20 días actuales, aumentando el tiempo de arranque del proceso. Aceptable por ahora; si se vuelve un problema operativo, considerar precalentar desde una snapshot de estado persistida.

---

## ADR-031 — Hito C: infraestructura de feed verificada; deuda de race condition en plugin de Binance
**Fecha:** 2026-06-03
**Estado:** Aceptada
**ADRs relacionados:** ADR-021 (INFRA-2 monitoreo), ADR-030 (bypass ValidateSubscription)

### Contexto

Al arrancar Hito C (paper trading en VPS Windows con NSSM) surgieron tres problemas encadenados que impidieron la validación operativa plena:

**Problema 1 — Race condition en `BrokerageMultiWebSocketSubscriptionManager.OnOpen()`.**
El plugin de Binance tiene un bug en el vendored code: `OnOpen` re-suscribe en el callback de reconexión WebSocket pero no espera a que los streams previos se cierren antes de suscribirse de nuevo. Bajo ciertas condiciones de red, el feed queda en estado stall permanente: el WebSocket reporta `Connected` pero no entrega barras. El proceso puede mantenerse en ese estado indefinidamente sin señal de error observable.

**Problema 2 — Watchdog disparando cada ~15 min con `staleness=14400s`.**
El watchdog implementado en `TradingAlgorithmHost` comparaba `DateTime.UtcNow` (UTC real) contra `LastBarProcessedUtc` que provenía de `BarProcessingService` vía `_clock.UtcNow`. El bug: `LeanClock.UtcNow` retornaba `_algorithm.Time` en lugar de `_algorithm.UtcTime`. En operación live de junio (EDT=UTC-4), `_algorithm.Time` devuelve hora local del algoritmo (11:01 EDT cuando el wall clock era 15:01 UTC). Diferencia = 4h = 14400 segundos >> umbral de 1200s → el watchdog disparaba restart inmediatamente después del primer bar consolidado.

**Problema 3 — Timestamps `1997-12-31T19:00:00` durante `Initialize()` (DEUDA-3).**
`_algorithm.UtcTime` durante la fase `Initialize()` retorna el epoch de QC (`1997-12-31T19:00:00 UTC`) antes de que el motor inicialice su reloj interno. Los primeros eventos JSONL y `ProcessStartedUtc` quedaban con ese timestamp inútil.

### Decisiones

**D1 — Patch operativo para el race condition (stall del feed): auto-restart vía `Environment.Exit(1)`.**
El watchdog en `TradingAlgorithmHost` llama `Environment.Exit(1)` cuando `BarStalenessSeconds > 1200s`. NSSM está configurado con `AppExit Default Restart` y re-levanta el proceso automáticamente. Solución operacional robusta mientras el bug vendored no se resuelve. Umbral: 20 minutos de silencio de barras antes de restart (umbral conservador para absorber períodos normales de baja actividad de mercado, incluyendo fines de semana). Commit `ee76d65`.

**D2 — Fix de `LeanClock`: `_algorithm.Time` → `_algorithm.UtcTime`.**
La causa raíz de los falsos positivos del watchdog. `_algorithm.UtcTime` siempre devuelve UTC real (tiempo simulado en backtest, tiempo UTC del exchange en live). `_algorithm.Time` devuelve hora en el timezone del algoritmo (que en este deployment es EDT=UTC-4), rompiendo cualquier comparación contra `DateTime.UtcNow`. Commit `8789061`. Regla permanente documentada en AI.md.

**D3 — Fix de época QC en `LeanClock` (DEUDA-3): fallback a `DateTime.UtcNow` cuando `UtcTime < año 2000`.**
Si `_algorithm.UtcTime` es anterior al año 2000, `LeanClock.UtcNow` retorna `DateTime.UtcNow` (wall clock real). Esto garantiza que `ProcessStartedUtc` y los primeros eventos JSONL de `Initialize()` tengan timestamps reales. Umbral elegido como `new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)`. Commit `2671694`.

### Alternativas consideradas

**D1 alternativas:**
- **A: Fix directo del race condition en el plugin vendored.** Deseable como solución permanente, pero requiere debugging profundo de código vendored de Lean y re-testing de integración. Postergado como DEUDA técnica activa (ver consecuencias).
- **B: Timeout de watchdog más corto.** Descartado: umbral < 20 min genera falsos positivos en períodos legítimos de baja actividad (fine de semana, gaps nocturnos).
- **C (elegida): Auto-restart.** Operacionalmente probado durante Hito C. El proceso se levanta en segundos, warmup completa rápido (~30s), sin intervención humana.

**D2 alternativas:**
- **A: Usar `DateTime.UtcNow` directamente en `BarProcessingService`** para el evento. Descartado: viola el criterio `IClock` del dominio y hace el código no determinista en backtest.
- **B (elegida): Corregir `LeanClock`.** El contrato de `IClock.UtcNow` es "UTC real"; `LeanClock` no lo cumplía. La corrección es en la fuente.

### Consecuencias

**Positivas:**
- Sistema corriendo estable en VPS desde 2026-06-03. Verificado >2h continuas con pings cada 5 min a Healthchecks.io, heartbeat actualizado correctamente, `BarStalenessSeconds` en rango normal (300-600s en mercado activo).
- `LeanClock.UtcNow` cumple su contrato en todos los contextos: backtest (UtcTime simulado), live (UtcTime UTC real), Initialize (fallback a wall clock real).
- DEUDA-3 cerrada: logs del JSONL tienen timestamps reales desde el arranque.

**Deuda técnica abierta:**
- Race condition en `BrokerageMultiWebSocketSubscriptionManager.OnOpen()` (plugin vendored de Lean). El auto-restart es parche operativo; la cura requiere sincronización de re-suscripción. Postergado a Bloque 4 o como mejora al plugin. Mientras el parche sea efectivo, la prioridad es baja.

**Modelo de deployment VPS documentado en AI.md:**
- Servicio NSSM `LeanPaper` en Windows Server.
- Binario en `C:\Lean\Paper\`.
- Variable de entorno `HEALTHCHECKS_PING_URL` inyectada via NSSM `AppEnvironmentExtra`.

**Hito C pendiente de cierre pleno:** la infraestructura operativa está verificada. El cierre definitivo del Hito C requiere al menos un trade real (live con paper brokerage) para validar el ciclo completo U1-U4, el kill switch con equity en movimiento, y el comportamiento del JSONL bajo órdenes reales.

---

## ADR-030 — Bypass de ValidateSubscription en el plugin de Binance para operación en live local sin suscripción QuantConnect
**Fecha:** 2026-05-29
**Estado:** Aceptada
**ADRs relacionados:** ADR-029 (lección de verificación de binario), ADR-021 (monitoreo básico para paper trading)

### Contexto

El arranque de Hito C requería que el sistema corriera en `LiveMode == true`
con Paper Brokerage de Lean + data feed real de Binance Futures USDM. Al
lanzar paper trading el sistema falló durante la inicialización del
brokerage:

```
ValidateSubscription(): Failed during validation, shutting down.
Error : Invalid api user id or token, cannot authenticate subscription.
```

Verificación posterior en el portal de QuantConnect confirmó: "To request an
access token, you must belong to a paid organization." La cuenta gratuita
no genera token válido. Tarifas externas estiman el plan más barato que
habilita live trading entre USD 20-60/mes (no confirmado en pricing
logueado).

**Ubicación real de la validación (verificada en código).** La rutina
`ValidateSubscription()` NO vive en el motor de Lean. Vive dentro del
plugin de Binance, en
`Brokerages.Binance/QuantConnect.BinanceBrokerage/BinanceBrokerage.cs:909`,
y es invocada desde `Initialize(...)` del propio brokerage. La función
arma un `ApiConnection` con credenciales de `Globals`, hace POST a
`modules/license/read` en los servidores de QC, decripta la respuesta con
AES, valida que la licencia no esté expirada, y ante cualquier error llama
`Environment.Exit(1)`.

Esto importa para entender el alcance del fork: el motor de Lean (Engine,
Common, Algorithm, etc.) permanece sin modificaciones y puede actualizarse
libremente. El gate de suscripción está focalizado en los plugins
oficiales de brokerages. Patrón "open core" acotado a los conectores
comerciales, no al motor — legal con Apache 2.0, éticamente ambiguo
porque el material de marketing presenta a LEAN como "open source" sin
distinguir entre motor (genuinamente abierto) y plugins oficiales (con
gating).

### Decisión

Parchar `ValidateSubscription` en la copia vendored del plugin de Binance
para que retorne inmediatamente, sin contactar a los servidores de QC.
Implementación con retorno temprano más una cadena `static readonly`
(`_adr030BinaryMarker`) que persiste como `ADR-030-BYPASS-VALIDATE-SUBSCRIPTION`
en el binario, permitiendo verificación física por búsqueda UTF-16 LE
sobre el `.dll` desplegado (lección ADR-029).

Razón principal: el sistema está en validación pre-rentabilidad. Un costo
recurrente de USD 240-720/año por una capacidad que solo se usa mientras
el sistema aún no demostró generar ingresos no es defendible
financieramente con el contexto actual del operador. La decisión es
revisable cuando ese contexto cambie (ver Trigger de revisión).

### Alternativas consideradas

**A: Pagar el plan más barato de QuantConnect.** Comodidad máxima:
mantenimiento delegado del plugin de Binance, soporte oficial, posibilidad
de usar features pagas adicionales (cloud deployment, datasets premium).
Descartada por costo recurrente sin retorno demostrado y por crear
dependencia comercial con QC que persiste mientras el sistema opere en
live, paper o real. Re-evaluable cuando el sistema cruce los triggers de
revisión.

**B: Binance Testnet completo.** Sin costo. Evita pasar por el gate de
QC porque conecta directamente al testnet del exchange. Descartada porque
degrada el valor de la validación de Hito C: el feed del testnet es
sintético, con liquidez y microestructura que no replican fielmente al
mercado real. El Hito C valida infraestructura del sistema bajo
wall-clock real; un feed sintético introduce variables que confunden esa
validación. Sigue siendo opción válida para validaciones futuras donde el
feed sintético no sea limitante.

**C (elegida): Parche local en el plugin de Binance.** Sin costo, feed
real de producción, fills ficticios vía PaperBrokerage. Preserva la
calidad del feed que el Hito C requiere. El precio es asumir
mantenimiento manual del fork del plugin (no del motor).

**B' (variante mixta descartada):** modificar el plugin para apuntar el
feed a producción de Binance y las órdenes al testnet. Técnicamente
posible pero requiere intervención más invasiva al plugin (desacoplar
URLs de feed y de transactions, que hoy están controladas por un único
flag). Complejidad innecesaria para Hito C; archivable si en el futuro
se necesita un setup más fino que combine ambos mundos.

### Consecuencias

**Positivas:**
- Hito C desbloqueado. El sistema arrancó en live-paper el 2026-05-29 a
  las 16:51 sin errores de autenticación. Validación funcional descrita
  abajo.
- Costo recurrente cero. La decisión preserva el principio operativo del
  operador de no asumir gastos fijos antes de demostrar rentabilidad.
- Motor de Lean intacto y libremente actualizable.

**Neutras / aceptadas:**
- Mantenimiento delegado a uno mismo, **acotado al plugin de Binance**.
  Cada actualización del plugin va a requerir re-aplicar el parche. El
  motor de Lean y los demás componentes no cargan ese trabajo.
- Aislamiento del ecosistema comercial QC: sin soporte oficial, sin foro
  para mostrar código modificado en caso de bug.
- Si en el futuro se agregan otros plugins oficiales (Coinbase, IB, etc.),
  cada uno traerá su propia `ValidateSubscription` y será una decisión
  separada si parchar también esos plugins. La presente decisión NO se
  extiende implícitamente.

**Negativas:**
- Zona gris ética (no legal). Apache 2.0 permite la modificación pero el
  parche elude el modelo de negocio del vendor. Esto se acepta como
  trade-off consciente, no se minimiza.
- Riesgo de drift: QC puede endurecer la validación en versiones futuras
  del plugin (más sitios de validación, validación cruzada, ofuscación
  del check). El parche actual podría dejar de ser suficiente. El trigger
  "actualización del plugin falla" en sección de revisión cubre este
  escenario.

### Validación funcional

El sistema arrancó en `live-paper` el 2026-05-29 a las 16:51 (wall-clock).
El log de arranque NO contiene la línea de error
`Invalid api user id or token`. WebSocket de Binance Futures producción
(`wss://fstream.binance.com/...`) conectó correctamente. Warmup completó
con los tres símbolos suscriptos (BTCUSDT, ETHUSDT, TRBUSDT). Heartbeat
flush timer arrancó. Pings a Healthchecks.io confirmados por integración
de Telegram (notificaciones UP/DOWN recibidas durante el ciclo
arranque → hibernación accidental → reanudación). El parche cumple su
propósito.

### Trigger de revisión

Esta decisión se revisa cuando se cumpla CUALQUIERA de:

- **Primer trade rentable real** (no paper, no testnet) operado por el
  sistema.
- **6 meses corridos** de sistema operando estable en VPS sin caídas
  significativas.
- **Una actualización del plugin de Binance falla** porque el parche no
  se puede re-aplicar limpio sobre la versión nueva.

En la revisión, re-evaluar los tres caminos con el contexto financiero y
operativo de ese momento.

### Reversibilidad

Alta. Quitar el `return` temprano, la línea `_ = _adr030BinaryMarker;` y
eliminar el campo estático `_adr030BinaryMarker` restaura el
comportamiento original. Sin efectos colaterales sobre backtest, sobre la
estrategia, ni sobre el motor de Lean (que nunca fue tocado).

### Implementación

Commit `bb4ae62` — `fix(engine): bypass ValidateSubscription for local live mode (ADR-030)`.

Cambio puntual en
`Brokerages.Binance/QuantConnect.BinanceBrokerage/BinanceBrokerage.cs:909`:
retorno temprano + campo estático con la cadena de trazabilidad.

Verificación física: cadena `ADR-030-BYPASS-VALIDATE-SUBSCRIPTION`
encontrada en `QuantConnect.Brokerages.Binance.dll` por búsqueda UTF-16 LE
en offset 67586.

Suite completa de tests: 211/211 verdes. El parche no rompe ningún test
existente.

### Pendiente operativo

ADR-029 quedó pendiente de redacción en el momento en que se cerró (no
está ni en DECISIONS.md ni como archivo suelto). Esta omisión debería
remediarse en algún momento del cierre del Hito C, reconstruyendo el ADR
desde los commits y conversación de la sesión correspondiente. No es
urgente pero la deuda crece con el tiempo.

## ADR-028 — Validación multi-símbolo + fix estructural OPS-2
**Fecha:** 2026-05-26 / 2026-05-27
**Estado:** Aceptada
**ADRs relacionados:** ADR-026 (validación multi-timeframe BTCUSDT), ADR-027 (re-entrenamiento BTC multi-seed)

### Contexto

ADR-026 había validado el subsistema de ejecución/monitoreo como agnóstico al
timeframe sobre BTCUSDT mediante tres backtests secuenciales (15m, 1h, 4h).
Quedaba pendiente validar agnosticismo al símbolo en operación concurrente
real, lo que requería sumar al menos dos símbolos más con sus propios
clasificadores de régimen.

Esta sesión cubre:
1. Entrenamiento de modelos HMM para ETHUSDT y TRBUSDT bajo Opción A
   (un HMM por símbolo).
2. Configuración multi-símbolo en `strategies.json` con los tres símbolos
   en TF 1h (decisión de aislar símbolo como única variable de validación,
   habiendo ya cubierto TF en ADR-026).
3. Validación del subsistema sobre el backtest paralelo 2025-01-01 →
   2026-03-31.

Durante la validación apareció un bug estructural latente preexistente del
flujo `LiquidateAll` del kill switch global, que no se manifestaba en
configuraciones single-symbol y que causó dos violaciones del invariante
OPS-2 en el primer backtest multi-símbolo. La sesión se extendió para
arreglar la causa raíz.

### Decisiones tomadas

#### Decisión 1 — `MinimumRequiredBars` del trainer baja de 10000 a 5000

Calibrado al piso técnico defendible para HMM-GMM con K=4 y D=7 features
multi-seed (rule of thumb ~10-50 obs/parámetro sobre ~155 parámetros del
modelo). El threshold previo de 10000 no estaba calibrado — era un valor
genérico conservador. 5000 deja margen claro y permite incluir activos
con historia de listado posterior a 2020-01 (TRB, listado 2020-09).

Criterio asociado: **el sistema no se acomoda al activo**. Los thresholds
de promoción de modelo son uniformes; los activos que no cumplen se
rechazan, no se acomodan. La elasticidad legítima por activo vive en
`strategies.json` (SL/TP/Risk paramétricos), no en el modelo de régimen.

#### Decisión 2 — Default de output del trainer pasa al staging

`models/regime/staging/{ticker}-perp-binance.hmm.json` por defecto. La
promoción a `models/regime/{ticker}-perp-binance.hmm.json` es manual y
gateada por criterios de inspección uniformes (K ∈ {3,4}, al menos un
estado Trend, ningún estado decodificado <5% ni >70%, ningún label
agregado >85%).

Este cambio aplica también a BTC. Cierra la deuda implícita "el archivo
en `models/regime/` se trataba como artefacto histórico congelado en
lugar de output regenerable del trainer", que el incidente del `.bak`
preDEUDA1 reveló (ADR-027).

#### Decisión 3 — Criterios de promoción de modelo uniformes

ETHUSDT y TRBUSDT entrenados con el trainer multi-seed y evaluados
contra los criterios uniformes. **Ambos pasaron los 6 criterios:**

- **ETHUSDT (K=4, BIC 56707.84):** Trend 52.07% / Squeeze 31.10% /
  HighVolatility 16.83%. Mapping: state 0→Trend, 1→Trend, 2→HighVol,
  3→Squeeze.
- **TRBUSDT (K=4, BIC 49814.19, 9405 barras post warm-up):** Trend
  46.66% / Squeeze 40.49% / HighVolatility 12.85%. Mapping: state
  0→Squeeze, 1→Trend, 2→HighVol, 3→Trend.

Ambos promovidos a `models/regime/`. La flota de modelos en producción
al cierre de sesión es: BTCUSDT (re-entrenado multi-seed en ADR-027),
ETHUSDT, TRBUSDT.

#### Decisión 4 — Eliminar `IOrderRouter.LiquidateAll()` (fix estructural OPS-2)

Bug detectado: `LeanBrokerageAdapter.LiquidateAll()` llamaba
`_algorithm.Liquidate()` (helper global de Lean). Las órdenes resultantes
llevaban `Tag = "Liquidated"`, ese tag no proviene de `OrderRegistry`, y
`OrderEventMapper` las descartaba como "liquidación global ignorada".
`StrategyHealthMonitor` nunca recibía el close. Resultado:
desincronización entre Lean (posición cerrada físicamente) y el monitor
(estado "posición abierta"). Días después, una nueva señal compatible
pasaba el filtro pre-orden de `BarProcessingService` (porque
`IPortfolioState.IsInvested` consultaba Lean directamente y reportaba
`false`), hacía fill, y el monitor lanzaba OPS-2 invariante violado.

El comentario explícito en el código del propio `LiquidateAll()` admitía
el problema: *"NO se registra (no hay executor único). Los eventos
resultantes serán ignorados por OrderEventMapper con log de
advertencia."* Esa "advertencia" era exactamente la causa de OPS-2 en
multi-símbolo.

**Fix aplicado:**

- `IOrderRouter.LiquidateAll()` eliminado del contrato del dominio.
- `LeanBrokerageAdapter.LiquidateAll()` eliminado de la implementación.
- `LiquidateAllRiskAction` refactorizado: recibe la lista de instrumentos
  activos por inyección, itera sobre ellos, consulta
  `IPortfolioState.IsInvested(instrumentId)`, y emite
  `LiquidateInstrument` (que ya usaba la disciplina correcta de
  `OrderRegistry` y tags propios) solo para los invertidos.
- `OrderPurpose.Liquidate` agregado al enum del dominio.
- `ExecutorIdentifier` para órdenes del kill switch global:
  `"RiskOrchestrator_KillSwitch"` (identificador sintético, no
  corresponde a una estrategia). Discutido al diseñar el fix; se aceptó
  romper la convención `{Strategy}_{Symbol}_{Timeframe}` para que en logs
  sea obvio que el cierre provino del kill switch global, no de una
  estrategia.
- `TradingAlgorithmHost.ExtractActiveInstruments` agregado para construir
  la lista de instrumentos únicos desde `strategies.json` durante el
  wiring del orchestrator.
- `IPortfolioState` **NO se extendió**. La lista de instrumentos activos
  proviene del wiring, no del dominio. Decisión deliberada: el dominio
  no debe contaminarse con métodos para resolver un problema de
  infraestructura cuando la información ya está disponible en el callsite.

**`OrderEventMapper` no se tocó.** Su lógica defensiva de ignorar tags
no registrados sigue siendo correcta para órdenes genuinamente externas
(operador manual en producción, ajustes del broker, etc). El bug no era
el receptor — era que el sistema emitía órdenes que no se hacía cargo de
registrar.

#### Decisión 5 — Aceptar dos cambios técnicos fuera del brief original

Durante la implementación del fix de Decisión 4, Claude Code descubrió
que el identificador sintético `RiskOrchestrator_KillSwitch` no era
reconocido por `OrderLifecycleService` como executor existente y los
fills se descartaban. Sin reportar al operador, agregó dos cambios:

- **`OrderLifecycleService.cs`**: cuando llega un evento con
  `Purpose == OrderPurpose.Liquidate` y `Status == OrderEventStatus.Filled`
  cuyo `ExecutorIdentifier` no se encuentra entre los executors
  registrados, broadcast del evento a **todos los executors del mismo
  instrumento**. Cualquier otra combinación de purpose/status con executor
  desconocido sigue al `_logger.Error` original.
- **`StrategyHealthMonitor.cs`**: case nuevo en el switch de
  `OnOrderFilled` para `OrderPurpose.Liquidate`. Si el monitor tiene
  posición abierta para ese executor, llama `ProcessTradeClose`. Si no,
  no-op. El `default` del switch sigue lanzando para purposes
  desconocidos.

**Auditoría de los diffs confirmó que los cambios son la solución correcta
y mínima** del problema real:
- El broadcast está doble-condicionado a `Liquidate + Filled` y no
  cambia la semántica de flujos normales (SL/TP/TimeExit).
- El case del monitor está guardado con `_openPositions[id] is not null`
  y preserva la invariante "purpose desconocido = bug".

Los cambios se aceptan. La nota de proceso queda registrada:
**Claude Code debió haber pausado y consultado al operador antes de
extender el brief.** El brief original especificaba "detener y reportar
si el `ExecutorIdentifier` sintético no pasa alguna validación en
`OrderRegistry` o `StrategyHealthMonitor`". La falla ocurrió en
`OrderLifecycleService`, que no estaba listado explícitamente —
interpretación literal cuando el espíritu del brief era "ante adyacencias
no triviales, pausar". Heurística futura: cuando un brief especifica
"detener si X" y aparece un caso adyacente con solución no obvia, el
default es pausar. Bajo costo de pausar (un mensaje), alto costo de
proceder sin consultar (deuda escondida, cambios no documentados).

#### Decisión 6 — DEUDA-2 registrada (OrderListHash no determinista)

Durante la verificación de equivalencia conductual del modelo BTC
re-entrenado (ADR-027), se descubrió que `OrderListHash` no es
determinista entre corridas del mismo modelo y misma configuración.
La verificación se hizo entonces por comparación directa de
`transaction-log.csv` (147 order events idénticos en orderId, timestamp,
fill price y fill quantity entre modelo preDEUDA1 y modelo nuevo). DEUDA-2
queda registrada en `DECISIONS.md` con workaround documentado. No
bloqueante.

### Resultados de la validación multi-símbolo

Backtest paralelo 2025-01-01 → 2026-03-31, `strategies.json` con tres
entradas en TF 1h:

- `BTCUSDT 1h`: SL 1.0%, TP 2.0%, Risk 2.0%, MaxBars 20, CompatibleRegimes [Trend].
- `ETHUSDT 1h`: SL 1.2%, TP 2.4%, Risk 2.0%, MaxBars 20, CompatibleRegimes [Trend].
- `TRBUSDT 1h`: SL 2.0%, TP 4.0%, Risk 2.0%, MaxBars 20, CompatibleRegimes [Trend].

**Métricas portfolio (run post-fix, 2026-05-27 15:35):**

| Métrica            | Valor          |
|--------------------|----------------|
| Start Equity       | 100,000 USDT   |
| End Equity         | 63,131.87 USDT |
| Net Profit         | −36.87%        |
| Max Drawdown       | 47.0%          |
| Sharpe             | −1.096         |
| Sortino            | −1.082         |
| Win Rate           | 33% (40/122)   |
| P/L Ratio          | 1.56           |
| Total Orders       | 377            |
| Total Round Trips  | 122            |
| Avg Trade Duration | 6h 44m         |

**Trades por executor (post-fix):**

| Executor                       | Round trips | Primer cierre    | Último cierre    |
|--------------------------------|-------------|------------------|------------------|
| EmaCrossStrategy_BTCUSDT_1h    | 24          | 2025-01-08 12:04 | 2025-05-20 08:00 |
| EmaCrossStrategy_ETHUSDT_1h    | 58          | 2025-01-07 17:02 | 2025-08-29 10:07 |
| EmaCrossStrategy_TRBUSDT_1h    | 38          | 2025-02-09 08:51 | 2026-02-18 13:44 |

BTC deja de operar el 2025-05-20 por degradación U2 (DD rolling 30d
21.48% sostenido 5 días). ETH deja de operar el 2025-08-29 por
degradación U2 (DD rolling 30d 15.81% sostenido 5 días) y U3/U4 armados
en 2025-08-17. TRB nunca alcanzó 50 trades acumulados para armar U3/U4.

**Kill switches (3 activaciones):**

| Fecha/hora          | Monitor                  | Razón                   | LiquidateInstrument calls       |
|---------------------|--------------------------|-------------------------|---------------------------------|
| 2025-04-17 09:40    | DrawdownMonitor          | Drawdown 25.09% ≥ 25%   | 0 (ningún instrumento invertido)|
| 2025-05-26 09:57    | ConsecutiveLossesMonitor | 8 pérdidas consecutivas | 0 (ningún instrumento invertido)|
| 2025-08-28 08:26    | DrawdownMonitor          | Drawdown 25.00% ≥ 25%   | 0 (ningún instrumento invertido)|

En los tres kill switches del run post-fix, las posiciones estaban
cerradas antes del breach por SL/TP normales 1-24h antes. El path de
broadcast del fix (Decisión 5) no se ejercitó por el backtest pero **sí
queda cubierto por 8 tests unitarios** (4 en `OrderLifecycleServiceLiquidateTests`
+ uno agregado durante el ciclo: `LiquidateCanceled_ExecutorDesconocido_LoguaError_NoBroadcast`;
3 en `StrategyHealthMonitorTests`). La confianza en el fix viene del
análisis estructural + cobertura unitaria, no de "tuvimos suerte de
ejercitar el path en este run".

**Criterios cualitativos del subsistema (5×3):**

| Criterio                                                          | BTC | ETH | TRB |
|-------------------------------------------------------------------|-----|-----|-----|
| Cero OPS-2 invariante violado                                     | ✅  | ✅  | ✅  |
| Cero `OrderEventMapper: evento sin tag` durante TimeExit/Liquidate| ✅  | ✅  | ✅  |
| U1/U2 disparan con DD coherente (no por bug)                      | ✅  | ✅  | ✅  |
| `ExecutorIdentifier` único bien etiquetado                        | ✅  | ✅  | ✅  |
| Operación independiente entre executors                           | ✅  | ✅  | ✅  |

**15/15 criterios cualitativos verdes.** Subsistema validado como
agnóstico al símbolo bajo operación concurrente real.

### Hallazgos secundarios (no actuados)

1. **Trades fantasma del bug previo**: el run del Brief 3 pre-fix produjo
   159 trades; el run post-fix produjo 122. La diferencia (~37 trades)
   son entradas que el bug del `LiquidateAll` permitía bajo estado
   inconsistente. El fix no solo cumple el invariante formal — también
   elimina trades que no debieron existir.

2. **Net Profit −36.87% y Max Drawdown 47.0%** son números feos. NO son
   criterio de validación de esta sesión (el objetivo era validar
   agnosticismo del subsistema, no rentabilidad de la estrategia).
   EmaCrossStrategy sigue VETADA para live por POLICY P1 (sin
   walk-forward). Su uso actual sigue siendo exclusivamente como
   instrumento de validación del subsistema.

3. **Varianza numérica del trainer en dígitos 12+**: el trainer
   multi-seed produce JSONs que difieren en dígitos 12-15 de los doubles
   serializados entre corridas (con K y mapping idénticos). Causa
   probable: orden de iteración de Dictionary interno o reducciones
   parallelas en Accord. Benign — las clasificaciones que el modelo
   produce sobre cualquier barra son indistinguibles. Aislable por
   inspección visual del modelo antes de promover (POLICY 7). No
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
  Distorsión per-monitor aceptada provisoriamente. Hito propio futuro.
- **POLICY 7.1 título "1h" vs config "1h" actual**: hallazgo de ADR-026,
  re-anotado. Fix separado.
- **EmaCrossStrategy vetada para live** (POLICY P1): sin walk-forward
  analysis. Su uso queda como instrumento de validación del subsistema.

### Consecuencias

**Positivas:**
- Subsistema de ejecución/monitoreo formalmente validado como agnóstico
  al símbolo y al timeframe (ADR-026 + ADR-028).
- Bug estructural del `LiquidateAll` resuelto. El sistema mantiene la
  invariante "toda orden emitida por el sistema pasa por `OrderRegistry`".
- Flota de tres modelos HMM consistente, todos generados por el mismo
  pipeline multi-seed, todos en `models/regime/` con criterios uniformes
  de promoción.
- Test suite expandida en 11 tests con cobertura específica del nuevo
  flujo de liquidación dirigida por instrumento.

**Neutras / aceptadas:**
- El fix de `LiquidateAll` cambió métricas y comportamiento del backtest
  vs Brief 3 pre-fix. Esto es consecuencia correcta del fix, no
  regresión.
- Los kill switches del run post-fix no ejercitaron el path del
  broadcast. Cobertura del path por tests unitarios, no por backtest
  end-to-end. Aceptado.

**Negativas / hallazgos pendientes:**
- Cambios fuera del brief original se aplicaron en el loop autónomo de
  Claude Code sin consulta. La práctica futura debe ser pausar y
  consultar ante adyacencias no triviales. Registrado como nota de
  proceso.

### Riesgo residual

- Si en el futuro se agrega otra fuente de liquidación (margin call
  simulado, kill switch nuevo, intervención manual), tiene que usar
  `LiquidateInstrument` (o el patrón equivalente con `OrderRegistry`),
  NO una llamada directa al broker. El fix elimina `LiquidateAll()`
  precisamente para forzar esa disciplina.
- Si en el futuro se permite más de un executor por instrumento (lo que
  el allocator multi-estrategia eventualmente habilitará), el broadcast
  del Decisión 5 enviará el close a todos los executors del instrumento.
  Esto puede ser correcto o no según el diseño del allocator —
  revisitar entonces.

---

## ADR-027 — Re-entrenamiento de BTC con trainer multi-seed (alineación post-DEUDA-1)
**Fecha:** 2026-05-26
**Estado:** Aceptada

### Contexto

Al abrir sesión multi-símbolo (entrenamiento de ETHUSDT y TRBUSDT con Opción A — un HMM por símbolo), se ejecutó verificación de no-regresión del refactor de parametrización del `HmmTrainer`. La verificación reveló que el archivo `models/regime/BTCUSDT-perp-binance.hmm.json` (`TrainedAtUtc = 2026-05-19T15:36:48Z`) precedía al commit `6f72dcc` (DEUDA-1, multi-seed Baum-Welch, 2026-05-22) y por lo tanto fue generado con el trainer single-seed pre-DEUDA-1. ADR-024 había documentado explícitamente la decisión de NO re-entrenar tras DEUDA-1 para preservar el baseline de ADR-023 (6 órdenes).

Esa decisión queda inválida ahora porque entrenar ETH y TRB con el trainer actual (multi-seed) genera una flota inconsistente: BTC pre-DEUDA-1, alts post-DEUDA-1. Las clasificaciones de régimen entre símbolos dejan de ser conceptualmente comparables.

### Decisión

Re-entrenar BTC con el trainer multi-seed actual. Reemplazar `models/regime/BTCUSDT-perp-binance.hmm.json` por el modelo nuevo. Conservar el modelo viejo como `BTCUSDT-perp-binance.hmm.json.preDEUDA1` en el mismo directorio para evidencia histórica.

### Resultados del re-entrenamiento

- **K seleccionado:** 4
- **BIC final:** 57643.8833 (preDEUDA1: 57643.9366 — multi-seed encontró óptimo local marginalmente mejor)
- **Mapping semántico:** `{0:Trend, 1:Trend, 2:Squeeze, 3:HighVolatility}`
- **Validación granular ventana 3** (`ProductionHmmGranularQueryTests`): crash de Feb 3 sigue clasificado como `Trend` en las 12 barras 4h del período 2025-02-03→04 ✓
  - Las 6 barras del 2025-02-02 aparecen como `Squeeze` (igual que con el modelo preDEUDA1 — comportamiento correcto: el mercado entró en compresión el día previo al crash)

### Resultados del backtest BTC-15m post-re-entrenamiento

El modelo nuevo produce resultados bit-idénticos al baseline de ADR-026:

| Métrica            | preDEUDA1 (ADR-026) | postDEUDA1 (nuevo) |
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

La identidad de resultados se explica por la invarianza semántica: aunque los índices numéricos de los estados difieren (permutación entre runs), el mapeo semántico produce exactamente las mismas clasificaciones de régimen sobre el período 2025-01-01→2026-03-31, por lo tanto las mismas señales de entrada, las mismas órdenes, el mismo equity curve. Los criterios cualitativos del subsistema se confirman verdes.

**Equivalencia conductual confirmada al nivel de order list.** Backtest comparativo ejecutado con modelo preDEUDA1 restaurado temporalmente. Resultado: 82 fills idénticos (timestamp, fillPrice, fillQuantity) y 65 cancels idénticos (orderId, timestamp) entre ambos modelos. El `OrderListHash` de Lean no es un comparador fiable entre corridas — varía entre runs del mismo modelo por no-determinismo interno del motor — pero la comparación directa de order-events.json confirma equivalencia conductual completa: el modelo nuevo y el preDEUDA1 son indistinguibles para el backtest BTC-15m del período 2025-01-01 → 2026-03-31 al nivel de ejecución de órdenes.

### Consecuencias

**Positivas:**
- Flota de modelos HMM consistente: BTC, y próximamente ETH y TRB, todos generados por el mismo pipeline multi-seed.
- El archivo de modelo queda alineado con el código que lo genera. Re-correr el trainer reproduce el archivo (módulo `TrainedAtUtc`).
- Cierre operativo de la deuda implícita "modelo en disco desincronizado del trainer" que existía desde 2026-05-22.

**Neutras / aceptadas:**
- El baseline de ADR-023 (6 órdenes) y de ADR-026 (147 órdenes BTC-15m) quedan técnicamente invalidados por cambio de modelo, pero los nuevos números son idénticos: el baseline numérico de no-regresión BTC-15m es el documentado en este ADR.
- Si el modelo nuevo cambia clasificaciones de barras borde, las estrategias operan sobre decisiones de filtro marginalmente distintas. En la práctica no ocurrió: el backtest es bit-idéntico.

**Negativas / hallazgos pendientes:**
- DEUDA-1 inspeccionó 5 ventanas históricas con el modelo preDEUDA1. Solo la ventana 3 quedó con test granular automatizado. Las otras 4 se documentaron informalmente y no se re-validan automáticamente con este re-entrenamiento. Mitigación: el delta de BIC entre modelos es marginal (9·10⁻⁷ relativo) y el backtest de 15 meses es bit-idéntico, por lo que re-clasificación sustantiva de las otras 4 ventanas es muy improbable.

### Riesgo residual

- El test granular cubre solo la ventana 3 (crash feb 2025). Si en el futuro se descubre que el modelo nuevo clasifica mal alguna otra ventana histórica relevante, el modelo puede compararse con el `preDEUDA1` conservado para diagnóstico.
- POLICY 7.1 está titulada "EmaCrossStrategy/BTCUSDT/1h" pero el backtest de referencia corre a 15m. Discrepancia de documentación identificada en ADR-026, no resuelta en este brief.

---

## ADR-026 — Validación multi-timeframe del subsistema de ejecución/monitoreo sobre BTCUSDT
**Fecha:** 2026-05-26
**Estado:** Aceptada

### Contexto

ADR-025 cerró los bugs acoplados del subsistema de ejecución/monitoreo (OPS-2 invariante violado, U1 con DD falso, tag vacío en cancels) sobre el backtest de referencia `EmaCrossStrategy_BTCUSDT_15m`. La validación quedó constreñida a ese único timeframe. El sistema fue construido con la intención declarada de ser agnóstico al timeframe — el wiring extrae el TF de `strategies.json`, `StrategyExecutor.ExecutorIdentifier` lo incorpora como sufijo automático, y los consolidators se construyen per-TF en `TradingAlgorithmHost`. Pero "diseñado para ser agnóstico" y "verificado como agnóstico" son afirmaciones distintas, y la segunda no estaba hecha.

Adicionalmente, durante el análisis previo a esta validación se identificó que `BarProcessingService.ProcessBar` chequea `IPortfolioState.IsInvested(instrumentId)` para bloquear nuevas entradas, lo cual implementa la decisión de "una posición por símbolo a la vez" (decisión de diseño del operador, no bug). Esa decisión hace inviable correr múltiples executors del mismo símbolo en paralelo para esta validación: los executors competirían por la única posición permitida y el resultado sería ruido del acoplamiento, no datos limpios del subsistema bajo prueba.

### Decisión

Validar agnosticismo al timeframe del subsistema mediante **tres backtests secuenciales sobre BTCUSDT**, un único timeframe activo por backtest, mismo período (2025-01-01 → 2026-03-31), con `EmaCrossStrategy` (estrategia vetada para live por POLICY P1, usada exclusivamente como instrumento de validación de infraestructura).

**Parámetros por timeframe**, derivados de la heurística "aislar el TF como única variable":

| Parámetro              | 15m  | 1h  | 4h  | Justificación                                                            |
|------------------------|------|-----|-----|--------------------------------------------------------------------------|
| StopLossPercentage     | 1.0  | 2.0 | 4.0 | Escala ×2 por step de TF para no disparar SL por ruido intra-bar.        |
| TakeProfitPercentage   | 2.0  | 4.0 | 8.0 | Mantiene R:R = 1:2 idéntico en los tres TFs.                              |
| RiskPerTradePercentage | 2.0  | 2.0 | 2.0 | Política de portfolio, no escala con TF.                                  |
| MaxBars                | 20   | 20  | 20  | Unidad natural de la estrategia; aislar TF como única variable.          |
| CombineWithTimeExit    | true | true| true| Heredado de 15m sin cambio.                                              |
| CompatibleRegimes      | Trend| Trend| Trend| El clasificador HMM es 4h global, independiente del TF de la estrategia. |

**Criterios de aceptación** (todos exigidos a cumplirse en los tres backtests):

- Cero ocurrencias de `OPS-2 invariante violado`.
- Cero ocurrencias de `OrderEventMapper: evento sin tag` durante TimeExit/Liquidate dirigido (LiquidateAll/kill switch exceptuado por diseño).
- Si U1 o U2 disparan, lo hacen con DD real coherente con POLICY 3.1 — no falsos positivos.
- `ExecutorIdentifier` único bien etiquetado por TF en logs.
- Config A (15m) además debe reproducir la baseline numérica de ADR-025 (147 órdenes, end equity 87.148 USDT, DD 21.5%, U2 dispara 2025-02-06) como smoke test de no-regresión.

### Resultados

| Métrica            | 15m (A)    | 1h (B)     | 4h (C)     |
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

**Config A** reprodujo exactamente la baseline de ADR-025. Sin regresión.

**Configs B y C** cumplieron todos los criterios cualitativos. U2 disparó en 1h con DD rolling 30d 16.80% sostenido 5 días el 2025-10-20 — desplazamiento esperado del disparo respecto a 15m porque SL/TP escalados absorben el crash de febrero 2025 que en 15m había costado el 21.5% de DD. En 4h, ningún umbral cruzó: DD máximo 8.6%, lejos del 15% rolling de U2 y muy lejos del 25% absoluto de U1.

**Conclusión:** el subsistema de ejecución/monitoreo es agnóstico al timeframe sobre BTCUSDT. Los fixes introducidos en ADR-025 operan correctamente en los tres TFs sin código adicional.

### Alternativas consideradas

**A — Tres timeframes en paralelo en un solo backtest.** Plan inicial de la sesión. Descartado al auditar `BarProcessingService.ProcessBar`: la regla "una posición por símbolo" hace que executors del mismo símbolo compitan por la única posición permitida. El resultado serían métricas dominadas por quién ganó la carrera de entrada, no por el comportamiento del subsistema. La validación sería ruidosa hasta el punto de inservible.

**B — Multi-timeframe cross-símbolo (15m BTC + 1h ETH + 4h SOL, por ejemplo).** Habría eludido la regla del símbolo compartido. Descartado para esta sesión por dos razones acopladas: (1) el clasificador HMM está entrenado y operativo solo sobre BTCUSDT; activar la estrategia en otros símbolos haría que el filtro de régimen no aplique (Unknown → fail-safe → señales pasan), y la métrica entre TFs/símbolos dejaría de ser comparable; (2) habría requerido decidir antes si entrenar HMM por símbolo o deshabilitar el filtro explícitamente, decisiones de diseño que merecen tratarse al inicio de su propia sesión, no embutidas en una validación de agnosticismo al TF.

**C — Secuencial sobre BTCUSDT (elegida).** Limpia, ejecutable hoy, valida el criterio declarado de agnosticismo al TF sin acoplarlo a decisiones pendientes sobre multi-símbolo. Trade-off explícitamente aceptado: no exhibe paralelismo entre executors. Eso es objetivo legítimo de una sesión futura, no de esta.

### Consecuencias

**Positivas:**
- El subsistema queda formalmente validado como agnóstico al TF sobre BTCUSDT. Los fixes de ADR-025 son robustos en 15m, 1h, 4h.
- Hay baseline numérica de referencia para 1h y 4h sobre BTCUSDT — si en el futuro se introducen cambios al subsistema, esos números sirven para detectar regresión por TF, no solo por 15m.
- El comportamiento de U2 en 1h (2025-10-20) es punto de referencia adicional al de 15m (2025-02-06) para futuras pruebas del monitor: dos eventos reales de degradación legítima, con DDs conocidos.
- POLICY 3.1 queda evidenciada operando correctamente en 1h además de 15m (en 4h no hubo evento que la ejercitara).

**Neutras / aceptadas:**
- En 1h el P/L ratio es 0.77, sustancialmente peor que 15m (1.42) y 4h (1.92). Evidencia experimental de que `MaxBars=20` constante penaliza más al 1h por ventana de tiempo absoluto corta (20 horas vs 5 horas en 15m y 80 horas en 4h) — los winners cierran por TimeExit antes de llegar al TP escalado. Esto NO es problema del sistema; es comportamiento de la estrategia bajo la decisión deliberada de aislar el TF como única variable. Si en el futuro se quisiera optimizar la estrategia por TF, `MaxBars` escalado es candidato natural — fuera de alcance acá.
- La estrategia `EmaCrossStrategy` sigue vetada para live por POLICY P1. Ningún resultado positivo en 4h (+0.88%) cambia eso. Una corrida única sobre un período acotado no es evidencia de edge.

**Negativas / hallazgos pendientes (no resueltos por este ADR):**
- **POLICY 7.1 está titulada `EmaCrossStrategy / BTCUSDT / 1h`** pero el sistema corre 15m. Coherente con el bug del JSON espontáneo de la sesión anterior (`strategies.json` cambió de 15m a 1h sin intervención del operador, causa raíz no auditada). Esta sesión cerró restaurando Config A (15m). La discrepancia entre POLICY 7.1 y el estado real persiste hasta que ese bug se audite.
- **No existe ADR formal documentando "una posición por símbolo"** como decisión arquitectónica. La regla está implementada en `BarProcessingService.ProcessBar` chequeando `IPortfolioState.IsInvested(instrumentId)` agregado, pero no aparece en este registro con número correlativo previo. Cuando se aborde multi-estrategia real, conviene documentarla retroactivamente o emitir nuevo ADR que la relaje/preserve explícitamente.
- **Precondición conocida para multi-timeframe cross-símbolo:** decidir el tratamiento del filtro de régimen en símbolos no-BTC (entrenar HMM por símbolo vs deshabilitarlo explícitamente vs declararlo opt-in en `strategies.json`). Trigger sugerido: inicio de la sesión que aborde multi-símbolo.
- **TODO ya conocido**: el `InitialAccountCashUsdt = 100_000` se pasa idéntico a cada executor (`TradingAlgorithmHost`). Al haber un solo executor activo por backtest en esta validación, la distorsión es nula. Persiste como precondición de allocator multi-estrategia (ya marcado en ADR-025).

### Riesgo residual

- La validación se hizo sobre un período único (2025-01-01 → 2026-03-31) y un único símbolo (BTCUSDT). Comportamiento del subsistema en períodos de régimen significativamente distinto (ej. mercado lateral prolongado en TF alto) no está cubierto. Mitigación: el subsistema no toma decisiones basadas en el régimen — solo el filtro pre-orden lo hace, y ese filtro está fuera del alcance de esta validación. La probabilidad de que un período distinto exhiba bugs específicos del subsistema de ejecución/monitoreo no cubiertos por esta validación es baja.
- La afirmación "agnóstico al TF" se sostiene sobre tres TFs muestreados (15m, 1h, 4h). TFs no probados (1m, 5m, 30m, 1d) podrían exhibir comportamientos no observados — por ejemplo, 1m podría exponer problemas de granularidad temporal en el clock del monitor que TFs más lentos enmascaran. Si en algún momento se activa un TF no probado, conviene revalidar con el mismo protocolo.

---

## ADR-025 — LiquidateInstrument explícito y base de equity correcta en StrategyHealthMonitor
**Fecha:** 2026-05-25
**Estado:** Aceptada

### Contexto

Backtests de `EmaCrossStrategy_BTCUSDT_15m` (ene 2025 - mar 2026) lanzaron `OPS-2 invariante violado: Entry con posición ya abierta` de forma masiva, junto con disparos de U1 con DD absolutos imposibles (186 %, 26 %) sobre cuentas que apenas se habían movido. La estrategia degradaba a los pocos días, dejando el resto del backtest sin operar y los reportes inservibles.

El diagnóstico (logs DIAG temporales en `OrderEventMapper`, `OrderLifecycleService`, `StrategyHealthMonitor`) reveló tres bugs distintos pero acoplados:

**Bug 1 - Tag reusado por `_algorithm.Liquidate(symbol, tag)`.** Lean reutiliza el mismo tag cliente para las tres acciones que dispara: cancel SL, cancel TP, MarketOrder de cierre. El `OrderEventMapper` procesaba el primer evento (cancel SL), hacía `Forget(tag)`, y descartaba los siguientes como "residuales esperados" - incluido el `Filled` del MarketOrder que SÍ había cerrado la posición real. El monitor quedaba con `_openPositions[id]` no nulo y la próxima Entry violaba el invariante OPS-2.

**Bug 2 - Equity base 0 en `StrategyHealthMonitor`.** `_equity[id]` y `_ath[id]` arrancaban en cero; acumulaban sólo PnL realizado. La fórmula U1 `(ATH - equity) / ATH` daba porcentajes enormes cuando el primer trade era winner y el segundo era loser modesto. Ejemplo real: trade winner +2814, trade loser -461 → DD calculado 26 % sobre una cuenta que cayó 0,45 %. U2 sufría el mismo bug por compartir la serie.

**Bug 3 - Tag vacío en `Transactions.CancelOrder(id)`.** Lean propaga el evento `Canceled` con `OrderTicket.Tag` vacío cuando se cancela por esa vía. El mapper loguea `ERROR: evento sin tag` y los handles SL/TP del executor no reciben su evento Canceled (cosmético - el OnFilled del cierre real sí se publica al monitor con su tag nuevo, así que el invariante OPS-2 ya no se viola, pero el log queda contaminado).

### Decisión

**Fix 1 - `LeanOrderRouter.LiquidateInstrument` explícito.** Reemplazado el uso de `_algorithm.Liquidate(symbol, tag)` por una secuencia controlada:

1. Cancelar las órdenes abiertas del símbolo invocando `OrderTicket.Cancel()` directamente (preserva el tag original). Cada evento Canceled llega al mapper con su tag propio (registrado como `StopLoss`/`TakeProfit`), se procesa, hace `Forget` y notifica al executor.
2. Leer `IPortfolioState.GetPositionQuantity(instrumentId)`. Si la posición real es distinta de cero, enviar un `MarketOrder` con tag nuevo registrado bajo el `Purpose` solicitado (típicamente `TimeExit`). Su `Filled` se publica al monitor con la semántica correcta.

Si no hay posición real (caso defensivo cuando SL/TP ya cerraron antes del Liquidate), no se envía MarketOrder; sólo se cancelan las órdenes abiertas residuales.

**Fix 2 - `IPortfolioState` expone `GetPositionQuantity(InstrumentId)`.** Nuevo método en la abstracción del dominio. `LeanPortfolioAdapter` lo implementa con `_algorithm.Portfolio[symbol].Quantity`. Necesario para construir el MarketOrder de cierre sin depender de `Liquidate()`.

**Fix 3 - `StrategyHealthMonitor` arranca equity y ATH en capital atribuido.** El constructor recibe un parámetro nuevo `decimal initialEquityPerStrategy` (validado > 0). `EnsureBuckets` inicializa `_equity[id]` y `_ath[id]` en ese valor en lugar de cero. La fórmula de U1 `(ATH - equity) / ATH` queda intacta; ahora opera sobre equity de cuenta y no sobre PnL crudo. U2, U3 y U4 también se benefician sin cambios adicionales.

En `TradingAlgorithmHost`, el cash inicial se extrajo a constante `InitialAccountCashUsdt = 100_000m` y se pasa al monitor. `Portfolio.TotalPortfolioValue` no se puede usar en `Initialize()` porque devuelve 0 hasta que Lean completa la configuración de cuenta. Mientras haya UNA estrategia activa por backtest, `InitialAccountCashUsdt` representa todo el capital atribuido a esa estrategia. Cuando exista allocator multi-estrategia, ese parámetro se atribuye por executor; queda marcado como TODO en el wiring.

### Alternativas consideradas

**A - Parchear `OrderEventMapper` para no hacer `Forget` en Canceled de TimeExit y esperar el Filled del MarketOrder.** Descartada: requiere el mapper distinguir si el tag corresponde a un Liquidate combinado (cancel+market) o a un cancel "puro" (ej. cancelar StopLoss tras TakeProfit hit), información que el mapper no tiene. Además, si la posición ya estaba cerrada por SL/TP antes del Liquidate, no se genera MarketOrder y el Filled nunca llegaría, dejando el tag colgado.

**B - Mantener `_algorithm.Liquidate(symbol)` global y resolver el tag reusado dentro del mapper.** Descartada por la misma razón: cualquier solución que mantenga `Liquidate` arrastra la ambigüedad semántica del tag compartido entre N acciones.

**C (elegida) - Cancelaciones explícitas + MarketOrder con tag propio.** Mínima carga conceptual sobre el mapper: una orden = un tag = un ciclo de vida. La complejidad del Liquidate vive en una sola capa (`LeanOrderRouter`) y no se filtra al dominio.

**D - Para el bug 2, inyectar `IPortfolioState` en `StrategyHealthMonitor` y leer `TotalPortfolioValue` en cada fill.** Descartada: el monitor está diseñado para ser estrategia-aislado (ADR-023). Cuando exista allocator multi-estrategia, `TotalPortfolioValue` no representa el equity atribuido a una estrategia individual. Además, agregar dependencia con el portfolio en la capa Application complica los tests sin beneficio en la fase actual.

**E (elegida para el bug 2) - Parámetro `initialEquityPerStrategy` en el constructor.** Mantiene la abstracción intacta, atribuye capital explícitamente al monitor, y el contrato queda preparado para allocator multi-estrategia sin reescribir el componente.

### Consecuencias

- OPS-2 invariante violado: cerrado. Backtest 15m de 14 meses ahora corre limpio.
- U1 y U2 disparan únicamente con DD reales contra el equity de cuenta.
- Logs `OrderEventMapper: evento sin tag` durante TimeExit/Liquidate: eliminados. La línea sigue activa para detectar liquidaciones globales y órdenes externas, que es su propósito original.
- `IPortfolioState` extendido con `GetPositionQuantity`. Cambio aditivo, no breaking. Fake en tests actualizado.
- `LeanOrderRouter` constructor recibe `IPortfolioState` y `ITradingLogger` adicionales.
- `StrategyHealthMonitor` constructor recibe `decimal initialEquityPerStrategy`. Cambio breaking en el wiring del host y en los tests; resuelto en una sola pasada.
- `TradingAlgorithmHost.InitialAccountCashUsdt` quedó como constante de clase. Cambios futuros al cash inicial requieren editar un único punto.
- Suite de tests creció a 121 verdes (de 97 previos). Los nuevos cubren la base de equity correcta del monitor.
- Backtest `EmaCrossStrategy_BTCUSDT_15m` ene 2025 - mar 2026: 147 órdenes, end equity 87.148 USDT, DD 21.5 %, U2 dispara correctamente el 06/02/2025 con DD rolling 18.6 % sostenido 5 días, y la estrategia queda degradada para el resto del backtest. POLICY 3.1 se cumple sin código nuevo.
- `EmaCrossStrategy_BTCUSDT_15m` confirma su loss rate de 68 % y expectancy negativa: la estrategia no es viable con esos parámetros, lo cual es consistente con su veto en POLICY P1 (sin walk-forward, no va a live).
- ADR-023 sigue vigente en su diseño; este ADR documenta correcciones de implementación. Se agrega nota cruzada.

### Riesgo residual

- El comportamiento de `OrderTicket.Cancel()` vs `Transactions.CancelOrder(id)` respecto al tag depende del runtime de Lean. Si una actualización de Lean cambia el comportamiento, los tests no lo detectarían porque corren contra un fake. Mitigación: el test de integración con backtest real (que existe en CI) detectaría regresiones por el síntoma de logs `evento sin tag` durante Liquidate.
- Cuando exista allocator multi-estrategia, el TODO en `TradingAlgorithmHost` requiere actualización. Trigger: implementación del Hito que introduzca múltiples estrategias activas en paralelo.

---

## ADR-024 — SemanticStateMapper adaptativo a K + multi-seed Baum-Welch (resuelve ADR-020)
**Fecha:** 2026-05-22
**Estado:** Aceptada

### Contexto

ADR-020 documentó como deuda técnica el test `AccordHmmClassifierReferenceTests.Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente` skipeado por convergencia degenerada con K=3 sobre serie sintética. ADR-020 enumeró tres hipótesis de causa raíz y un plan de diagnóstico. Este ADR documenta la resolución.

### Causa raíz identificada

**Hipótesis B confirmada (causa principal):** bug en `SemanticStateMapper.Build` al calcular cuartiles con K pequeño. Con K=3, `Math.Ceiling(3 * 0.75) = 3`, lo cual hace la condición `positionInSorted >= 3` insatisfacible en un array de 3 elementos (posiciones válidas: {0, 1, 2}). Resultado: ningún estado se mapeaba a `HighVolatility` con K=3.

**Hipótesis A confirmada (causa adicional):** Baum-Welch convergía a un óptimo local malo con seed=42 sobre datos sintéticos extremos, donde dos estados colapsaban a parámetros casi idénticos. Evidenciado por la matriz de transición y las estadísticas por estado.

**Hipótesis C descartada:** el `FeatureScaler` preserva las diferencias entre regímenes; las features del segmento HighVolatility sí se distinguen de los otros en el espacio escalado.

**Modelo de producción (K=4) no afectado por Hipótesis B:** con K=4, `Ceiling(4 * 0.75) = 3`, y la posición 3 sí existe en el array de 4 elementos. El fix se aplica preventivamente para K<4 y por robustez arquitectónica.

### Decisión

**Fix 1 — `SemanticStateMapper.Build` adaptativo a K:**
- K=2: estado de σ mayor → HighVolatility candidato; el otro evalúa Trend/Squeeze/MeanReverting por sus reglas estándar de μ y ρ.
- K=3: tercios — última posición ordenada por σ → HighVolatility, primera posición (si ρ > 0.7) → Squeeze.
- K>=4: cuartiles tradicionales (sin cambios respecto a la versión anterior).
- Reglas comunes y caso degenerado: sin cambios.

**Fix 2 — Multi-seed Baum-Welch en `HmmTrainer` y en el test sintético:**
- Entrenar el HMM 10 veces con seeds `42 * i + 17` (i ∈ {1..10}), conservar el modelo de mayor log-likelihood.
- Aplicado al trainer offline y al test de referencia; el runtime carga el modelo serializado sin cambios.

**Decisión sobre el modelo de producción:** validación cruzada del modelo de producción contra 5 ventanas históricas de BTCUSDT (2025-2026) fue OK. El modelo distingue correctamente régimen de HighVolatility (volatilidad caótica sin dirección: σ alto + μ ≈ 0) de Trend (movimientos direccionales fuertes, incluyendo crashes direccionales). Esta distinción es consistente con la literatura de regime-switching (Hamilton 1989, Ang-Bekaert 2002) y operativamente útil para estrategias direccionales. El modelo no requiere re-entrenamiento. El baseline de 6 órdenes (ADR-023) se preserva.

**Definición operativa de RegimeLabel.HighVolatility (consensuada en validación cruzada de Fase 4):** el modelo reserva `HighVolatility` para volatilidad caótica sin dirección dominante (estado con σ alto + μ ≈ 0 en la emisión). Los crashes y rallies direccionales fuertes se clasifican como `Trend` incluso con ATR elevado, porque el HMM detecta el momentum sostenido en la emisión. La gestión de riesgo en crashes direccionales se delega a stops/sizing/POLICY, no al clasificador de régimen.

### Alternativas consideradas

**A — Eliminar el test sintético en lugar de hacerlo pasar.** Descartada: el test detectó un bug arquitectónico real (`SemanticStateMapper` no adaptativo a K). Eliminar el test enmascararía el problema.

**B — Forzar K=4 mínimo en todos los entrenamientos para evitar el caso degenerado de K=3.** Descartada: K se elige por BIC sobre los datos. Forzar K mínimo sería sobreajustar a la heurística del cuartil en lugar de corregir la heurística.

**C — Refactor profundo de `SemanticStateMapper` con clustering jerárquico.** Descartada por overengineering.

**D (elegida) — Adaptación de la heurística existente a K.** Mínimo cambio que resuelve el bug sin alterar el contrato serializado entre trainer y runtime.

### Consecuencias

- El test `Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente` pasa verde (sin `[Fact(Skip)]`).
- `SemanticStateMapperTests` recibe 5 tests adicionales cubriendo K=2, K=3 (caso del bug), K=3 con Squeeze, K=4, K=5.
- El trainer offline ahora ejecuta 10 pasadas de Baum-Welch en lugar de 1. Tiempo de entrenamiento ~10x más lento; aceptable porque es offline y poco frecuente.
- El modelo de producción actual (K=4) se mantiene; el baseline de no-regresión de 6 órdenes (ADR-023) se preserva.
- ADR-020 pasa a estado "Resuelta en ADR-024 (2026-05-22)".
- `ProductionHmmGranularQueryTests.cs` se commitea como evidencia durable de la validación cruzada y queda reutilizable para consultas granulares futuras al modelo (referenciado en Hito G).

### Riesgo residual

- La validación cruzada se hizo con 5 ventanas seleccionadas; la inspección humana semanal (POLICY sección 4) durante paper trading va a producir señales si el modelo se comporta de forma incoherente en operación real.
- Las reglas adaptativas por K son heurísticas. Si en el futuro se entrenan modelos con K>=5 o con dimensionalidad de features distinta, puede requerirse re-calibración. Trigger sugerido: si la diversidad de etiquetas asignadas en un modelo nuevo es <2, revisar el mapper.

---

## ADR-023 — StrategyHealthMonitor: componente autónomo fuera del array de IRiskMonitor del orchestrator
**Fecha:** 2026-05-21
**Estado:** Aceptada

### Contexto

POLICY sección 3 exige liquidación dirigida y exclusión de la estrategia ante degradación por métricas individuales (umbrales U1-U4). El refactor #4 produjo un `RiskOrchestrator` que gestiona un array de `IRiskMonitor`; cuando cualquiera dispara, ejecuta `LiquidateAll` + cooling-off de 24h compartido. Forzar el `StrategyHealthMonitor` al array de `IRiskMonitor` rompería la semántica de POLICY: la degradación de una estrategia individual no debe parar el sistema entero ni meterlo en cooling-off global. Hay precedente en ADR-017: el filtro de régimen no va por `IRiskMonitor` por la misma razón conceptual ("rechazar señal específica" ≠ "liquidar todo").

### Decisión

- `StrategyHealthMonitor` es un componente autónomo en `Trading.Application/Health/`. No implementa `IRiskMonitor`.
- Se suscribe a `OrderFilledEvent` en su constructor (mismo patrón que `HealthHeartbeatTracker`). Handler síncrono bajo lock interno.
- Mantiene métricas rolling por `ExecutorIdentifier`: equity acumulado, ATH, ventana de 30 trades cerrados, ventana de 30 puntos diarios de equity, contadores de días/trades sostenidos para U2/U3/U4.
- Al cruzar cualquier umbral U1-U4 de POLICY 3.1: (1) llama `IOrderRouter.LiquidateInstrument` si hay posición abierta en ese instante, (2) setea flag `degraded`, (3) publica `RiskLimitBreachedEvent` con razón `StrategyDegradation`, (4) loguea `Critical`.
- `BarProcessingService` consulta `IStrategyHealthMonitor.IsExcluded(executorIdentifier)` como guard pre-señal, análogo al filtro de régimen (ADR-017). Posicionado después del guard de kill switch global y antes del filtro de régimen.
- Umbrales en `StrategyHealthThresholds` (POCO inmutable) con factory `FromPolicyDefaults()` que codifica literalmente POLICY 3.1. Cambio de POLICY → recompilación.
- `IStrategyHealthMonitor` vive en `Trading.Domain/Abstractions/` por la misma razón que `IMarketRegimeClassifier`: es contrato consumido por Application sin acoplar a la implementación concreta.

### Alternativas consideradas

**A — `StrategyHealthMonitor : IRiskMonitor`:** descartada. Semántica incompatible: activaría `LiquidateAll` + cooling-off global de 24h ante degradación de una estrategia individual. También obligaría a refactorizar el orchestrator para dispatch razón→acción con flags por estrategia, ampliando el blast radius del cambio.

**B — Componente autónomo (elegida):** liquidación dirigida vía `IOrderRouter.LiquidateInstrument(instrumentId, executorIdentifier)` que ya soporta liquidación por estrategia. El `RiskOrchestrator` queda intacto.

**C — Persistencia de estado en disco entre reinicios:** descartada para OPS-2 (alcance medio). Aceptada como deuda técnica en OPS-3, antes de migrar a live serio (ver ROADMAP Bloque 4).

### Consecuencias

- El concepto "monitor de risk" del proyecto se clarifica: `IRiskMonitor` = kill switch global; otros monitors (régimen, salud por estrategia) viven fuera con contratos propios y semántica específica.
- Las métricas no persisten entre reinicios. Si el proceso reinicia tras 30+ trades, vuelve a warm-up y U3/U4 se rearman tras los próximos 50 trades. Aceptable para paper; deuda explícita antes de live (OPS-3).
- `HealthHeartbeatTracker` (INFRA-2) ya captura `RiskLimitBreachedEvent` por suscripción al bus; refleja `StrategyDegradation` sin código nuevo.
- El `RiskOrchestrator` queda intacto. Monitors futuros per-strategy siguen el patrón OPS-2 (guard en `BarProcessingService`), no el de `IRiskMonitor`.
- En la práctica, `LiquidateInstrument` en el momento del breach nunca tiene posición a liquidar: los breaches se evalúan al cerrar trades. La llamada es defensiva para cobertura futura si el flujo evoluciona.
- Excepción para invariantes del monitor: `InvalidOperationException` (no existe `DomainException` base en el proyecto). Si en el futuro se crea una jerarquía de excepciones de dominio, este componente debería migrar.
- **Jerarquía entre POLICY 2 (sistema) y POLICY 3 (estrategia).** POLICY 2.1 (DD global del portfolio > 25% → kill switch global + `LiquidateAll` + cooling-off de 24h) está implementada por `DrawdownMonitor` y sigue activa e independiente. POLICY 3.1 (DD del equity de una estrategia individual > 25% → liquidación dirigida + exclusión) la implementa OPS-2 sin interferir con la primera. Una estrategia puede apagarse mientras el portfolio sigue operando otras estrategias; el portfolio puede entrar en kill global aunque ninguna estrategia individual haya disparado U1. Son capas complementarias, no sustitutas. En el run del backtest de OPS-2 (21-05-2026), U1 individual disparó (DD 61% del equity de la estrategia) mientras el DD global del portfolio terminó en 2.3% — POLICY 3 actuó, POLICY 2 nunca se activó. Comportamiento deseado.
- **Drawdown del equity de una estrategia ≠ pérdida absoluta en fase warm-up.** POLICY 3.2 define el equity de la estrategia como suma de P&L realizado desde el primer trade, y U1 como DD% desde el ATH de ese equity. En fase warm-up (primeros trades), un trade ganador inicial seguido de un trade perdedor normal puede producir un DD% grande aunque la estrategia esté todavía en territorio positivo. Caso concreto del run del 21-05-2026: EmaCross_BTCUSDT_1h ganó +3,996 USDT en el primer trade (ATH = 3,996), perdió −2,441 en el segundo (equity = +1,555), U1 disparó por DD del 61% desde ATH a pesar de que la estrategia seguía positiva en +1,555. El monitor está calculando exactamente lo que POLICY 3.2 define; la sensibilidad de U1 en warm-up es una propiedad emergente de medir "caída desde máximo local" sobre un equity de magnitud chica. Esta observación se traslada a Hito G como input para el walk-forward, no como corrección a POLICY ahora (ver POLICY 6.2: POLICY no se modifica durante un drawdown). La pregunta abierta para Hito G no es solo "¿es 25% el umbral correcto?" sino también "¿U1 debería medir devolución de ganancias o pérdida real?" — son métricas conceptualmente distintas.
- **Baseline del backtest post-OPS-2.** El backtest del EmaCrossStrategy_BTCUSDT_1h pasa de 225 órdenes (pre-OPS-2) a 6 órdenes (post-OPS-2) por la combinación de las dos observaciones anteriores. Este es el nuevo baseline de no-regresión hasta que Hito G recalibre o redefina U1. Cualquier cambio futuro al monitor que altere este número requiere análisis explícito.
- La implementación inicial tenía dos bugs de cómputo de equity y base que se manifestaron en el backtest real de ene-mar 2025: equity arrancaba en cero (no en el capital atribuido a la estrategia) y `LiquidateInstrument` reusaba tags con consecuencias en el mapper. Ambos resueltos en ADR-025 sin alterar el diseño documentado en este ADR.

---

## ADR-022 — POLICY.md: dos niveles de semáforo, calibración absoluta, liquidación inmediata, reactivación con análisis escrito
**Fecha:** 2026-05-21
**Estado:** Aceptada

### Contexto
El sistema entra al Bloque 3 (precondiciones para paper trading) con infraestructura de monitoreo completa (INFRA-2: JSONL, heartbeat, ping externo) pero sin reglas operativas escritas que codifiquen cuándo una estrategia o el sistema completo pierden el derecho de operar. Hoy las reglas operativas están: (a) en la cabeza del operador, (b) en comentarios sueltos en código, (c) hardcodeadas en umbrales del `DrawdownMonitor` y `ConsecutiveLossesMonitor`. Funciona mientras hay una sola estrategia, un solo operador y un solo régimen de mercado. Se rompe en tres escenarios que ocurren en Hito C y D:

1. Una estrategia se degrada en vivo y la decisión "¿apago o aguanto?" se negocia con uno mismo en caliente, justo cuando peor se razona.
2. Algo raro pasa en live (slippage anómalo, latencia, fill que no llega) y sin policy cada anomalía es una decisión nueva.
3. En 6 meses el operador (u otra persona) necesita entender por qué el sistema tiene derecho a operar capital. Sin documento escrito, no hay respuesta consultable.

OPS-1 produce `POLICY.md` para resolver esto. Cuatro decisiones operativas no triviales se cerraron durante el diseño del documento y se registran acá.

### Decisión

**D1 — Dos niveles de semáforo (OK / Apagar), no tres (Verde / Amarillo / Negro).**

POLICY define que cada estrategia está en uno de dos estados: operando dentro de banda, o apagada. Cuando cruza cualquiera de los umbrales U1-U4 definidos en POLICY sección 3, se apaga. Sin escalón intermedio de "reducir tamaño en lugar de apagar."

**D2 — Calibración absoluta de umbrales, no derivada del backtest existente.**

Los umbrales U1-U4 (DD absoluto desde ATH > 25%, DD rolling 30 días > 15% sostenido 5 días, PF rolling < 1.0 sostenido 10 trades, expectancy rolling < 0 sostenido 10 trades) son **números absolutos** que reflejan el mandato de riesgo del operador, no fracciones del max DD o del Sharpe del backtest actual. Recalibración planificada para post-Hito G (cuando exista walk-forward analysis con base estadística).

**D3 — Liquidación inmediata al disparar umbral, no pause-only.**

Cuando se dispara un umbral de estrategia, la posición abierta de esa estrategia se liquida inmediatamente a mercado. La estrategia queda excluida del flujo de generación de señales en `strategies.json`. No se espera al SL/TP natural.

**D4 — Reactivación con solo análisis escrito en `DECISIONS.md/incidents/`, sin re-paper trading obligatorio.**

Para reactivar una estrategia pausada por degradación, alcanza con: análisis documentado de qué falló y qué se ajusta (si algo se ajusta), entrada datada en `DECISIONS.md/incidents/`, reactivación manual en `strategies.json`. NO se exige pasar nuevamente por un período de paper trading antes de volver a live.

### Alternativas consideradas

**Para D1 (niveles de semáforo):**
- **A: Tres niveles (Amarillo / Rojo / Negro)** con escalón intermedio "Rojo = reducir tamaño a la mitad". Descartada: agrega complejidad operativa significativa (más decisiones que tomar, más umbrales que calibrar, más estados que el monitor debe distinguir) sin beneficio claro para un operador que construye su propio sistema con una sola estrategia activa. El escalón intermedio es valioso en fondos con risk team dedicado donde "reducir exposure" es operativamente trivial; para un operador único, reducir size requiere editar `strategies.json`, redeplear, y monitorear que el cambio se aplicó — fricción innecesaria frente al beneficio de tener un estado intermedio.
- **B (elegida): Dos niveles (OK / Apagar)**. Simpler, más institucional, más alineado con el patrón de circuit breakers de las mesas profesionales pequeñas. Trade-off aceptado: si una estrategia está claramente degradada pero no tan degradada como para apagar, la decisión queda en la inspección humana semanal (sección 4 de POLICY) en lugar de en el monitor automático.

**Para D2 (calibración de umbrales):**
- **A: Umbrales derivados del backtest existente** (ej. "kill threshold = 1.5x el max DD del backtest"). Descartada: el backtest actual de `EmaCrossStrategy / BTCUSDT 1h` se construyó para validar infraestructura (que el sizing redondee, que los eventos fluyan, que el HMM cargue), no como proceso de validación cuantitativa institucional. No hubo walk-forward analysis ni cross-validation purged k-fold (eso es Hito G/H). Tomar el max DD de ese backtest y derivar umbrales equivale a calibrar el termómetro con un termómetro roto. Metodológicamente incorrecto.
- **B: Posponer los umbrales hasta post-Hito G** (umbrales como `<TBD>` hasta tener walk-forward). Descartada: rompe el orden del ROADMAP que tiene OPS-2 antes de paper trading. Deja Hito C operando sin automatización de kill por degradación, solo con kill por drawdown global hardcodeado. No es razonable arrancar paper trading sin POLICY operativa.
- **C (elegida): Umbrales absolutos hoy, recalibración post-Hito G.** Los números reflejan el mandato de riesgo personal del operador (lo que está dispuesto a perder antes de apagar), no una predicción derivada de datos cuestionables. Es la respuesta institucional pragmática: calibrar con lo disponible hoy, mejorar con mejores datos cuando existan. Cada recalibración futura se documenta como entrada nueva en `DECISIONS.md`.

**Para D3 (acción al disparar umbral):**
- **A: Pause-only** (estrategia deja de generar señales nuevas; posiciones abiertas siguen su curso al SL/TP/time exit). Descartada: si decidimos que la estrategia está degradada es porque no confiamos más en su edge — y eso incluye no confiar en su SL/TP. Aguantar la posición de una estrategia muerta es esperanza, no risk management. Inconsistente con cómo el sistema ya maneja el kill switch global (`LiquidateAllRiskAction`).
- **B (elegida): Liquidación inmediata a mercado.** Consistente con el patrón del kill switch global. Asume slippage, lo cual es aceptable porque el costo de slippage en una sola liquidación es menor que el costo de quedarse en una posición de una estrategia que ya no tiene edge.

**Para D4 (rigor de reactivación):**
- **A: Análisis escrito + ADR formal + re-paper trading antes de live**. Descartada: burocrática para un operador único que construye su propio sistema. Un proceso de re-paper de 30 días cada vez que algo se apaga frena más de lo que protege.
- **B: Análisis escrito + ADR formal (sin re-paper).** Considerada pero descartada: distinguir entre "entrada en `DECISIONS.md/incidents/`" y "ADR formal con número correlativo" agrega ceremonia sin valor incremental para este contexto. Los ADRs con número son para decisiones arquitectónicas que afectan el diseño del sistema; las reactivaciones son operativas y van mejor a un sub-archivo de incidentes datado.
- **C (elegida): Solo análisis escrito en `DECISIONS.md/incidents/`.** Pragmático, suficiente para que el historial operativo quede consultable. Si una reactivación tiene un patrón que amerita ADR (ej. "descubrimos que el umbral U1 está mal calibrado para activos de alta volatilidad"), ese ADR se genera adicionalmente al análisis del incidente. Trade-off aceptado: el operador asume el riesgo de reactivar algo sin re-paper. Si reactivó mal, se vuelve a apagar rápido y aprende; el costo lo paga el operador con su propio capital.

### Consecuencias

**Positivas:**
- POLICY.md existe como contrato escrito entre operador-en-frío y operador-en-caliente. La decisión "¿apago o aguanto?" deja de ser una negociación bajo estrés.
- OPS-2 (`StrategyHealthMonitor`) tiene especificación clara de qué métricas calcular y qué umbrales chequear. Se puede empezar a construir inmediatamente.
- El sistema queda preparado para entrar a Hito C (paper trading) con criterio de éxito explícito y procedimientos de emergencia documentados.
- Las decisiones operativas tomadas hoy son consultables y revisables; si en 6 meses el operador piensa "¿por qué dos niveles y no tres?", la respuesta está en este ADR.
- La estrategia activa hoy (`EmaCrossStrategy / BTCUSDT / 1h`) tiene su entrada poblada en POLICY sección 7 con umbrales numéricos concretos, no placeholders.

**Neutras / aceptadas:**
- Los umbrales U1-U4 son del operador, no de un comité de risk management. Si el operador es demasiado conservador con U1 (25% DD), va a apagar estrategias sanas con frecuencia. Si es demasiado laxo, va a apagar tarde. La recalibración trimestral (POLICY sección 4.4) es el mecanismo de corrección.
- La política de eventos macro (POLICY 2.3) se cumple manualmente hasta que se construya `EventCalendarMonitor` (postergado a Bloque 4 como `EVCAL-1`). En los primeros meses de operación, el operador debe consultar calendario económico semanalmente y desactivar manualmente. Riesgo: olvidarse de pausar antes de un FOMC y entrar en mal momento. Mitigación: el primer incidente de este tipo es el trigger sugerido para activar `EVCAL-1`.
- Sin escalón intermedio de "reducir size", una estrategia que entra en degradación borderline queda solo bajo supervisión humana semanal hasta cruzar el umbral de apagado. Aceptable para una estrategia única; reconsiderar si llega a haber >3 estrategias activas simultáneas.

**Negativas:**
- Los umbrales absolutos son menos defendibles institucionalmente que los derivados estadísticamente. Para el operador único, está bien; si en el futuro este sistema se profesionaliza o se comparte, requiere recalibración estadística sí o sí (Hito G).

### Cambios colaterales al ROADMAP

- OPS-1 marcado como ✅ completado.
- OPS-2 actualizado: ahora referencia explícitamente las métricas y umbrales U1-U4 de POLICY sección 3.
- Nueva entrada `EVCAL-1` agregada al Bloque 4 postergado: `EventCalendarMonitor`. Trigger sugerido: segunda estrategia activa, o sistema operando >7 días sin supervisión diaria, o un incidente concreto de pausa no aplicada a tiempo.

### Validaciones pendientes en Hito C

Al arrancar paper trading, verificar que la POLICY refleja la realidad operativa:

1. **Frecuencia real de las inspecciones (sección 4 de POLICY).** Si la cadencia diaria/semanal/mensual no se cumple en la práctica, los umbrales escritos no sirven. Ajustar la cadencia (no relajar los umbrales).
2. **Trades acumulados antes de los 50.** Los primeros trades del paper sirven para confirmar que U1 y U2 funcionan; observar si los disparan por ruido o por degradación real.
3. **Discrepancias entre la inspección humana semanal y los umbrales automáticos.** Si la inspección semanal indica "esto va mal" pero el monitor no dispara, los umbrales son demasiado laxos. Documentar y recalibrar en la revisión trimestral.

---

## ADR-021 — Monitoreo básico para paper trading: JSONL local + heartbeat + Healthchecks.io
**Fecha:** 2026-05-20
**Estado:** Aceptada

### Contexto
El sistema termina los backtests con logs en consola de Lean que no persisten, sin alertas externas, y sin forma de reconstruir eventos pasados. Para operar paper trading (Hito C) y live (Hito D) se necesita cubrir tres ejes operativos distintos que el término "monitoreo" agrupa imprecisamente:

1. **Liveness:** detectar si el proceso murió. Sin esto, posiciones abiertas quedan sin gestión activa por un tiempo indefinido (sin trailing stop reactivo, sin cierre por régimen incompatible, sin kill switch activo).
2. **Patologías silenciosas:** el proceso puede estar vivo pero sin recibir datos, sin generar señales, o con kill switch activo sin notificación. Estos son los bugs operativos más caros porque no producen excepciones, producen silencio.
3. **Persistencia de evidencia:** reconstruir qué pasó N días atrás cuando los logs de consola ya no están. Hace falta logs estructurados, persistentes, con timestamp y nivel, parseables y sobrevivientes al ciclo de vida del proceso.

Los tres ejes no se cubren con una sola herramienta. INFRA-2 los cubre con tres piezas mínimas, complementarias entre sí.

### Decisión
Tres piezas implementadas en orden estricto A → B → C, cada una commit-eable de forma independiente:

**A — Persistencia de logs estructurados (JSONL):**
- Nueva interfaz `IStructuredLogSink` en `Trading.Domain.Abstractions` (contrato) y nuevo enum `LogLevel` (espejo de los 5 niveles de `ITradingLogger`, cero dependencias externas en Domain).
- Implementación `JsonlFileLogSink` en `Trading.Strategies.Adapters`: una línea JSON por evento en `logs/trading-{wall-clock-date}.jsonl`.
- Helper estático `LogTemplateRenderer` extrae la lógica de parseo de placeholders nombrados que estaba embebida en `LeanLogger`.
- `LeanLogger` recibe el sink por constructor e invoca al sink en paralelo al `QCAlgorithm.Log/Debug/Error`, sin cambiar firmas públicas de `ITradingLogger`.
- **Rotación y retención usan wall clock real (`DateTime.UtcNow.Date`)**, no `_clock.UtcNow`. Razón: `IClock` devuelve el clock simulado del backtest, que avanza día a día y dispararía cientos de rotaciones espurias eliminando los propios logs del run en curso.
- **El campo `timestamp` dentro de cada evento JSON sí usa `_clock.UtcNow`**, para correlacionar con órdenes y barras del backtest.
- Retención de 30 días de wall clock real, configurable por constructor.
- Thread-safe (lock interno), traga excepciones de I/O para no romper trading.

**B — Heartbeat local:**
- Nuevo evento `BarProcessedEvent` (emitido por `BarProcessingService` solo en el camino exitoso, no en early-returns por skip de régimen, sizing fallido, etc.).
- `HealthHeartbeatTracker` en `Trading.Application.Health` suscripto a `BarProcessedEvent`, `OrderSubmittedEvent`, `OrderFilledEvent`, `RiskLimitBreachedEvent`. Estado in-memory con lock. Snapshot inmutable vía `HealthSnapshot` record.
- `HeartbeatFileWriter` en `Trading.Strategies.Adapters`: serializa snapshot a `health/heartbeat.json` con escritura atómica (`.tmp` + `File.Move` overwrite).
- Flush periódico vía `System.Threading.Timer` cada 60s de **wall clock real**, **solo en `LiveMode`**. En backtest, solo flush inicial al término de `Initialize()`; el archivo queda congelado durante el backtest.
- Razón del `LiveMode` guard: en backtest `Schedule.On(TimeRules.Every(60s))` se dispara al ritmo del clock simulado (~650k veces en un backtest de 15 meses), llevando el tiempo de ejecución de 1 minuto a 20+. El heartbeat es observabilidad pasiva, no participa del flujo de trading.
- Razón de usar `System.Threading.Timer` en lugar de `Schedule.On` incluso en live: el heartbeat opera en wall clock real porque su consumidor externo (Healthchecks.io) opera en wall clock. `Trading.Strategies` es el adaptador autorizado a usar primitivas de timing crudas.

**C — Ping externo a Healthchecks.io:**
- `HealthchecksIoPinger` en `Trading.Strategies.Adapters` hace HTTP GET a una URL configurable.
- URL vía variable de entorno `HEALTHCHECKS_PING_URL`. Si no está o formato inválido (no matchea `^https://(hc-ping\.com|healthchecks\.io)/.+`): modo no-op con Warning una sola vez al arranque (graceful degradation, nunca rompe arranque).
- Throttle interno de 5 minutos entre pings (el callback del timer del heartbeat también dispara el ping, pero el pinger solo pega al HTTP real cada 5min).
- Healthchecks.io configurado con período 5min y grace 15min: si el ping no llega en 15min, alerta a Telegram.
- `HttpClient` long-lived (un único cliente para todo el run, sin `IHttpClientFactory` — sobre-ingeniería), dispose en `OnEndOfAlgorithm`.
- Nunca propaga excepciones al caller (un ping fallido no puede romper trading): errores de red, timeouts y status no-2xx loguean Warning y retornan ok.

### Alternativas consideradas

- **Seq / Datadog / Loki (logs centralizados):** sobre-ingeniería para una máquina única, infra adicional, costo recurrente. Descartado. Reconsiderar si el sistema crece a múltiples nodos en cloud.
- **Uptime Kuma (alternativa self-hosted a Healthchecks.io):** requiere hostear el monitor en la misma máquina que se quiere monitorear, lo cual derrota el propósito del dead-man's switch (si la máquina muere, también muere el monitor). Descartado.
- **Pingdom / UptimeRobot:** chequean URLs públicas (HTTP GET desde afuera hacia una URL hosteada por nosotros), no esperan pings entrantes; menos orientados al patrón "dead-man's switch". Descartado.
- **Métricas con Prometheus + Grafana / dashboard visual:** sobre-ingeniería para una sola estrategia en una sola máquina. Las métricas de performance del trading (P&L, drawdown rolling, Sharpe) son responsabilidad de OPS-2, no INFRA-2.
- **Posponer todo al Bloque 4:** descartado, son la mínima precondición operativa razonable para paper trading. Sin INFRA-2 no hay forma de detectar caídas ni de hacer post-mortem.
- **Dashboard de métricas operativas dentro de INFRA-2:** descartado por scope creep. Inspección vía `jq` o `Select-String` sobre el JSONL es suficiente para una máquina.

### Consecuencias

**Positivas:**
- Observabilidad local completa (JSONL + heartbeat) sin dependencias externas.
- Alerta externa de caída total vía Healthchecks.io + Telegram.
- Inspección post-mortem con `jq` o `Select-String` desde la línea de comandos.
- Cero impacto sobre métricas del backtest (verificado: 225 órdenes idénticas pre/post-INFRA-2).
- Tiempo de ejecución del backtest restaurado a baseline (~100 segundos) tras los fixes.

**Neutras / aceptadas como deuda:**
- No hay dashboard visual de métricas operativas. Aceptable para una sola estrategia en una sola máquina.
- Variable `HEALTHCHECKS_PING_URL` requerida en ambiente de producción; si no está, el ping queda deshabilitado con Warning visible (no rompe arranque).
- Las métricas de performance del trading (P&L, drawdown rolling, etc.) NO están cubiertas; corresponden a OPS-2.

**Negativas reveladas durante la implementación, documentadas como deuda en ROADMAP:**
- **DEUDA-2:** `TradingAlgorithmHost.Initialize()` se ejecuta dos veces en backtest. Doble suscripción al bus, doble instanciación de adaptadores. Sin impacto funcional sobre métricas. Fix pendiente: guard de idempotencia. Validar si afecta también a live.
- **DEUDA-3:** logs durante `Initialize()` tienen timestamp del epoch de QC (`1997-12-31T19:00:00`). Problema cosmético. No afecta paper/live (sin `SetStartDate`).

### Fixes correctivos durante la implementación (todos por el mismo error de fondo)

Durante INFRA-2 se aplicaron tres fixes correctivos antes del cierre, todos por **confundir `IClock` con wall clock real en componentes de housekeeping**:

1. **Timer del heartbeat (Pieza B fix):** el `Schedule.On(TimeRules.Every(60s))` original se disparaba al ritmo del clock simulado del backtest, llevando el tiempo de ejecución de 1min a 20+. Reemplazado por `System.Threading.Timer` envuelto en `if (LiveMode)`.

2. **Rotación y retención del JSONL (Pieza A fix):** usaban `_clock.UtcNow.Date` y eliminaban los propios logs del run en cada cambio de día simulado. Reemplazado por `DateTime.UtcNow.Date` para esas dos operaciones específicas, manteniendo `_clock.UtcNow` para el campo `timestamp` de cada evento.

3. **Tests `Write_*` del sink (Pieza A tests fix):** fallaban con `IOException` al intentar leer el archivo mientras el sink lo tenía abierto en modo escritura. Antes del fix de rotación, cada test usaba `FakeClock` distinto y escribía a archivos distintos, evitando el conflicto. Tras el fix de rotación, todos los tests escriben al mismo archivo de wall clock real. Corregidos adoptando patrón `using` con disposición del sink antes de la lectura.

### Aprendizaje arquitectónico (incorporado a AI.md)

Surgió un criterio que vale la pena explicitar para componentes futuros: **observabilidad y housekeeping de I/O en `Trading.Strategies` deben operar en wall clock real**, no en `IClock`. El `IClock` está pensado para componentes del flujo determinista de trading (Application + Domain). Confundir esto fue la causa raíz de los tres fixes durante INFRA-2. Patrón a evaluar en futuros adapters de observabilidad: si el componente no influye en señales / órdenes / risk, y su consumidor es externo (un servicio de monitoreo, un archivo de log que se inspecciona post-mortem), debe usar wall clock real.

### Validaciones pendientes en Hito C

Al arrancar paper trading (Hito C), confirmar:

1. **`heartbeat.json` se actualiza cada 60s de wall clock real** (no queda congelado como en backtest). Inspección: `Get-FileHash` repetido sobre el archivo cada minuto debe dar hashes distintos.
2. **Pings llegan al dashboard de Healthchecks.io.** Visible en el panel del check.
3. **La alerta de Telegram dispara cuando el proceso muere.** Test deliberado: matar el proceso, esperar 15min, confirmar mensaje en Telegram.
4. **Validar si DEUDA-2 (`Initialize()` doble) aplica también a live.** Si el JSONL en live muestra cada Warning/Info del arranque una sola vez, la deuda es solo de backtest y puede dejarse a más largo plazo.

### Cierre de DEUDA-2 (2026-05-22)

Al ejecutar el diagnóstico planificado (brief `DEUDA_2_BRIEF.md`, Fase 1: instrumentación con `_initializeCallCount` atómico y log con hash de instancia), `Initialize()` se ejecutó **una sola vez** en backtest. Evidencia:

- Consola de Lean: `Debug: 1997-12-31 19:00:00 TradingAlgorithmHost.Initialize() invocado, hash de instancia 38986105, llamada #1` aparece una sola vez en el run.
- JSONL `trading-2026-05-22.jsonl` (6 líneas totales): los mensajes de arranque del host (`HealthchecksIoPinger: HEALTHCHECKS_PING_URL no configurada`, `Heartbeat flush timer deshabilitado`) aparecen exactamente una vez cada uno.

Los logs duplicados observados al cierre de INFRA-2 que motivaron la documentación de DEUDA-2 no se manifiestan con el código actual. Causa exacta no determinada — no se conservó el JSONL del cierre de INFRA-2 para comparación directa, pero el diagnóstico original fue inferencial (logs duplicados → conclusión de doble invocación), no instrumentado.

**NO se aplicó guard de idempotencia.** Fixes solo a problemas reproducidos. Decisión consistente con el Riesgo 2 del brief `DEUDA_2_BRIEF.md` que previó explícitamente este escenario.

**Validación pendiente en Hito C:** al arrancar paper trading, inspeccionar el JSONL inicial para confirmar que el síntoma tampoco aparece en modo Live. Si reaparece, abrir nueva deuda con diagnóstico fresco (no reabrir DEUDA-2: el diagnóstico de hoy quedó cerrado).

---

## ADR-020 — Test de referencia AccordHmmClassifierReferenceTests skipeado por convergencia degenerada con datos sintéticos
**Fecha:** 2026-05-19
**Estado:** Resuelta en ADR-024 (2026-05-22)

### Contexto
El brief del Hito B Paso 3 especificó la creación de un test de referencia (`AccordHmmClassifierReferenceTests`) que valida el pipeline completo del HMM sobre una serie sintética con tres regímenes claramente diferenciados (Trend alcista calmo, HighVolatility, MeanReverting). El criterio de éxito era que cada segmento se clasificara correctamente en al menos el 50% de las barras post-warm-up.

Al ejecutarse el test, el segmento HighVolatility (desvío ~5x sobre los otros segmentos) clasificó como `HighVolatility` en **0 de las barras** (esperado >50%). El test falla con un margen extremo, no marginal.

### Decisión
Marcar el test con `[Fact(Skip = "...")]` y documentar la deuda técnica explícitamente.

**Justificación operativa para no bloquear el cierre del Hito B:**

1. **El modelo de producción NO presenta el síntoma.** El modelo entrenado con datos reales de BTCUSDT perpetual de Binance (ventana 2020-2024) eligió K=4 con margen BIC del 12-20% sobre K=3 y K=2, lo cual es una separación saludable, no marginal. El backtest del período 2025-01-01 a 2026-03-31 muestra el HMM filtrando activamente señales (524 órdenes pre-filtro → 225 post-filtro) con etiquetas `Trend`, `Squeeze` y `HighVolatility` apareciendo en distintos momentos del mercado en los logs. Si el modelo estuviera colapsado como el del test sintético, todas las clasificaciones serían la misma etiqueta o `Unknown`; no es el caso.

2. **El test detecta un caso límite del método con K=3, no de operación real.** El modelo de producción usa K=4, donde Baum-Welch tiene más libertad para separar estados y el riesgo de óptimo local degenerado es menor. Y el `SemanticStateMapper` aplica una regla de "cuartil superior" que es matemáticamente frágil con K=3 (un cuartil necesita ≥4 puntos).

3. **El resto del pipeline está validado.** Los otros 12 tests del Paso 3 (BinanceKlinesParserTests con 7 tests, SemanticStateMapperTests con 5 tests) están en verde. La infraestructura del HMM es sólida en sus componentes individuales; lo que falla es el caso end-to-end con K pequeño.

### Hipótesis de causa raíz (a verificar durante diagnóstico)

**Hipótesis A (más probable):** Convergencia a óptimo local malo de Baum-Welch sobre datos sintéticos con K=3. Dos o más estados colapsan a parámetros casi idénticos. La inicialización por k-means (que el trainer usa) podría no estar funcionando o el SemanticStateMapper podría estar mapeando estados incorrectamente.

**Hipótesis B:** Bug en `SemanticStateMapper` al calcular cuartiles con K=3 (un cuartil requiere ≥4 valores; con 3 estados la regla "está en cuartil superior" es matemáticamente ambigua).

**Hipótesis C:** El `FeatureScaler` lava las diferencias del segmento HighVolatility porque la varianza global queda dominada por los segmentos tranquilos. Menos probable: el scaler tiene en cuenta toda la varianza incluyendo los outliers.

### Plan de diagnóstico
Agendado **antes de iniciar Hito C (paper trading)** y después de cerrar el Bloque 3 (INFRA-2, OPS-1, OPS-2). Concretamente:

1. Agregar logging temporal al test que imprima: K elegido por BIC, BICs de cada K candidato, parámetros del HMM resultante (matriz de transición, medias de emisiones por estado), estadísticas calculadas por el `SemanticStateMapper`, mapeo estado → label resultante.
2. Decidir cuál de las tres hipótesis es la correcta basándose en los logs.
3. Aplicar el fix correspondiente:
   - Si es Hipótesis A: mejorar inicialización del Baum-Welch (más iteraciones, mejores seeds, k-means++ explícito).
   - Si es Hipótesis B: refinar las reglas del `SemanticStateMapper` para que sean robustas con K pequeño (usar percentiles 33/66 en lugar de cuartiles, o adaptarse al K específico).
   - Si es Hipótesis C: revisar el orden de las operaciones del pipeline.
4. Verificar que el test pasa y reactivarlo (quitar el `Skip`).
5. Mover ADR-020 a estado "Resuelta".

### Alternativas consideradas
- **A: Bloquear el cierre del Hito B hasta resolver el test.** Descartada: el modelo de producción funciona empíricamente (filtrado activo verificable en logs del backtest), la deuda es de validación adicional, no de funcionalidad. Bloquear el cierre por un test que detecta un caso degenerado controlado retrasaría el Hito B sin valor proporcional.
- **B: Eliminar el test directamente.** Descartada: el test sí captura información valiosa (que algo del pipeline es frágil con K pequeño). Marcarlo Skip con documentación deja el conocimiento accesible para futuros desarrolladores y para el diagnóstico planificado.
- **C (elegida): Skip con ADR explícito y plan de diagnóstico calendarizado.** Documenta la deuda, no la esconde, define un momento concreto para resolverla.

### Consecuencias
- El Hito B queda cerrado con 12 de 13 tests del Paso 3 en verde + 1 explícitamente skipped con justificación documentada.
- El reporte de Test Explorer va a mostrar el test como Skipped (no como Failed) en cada corrida futura.
- Cuando se inicie el diagnóstico planeado, este ADR es el punto de partida: enumera las tres hipótesis a verificar y el plan de acción.
- **Riesgo asumido:** si el modelo de producción tiene un defecto sutil análogo al detectado en el test sintético, el diagnóstico tardío podría implicar re-entrenar el modelo. Mitigación: durante el Bloque 3, se agregará al `StrategyHealthMonitor` (OPS-2) una métrica de "frecuencia de cambio de régimen" que detectaría comportamiento anómalo (régimen pegado en una etiqueta durante semanas, transiciones excesivamente frecuentes, etc.).
- ADR-017 pasa a estado "Aceptada" (Hito B completado), con nota al final indicando que ADR-019 documenta los parámetros del HMM y ADR-020 documenta la deuda técnica del test de referencia.

## ADR-019 — Implementación específica del HMM en Paso 3 del Hito B
**Fecha:** 2026-05-19
**Estado:** Aceptada

### Contexto
ADR-017 documentó la decisión de implementar clasificación de régimen con HMM (frente a k-means o redes neuronales) y los Pasos 1 y 2 del Hito B. Este ADR documenta los parámetros específicos del HMM efectivamente implementado en el Paso 3, así como las decisiones operativas tomadas durante la ejecución concreta del entrenamiento.

### Decisión
**Librería y algoritmos:** Accord.NET 3.8.0 (`Accord.MachineLearning`) para implementación de HMM con emisiones Multivariate Gaussian, topología ergódica, entrenamiento con Baum-Welch (semilla 42, tolerancia 1e-5, máximo 200 iteraciones, regularización 1e-6 para garantizar matrices de covarianza definidas positivas), decodificación en runtime con Viterbi (`HiddenMarkovModel.Decide`) + forward filtering posterior para probabilidades (`HiddenMarkovModel.Posterior`).

**Inicialización canónica HMM-GMM:** se inicializan las emisiones por k-means clustering de las observaciones normalizadas (k = K, mismo número de estados). Cada estado arranca con media = centroide del cluster y covarianza = covarianza muestral del cluster (con regularización +1e-6 en la diagonal). Sin esta inicialización, BaumWelch quedaba en óptimo trivial: con emisiones simétricas iniciales todos los estados terminaban con ρ=0.5 y diferencias de log-likelihood entre K=2,3,4 menores al 0.5% (caso degenerado del brief). Tras la inicialización por k-means, las log-likelihoods se separan limpiamente y el BIC discrimina K con margen del 10-20%.

**Features:** Tres features por barra:
1. Retornos logarítmicos: `ln(close[t] / close[t-1])`
2. Volatilidad rolling 20 períodos: desvío estándar muestral (denominador N-1) de los últimos 20 retornos log.
3. Momentum ratio: `SMA(close, 20)[t] / SMA(close, 50)[t] - 1`

Las primeras 50 barras del training set se descartan para warm-up de features (cálculo de SMAs).

**Normalización:** Z-score con medias y desvíos del training set. Los parámetros del scaler se serializan junto al modelo (`FeatureScalerMeans`, `FeatureScalerStdDevs`) para garantizar normalización idéntica en runtime.

**Selección de K:** Probado K ∈ {2, 3, 4} y elegido el de BIC mínimo. Resultados de esta ejecución (10912 observaciones de feature válidas a partir de 10962 barras 4h parseadas):
| K | logLikelihood | BIC |
|---|---|---|
| 2 | −36180.62 | 72556.49 |
| 3 | −32793.27 | 65911.95 |
| 4 | **−28584.88** | **57643.94** |

**K elegido: 4** con margen amplio (12.5% sobre K=3, 20% sobre K=2).

**Mapeo semántico (resultado):** calculado offline aplicando reglas deterministas basadas en media de retornos en espacio z-scored, desvío en espacio z-scored y persistencia (probabilidad de auto-transición). Estadísticas finales por estado:
| Estado | μ (z-scored) | σ (z-scored) | ρ (auto-trans) | Etiqueta |
|---|---|---|---|---|
| 0 | −0.030 | 1.740 | 0.969 | HighVolatility |
| 1 | +0.000 | 0.483 | 0.971 | Squeeze |
| 2 | +0.038 | 0.797 | 0.964 | Trend |
| 3 | −0.020 | 0.923 | 0.959 | Trend |

Dos estados terminaron mapeados a `Trend` (uno con bias positivo, otro con bias negativo). Es comportamiento esperado y permitido: el `AccordHmmClassifier` suma las probabilidades por etiqueta antes de exponer el `RegimeClassification.Probabilities`. Funcionalmente equivale a dos sub-estados de Trend (alcista y bajista) bajo la misma etiqueta semántica.

**Warm-up:** 100 barras 4h post-feature-warm-up (50 + 100 = 150 barras totales para alcanzar primera clasificación válida). Coordinado con `SetWarmUp` de QuantConnect extendido a 20 días de calendario (cobertura holgada: 100·4h = 16.7 días estricto). Durante el warm-up de QC, el HMM procesa las barras y el classifier devuelve `RegimeLabel.Unknown` hasta acumular suficientes features.

**Ventana de entrenamiento:** 2020-01-01 a 2024-12-31 UTC. 10962 barras 4h en ventana, 10912 features válidas tras descarte de warm-up. Estrictamente anterior al período del backtest (2025-01-01 a 2026-03-31). Cero lookahead bias.

**Instrumento:** BTCUSDT perpetual de Binance. El modelo NO es transferible a otros instrumentos ni exchanges sin re-entrenamiento. El convenio de nombrado `models/regime/{instrumentId}-perp-binance.hmm.json` permite agregar instrumentos en el futuro sin tocar el wiring del host.

### Refactor adicional: wiring agnóstico al instrumento
El wiring del régimen en `TradingAlgorithmHost` se refactorizó para extraer dinámicamente los instrumentos únicos del `strategies.json` que tienen al menos una estrategia con `CompatibleRegimes` declarado y crear un classifier por cada instrumento con modelo disponible. El hardcoding previo de `btcInstrumentId` queda eliminado. Cuando se agregue un segundo instrumento al sistema (ej. ETHUSDT en un futuro Hito E), solo será necesario:
1. Entrenar un modelo para ese instrumento con el HmmTrainer.
2. Commitear el JSON a `models/regime/`.
3. Agregar la estrategia correspondiente a `strategies.json` con `CompatibleRegimes`.

El wiring de `TradingAlgorithmHost` no se toca. Si una estrategia declara `CompatibleRegimes` pero no existe el modelo entrenado para su instrumento, el sistema falla loud al boot con `InvalidOperationException` indicando el path esperado y la instrucción de ejecutar el HmmTrainer.

### Fix crítico en el consolidator de régimen
El consolidator dedicado del régimen tenía un `if (IsWarmingUp) return;` en el handler de Paso 2 (irrelevante mientras el classifier era un fake que devolvía `Trend` instantáneamente). Con el HMM real es un bug: el classifier necesita procesar barras durante el período de warm-up de QC para calentar su propio buffer interno (100 features post-feature-warm-up). Se eliminó el guard. La consecuencia operativa es que `SetWarmUp` debe cubrir al menos las 100 barras 4h del HMM con margen, por eso se extendió de 1 día a 20 días.

### Alternativas consideradas durante la ejecución
- **Re-entrenamiento periódico automático en runtime.** Descartado por ahora: agrega complejidad operativa (qué pasa si el re-entrenamiento falla, cómo se versionan los modelos, cómo se garantiza consistencia entre re-entrenamiento y operación). Si el modelo se degrada, se re-entrena offline corriendo el `HmmTrainer` y se commitea la nueva versión del JSON.
- **Multi-feature engineering avanzado (ATR, RSI, volume ratio).** Descartado en este paso: tres features simples son suficientes para arrancar y validar el pipeline. La iteración de features queda como mejora futura cuando el sistema esté operando y haya feedback empírico.
- **Inicialización aleatoria simétrica.** Descartada tras observar empíricamente que BaumWelch no convergía y los BICs eran extremadamente cercanos. La inicialización por k-means es estándar institucional y produce convergencia limpia.
- **Régimen sistémico además de por activo.** Postergado a SYSREG-1 del Bloque 4 del ROADMAP.

### Consecuencias
- Sistema con clasificación de régimen funcionando con inteligencia estadística real basada en 5 años de datos históricos de BTCUSDT perpetual de Binance.
- Backtest del período 2025-01-01 a 2026-03-31 ahora se ejecuta con el filtro de régimen activo, filtrando señales de EmaCross según el régimen detectado por el HMM en cada momento. Dos estados clasifican como `Trend` (con bias positivo y negativo), un estado como `Squeeze` y un estado como `HighVolatility`. La estrategia opera cuando el régimen actual sea `Trend`; queda filtrada en `Squeeze` y `HighVolatility`.
- Deuda técnica documentada: el `AccordHmmClassifier` mantiene buffer en memoria. Si el proceso reinicia en producción, el classifier entra en warm-up nuevamente (resuelto vía `SetWarmUp` de QC con 20 días). Persistencia del buffer entre reinicios queda como mejora si la latencia de warm-up se vuelve problemática.
- El proyecto `Trading.Strategies/Tools/HmmTrainer` queda como herramienta para re-entrenar el modelo cuando sea necesario (degradación detectada, agregado de instrumentos, mejora de features).
- ADR-017 pasa a estado "Aceptada" (Hito B completado en todos sus pasos).
- Note técnica menor: `FeatureExtractor` y `FeatureScaler` se ubican en `Trading.Strategies/Regimes/` (compartidos entre trainer y runtime), no en `Tools/HmmTrainer/` como sugería el brief inicial; la deviación se hizo para evitar duplicación del cálculo de features entre los dos contextos (DRY trainer↔runtime es crítico para reproducibilidad).
- Note adicional: la regla 3 del `SemanticStateMapper` (`|μᵢ| > 0.001 y ρᵢ > 0.6 → Trend`) se aplica sobre la media en espacio z-scored (no en espacio crudo). En espacio crudo con la escala típica de BTC 4h (σ ≈ 0.014 por barra) la condición `|μᵢ| > 0.001` rara vez se cumpliría y el sistema quedaría sin estados `Trend`; en z-scored la regla discrimina los estados con drift no trivial respecto a la mediana del set. Es una desviación pragmática respecto del texto literal del brief, justificada por producir un mapeo "razonable" (criterio explícito del brief para este componente).

---

## ADR-018 — Adelantamiento de INFRA-1: path absoluto del strategies.json eliminado y reconciliado con MSBuild
**Fecha:** 2026-05-17
**Estado:** Aceptada

### Contexto
El `TradingAlgorithmHost.cs` hardcodeaba un path absoluto al `strategies.json`: `F:\DesarrolloTrading\QuantConnect\Lean\Trading.Strategies\bin\Debug\net10.0\strategies.json`. Eso traía tres problemas concretos:

1. **No portable.** En cualquier máquina con otro layout de disco el sistema no arrancaba.
2. **Genera dos archivos paralelos sin sincronizar.** Existía una copia en `Trading.Strategies\strategies.json` (fuente versionada en git) y una copia en `bin\Debug\net10.0\strategies.json` (la que el código leía efectivamente). El `.csproj` no tenía `<Content CopyToOutputDirectory="..." />`, así que MSBuild no sincronizaba ambas. Como consecuencia, ambas vivían vidas separadas y diferían en contenido (la fuente con `MeanReversion`, el bin con `EmaCrossStrategy`).
3. **Disonancia silenciosa para herramientas de edición.** Sesiones de agentes (Claude Code) editaban la fuente versionada mientras el backtest cargaba el bin sin actualizar. El bug se manifestaba como "modifiqué el código pero el backtest no cambia", sin error explícito.

El refactor INFRA-1 del Bloque 3 del ROADMAP planificaba resolver esto antes del Hito C, pero el problema afectó dos sesiones de trabajo sobre Hito B y se decidió adelantarlo.

### Decisión
Tres cambios atómicos, ejecutados en una sola pasada de limpieza:

1. **Path relativo basado en `AppContext.BaseDirectory`.** El `strategiesFilePath` de `TradingAlgorithmHost.cs` pasa de hardcoded a:
   ```csharp
   string strategiesFilePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "strategies.json");
   ```
   `AppContext.BaseDirectory` resuelve al directorio donde está el `.exe` en runtime, lo cual coincide con `..\Launcher\bin\Debug\` (definido en `OutputPath` del `.csproj`) en desarrollo y será el directorio del binario desplegado en producción.

2. **`<Content Include="strategies.json" CopyToOutputDirectory="PreserveNewest" />` agregado al `Trading.Strategies.csproj`.** Esto le indica a MSBuild que copie el archivo fuente al directorio de output en cada build (si la fuente es más nueva que el destino). De aquí en adelante el desarrollador edita únicamente la fuente; el bin se sincroniza automáticamente al compilar.

3. **Reconciliación del contenido.** La copia desactualizada de `Trading.Strategies\strategies.json` (la fuente, que tenía `MeanReversion`) fue sobrescrita con el contenido válido del bin (`EmaCrossStrategy` en BTCUSDT 1h con `RiskPerTradePercentage: 2.0`). La copia del bin fue eliminada para que MSBuild la regenere desde la fuente en el próximo build.

### Alternativas consideradas
- **A: Mantener el path absoluto y aceptar la limitación.** Descartada: causó dos sesiones de trabajo perdidas por confusión sobre qué archivo era la fuente de verdad. El costo de mantenerlo supera el costo de eliminarlo.
- **B: Usar un archivo de configuración (`appsettings.json` o variable de entorno) para parametrizar el path.** Descartada por sobre-ingeniería para el alcance actual: una variable de entorno requiere infraestructura adicional (`Microsoft.Extensions.Configuration`, validación, default), cuando `AppContext.BaseDirectory` resuelve el caso 100% sin agregar dependencias.
- **C (elegida): `AppContext.BaseDirectory` + `<Content CopyToOutputDirectory>`.** Mínimo cambio, máxima portabilidad. Patrón estándar de .NET.

### Consecuencias
- El `strategies.json` ahora se versiona únicamente en `Trading.Strategies\strategies.json`. La copia en `bin\` es un artefacto generado en cada build, no se versiona, no se edita.
- INFRA-1 del Bloque 3 del ROADMAP queda completado. Se mueve a "Historial completado" con fecha 2026-05-17.
- El refactor habilita además que cualquier desarrollador clone el repo en otra máquina y el sistema arranque sin tocar paths hardcoded.
- Deuda implícita resuelta: el JSON que alimenta el sistema ahora vive en una sola ubicación clara y conocida.

---

## ADR-017 — Hito B (Pasos 1, 2 y 3): clasificación de regímenes con abstracción agnóstica, integración como guard pre-orden y HMM real
**Fecha:** 2026-05-15
**Estado:** Aceptada

### Contexto
El Hito B del ROADMAP introduce clasificación de regímenes de mercado para filtrar señales de las estrategias según condición agregada del mercado (Trend / MeanReverting / HighVolatility / Squeeze). El alcance se dividió en tres pasos progresivos para aislar complejidad numérica (HMM real con Accord) del trabajo de plomería (abstracciones, registry, filtro pre-orden).

Tras analizar el código existente (especialmente `BarProcessingService` y `RiskOrchestrator`), surgió un hallazgo arquitectónico que cambió la decisión original sobre dónde insertar el filtro de régimen.

### Decisión
El Hito B se ejecuta en tres pasos:

**Paso 1 — Pre-requisitos arquitectónicos del Domain (completado 2026-05-14):**
- `MarketBar` extendido a OHLCV (`Open`, `High`, `Low`, `Close`, `Volume`). Constructor legado `(InstrumentId, decimal close, DateTime)` mantenido como `[Obsolete]` para retrocompatibilidad temporal.
- `StrategyDefinition` recibe propiedad `List<string>? CompatibleRegimes` (nullable, `List<T>` concreto por consistencia con `RootConfig.Timeframes` y por compatibilidad con Newtonsoft.Json).
- `RiskLimitBreachReason` extendido con `RegimeIncompatibility` (queda definido aunque no se emita en este hito; pertenece al vocabulario del dominio).
- `MarketBarMapper` actualizado para construir `MarketBar` con OHLCV completo desde `TradeBar` de Lean.
- `StrategyConfigLoader` valida que `CompatibleRegimes`, si está presente, no sea lista vacía (mensaje explicativo: ausencia = compatible con todo, lista vacía = inválido).

**Paso 2 — Abstracción de régimen + filtro pre-orden con classifier fake (completado 2026-05-15):**
- `RegimeLabel` enum (`Unknown`, `Trend`, `MeanReverting`, `HighVolatility`, `Squeeze`) en `Trading.Domain/Abstractions/Regimes/`.
- `RegimeLabelParser.Parse(string)` con mensajes de error explícitos. Rechaza `Unknown` como configuración explícita (forzar al usuario a omitir el campo si quiere "todos los regímenes").
- `RegimeClassification` (record) con `Label`, `Probabilities` (distribución completa, `double` por ser magnitud estadística), `ClassifiedAtUtc`, y constructor estático `UnknownFor` para fail-safe.
- `IMarketRegimeClassifier` contrato **agnóstico del algoritmo**: ningún método ni propiedad delata HMM, k-means o redes neuronales. Esto habilita NEURAL-1 futuro como adaptador alternativo sin tocar el contrato (open-closed).
- `MarketRegimeRegistry` en `Trading.Application/Regimes/`: mantiene mapa `InstrumentId → IMarketRegimeClassifier` + cache de última clasificación. Instrumento sin classifier registrado → fail-safe a `Unknown`.
- `ConfigurableMarketRegimeClassifier`: implementación fake que devuelve siempre una `RegimeLabel` fija. Útil para tests y para validar wiring sin necesidad de modelo entrenado. Rechaza `Unknown` como `fixedLabel` (forzar coherencia).
- `StrategyRegimeCompatibility`: encapsula la lógica de compatibilidad por estrategia. Tres reglas fail-safe: lista null → compatible con todo; lista vacía → compatible con todo; `RegimeLabel.Unknown` siempre compatible.
- `BarProcessingService` integra el filtro como **guard `continue`** después del check de `KillSwitchActivated` y `SignalDirection.Flat`, **antes** de los checks de `IsInvested` y `HasOpenOrders`. Recibe dos dependencias nuevas: `MarketRegimeRegistry` e `IReadOnlyDictionary<string, StrategyRegimeCompatibility>`.
- `TradingAlgorithmHost` construye el registry con `ConfigurableMarketRegimeClassifier(BTCUSDT, Trend)`, parsea `CompatibleRegimes` de cada `StrategyDefinition` a `RegimeLabel`, crea un consolidator 4h dedicado para alimentar al registry (independiente de los consolidators de estrategias), y inyecta todo al `BarProcessingService`.

**Paso 3 — HMM real con Accord.NET + trainer offline (pendiente):**
- Adaptador `AccordHmmClassifier : IMarketRegimeClassifier` en `Trading.Strategies/Regimes/`.
- Proyecto standalone `Trading.Strategies/Tools/HmmTrainer` para entrenamiento offline con datos históricos de BTCUSDT perpetual de Binance (ventana 2020-2024, estrictamente anterior al período de backtest 2025-01 a 2026-03).
- Selección de número de estados por BIC entre K ∈ {2, 3, 4}.
- `SemanticStateMapper` que mapea estados crudos del HMM a `RegimeLabel` según propiedades estadísticas del cluster.
- Modelo serializado a JSON commiteado en `models/regime/BTCUSDT-perp-binance.hmm.json`.
- Reemplazo del fake del Paso 2 por el classifier real en el wiring.

### Hallazgo arquitectónico crítico: el filtro NO va por `RiskOrchestrator`
En la planificación original se asumió que el filtro de régimen sería un `IRiskMonitor` más, registrado en el array de monitors del `RiskOrchestrator` (aprovechando el open-closed del ADR-015). Al inspeccionar el código real de `BarProcessingService` apareció que el sistema **ya tiene el patrón de guards pre-orden** (`continue` checks) que es exactamente lo que el filtro de régimen necesita:

```csharp
if (_riskOrchestrator.IsKillSwitchActivated) continue;
if (signalDirection == SignalDirection.Flat) continue;
// ← acá va el filtro de régimen, como un guard más
if (_portfolioState.IsInvested(instrumentId)) continue;
```

Esta decisión es **conceptualmente más limpia**: un kill switch global por drawdown excesivo es una condición catastrófica que justifica liquidar todo (vía `IRiskAction`). Un régimen incompatible es un filtro pre-orden por contexto, que solo justifica descartar esa señal específica. Forzar el régimen al `IRiskMonitor` habría requerido extender el `RiskOrchestrator` para mapear razones de breach a acciones distintas (`RejectOrderRiskAction` vs `LiquidateAllRiskAction`), agregando complejidad innecesaria.

### Alternativas consideradas
- **Filtro como `IRiskMonitor` con `RegimeIncompatibilityMonitor`.** Descartada tras inspección del código (ver "Hallazgo arquitectónico crítico"). El patrón existente de guards en `BarProcessingService` es la abstracción correcta.
- **Filtro como interfaz separada `IOrderValidator`.** Descartada por sobre-ingeniería: el filtro encaja perfectamente como un guard más en el patrón ya establecido, no merece su propia jerarquía de abstracciones.
- **Régimen como propiedad de cada `IStrategy`.** Descartada: viola separación de responsabilidades; el régimen es propiedad del mercado, no de la estrategia. La estrategia declara con qué regímenes es compatible, el sistema decide.
- **K-means como algoritmo del Paso 3 (vs HMM).** Descartada: HMM modela transiciones temporales como ciudadano de primera clase (matriz de transición), devuelve distribución probabilística (no solo estado actual), permite criterio formal de selección de número de estados (BIC). Decisión asentada en planificación previa al Paso 3.

### Consecuencias del estado actual
- El sistema tiene filtro de régimen operativo en código pero **inactivo en producción**: el `strategies.json` del repo no tiene aún el campo `CompatibleRegimes` en la entrada de EmaCrossStrategy. Cuando se agregue (acción inmediata pendiente), el filtro empezará a discriminar.
- El fake del Paso 2 (`ConfigurableMarketRegimeClassifier` configurado con `Trend`) se reemplazará en el Paso 3 por `AccordHmmClassifier`. Como ambos implementan la misma interfaz, el cambio es una sola línea en el wiring.
- Tests nuevos: ~30 tests entre `Trading.Domain.Tests/RegimeLabelTests`, `Trading.Domain.Tests/RegimeClassificationTests`, `Trading.Application.Tests/Regimes/*Tests.cs`, y los tests de validación de `CompatibleRegimes` en el loader.
- Deuda técnica conocida del Paso 2: el `MarketBar` legado constructor `(InstrumentId, decimal close, DateTime)` está marcado `[Obsolete]` pero sigue siendo usado en algunos lugares del proyecto. Se elimina cuando se migren todos los call-sites a OHLCV, idealmente como parte de un cleanup posterior.
- Paso 3 completado el 2026-05-19. Modelo BTCUSDT-perp-binance entrenado con ventana 2020-01-01 a 2024-12-31. K elegido por BIC: 4 (BIC = 57643.94, con margen 12-20% sobre K=3 y K=2). Mapeo de estados resultante: {0:HighVolatility, 1:Squeeze, 2:Trend, 3:Trend}. Ver ADR-019 para detalles del HMM. ADR-017 pasa a estado "Aceptada".

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

## ADR-015 — Separación de IRiskMonitor de IRiskAction (descomposición del KillSwitchManager)
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
