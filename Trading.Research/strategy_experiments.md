# Strategy Experiments Log

Registro de hipÃ³tesis evaluadas por Fase 0. Fuente de verdad para evitar re-explorar candidatas ya descartadas.

| Hito | Estrategia | TF | Activos | M4 | Backtest Sharpe | Win Rate | Estado | RazÃ³n descarte |
|---|---|---|---|---|---|---|---|---|
| E | CVD Divergence bidireccional | 1h | BTC, ETH, SOL | âŒ 0/9 configs (0%) bidireccional. Long-only: âœ… 9/9 configs, BTC +1.74 / ETH +0.52 / SOL +0.95 (3/3) | N/A â€” ver CvdBullishDivergenceStrategy | N/A | âœ…â†’ CvdBullishDivergenceStrategy implementada | Bidireccional falla: Short signals destruyen el Sharpe en ETH y SOL (SOL Short -0.92). Long-only pasa 3/3 activos con lookback=24, hold=6. Implementado como estrategia Long-only separada. Script: `Trading.Research/m4_cvd_divergence.py`. |
| E | CvdBullishDivergenceStrategy | 1h | BTC, ETH, SOL | âœ… BTC +1.74, ETH +0.52, SOL +0.95 (lookback=24, hold=6 â€” 3/3 activos) | -1.85 (IS 2021-2024, sin OPS-2, SL=10%) | 48% | âŒ | M4 pasÃ³ pero QC IS falla. Root cause: M4 contaba seÃ±ales solapadas como trades independientes â€” inflaciÃ³n artificial del Sharpe. QC solo entra una vez por posiciÃ³n. Expectancy trade-level negativa (-0.022%/trade). Eliminada. Ver nota. |
| E | DonchianBreakoutStrategy | 4h | BTC | âš ï¸ +0.705 (seÃ±al mensual â€” escala incorrecta) | -2.623 (lookback 126) | 13% | âŒ | Win rate 13-24% con cualquier lookback. DesconexiÃ³n de escala entre M4 (mensual) e implementaciÃ³n (4h). |
| E | IntradayMomentumStrategy | 30m | ETH, BNB, BTC | âœ… ETH +0.645, BNB +0.691 / âŒ BTC -0.204 | -3.28 (ETH OOS 2025) | 36% | âŒ | Edge arbitrado por institucionales en 2025. M4 validÃ³ 2020-2024; OOS falla M1+M2. |
| E | BollingerBandsStrategy | 4h | BTC, ETH, BNB | âŒ 5/9 configs (55.6%) | N/A (M4 rechazado) | N/A | âŒ | M4 falla: BTC OK en oversold=1,4; ETH falla todas; BNB OK todas. Inconsistencia cross-asset. |
| E | H3 â€” Lead-lag BTCâ†’ETH/BNB | 1h | BTC (seÃ±al), ETH, BNB | âŒ 0/6 configs (0%) | N/A (M4 rechazado) | ~47% | âŒ | Win rate sistemÃ¡ticamente < 50% en todos los thresholds (0.5/1.0/1.5%) y ambos activos. CorrelaciÃ³n BTC-ETH/BNB ocurre en la misma barra (simultaneidad), no con lag de 1 barra. Edge ya arbitrado. |
| E | H1 â€” RSI(14) + HMM Squeeze | 4h | BTC, ETH, BNB | âŒ 0/18 configs (0%) | N/A (M4 rechazado) | 55-69% | âŒ | Win rate alto (55-69%) pero Sharpe negativo â€” retornos perdedores superan en magnitud a los ganadores. Muy pocas seÃ±ales (RSI<25 en Squeeze: 12-19 trades/5 aÃ±os = insuficiente poder estadÃ­stico). Sin edge explotable. |
| E | Funding Rate Positioning (FRP) | Diario | BTC, ETH, SOL | âŒ Bidireccional: BTC 8/54, ETH 0/54, SOL 11/54. SHORT-only: BTC 0/54, ETH 3/54, SOL 5/54 | N/A (M4 rechazado) | N/A | âŒ | SeÃ±al no generalizable cross-asset. BTC tiene edge en el lado LONG (crowded shorts â†’ squeeze alcista), no en el SHORT. ETH resistente a cualquier seÃ±al de funding. El mecanismo existe pero opera en timeframes intraday, no diarios. |
| E | H2 â€” ATR Compression Breakout | 4h | BTC, ETH, BNB | âœ… BTC 6/9, ETH 5/9, BNB 7/9 | -0.922 (BTC, Sharpe) | 37% | âŒ | M4 pasado. Backtest con SL 2%: Sharpe -0.922, Win 37%, DD 30.3% (kill switch 2025-03-19). SL 2% fijo mata el edge antes de que se materialice. Ver ADR-035. |
| E | ATR Compression + SL ATR (2.5Ã—, TP 3.5Ã—) | 4h | BTC, ETH, BNB | âœ… (H2) | -0.779 | 37% | âŒ | ATR SL mejora DD (30.3%â†’18.3%) y Sharpe (-0.922â†’-0.779) pero win rate permanece 37%. OPS-2 disparÃ³ BTC 2025-07-27 (PF 0.70), ETH 2025-12-01 (PF 0.75). Falla M1 y M2. El problema es el edge de la seÃ±al, no el risk management. |
| E | ATR Compression + Taker Buy Ratio | 1h | BTC, ETH, BNB | âŒ BTC 0/54, ETH 9/54, BNB 0/54 | N/A (M4 rechazado) | N/A | âŒ | TBR no aÃ±ade edge cross-asset en 1h. ETH tiene seÃ±al parcial (TBR=0.55, hold=4-6b, Sharpe ~0.8) pero estadÃ­sticamente dÃ©bil (30-40 trades/4 aÃ±os). BTC y BNB: 0 configs. El filtro TBR estrecha tanto la seÃ±al que no hay frecuencia suficiente. Script: `Trading.Research/m4_atr_tbr.py`. |

