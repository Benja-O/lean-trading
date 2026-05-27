# Brief 3 — Configuración multi-símbolo en `strategies.json` y backtest paralelo

## Contexto

Brief 2 cerrado: modelos HMM de ETHUSDT y TRBUSDT entrenados con
multi-seed, promovidos a `models/regime/`, ambos con K=4 y mapping
saludable (sin estados degenerados, sin labels agregados >85%).

Modelos en producción al inicio de este brief:

- `models/regime/BTCUSDT-perp-binance.hmm.json` (K=4, BIC 57643)
- `models/regime/ETHUSDT-perp-binance.hmm.json` (K=4, BIC 56707, Trend 52% / Squeeze 31% / HighVol 17%)
- `models/regime/TRBUSDT-perp-binance.hmm.json` (K=4, BIC 49814, Trend 47% / Squeeze 40% / HighVol 13%)

El runtime ya es agnóstico al símbolo:
`TradingAlgorithmHost.ExtractInstrumentsRequiringRegime` extrae
dinámicamente del JSON los símbolos con `CompatibleRegimes` y crea un
`MarketRegimeRegistry` con consolidator 4h por símbolo. **No hay
trabajo de wiring del runtime.**

Este brief activa los tres símbolos en `strategies.json`, corre un
backtest paralelo único, y valida que el subsistema de
ejecución/monitoreo sigue agnóstico al símbolo y al timeframe en
operación concurrente real.

## Objetivo

Validar que tres `StrategyExecutor` distintos, operando símbolos
distintos en el mismo TF simultáneamente, cumplen los criterios de
integridad del subsistema validados sobre BTCUSDT en ADR-026:

- Cero `OPS-2 invariante violado` en cualquiera de los tres.
- Cero `OrderEventMapper: evento sin tag` durante TimeExit/Liquidate.
- U1/U2 disparan solo con DD coherente, **por executor independiente**.
- Cada executor opera aislado: degradación de uno no afecta a los otros.
- Tres `ExecutorIdentifier` únicos creados, etiquetados, y separables en
  logs.

**Lo que NO se valida acá:** que la estrategia sea rentable en ETH o
TRB, que los regímenes sean comparables cross-símbolo, que el sizing
multi-estrategia sea coherente (allocator pendiente, deuda conocida).

## Cambio 1 — `strategies.json`

Reemplazar el contenido actual por la configuración multi-símbolo. Las
tres entradas van bajo el TF `1h` (decisión arquitectónica del operador:
mismo TF para aislar símbolo como variable).

```json
{
  "Timeframes": {
    "1m":  { "Strategies": [] },
    "5m":  { "Strategies": [] },
    "15m": { "Strategies": [] },
    "30m": { "Strategies": [] },
    "1h": {
      "Strategies": [
        {
          "StrategyName": "EmaCrossStrategy",
          "FileModelName": "",
          "Symbol": "BTCUSDT",
          "CompatibleRegimes": [ "Trend" ],
          "StopTakeMode": "Percentage",
          "StopLossPercentage": 1.0,
          "TakeProfitPercentage": 2.0,
          "StopLossAtrMultiplier": 0,
          "TakeProfitAtrMultiplier": 0,
          "RiskPerTradePercentage": 2.0,
          "MaxBars": 20,
          "CombineWithTimeExit": true
        },
        {
          "StrategyName": "EmaCrossStrategy",
          "FileModelName": "",
          "Symbol": "ETHUSDT",
          "CompatibleRegimes": [ "Trend" ],
          "StopTakeMode": "Percentage",
          "StopLossPercentage": 1.2,
          "TakeProfitPercentage": 2.4,
          "StopLossAtrMultiplier": 0,
          "TakeProfitAtrMultiplier": 0,
          "RiskPerTradePercentage": 2.0,
          "MaxBars": 20,
          "CombineWithTimeExit": true
        },
        {
          "StrategyName": "EmaCrossStrategy",
          "FileModelName": "",
          "Symbol": "TRBUSDT",
          "CompatibleRegimes": [ "Trend" ],
          "StopTakeMode": "Percentage",
          "StopLossPercentage": 2.0,
          "TakeProfitPercentage": 4.0,
          "StopLossAtrMultiplier": 0,
          "TakeProfitAtrMultiplier": 0,
          "RiskPerTradePercentage": 2.0,
          "MaxBars": 20,
          "CombineWithTimeExit": true
        }
      ]
    },
    "4h":  { "Strategies": [] },
    "1d":  { "Strategies": [] }
  }
}
```

**No tocar nada más en el JSON.** Solo reemplazar el contenido de `1h.Strategies`.

## Cambio 2 — Sin cambios de código

Este brief no toca código. El runtime y todos los servicios ya soportan
multi-símbolo. La configuración del JSON es input suficiente.

## Paso 1 — Verificación pre-backtest

