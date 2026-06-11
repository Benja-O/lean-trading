# Strategy Experiments Log

Registro de hipótesis evaluadas por Fase 0. Fuente de verdad para evitar re-explorar candidatas ya descartadas.

| Hito | Estrategia | TF | Activos | M4 | Backtest Sharpe | Win Rate | Estado | Razón descarte |
|---|---|---|---|---|---|---|---|---|
| E | CVD Divergence bidireccional | 1h | BTC, ETH, SOL | ❌ 0/9 configs (0%) bidireccional. Long-only: ✅ 9/9 configs, BTC +1.74 / ETH +0.52 / SOL +0.95 (3/3) | N/A — ver CvdBullishDivergenceStrategy | N/A | ✅→ CvdBullishDivergenceStrategy implementada | Bidireccional falla: Short signals destruyen el Sharpe en ETH y SOL (SOL Short -0.92). Long-only pasa 3/3 activos con lookback=24, hold=6. Implementado como estrategia Long-only separada. Script: `Research/m4_cvd_divergence.py`. |
| E | CvdBullishDivergenceStrategy | 1h | BTC, ETH, SOL | ✅ BTC +1.74, ETH +0.52, SOL +0.95 (lookback=24, hold=6 — 3/3 activos) | -1.85 (IS 2021-2024, sin OPS-2, SL=10%) | 48% | ❌ | M4 pasó pero QC IS falla. Root cause: M4 contaba señales solapadas como trades independientes — inflación artificial del Sharpe. QC solo entra una vez por posición. Expectancy trade-level negativa (-0.022%/trade). Eliminada. Ver nota. |
| E | DonchianBreakoutStrategy | 4h | BTC | ⚠️ +0.705 (señal mensual — escala incorrecta) | -2.623 (lookback 126) | 13% | ❌ | Win rate 13-24% con cualquier lookback. Desconexión de escala entre M4 (mensual) e implementación (4h). |
| E | IntradayMomentumStrategy | 30m | ETH, BNB, BTC | ✅ ETH +0.645, BNB +0.691 / ❌ BTC -0.204 | -3.28 (ETH OOS 2025) | 36% | ❌ | Edge arbitrado por institucionales en 2025. M4 validó 2020-2024; OOS falla M1+M2. |
| E | BollingerBandsStrategy | 4h | BTC, ETH, BNB | ❌ 5/9 configs (55.6%) | N/A (M4 rechazado) | N/A | ❌ | M4 falla: BTC OK en oversold=1,4; ETH falla todas; BNB OK todas. Inconsistencia cross-asset. |
| E | H3 — Lead-lag BTC→ETH/BNB | 1h | BTC (señal), ETH, BNB | ❌ 0/6 configs (0%) | N/A (M4 rechazado) | ~47% | ❌ | Win rate sistemáticamente < 50% en todos los thresholds (0.5/1.0/1.5%) y ambos activos. Correlación BTC-ETH/BNB ocurre en la misma barra (simultaneidad), no con lag de 1 barra. Edge ya arbitrado. |
| E | H1 — RSI(14) + HMM Squeeze | 4h | BTC, ETH, BNB | ❌ 0/18 configs (0%) | N/A (M4 rechazado) | 55-69% | ❌ | Win rate alto (55-69%) pero Sharpe negativo — retornos perdedores superan en magnitud a los ganadores. Muy pocas señales (RSI<25 en Squeeze: 12-19 trades/5 años = insuficiente poder estadístico). Sin edge explotable. |
| E | Funding Rate Positioning (FRP) | Diario | BTC, ETH, SOL | ❌ Bidireccional: BTC 8/54, ETH 0/54, SOL 11/54. SHORT-only: BTC 0/54, ETH 3/54, SOL 5/54 | N/A (M4 rechazado) | N/A | ❌ | Señal no generalizable cross-asset. BTC tiene edge en el lado LONG (crowded shorts → squeeze alcista), no en el SHORT. ETH resistente a cualquier señal de funding. El mecanismo existe pero opera en timeframes intraday, no diarios. |
| E | H2 — ATR Compression Breakout | 4h | BTC, ETH, BNB | ✅ BTC 6/9, ETH 5/9, BNB 7/9 | -0.922 (BTC, Sharpe) | 37% | ❌ | M4 pasado. Backtest con SL 2%: Sharpe -0.922, Win 37%, DD 30.3% (kill switch 2025-03-19). SL 2% fijo mata el edge antes de que se materialice. Ver ADR-035. |
| E | ATR Compression + SL ATR (2.5×, TP 3.5×) | 4h | BTC, ETH, BNB | ✅ (H2) | -0.779 | 37% | ❌ | ATR SL mejora DD (30.3%→18.3%) y Sharpe (-0.922→-0.779) pero win rate permanece 37%. OPS-2 disparó BTC 2025-07-27 (PF 0.70), ETH 2025-12-01 (PF 0.75). Falla M1 y M2. El problema es el edge de la señal, no el risk management. |
| E | ATR Compression + Taker Buy Ratio | 1h | BTC, ETH, BNB | ❌ BTC 0/54, ETH 9/54, BNB 0/54 | N/A (M4 rechazado) | N/A | ❌ | TBR no añade edge cross-asset en 1h. ETH tiene señal parcial (TBR=0.55, hold=4-6b, Sharpe ~0.8) pero estadísticamente débil (30-40 trades/4 años). BTC y BNB: 0 configs. El filtro TBR estrecha tanto la señal que no hay frecuencia suficiente. Script: `Research/m4_atr_tbr.py`. |

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