| E | OFI Momentum | 1h | BTC, ETH, SOL | âŒ 0/27 configs (0%) â€” Sharpe negativo en todos. Mejor: BTC +0.383 (window=24, thr=0.75, hold=8) | N/A (M4 rechazado) | N/A | âŒ | OFI en top percentil no predice continuaciÃ³n en 1h. Contrariamente, Short cuando OFI bajo da Sharpe -1.2 (implica que precio SUBE despuÃ©s de venta agresiva). Script: `Trading.Research/m4_ofi_momentum.py`. |
| E | OFI Contrarian (Long-only) | 1h | BTC, ETH, SOL | âœ… 25/27 configs. Mejor: BTC +0.869, ETH +1.475, SOL +1.367 (window=24, thr=0.85, hold=8) | **0.503** (IS 2021-2024) | 44% IS / 36% OOS | âŒ | Hito G OOS FALLA. OOS 2025-2026Q1: Sharpe=-0.703, Net=-12.7%, PF=0.84. Win rate colapsÃ³ 44%â†’36%. P(Sharpe<0)=77%. Edge ligado al bull market 2021-2024; no generaliza. Eliminada. Ver ADR-039. |

---

## Hipotesis nuevas post-deuda #1 (datos correctos, pipeline ADR-053)

### Eje 1 — Cross-Sectional Order-Flow (2026-06-25) — RECHAZADA M4

| Eje | Hipotesis | TF | Activos | M4 IS Sharpe | Win Rate IS | Beta BTC | Estado |
|---|---|---|---|---|---|---|---|
| 1 (cross-sectional) | Long z-score maximo de flujo, Short minimo — dollar-neutral | 1h | BTC, ETH, SOL | -13.97 (mejor config) | 15-22% (todas configs) | ~0 (PASA) | RECHAZADA M4 |

**Grid:** metric in {ofi, cvd_delta, buy_sell_ratio, composite}, W in {24,48,96}, N in {4,6,8} — 36 configs. IS 2021-2024.
**Gate pre-registrado:** Sharpe >= 0.5, >= 2/3 configs con Sharpe > 0, |beta BTC| < 0.3. **0/3 criterios pasados** (solo beta neutro).
**OOS:** no corrido (gate IS fallo — touch-once por diseno).
**Script:** `Trading.Research/m4_cross_sectional_flow.py`