Antes de correr el backtest, validar que la configuración carga
correctamente:

1. Compilar la solución completa. Suite 121/121 + 38/38 verde.
2. Iniciar el algoritmo en modo dry-run (si el sistema lo soporta) o
   correr un backtest de 1 día para verificar que el host inicializa los
   tres executors sin fallar.
3. Capturar los logs de inicialización. Verificar que aparecen:
   - `MarketRegimeRegistry` carga 3 modelos: BTCUSDT, ETHUSDT, TRBUSDT.
   - 3 `StrategyExecutor` instanciados con `ExecutorIdentifier`:
     - `EmaCrossStrategy_BTCUSDT_1h`
     - `EmaCrossStrategy_ETHUSDT_1h`
     - `EmaCrossStrategy_TRBUSDT_1h`
4. Si la inicialización falla (cualquier excepción durante setup) →
   **detener y reportar el stack trace completo**. No avanzar al
   backtest largo.

## Paso 2 — Backtest paralelo

Configuración del backtest (idéntica a ADR-026 para comparabilidad):

- **Período:** 2025-01-01 → 2026-03-31
- **InitialCash:** 100,000 USDT (default del sistema)
- **Símbolos activos:** BTCUSDT, ETHUSDT, TRBUSDT (los tres en 1h)
- **Strategies activas:** una EmaCrossStrategy por símbolo

Correr el backtest hasta el final. Capturar:

- Logs completos del backtest (filtrables por `ExecutorIdentifier`).
- `transaction-log.csv` del run completo.
- `summary.json` o equivalente con métricas agregadas.
- Cualquier ocurrencia de `OPS-2 invariante violado` en logs.
- Cualquier ocurrencia de `OrderEventMapper: evento sin tag`.
- Eventos de U1 (early stop por DD warm-up) o U2 (early stop por DD
  running), con el `ExecutorIdentifier` que disparó.

**Nota sobre tiempo wall-clock:** 15 meses × 3 símbolos en 1h ≈ 32,000
barras procesadas (vs ~43,000 que procesa el sistema actual con 1
símbolo en 15m por 15 meses, por timeframe más grueso pero 3 símbolos).
Tiempo de ejecución estimado: similar o ligeramente menor al backtest
de ADR-026.

## Paso 3 — Análisis del run

Para cada uno de los tres executors, **por separado**, reportar:

### Métricas agregadas por executor

| Métrica            | BTCUSDT | ETHUSDT | TRBUSDT |
|--------------------|---------|---------|---------|
| Total Orders       |         |         |         |
| Total Fills        |         |         |         |
| Total Cancels      |         |         |         |
| End Equity (USDT)  |         |         |         |
| Net Profit %       |         |         |         |
| Max Drawdown %     |         |         |         |
| Win Rate %         |         |         |         |
| P/L Ratio          |         |         |         |
| Sharpe             |         |         |         |
| U1 dispara         | fecha o nunca | fecha o nunca | fecha o nunca |
| U2 dispara         | fecha o nunca | fecha o nunca | fecha o nunca |

**Nota importante sobre métricas agregadas con 3 executors compartiendo
una cuenta:** el End Equity, Net Profit y Sharpe **del backtest
completo** son los del portfolio agregado, NO por executor. Las
"métricas por executor" arriba son una visión por executor del subset
de trades que ese executor generó — útil para diagnóstico, no son
métricas de account-level. La distorsión del DD calculado por cada
monitor sobre los 100k notional de su executor (cuando la cuenta es
compartida) es la deuda del allocator multi-estrategia, conocida y
documentada. Se acepta para este brief.

### Criterios de validación cualitativa (gating del brief)

Para cada executor:

| Criterio                                                | BTC | ETH | TRB |
|---------------------------------------------------------|-----|-----|-----|
| Cero `OPS-2 invariante violado` en su log               |     |     |     |
| Cero `OrderEventMapper: evento sin tag` durante TimeExit/Liquidate dirigido por su executor |  |  |  |
| Si U1/U2 disparan, lo hacen con DD coherente con POLICY 3.1 (no por bug) |  |  |  |
| `ExecutorIdentifier` único bien etiquetado en todos los logs de su executor |  |  |  |
| Operación independiente: orders/cancels/fills del executor no se mezclan con los de otros |  |  |  |

**Los tres executors deben pasar los cinco criterios.** Si cualquiera
falla cualquier criterio, el subsistema no está agnóstico al símbolo
para operación concurrente — pausa y diagnóstico antes de seguir.

### Sanity checks adicionales

- **Cardinality de `ExecutorIdentifier` en logs:** debe haber
  exactamente 3 valores distintos en el campo, no 2 (señal de que un
  executor no se inició) ni 4 (señal de identificador mal construido).
