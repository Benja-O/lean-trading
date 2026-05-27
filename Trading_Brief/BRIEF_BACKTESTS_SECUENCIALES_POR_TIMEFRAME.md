# Brief — Validación multi-timeframe en backtests SECUENCIALES

## Contexto y hallazgo bloqueante para paralelo

Plan original: activar 15m + 1h + 4h en paralelo en `strategies.json` y un
solo backtest. **Descartado** tras auditar `BarProcessingService.ProcessBar`.

El servicio chequea posición existente vía
`_portfolioState.IsInvested(instrumentId)`, que consulta
`_algorithm.Portfolio[symbol].Invested` — la posición agregada del broker
para BTCUSDT, sin distinción por executor. Consecuencias si se corren tres
executors del mismo símbolo en paralelo:

1. El primer executor que dispare señal abre posición; los otros dos quedan
   bloqueados de entrada hasta que esa posición cierre (mutual exclusion
   accidental por símbolo compartido).
2. Lógica de TimeExit acoplada: cualquier executor con
   `HasActivePosition + IsInvested` cuyo `BarsHeld >= MaxBars` dispara
   `LiquidateInstrument`, cerrando potencialmente una posición que abrió
   otro executor. Tag y atribución de PnL al monitor van al executor que
   ejecuta el liquidate, no al que abrió.
3. El monitor de salud recibe métricas contaminadas: el executor que abrió
   acumula el PnL, pero los otros pueden incrementar `BarsHeld` mientras
   ven la posición compartida y eventualmente cerrar trades ajenos.

Esto **viola el criterio central de la sesión** ("cada executor opera con
su propio ciclo de vida sin interferir"). Forzar paralelo produciría
ruido del bug de acoplamiento, no datos limpios del subsistema bajo
validación.

**Decisión:** se ejecutan **tres backtests secuenciales**, un TF activo por
backtest. Mismo período (2025-01-01 → 2026-03-31) en los tres.

El hallazgo de acoplamiento entre executors se registra como deuda técnica
para tratar después de esta validación. NO se intenta arreglar en este
brief.

## Alcance del cambio

**Un único archivo, editado tres veces en secuencia:**
`Trading.Strategies/strategies.json`

MSBuild copia automáticamente a `Launcher/bin/Debug/` por la regla
`<Content Include="strategies.json" CopyToOutputDirectory="PreserveNewest" />`.
NO editar la copia del bin manualmente.

## Tres configuraciones a aplicar

### Config A — solo 15m (smoke test de no-regresión)

Es la configuración actual del repo. Si fue tocada, restaurarla a este
estado exacto:

```json
{
  "Timeframes": {
    "1m":  { "Strategies": [] },
    "5m":  { "Strategies": [] },
    "15m": {
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
        }
      ]
    },
    "30m": { "Strategies": [] },
    "1h":  { "Strategies": [] },
    "4h":  { "Strategies": [] },
    "1d":  { "Strategies": [] }
  }
}
```

### Config B — solo 1h

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

### Config C — solo 4h

```json
{
  "Timeframes": {
    "1m":  { "Strategies": [] },
    "5m":  { "Strategies": [] },
    "15m": { "Strategies": [] },
    "30m": { "Strategies": [] },
    "1h":  { "Strategies": [] },
    "4h": {
      "Strategies": [
        {
          "StrategyName": "EmaCrossStrategy",
          "FileModelName": "",
          "Symbol": "BTCUSDT",
          "CompatibleRegimes": [ "Trend" ],
          "StopTakeMode": "Percentage",
          "StopLossPercentage": 4.0,
          "TakeProfitPercentage": 8.0,
          "StopLossAtrMultiplier": 0,
          "TakeProfitAtrMultiplier": 0,
          "RiskPerTradePercentage": 2.0,
          "MaxBars": 20,
          "CombineWithTimeExit": true
        }
      ]
    },
    "1d":  { "Strategies": [] }
  }
}
```

## Justificación de parámetros por TF

| Parámetro              | 15m  | 1h  | 4h  | Racional                                                                  |
|------------------------|------|-----|-----|---------------------------------------------------------------------------|
| StopLossPercentage     | 1.0  | 2.0 | 4.0 | Escala ×2 por step de TF para no disparar SL por ruido intra-bar.         |
| TakeProfitPercentage   | 2.0  | 4.0 | 8.0 | Mantiene R:R = 1:2 idéntico en los tres TFs.                              |
| RiskPerTradePercentage | 2.0  | 2.0 | 2.0 | Política de portfolio, no escala con TF.                                  |
| MaxBars                | 20   | 20  | 20  | Unidad natural de la estrategia; aislamos TF como única variable.         |
| CombineWithTimeExit    | true | true| true| Heredado de 15m sin cambio.                                               |
| CompatibleRegimes      | Trend| Trend| Trend| El clasificador HMM es 4h global, independiente del TF de la estrategia. |

## Orden operativo

Para cada configuración (A, B, C) **en este orden**:

1. Editar `Trading.Strategies/strategies.json` con la config correspondiente.
2. `dotnet build`. Debe pasar limpio.
3. `dotnet test`. Debe ser 121/121 verde.
4. Confirmar visualmente en el log de arranque que se crea **exactamente UN**
   `ExecutorIdentifier`:
   - Config A → `EmaCrossStrategy_BTCUSDT_15m`
   - Config B → `EmaCrossStrategy_BTCUSDT_1h`
   - Config C → `EmaCrossStrategy_BTCUSDT_4h`
5. Disparar backtest del período 2025-01-01 → 2026-03-31.
6. Guardar los logs y el reporte de backtest etiquetados claramente
   (`backtest_15m.log`, `backtest_1h.log`, `backtest_4h.log` o equivalente).
   El operador los pega después en chat para análisis.

## Criterios de análisis post-backtest (referencia, no acción del brief)

Por cada backtest se va a validar:

- Cero ocurrencias de `OPS-2 invariante violado` en el log.
- Cero ocurrencias de `OrderEventMapper: evento sin tag` durante TimeExit
  o Liquidate (excepto LiquidateAll/kill switch global).
- Si U1 o U2 disparan, lo hacen con DD real consistente con la POLICY 3.1.
  No falsos positivos en los primeros trades.
- Cantidad de órdenes, end equity, max DD por backtest. Para Config A
  (15m) los números deben coincidir con la baseline ya validada (147
  órdenes, end equity 87.148 USDT, DD 21.5 %, U2 dispara 06/02/2025). Si
  difieren materialmente, hay regresión y se detiene la validación.

## Lo que NO se hace en este brief

- No se arregla el acoplamiento de estado entre executors. Hallazgo
  registrado, se trata como precondición de multi-estrategia real
  (junto con el TODO del allocator en `TradingAlgorithmHost`).
- No se modifica POLICY.md ni se toca el bug del JSON espontáneo
  (hallazgos laterales fuera de alcance).
- No se modifica código C# de Domain/Application/Strategies.

## Reportar al cerrar cada iteración (A, B, C)

- Resultado de `dotnet build` y `dotnet test`.
- Confirmación de que se creó un único `ExecutorIdentifier` esperado.
- Si el backtest corrió, métricas top-line (órdenes, end equity, DD).
- Logs completos disponibles para pegar en chat.