**Diagnostico:** la mecanica de neutralidad funciona (beta ~0 en todas las configs). El z-score de flujo por activo SI tiene senial, pero invertida: win rate consistentemente 15-22% en TODO el grid (no aleatorio — sistematicamente elige al activo que underperforma). La hipotesis de continuacion de flujo cruzada es incorrecta; lo que hay es mean reversion cross-sectional (el activo con mayor presion compradora inusual tiende a revertir). No es un rescate: seria una hipotesis nueva (long el z-score minimo, short el maximo). Anotada como posible Eje 1b a evaluar en el futuro.

---

## Hito G — RECHAZO por lookahead de apareo (2026-06-24)

**Las dos estrategias aprobadas por Hito G se rechazan: su aprobación fue un artefacto de un bug de apareo de features (lookahead de 1 hora) en el camino QC IS/OOS, no edge real.**

| Hito | Estrategia | TF | Activos | Backtest OLD (lookahead) | Backtest NEW (corregido, ADR-053) | Estado |
|---|---|---|---|---|---|---|
| G | TradeSizeInstitutionalStrategy (H5) | 1h | BTC,ETH,SOL | Sharpe 6.645 / Net +1384% / Win 60% | Sharpe -0.289 / Net +2.4% / Win 48% | RECHAZADA |
| G | CvdSellExhaustionStrategy (H3) | 1h | BTC,ETH,SOL | Sharpe 2.193 / Net +129% / Win 61% | Sharpe -1.224 / Net -7.9% / Win 45% | RECHAZADA |

Root cause: el `MarketBarMapper` viejo seteaba `marketBar.TimestampUtc = TradeBar.EndTime` (fin de barra), pero el `MicrostructureRegistry` indexa por el INICIO. `GetBar(EndTime)` devolvia las features de la barra SIGUIENTE -> el precio de la hora t se evaluaba contra el flujo de la hora t+1 (sesgo forward-looking). Un Sharpe 6.6 con +1384% en 4 anios es la firma del lookahead. ADR-053 unifico el camino de datos y corrigio el apareo; sobre el camino correcto, ninguna tiene edge (verificado con backtest viejo vs nuevo, misma data/estrategia/periodo). Ver ADR-054 (el bug de apareo) y ADR-053.

