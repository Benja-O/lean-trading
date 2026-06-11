# Validation Report — Strategy
Generado: 2026-06-11 20:51 UTC

## Resultado final
**✅ APROBADA**

| | Estado |
|---|---|
| Gate 1 — métricas OOS | ✅ PASS |
| Gate 2 — Monte Carlo  | ✅ PASS |

## Comparativa IS vs OOS
- IS: 2021-01-06 → 2023-09-01  (726 trades)
- OOS: 2025-01-05 → 2026-04-09  (650 trades)

| Métrica | IS | OOS | Δ |
|---|---|---|---|
| Sharpe | 2,178 | 1,718 | ✅ -21 % |
| Sortino | 3,491 | 2,328 | ✅ -33 % |
| Calmar | 4,163 | 4,189 | ✅ 1 % |
| Profit Factor | 1,626 | 1,429 | ✅ -12 % |
| Expectancy | 0,584 % | 0,265 % | ⚠️ -55 % |
| Win Rate | 61,3 % | 60,0 % | ✅ -2 % |
| Max DD | 8,8 % | 7,3 % | ✅ -18 % |
| CAGR | 36,76 % | 30,41 % | ✅ -17 % |
| Net Profit | 129,25 % | 39,61 % | ⚠️ -69 % |
| Recovery Factor | 14,64 | 5,46 | ⚠️ -63 % |

## Gate 1 — Criterios OOS
- ✅ **Trades ≥ 50**: 650
- ✅ **Net profit > 0**: 39,61 %
- ✅ **Sharpe ≥ 0.30**: 1,718
- ✅ **Profit factor ≥ 1.10**: 1,429
- ✅ **Expectancy > 0**: 0,265 %

## Gate 2 — Monte Carlo
_10.000 simulaciones, block-bootstrap tamaño 5_

| Métrica | P5 | P50 | P95 |
|---|---|---|---|
| Sharpe | 0,846 | 2,812 | 4,747 |
| Max DD | — | 8,1 % | 16,4 % |
| CAGR | 9,2 % | 30,7 % | 51,0 % |

- P(Sharpe < 0): **1,0 %**
- P(CAGR < 0): **1,1 %**

- ✅ **P(Sharpe < 0) ≤ 20%**: 1,0 %
- ✅ **Mediana Max DD ≤ 55%**: 8,1 %
- ✅ **CAGR P5 > -5%**: 9,2 %
