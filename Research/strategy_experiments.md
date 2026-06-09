# Strategy Experiments Log

Registro de hipótesis evaluadas por Fase 0. Fuente de verdad para evitar re-explorar candidatas ya descartadas.

| Hito | Estrategia | TF | Activos | M4 | Backtest Sharpe | Win Rate | Estado | Razón descarte |
|---|---|---|---|---|---|---|---|---|
| E | DonchianBreakoutStrategy | 4h | BTC | ⚠️ +0.705 (señal mensual — escala incorrecta) | -2.623 (lookback 126) | 13% | ❌ | Win rate 13-24% con cualquier lookback. Desconexión de escala entre M4 (mensual) e implementación (4h). |
| E | IntradayMomentumStrategy | 30m | ETH, BNB, BTC | ✅ ETH +0.645, BNB +0.691 / ❌ BTC -0.204 | -3.28 (ETH OOS 2025) | 36% | ❌ | Edge arbitrado por institucionales en 2025. M4 validó 2020-2024; OOS falla M1+M2. |
| E | BollingerBandsStrategy | 4h | BTC, ETH, BNB | ❌ 5/9 configs (55.6%) | N/A (M4 rechazado) | N/A | ❌ | M4 falla: BTC OK en oversold=1,4; ETH falla todas; BNB OK todas. Inconsistencia cross-asset. |

---

## Notas por experimento

### DonchianBreakoutStrategy
- Hipótesis: breakout de canal Donchian en 4h predice inicio de tendencia (Li et al. arXiv 2512.02227).
- Lookback 20 barras: Sharpe -1.742, Win 24%. Falsas rupturas sistemáticas en BTC 4h.
- Lookback 126 barras: Sharpe -2.623, Win 13%. El lookback más largo empeoró los resultados.
- El M4 usó datos mensuales (señal de 12 meses, Sharpe +0.705) — el mecanismo validado no es el mismo que se implementó en 4h.
- Fade del Donchian también descartado: M4 negativo en 27/27 combinaciones (3 lookbacks × 3 holds × 3 activos).

### IntradayMomentumStrategy (Shen, Urquhart & Wang, Financial Review 2022)
- Hipótesis: primera barra 30m del día UTC (00:00-00:30) predice dirección de la última (23:30-00:00).
- M4 sobre Binance 2020-2024: ETH +0.645, BNB +0.691, BTC -0.204. BTC excluido por adopción institucional post-2021.
- V1 (entrada en bar_0, MaxBars 46): Sharpe -1.394. Error de diseño — las 23h de holding destruyen el edge puntual.
- V2 (entrada en bar_47, MaxBars 1): Sharpe -3.28, Win 36%. OPS-2 disparó a 2025-04-27.
- El efecto documentado en el paper (datos 2013-2020) ya no existe en el mercado de 2025.

### BollingerBandsStrategy (Connors Research variación #217, adaptada a 4h)
- Hipótesis: oversold en Bollinger Bands (%b < 0) durante N barras consecutivas predice reversión (M4 pure signal test).
- M4 Binance 4h 2020-2025 en BTC/ETH/BNB: 5/9 configs pasan M4 (Sharpe >= 0.5), fallando el threshold 66.7%.
  - BTC: oversold=1 (Sharpe +0.822, 642 trades), oversold=4 (+0.573, 57 trades) — PASS. oversold=2 (-0.066) — FAIL.
  - ETH: todos fallan (oversold=1: +0.276, oversold=2: +0.324, oversold=4: -0.851).
  - BNB: todos pasan (oversold=1: +0.848, oversold=2: +0.620, oversold=4: +1.896).
- Inconsistencia cross-asset: ETH sin edge claro, BTC parcial, BNB fuerte. No hay configuración que funcione uniformemente.
- Código no bugueado pre-V1 tenía defecto `InPosition` que bloqueaba re-entradas. Fix aplicado pero hipótesis rechazada por M4.