### H3 — Lead-lag BTC→ETH/BNB (hipótesis pura, sin implementación)
- Hipótesis: retorno BTC(t-1) > umbral predice dirección ETH/BNB en la barra t. Flujos institucionales entran por BTC primero.
- M4 Binance 1h 2020-2025 sobre ETH y BNB, thresholds 0.5/1.0/1.5%, hold 4 barras, bidireccional (Long + Short):
  - ETH: Sharpe -0.569 / -0.785 / -0.820. Win rate 47.6 / 47.2 / 47.4%. FAIL en los 3 configs.
  - BNB: Sharpe -0.945 / -0.599 / -0.489. Win rate 47.5 / 46.7 / 47.8%. FAIL en los 3 configs.
- Win rate consistentemente ~47% en todos los 6 configs y ambos activos — la señal es ligeramente contraria a la hipótesis.
- Diagnóstico: la correlación BTC-ETH/BNB es contemporánea (misma barra 1h), no hay lag de 1 barra explotable.
  El efecto lead-lag documentado en literatura existe en timeframes de segundos/minutos (microestructura), no en 1h.
- Script: `Research/m4_lead_lag_btc_eth.py`

### H1 — RSI Mean Reversion condicionado por HMM Squeeze
- Hipótesis: RSI(14) < umbral en régimen HMM Squeeze (baja vol, sin trend) filtra señales falsas de oversold durante downtrends fuertes.
- M4 Binance 4h 2020-2025 sobre BTC/ETH/BNB, thresholds RSI 25/30/35, hold 8/12 barras:
  - Win rate alto: 55-69% en casi todos los configs. El condicionamiento HMM sí filtra algo.
  - Sharpe negativo en todos los 18 configs (0/18). Mejor resultado: ETH RSI<25 hold=8b → Sharpe +0.343 con 19 trades.
  - Problema estructural: RSI<25 en Squeeze produce 12-19 trades en 5 años (~3/año). Insuficiente frecuencia para compensar la varianza de los retornos por trade.
  - El retorno medio por trade es positivo en los thresholds más extremos pero la std es ~3-5x el mean → Sharpe inevitablemente bajo.
- Clasificación de régimen: centroide más cercano en espacio de features HMM escaladas (replica exacta de FeatureExtractor.cs). BNB usa modelo BTC como proxy.
- Script: `Research/m4_rsi_hmm_squeeze.py`

### H2 — ATR Compression Breakout
- Hipótesis: el mercado alterna entre fases de compresión (ATR bajo) y expansión. Un rompimiento de rango durante compresión predice un movimiento direccional significativo.
- M4 Binance 4h 2020-2025 sobre BTC/ETH/BNB, grid: ATR<P25/P35, lookback=10/20b, hold=4/8b (8 configs):
  - BTC: 1/8, ETH: 2/8. Gate falla. Diagnóstico: hold=8 destruye la señal.
- Diagnóstico A: grid reducido hold=[2,3,4], ATR=[P15/P20/P25], lookback=10 (9 configs):
  - BTC: 6/9 ✅, ETH: 5/9 ✅, BNB: 7/9 ✅ — Gate pasado.
  - hold=3 (12h) pasa cross-asset sin excepción: BTC +0.565, ETH +0.703, BNB +0.984 (Sharpe).
  - hold=4 (16h) también pasa: BTC +0.822, ETH +0.659, BNB +0.670.
  - hold=2 (8h) falla — edge no se materializó todavía.
- Parámetros nominales: ATR<P20, lookback=10, hold=3 barras 4h (12h).
- Implementación: `AtrCompressionBreakoutStrategy.cs`. BTCUSDT 4h, MaxBars=3, CombineWithTimeExit=true.
- Scripts: `Research/m4_atr_compression_breakout.py` (grid original), diagnóstico A inline en sesión.
- **Backtest QC (2025, BTC/ETH/BNB, SL 2% fijo, TP 4%):**
  - Sharpe: -0.922. Win Rate: 37%. DD máximo: 30.3% (kill switch disparó 2025-03-19).
  - Causa raíz: SL 2% fijo se activa durante la volatilidad intraday de las 12h de hold, antes de que el edge se materialice. Ver ADR-035.