**Implicacion de alcance:** el bug vivia en la capa QC (C#), no en M4 (Python, apareo correcto). El lookahead **INFLABA** (usa informacion del futuro), no penalizaba: las aprobadas (H3/H5) eran artefactos; una rechazada-con-lookahead fue favorecida y aun asi fallo (sin rescate). **OfiContrarian NO tenia el lookahead** — su `GetBar(TimestampUtc.AddHours(-1))` cancelaba exactamente la convencion `EndTime` del camino viejo, dejandola correctamente apareada; su rechazo OOS (-0.703) es **genuino**, no artefacto. **Deuda #1 (re-validacion) CERRADA por analisis de costura, sin re-correr** (2026-06-24): no hay falsos negativos del bug; el camino es generar hipotesis NUEVAS sobre datos correctos, no re-validar viejas. Ver ADR-054 (correccion del radio-de-impacto) y banner de ROADMAP.

## Notas por experimento

### DonchianBreakoutStrategy
- HipÃ³tesis: breakout de canal Donchian en 4h predice inicio de tendencia (Li et al. arXiv 2512.02227).
- Lookback 20 barras: Sharpe -1.742, Win 24%. Falsas rupturas sistemÃ¡ticas en BTC 4h.
- Lookback 126 barras: Sharpe -2.623, Win 13%. El lookback mÃ¡s largo empeorÃ³ los resultados.
- El M4 usÃ³ datos mensuales (seÃ±al de 12 meses, Sharpe +0.705) â€” el mecanismo validado no es el mismo que se implementÃ³ en 4h.
- Fade del Donchian tambiÃ©n descartado: M4 negativo en 27/27 combinaciones (3 lookbacks Ã— 3 holds Ã— 3 activos).

### IntradayMomentumStrategy (Shen, Urquhart & Wang, Financial Review 2022)
- HipÃ³tesis: primera barra 30m del dÃ­a UTC (00:00-00:30) predice direcciÃ³n de la Ãºltima (23:30-00:00).
- M4 sobre Binance 2020-2024: ETH +0.645, BNB +0.691, BTC -0.204. BTC excluido por adopciÃ³n institucional post-2021.
- V1 (entrada en bar_0, MaxBars 46): Sharpe -1.394. Error de diseÃ±o â€” las 23h de holding destruyen el edge puntual.
- V2 (entrada en bar_47, MaxBars 1): Sharpe -3.28, Win 36%. OPS-2 disparÃ³ a 2025-04-27.
- El efecto documentado en el paper (datos 2013-2020) ya no existe en el mercado de 2025.

### BollingerBandsStrategy (Connors Research variaciÃ³n #217, adaptada a 4h)
- HipÃ³tesis: oversold en Bollinger Bands (%b < 0) durante N barras consecutivas predice reversiÃ³n (M4 pure signal test).
- M4 Binance 4h 2020-2025 en BTC/ETH/BNB: 5/9 configs pasan M4 (Sharpe >= 0.5), fallando el threshold 66.7%.
  - BTC: oversold=1 (Sharpe +0.822, 642 trades), oversold=4 (+0.573, 57 trades) â€” PASS. oversold=2 (-0.066) â€” FAIL.
  - ETH: todos fallan (oversold=1: +0.276, oversold=2: +0.324, oversold=4: -0.851).
  - BNB: todos pasan (oversold=1: +0.848, oversold=2: +0.620, oversold=4: +1.896).
- Inconsistencia cross-asset: ETH sin edge claro, BTC parcial, BNB fuerte. No hay configuraciÃ³n que funcione uniformemente.
- CÃ³digo no bugueado pre-V1 tenÃ­a defecto `InPosition` que bloqueaba re-entradas. Fix aplicado pero hipÃ³tesis rechazada por M4.

### H3 â€” Lead-lag BTCâ†’ETH/BNB (hipÃ³tesis pura, sin implementaciÃ³n)
- HipÃ³tesis: retorno BTC(t-1) > umbral predice direcciÃ³n ETH/BNB en la barra t. Flujos institucionales entran por BTC primero.
- M4 Binance 1h 2020-2025 sobre ETH y BNB, thresholds 0.5/1.0/1.5%, hold 4 barras, bidireccional (Long + Short):
  - ETH: Sharpe -0.569 / -0.785 / -0.820. Win rate 47.6 / 47.2 / 47.4%. FAIL en los 3 configs.
  - BNB: Sharpe -0.945 / -0.599 / -0.489. Win rate 47.5 / 46.7 / 47.8%. FAIL en los 3 configs.
- Win rate consistentemente ~47% en todos los 6 configs y ambos activos â€” la seÃ±al es ligeramente contraria a la hipÃ³tesis.
- DiagnÃ³stico: la correlaciÃ³n BTC-ETH/BNB es contemporÃ¡nea (misma barra 1h), no hay lag de 1 barra explotable.
  El efecto lead-lag documentado en literatura existe en timeframes de segundos/minutos (microestructura), no en 1h.
- Script: `Trading.Research/m4_lead_lag_btc_eth.py`

### H1 â€” RSI Mean Reversion condicionado por HMM Squeeze
- HipÃ³tesis: RSI(14) < umbral en rÃ©gimen HMM Squeeze (baja vol, sin trend) filtra seÃ±ales falsas de oversold durante downtrends fuertes.
- M4 Binance 4h 2020-2025 sobre BTC/ETH/BNB, thresholds RSI 25/30/35, hold 8/12 barras:
  - Win rate alto: 55-69% en casi todos los configs. El condicionamiento HMM sÃ­ filtra algo.
  - Sharpe negativo en todos los 18 configs (0/18). Mejor resultado: ETH RSI<25 hold=8b â†’ Sharpe +0.343 con 19 trades.
  - Problema estructural: RSI<25 en Squeeze produce 12-19 trades en 5 aÃ±os (~3/aÃ±o). Insuficiente frecuencia para compensar la varianza de los retornos por trade.
  - El retorno medio por trade es positivo en los thresholds mÃ¡s extremos pero la std es ~3-5x el mean â†’ Sharpe inevitablemente bajo.
- ClasificaciÃ³n de rÃ©gimen: centroide mÃ¡s cercano en espacio de features HMM escaladas (replica exacta de FeatureExtractor.cs). BNB usa modelo BTC como proxy.
- Script: `Trading.Research/m4_rsi_hmm_squeeze.py`

### H2 â€” ATR Compression Breakout
- HipÃ³tesis: el mercado alterna entre fases de compresiÃ³n (ATR bajo) y expansiÃ³n. Un rompimiento de rango durante compresiÃ³n predice un movimiento direccional significativo.
- M4 Binance 4h 2020-2025 sobre BTC/ETH/BNB, grid: ATR<P25/P35, lookback=10/20b, hold=4/8b (8 configs):
  - BTC: 1/8, ETH: 2/8. Gate falla. DiagnÃ³stico: hold=8 destruye la seÃ±al.
- DiagnÃ³stico A: grid reducido hold=[2,3,4], ATR=[P15/P20/P25], lookback=10 (9 configs):
  - BTC: 6/9 âœ…, ETH: 5/9 âœ…, BNB: 7/9 âœ… â€” Gate pasado.
  - hold=3 (12h) pasa cross-asset sin excepciÃ³n: BTC +0.565, ETH +0.703, BNB +0.984 (Sharpe).
  - hold=4 (16h) tambiÃ©n pasa: BTC +0.822, ETH +0.659, BNB +0.670.
  - hold=2 (8h) falla â€” edge no se materializÃ³ todavÃ­a.
- ParÃ¡metros nominales: ATR<P20, lookback=10, hold=3 barras 4h (12h).
- ImplementaciÃ³n: `AtrCompressionBreakoutStrategy.cs`. BTCUSDT 4h, MaxBars=3, CombineWithTimeExit=true.
- Scripts: `Trading.Research/m4_atr_compression_breakout.py` (grid original), diagnÃ³stico A inline en sesiÃ³n.
- **Backtest QC (2025, BTC/ETH/BNB, SL 2% fijo, TP 4%):**
  - Sharpe: -0.922. Win Rate: 37%. DD mÃ¡ximo: 30.3% (kill switch disparÃ³ 2025-03-19).
  - Causa raÃ­z: SL 2% fijo se activa durante la volatilidad intraday de las 12h de hold, antes de que el edge se materialice. Ver ADR-035.
- **Backtest QC (2025, ATR SL 2.5Ã—, TP 3.5Ã—) â€” Candidata 9:**
  - Sharpe: -0.779. Win Rate: 37%. DD mÃ¡ximo: 18.3%. Net Profit: -6.813%. Total Orders: 554.
  - OPS-2 disparÃ³ BTC 2025-07-27 (PF rolling 0.70 sostenido 10 trades), ETH 2025-12-01 (PF rolling 0.75).
  - ATR SL mejora Sharpe (-0.922â†’-0.779) y DD (30.3%â†’18.3%), pero no resuelve el win rate: 37% en ambas variantes.
  - DiagnÃ³stico final: con 63% loss rate y P/L ratio 1.55, la expectancia es negativa. La compresiÃ³n ATR no predice con suficiente precisiÃ³n la direcciÃ³n del breakout en 4h. El problema es la seÃ±al, no el risk management.
  - **Estrategia eliminada** â€” rechazada por M1 (Sharpe -0.779 < 0.5) y M2 (Win Rate 37% < 40%). Ver ADR-036.

### ATR Compression + Taker Buy Ratio (1h)
- HipÃ³tesis: el TBR (taker_buy_base_vol / total_vol) confirma la direccionalidad del breakout, filtrando fakeouts donde el precio rompe pero el flujo agresivo no acompaÃ±a.
- M4 Binance 1h 2021-2024 sobre BTC/ETH/BNB, grid 54 configs (3 ATR Ã— 2 lookback Ã— 3 hold Ã— 3 TBR_thr):
  - BTC: 0/54 (0%). Mejor: P25/look=10b/hold=3b/TBR=0.55 â†’ Sharpe +0.386.
  - ETH: 9/54 (16.7%). Mejores: P15/look=10b/hold=4b/TBR=0.55 â†’ Sharpe +0.829, T=36.
  - BNB: 0/54 (0%). "Mejores" con TBR=0.58 tienen solo 11 trades â€” ruido estadÃ­stico.
- DiagnÃ³stico: TBR_thr bajo (0.52) â†’ muchas seÃ±ales pero Sharpe negativo en BTC/BNB. TBR_thr alto (0.55-0.58) â†’ seÃ±ales tan raras que no hay poder estadÃ­stico. El filtro TBR no aÃ±ade valor cross-asset en 1h.
- ETH tiene edge local pero los mejores configs tienen 30-40 trades/4 aÃ±os â€” insuficiente para conclusiÃ³n robusta.
- Script: `Trading.Research/m4_atr_tbr.py`.

### CVD Bullish Divergence (CvdBullishDivergenceStrategy)
- HipÃ³tesis: cuando precio hace nuevo mÃ­nimo N-barra pero CVD no lo confirma, hay buying hidden â†’ Long.
- M4 IS 2021-2024 (Long-only): 9/9 configs pasan, BTC +1.74 / ETH +0.52 / SOL +0.95 (3/3 activos). lookback=24, hold=6.
- Implementada: `CvdBullishDivergenceStrategy.cs`, 10 tests unitarios, registrada en StrategyFactory.
- Backtest QC IS 2021-2024 (SL=10%, TP=15%, MaxBars=6, sin OPS-2): Sharpe -1.854, DD 30.7%, expectancy -0.022%/trade.
- Root cause del gap M4â†”QC: M4 trata cada seÃ±al como trade independiente aunque sean solapadas. Durante una caÃ­da de precio de 12h donde CVD se recupera gradualmente, M4 registra ~12 trades de entrada a distintos precios; QC entra solo una vez y mantiene la posiciÃ³n. Los "12 trades" de M4 en la recuperaciÃ³n tienen todos buen retorno â†’ Sharpe inflado. QC captura solo el primero. La expectancy trade-by-trade es negativa: Average Win 0.15% Ã— 48% win rate + Average Loss -0.18% Ã— 52% = -0.022% por trade.
- ConclusiÃ³n: el edge observado en M4 era un artefacto metodolÃ³gico de seÃ±ales solapadas, no alpha real.
- Scripts: `Trading.Research/m4_cvd_divergence.py`.

### Funding Rate Positioning (FRP)
- HipÃ³tesis: z-score del funding rate (ventanas 14/30/60d) en extremo â†’ mercado overcrowded â†’ seÃ±al contraria.
- Test 1 bidireccional (BTC/ETH/SOL, diario, 2020-2025, 54 configs): BTC 8/54, ETH 0/54, SOL 11/54. Gate: 27/54 en 2/3 activos â€” FAIL.
- Test 2 SHORT-only (crowded longs â†’ short): BTC 0/54, ETH 3/54, SOL 5/54. Peor que bidireccional â€” FAIL.
- Hallazgo clave: el edge en BTC estaba en el lado LONG (funding muy negativo â†’ short squeeze â†’ precio sube), no en el SHORT como postulaba la teorÃ­a.
- El filtro de tendencia (MA) es condiciÃ³n necesaria pero no suficiente: sin MA, todas las configs son negativas.
- Ventana de 60d sistemÃ¡ticamente peor: demasiado lenta para capturar eventos de crowding.
- ETH sin seÃ±al en ninguna direcciÃ³n, posiblemente por cambio estructural post-Merge (2022).
- DiagnÃ³stico: el mecanismo de desapalancamiento existe pero opera en timeframes sub-diarios (horas), donde los datos histÃ³ricos de order book/funding granular no estÃ¡n disponibles sin costo.
- Script: `Trading.Research/m4_funding_rate_positioning.py`

### OFI Momentum (candidata A â€” rechazada M4)
- HipÃ³tesis: OFI en top percentil del historial reciente indica compra institucional agresiva â†’ precio continÃºa subiendo (momentum de flujo).
- M4 IS 2021-2024 (1h, BTC/ETH/SOL, grid 27 configs: window=[24,48,96], thr=[0.75,0.80,0.85], hold=[4,6,8]):
  - 0/27 configs pasan el gate. Sharpe negativo en casi todos.
- DiagnÃ³stico direccional (window=48, thr=0.80, hold=6):
  - Short cuando OFI bajo (vendedores agresivos): BTC -0.220, ETH -0.935, SOL -1.203 â†’ precio SUBE despuÃ©s de venta agresiva.
  - Long cuando OFI alto (compradores agresivos): BTC +0.010, ETH +0.120, SOL +0.775 â†’ edge dÃ©bil, solo SOL relevante.
- Hallazgo: el OFI en 1h es mean-reverting, no momentum. Buying pressure gets absorbed (precio ya subiÃ³ con la compra agresiva). La seÃ±al contraria (buy the dip after heavy selling) tiene mucho mÃ¡s edge.
- Script: `Trading.Research/m4_ofi_momentum.py`

---

## HipÃ³tesis de Microestructura (Hito E â€” batch 2, sesiÃ³n 2026-06-11)

EvaluaciÃ³n de 10 hipÃ³tesis basadas en datos AggTrades (OFI, CVD, ArrivalRate, MeanTradeSize, BuySellRatio, PriceReturn).
PerÃ­odo IS: 2021-2024. PerÃ­odo OOS: 2025-2026-06-09. Activos: BTCUSDT, ETHUSDT, SOLUSDT. Timeframe: 1h.

| ID | HipÃ³tesis | ImplementaciÃ³n | M4 Sharpe (BTC/ETH/SOL) | QC IS Sharpe | QC OOS Sharpe | Estado |
|---|---|---|---|---|---|---|
| H1 | VWAP Deviation â€” Long cuando (close-vwap)/vwap < -1.5% | VwapDeviationStrategy | âœ… PASS (â‰¥2/3) | -0.369 | â€” | âŒ FAIL IS |
| H2 | Trade Count Spike â€” Long cuando ArrivalRate en P95 y PriceReturn plano | TradeCountSpikeStrategy | âœ… PASS (â‰¥2/3) | -1.553 | â€” | âŒ FAIL IS |
| H3 | CVD Sell Exhaustion â€” Long cuando close=min(47b) y CvdDelta>0 | CvdSellExhaustionStrategy | âœ… PASS (â‰¥2/3) | 2.178 | 1.718 | âœ… APROBADA |
| H4 | CVD Structure Shift â€” Long cuando CVD cambia de negativo a positivo | â€” (M4 FAIL) | âŒ FAIL | â€” | â€” | âŒ |
| H5 | Trade Size Institutional â€” Long cuando MeanTradeSize en P90 y BSR>1.02 | TradeSizeInstitutionalStrategy | âœ… PASS (â‰¥2/3) | 3.985 | 4.186 | âœ… APROBADA |
| H6 | CVD-OFI Divergence â€” Long cuando CVD positivo pero OFI negativo | â€” (M4 FAIL) | âŒ FAIL | â€” | â€” | âŒ |
| H7 | Arrival Rate Momentum â€” Long cuando ArrivalRate acelerando | â€” (M4 FAIL) | âŒ FAIL | â€” | â€” | âŒ |
| H8 | Bid-Ask Imbalance â€” Long cuando BuySellRatio en percentil extremo | â€” (M4 FAIL) | âŒ FAIL | â€” | â€” | âŒ |
| H9 | Trade Count Spike Short â€” Short cuando ArrivalRate spike y return positivo | â€” (M4 FAIL) | âŒ FAIL | â€” | â€” | âŒ |
| H10 | Selling Climax â€” Long cuando SellingPressure extrema (ArrivalRate+return<-0.3%) | SellingClimaxStrategy | âœ… PASS (â‰¥2/3) | -5.128 (SL=30%) | â€” | âŒ FAIL IS |

### Notas por hipÃ³tesis

**H1 â€” VwapDeviation**: Sin filtro de direcciÃ³n. Entra en "dips" que en enero 2021 eran caÃ­das de -10-14%. OPS-2/ConsecutiveLossesMonitor la mata antes de que pueda recuperar. Sharpe IS=-0.369, WR=50%.

**H2 â€” TradeCountSpike**: ArrivalRate spike + PriceReturn plano. El filtro `|return| < 0.5%` previene entradas en los crashes de enero 2021, pero la estrategia igual fue matada por OPS-2 en febrero 2022. Sharpe IS=-1.553, WR=58%. No hay direcciÃ³n de flujo que proteja.

**H3 â€” CvdSellExhaustion**: CondiciÃ³n `close â‰¤ min(47b)` AND `CvdDelta > 0` proporciona dos filtros naturales: (a) precio en mÃ­nimo local genuino, (b) flujo neto positivo (compradores superan vendedores netos). El CvdDelta > 0 durante crashes donde los vendedores dominan protege de las caÃ­das de enero 2021. OPS-2 matÃ³ ETHUSDT en sep-2023 y BTC/SOL en ene-2022, pero antes habÃ­an sido rentables. IS Sharpe=2.178, OOS Sharpe=1.718, CAGR OOS=30.4%, P(Sharpe<0)=1%. **APROBADA Hito G**.

**H5 â€” TradeSizeInstitutional**: MeanTradeSize en P90 + BuySellRatio > 1.02. El filtro BSR > 1.02 previene entrada cuando hay presiÃ³n vendedora neta â€” mecanismo de protecciÃ³n clave durante el crash de enero 2021 (BSR colapsa <1 durante pÃ¡nico). IS Sharpe=3.985, OOS Sharpe=4.186 (OOS mejor que IS â€” seÃ±al robusta), CAGR OOS=97%, MaxDD=5.9%, P(Sharpe<0)=0%. **APROBADA Hito G â€” resultado extraordinario**.

**H10 â€” SellingClimax**: ArrivalRate spike + PriceReturn < -0.3%. Contrarian puro sin filtro de flujo. Con SL=5%: Sharpe=-3.183 (caÃ­das de enero 2021 >5% causan stop masivos). Con SL=30%: Sharpe=-5.128 (permite pÃ©rdidas enormes en crashes). No existe SL viable para este tipo de seÃ±al contrarian sin filtro de flujo.

**H4/H6/H7/H8/H9**: Rechazadas en M4. HipÃ³tesis de momentum de CVD/OFI/ArrivalRate o bid-ask imbalance no mostraron Sharpe â‰¥ 0.5 en â‰¥2/3 activos en el grid de parÃ¡metros evaluado (scripts: `Trading.Research/m4_micro_*.py`).

### OFI Contrarian â€” Long-only (candidata B â€” APROBADA QC IS)
- HipÃ³tesis: cuando OFI estÃ¡ en el percentil inferior de su distribuciÃ³n reciente (vendedores agresivos), el mercado estÃ¡ sobre-vendido localmente y el precio rebota en las prÃ³ximas N horas.
- M4 IS 2021-2024 (1h, BTC/ETH/SOL, grid 27 configs: window=[24,48,96], thr=[0.75,0.80,0.85], hold=[4,6,8]):
  - 25/27 configs pasan el gate (93%).
  - Mejor: window=24, threshold=0.85, hold=8: BTC +0.869, ETH +1.475, SOL +1.367 (media +1.237).
- AnÃ¡lisis anual (window=24, thr=0.85, hold=8):
  - 2021: BTC +1.353, ETH +2.618, SOL +2.793 (bull market fuerte)
  - 2022: BTC -0.863, ETH +0.919, SOL -0.586 (bear market: Long-only pierde en BTC/SOL)
  - 2023: BTC +1.653, ETH +0.844, SOL +1.863 (recovery)
  - 2024: BTC +1.744, ETH +1.100, SOL +1.126 (bull market)
- Win rate M4: 50-52%, expectancy BTC +0.060%/trade, ETH +0.130%, SOL +0.179%.
- **QC IS 2021-2024 (SL=10%, TP=15%, MaxBars=8, Risk=1%, NullStrategyHealthMonitor):**
  - Sharpe: **0.503** â€” PASA M1 (â‰¥0.5). Sortino: 0.702.
  - CAGR: 11.69%, Net Profit: +55.7% ($100kâ†’$155,663).
  - Max Drawdown: **41.1%** (alto; esperado con 3 activos cripto correlacionados).
  - Win Rate: 44%, Avg Win: +1.48%, Avg Loss: -1.01%, P/L Ratio: 1.46.
  - Trades cerrados: 640, expectancy portfolio: +0.078.
  - Kill switch: 1 vez en 2024-08-05 (8 pÃ©rdidas consecutivas), cooling-off 1 dÃ­a. Luego recuperÃ³.
  - Fees: â‚®1.28 fijo (negligible); slippage 0.2% round-trip ya embedido en fills.
  - Estado backtest: Completed (2021-01-01 â†’ 2024-12-31).
- Implementada: `OfiContrarianStrategy.cs`, 7 tests unitarios, registrada en StrategyFactory.
- Scripts: `Trading.Research/m4_ofi_contrarian.py`, `Trading.Research/m4_ofi_momentum.py`.
- ADR: ADR-038.