- **Trades por executor:** ningún executor debe tener 0 trades en 15
  meses. Si TRB o ETH no opera nada, hay un problema (régimen siempre
  fuera de Trend, o filtro de régimen mal cargado, o señales de la
  estrategia nunca disparan).
  - Umbral mínimo razonable: **≥10 trades por executor en 15 meses.**
    Por debajo, indagar si es por falta de oportunidades reales o por
    bug.
- **Distribución temporal de trades:** los trades de cada executor
  deben distribuirse a lo largo del período, no concentrarse en un mes
  específico (señal de que el régimen `Trend` solo aparece en una
  ventana).

## Paso 4 — Comparación con baseline ADR-026

El backtest de ADR-026 (BTCUSDT 15m) NO es directamente comparable a
este (BTCUSDT 1h en flota multi-símbolo), por dos razones:

1. **TF distinto:** 15m vs 1h. Distinta cantidad de barras, distintas
   señales, distinta estructura de trades. NO son la misma serie de
   trades en TFs distintos.
2. **Account compartida:** ADR-026 tenía BTC solo; este brief tiene los
   3 símbolos compartiendo capital. Aunque cada monitor de DD ve los
   100k como suyos (deuda allocator), la cuenta real es la suma.

**No reportar "BTC nuevo vs BTC ADR-026" como comparación**. Reportar
los tres executors de este brief como un nuevo baseline propio
(multi-símbolo 1h, post-DEUDA-1, con modelo BTC re-entrenado).

## Paso 5 — Sin paso 5

El brief termina cuando los tres executors cumplen los criterios y el
reporte está completo. ADR-028 (cierre de sesión multi-símbolo) se
redacta como brief separado al cierre, no acá.

## Tests

Correr la suite completa antes y después del cambio de
`strategies.json`:

- `Trading.Application.Tests`: 121/121 esperado.
- `Trading.Domain.Tests`: 38/38 esperado.

El cambio del JSON no debería afectar tests (los tests no leen
`strategies.json` directamente; usan fixtures). Verificar como
precaución.

## Criterio de cierre del brief

1. `strategies.json` actualizado con las tres entradas, sintaxis válida.
2. Backtest paralelo 2025-01-01 → 2026-03-31 completado sin excepciones
   no manejadas.
3. Los tres executors instanciados con `ExecutorIdentifier` únicos.
4. Cada executor ≥10 trades en el período.
5. Cero `OPS-2 invariante violado` en logs (cualquier executor).
6. Cero `OrderEventMapper: evento sin tag` durante TimeExit/Liquidate
   (cualquier executor).
7. Si U1/U2 disparan, lo hacen con DD coherente (no por bug).
8. Tabla de métricas por executor completa.
9. Tabla de criterios de validación cualitativa con los 5 checks por
   los 3 executors.
10. Suite 121/121 + 38/38 verde.

## Reporte de cierre

Devolver al operador:

1. Confirmación de los 10 puntos del criterio de cierre.
2. Tabla de métricas por executor.
3. Tabla de criterios cualitativos (5x3).
4. Sanity checks: cardinality de ExecutorIdentifier, trades por
   executor, distribución temporal.
5. Cualquier desviación o sorpresa inesperada durante la ejecución.
6. Si hubo eventos U1/U2: fecha, executor, DD al momento del disparo.

## Out of scope

- ADR-028 de cierre de sesión. (Brief de cierre separado al final.)
- Allocator multi-estrategia. (Hito propio, no se aborda acá.)
- Calibrar parámetros distintos en BTC-15m / ETH-1h / TRB-4h
  (combinación cruzada). (Sesión siguiente, después de cerrar agnosticismo
  al símbolo en este brief.)
- Investigar por qué TRB tiene Squeeze 40% (vs ETH 31% / BTC viejo
  similar). (Es output legítimo del activo, no hace falta diagnosticar.)
- Cerrar DEUDA-2 (OrderListHash).
- Cerrar deuda POLICY 7.1 (título 1h/15m).
- Investigar la varianza numérica del trainer en dígitos 12+ (hallazgo
  benign del Brief 2, registrado en notas, no abierto como deuda).

## Notas operativas

- El backtest puede tardar tiempo wall-clock significativo (15 meses x
  3 símbolos). Si Claude Code necesita correrlo en segmentos, partir
  por trimestres es una opción válida — pero el reporte final tiene que
  ser sobre el run completo 2025-01-01 → 2026-03-31, no sobre segmentos
  agregados post-hoc.
- Si algún executor lanza excepción durante el backtest pero el run
  continúa (otros executors siguen operando), eso ES un fallo del
  subsistema: la promesa es aislamiento por executor, no continuidad
  con un executor caído. Reportar y pausar.
- Si el log muestra clasificación `Unknown` para un símbolo más allá del
  período razonable de warm-up (~100 barras 4h = ~17 días desde inicio),
  ese executor no está leyendo bien su modelo HMM. Reportar y pausar.