- **Backtest QC (2025, ATR SL 2.5×, TP 3.5×) — Candidata 9:**
  - Sharpe: -0.779. Win Rate: 37%. DD máximo: 18.3%. Net Profit: -6.813%. Total Orders: 554.
  - OPS-2 disparó BTC 2025-07-27 (PF rolling 0.70 sostenido 10 trades), ETH 2025-12-01 (PF rolling 0.75).
  - ATR SL mejora Sharpe (-0.922→-0.779) y DD (30.3%→18.3%), pero no resuelve el win rate: 37% en ambas variantes.
  - Diagnóstico final: con 63% loss rate y P/L ratio 1.55, la expectancia es negativa. La compresión ATR no predice con suficiente precisión la dirección del breakout en 4h. El problema es la señal, no el risk management.
  - **Estrategia eliminada** — rechazada por M1 (Sharpe -0.779 < 0.5) y M2 (Win Rate 37% < 40%). Ver ADR-036.

### ATR Compression + Taker Buy Ratio (1h)
- Hipótesis: el TBR (taker_buy_base_vol / total_vol) confirma la direccionalidad del breakout, filtrando fakeouts donde el precio rompe pero el flujo agresivo no acompaña.
- M4 Binance 1h 2021-2024 sobre BTC/ETH/BNB, grid 54 configs (3 ATR × 2 lookback × 3 hold × 3 TBR_thr):
  - BTC: 0/54 (0%). Mejor: P25/look=10b/hold=3b/TBR=0.55 → Sharpe +0.386.
  - ETH: 9/54 (16.7%). Mejores: P15/look=10b/hold=4b/TBR=0.55 → Sharpe +0.829, T=36.
  - BNB: 0/54 (0%). "Mejores" con TBR=0.58 tienen solo 11 trades — ruido estadístico.
- Diagnóstico: TBR_thr bajo (0.52) → muchas señales pero Sharpe negativo en BTC/BNB. TBR_thr alto (0.55-0.58) → señales tan raras que no hay poder estadístico. El filtro TBR no añade valor cross-asset en 1h.
- ETH tiene edge local pero los mejores configs tienen 30-40 trades/4 años — insuficiente para conclusión robusta.
- Script: `Research/m4_atr_tbr.py`.

### CVD Bullish Divergence (CvdBullishDivergenceStrategy)
- Hipótesis: cuando precio hace nuevo mínimo N-barra pero CVD no lo confirma, hay buying hidden → Long.
- M4 IS 2021-2024 (Long-only): 9/9 configs pasan, BTC +1.74 / ETH +0.52 / SOL +0.95 (3/3 activos). lookback=24, hold=6.
- Implementada: `CvdBullishDivergenceStrategy.cs`, 10 tests unitarios, registrada en StrategyFactory.
- Backtest QC IS 2021-2024 (SL=10%, TP=15%, MaxBars=6, sin OPS-2): Sharpe -1.854, DD 30.7%, expectancy -0.022%/trade.
- Root cause del gap M4↔QC: M4 trata cada señal como trade independiente aunque sean solapadas. Durante una caída de precio de 12h donde CVD se recupera gradualmente, M4 registra ~12 trades de entrada a distintos precios; QC entra solo una vez y mantiene la posición. Los "12 trades" de M4 en la recuperación tienen todos buen retorno → Sharpe inflado. QC captura solo el primero. La expectancy trade-by-trade es negativa: Average Win 0.15% × 48% win rate + Average Loss -0.18% × 52% = -0.022% por trade.
- Conclusión: el edge observado en M4 era un artefacto metodológico de señales solapadas, no alpha real.
- Scripts: `Research/m4_cvd_divergence.py`.

### Funding Rate Positioning (FRP)
- Hipótesis: z-score del funding rate (ventanas 14/30/60d) en extremo → mercado overcrowded → señal contraria.
- Test 1 bidireccional (BTC/ETH/SOL, diario, 2020-2025, 54 configs): BTC 8/54, ETH 0/54, SOL 11/54. Gate: 27/54 en 2/3 activos — FAIL.
- Test 2 SHORT-only (crowded longs → short): BTC 0/54, ETH 3/54, SOL 5/54. Peor que bidireccional — FAIL.
- Hallazgo clave: el edge en BTC estaba en el lado LONG (funding muy negativo → short squeeze → precio sube), no en el SHORT como postulaba la teoría.
- El filtro de tendencia (MA) es condición necesaria pero no suficiente: sin MA, todas las configs son negativas.
- Ventana de 60d sistemáticamente peor: demasiado lenta para capturar eventos de crowding.
- ETH sin señal en ninguna dirección, posiblemente por cambio estructural post-Merge (2022).
- Diagnóstico: el mecanismo de desapalancamiento existe pero opera en timeframes sub-diarios (horas), donde los datos históricos de order book/funding granular no están disponibles sin costo.
- Script: `Research/m4_funding_rate_positioning.py`
