# Strategy Experiments Log

Registro de hipótesis evaluadas por Fase 0. Fuente de verdad para evitar re-explorar candidatas ya descartadas.

| Hito | Estrategia | TF | Activos | M4 | Backtest Sharpe | Win Rate | Estado | Razón descarte |
|---|---|---|---|---|---|---|---|---|
| E | CVD Divergence bidireccional | 1h | BTC, ETH, SOL | âŒ 0/9 configs (0%) bidireccional. Long-only: ✅ 9/9 configs, BTC +1.74 / ETH +0.52 / SOL +0.95 (3/3) | N/A — ver CvdBullishDivergenceStrategy | N/A | ✅→ CvdBullishDivergenceStrategy implementada | Bidireccional falla: Short signals destruyen el Sharpe en ETH y SOL (SOL Short -0.92). Long-only pasa 3/3 activos con lookback=24, hold=6. Implementado como estrategia Long-only separada. Script: `Trading.Research/m4_cvd_divergence.py`. |
| E | CvdBullishDivergenceStrategy | 1h | BTC, ETH, SOL | ✅ BTC +1.74, ETH +0.52, SOL +0.95 (lookback=24, hold=6 — 3/3 activos) | -1.85 (IS 2021-2024, sin OPS-2, SL=10%) | 48% | âŒ | M4 pasó pero QC IS falla. Root cause: M4 contaba señales solapadas como trades independientes — inflación artificial del Sharpe. QC solo entra una vez por posición. Expectancy trade-level negativa (-0.022%/trade). Eliminada. Ver nota. |
| E | DonchianBreakoutStrategy | 4h | BTC | âš ï¸ +0.705 (señal mensual — escala incorrecta) | -2.623 (lookback 126) | 13% | âŒ | Win rate 13-24% con cualquier lookback. Desconexión de escala entre M4 (mensual) e implementación (4h). |
| E | IntradayMomentumStrategy | 30m | ETH, BNB, BTC | ✅ ETH +0.645, BNB +0.691 / âŒ BTC -0.204 | -3.28 (ETH OOS 2025) | 36% | âŒ | Edge arbitrado por institucionales en 2025. M4 validó 2020-2024; OOS falla M1+M2. |
| E | BollingerBandsStrategy | 4h | BTC, ETH, BNB | âŒ 5/9 configs (55.6%) | N/A (M4 rechazado) | N/A | âŒ | M4 falla: BTC OK en oversold=1,4; ETH falla todas; BNB OK todas. Inconsistencia cross-asset. |
| E | H3 — Lead-lag BTC→ETH/BNB | 1h | BTC (señal), ETH, BNB | âŒ 0/6 configs (0%) | N/A (M4 rechazado) | ~47% | âŒ | Win rate sistemáticamente < 50% en todos los thresholds (0.5/1.0/1.5%) y ambos activos. Correlación BTC-ETH/BNB ocurre en la misma barra (simultaneidad), no con lag de 1 barra. Edge ya arbitrado. |
| E | H1 — RSI(14) + HMM Squeeze | 4h | BTC, ETH, BNB | âŒ 0/18 configs (0%) | N/A (M4 rechazado) | 55-69% | âŒ | Win rate alto (55-69%) pero Sharpe negativo — retornos perdedores superan en magnitud a los ganadores. Muy pocas señales (RSI<25 en Squeeze: 12-19 trades/5 años = insuficiente poder estadístico). Sin edge explotable. |
| E | Funding Rate Positioning (FRP) | Diario | BTC, ETH, SOL | âŒ Bidireccional: BTC 8/54, ETH 0/54, SOL 11/54. SHORT-only: BTC 0/54, ETH 3/54, SOL 5/54 | N/A (M4 rechazado) | N/A | âŒ | Señal no generalizable cross-asset. BTC tiene edge en el lado LONG (crowded shorts → squeeze alcista), no en el SHORT. ETH resistente a cualquier señal de funding. El mecanismo existe pero opera en timeframes intraday, no diarios. |
| E | H2 — ATR Compression Breakout | 4h | BTC, ETH, BNB | ✅ BTC 6/9, ETH 5/9, BNB 7/9 | -0.922 (BTC, Sharpe) | 37% | âŒ | M4 pasado. Backtest con SL 2%: Sharpe -0.922, Win 37%, DD 30.3% (kill switch 2025-03-19). SL 2% fijo mata el edge antes de que se materialice. Ver ADR-035. |
| E | ATR Compression + SL ATR (2.5×, TP 3.5×) | 4h | BTC, ETH, BNB | ✅ (H2) | -0.779 | 37% | âŒ | ATR SL mejora DD (30.3%→18.3%) y Sharpe (-0.922→-0.779) pero win rate permanece 37%. OPS-2 disparó BTC 2025-07-27 (PF 0.70), ETH 2025-12-01 (PF 0.75). Falla M1 y M2. El problema es el edge de la señal, no el risk management. |
| E | ATR Compression + Taker Buy Ratio | 1h | BTC, ETH, BNB | âŒ BTC 0/54, ETH 9/54, BNB 0/54 | N/A (M4 rechazado) | N/A | âŒ | TBR no añade edge cross-asset en 1h. ETH tiene señal parcial (TBR=0.55, hold=4-6b, Sharpe ~0.8) pero estadísticamente débil (30-40 trades/4 años). BTC y BNB: 0 configs. El filtro TBR estrecha tanto la señal que no hay frecuencia suficiente. Script: `Trading.Research/m4_atr_tbr.py`. |

| E | OFI Momentum | 1h | BTC, ETH, SOL | âŒ 0/27 configs (0%) — Sharpe negativo en todos. Mejor: BTC +0.383 (window=24, thr=0.75, hold=8) | N/A (M4 rechazado) | N/A | âŒ | OFI en top percentil no predice continuación en 1h. Contrariamente, Short cuando OFI bajo da Sharpe -1.2 (implica que precio SUBE después de venta agresiva). Script: `Trading.Research/m4_ofi_momentum.py`. |
| E | OFI Contrarian (Long-only) | 1h | BTC, ETH, SOL | ✅ 25/27 configs. Mejor: BTC +0.869, ETH +1.475, SOL +1.367 (window=24, thr=0.85, hold=8) | **0.503** (IS 2021-2024) | 44% IS / 36% OOS | âŒ | Hito G OOS FALLA. OOS 2025-2026Q1: Sharpe=-0.703, Net=-12.7%, PF=0.84. Win rate colapsó 44%→36%. P(Sharpe<0)=77%. Edge ligado al bull market 2021-2024; no generaliza. Eliminada. Ver ADR-039. |

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

**Diagnostico:** la mecanica de neutralidad funciona (beta ~0 en todas las configs). El z-score de flujo por activo SI tiene senial, pero invertida: win rate consistentemente 15-22% en TODO el grid (no aleatorio — sistematicamente elige al activo que underperforma). La hipotesis de continuacion de flujo cruzada es incorrecta; lo que hay es mean reversion cross-sectional (el activo con mayor presion compradora inusual tiende a revertir). No es un rescate: seria una hipotesis nueva (long el z-score minimo, short el maximo). Ver Eje 1b.

### Eje 1b — Cross-Sectional Invertido (2026-06-25) — RECHAZADO, limitacion estructural de costos

Signal invertida: long z-score MINIMO, short z-score MAXIMO. Resultado: win rate after-cost ~21% — IDENTICO al eje 1 (~22%). Sharpe IS -14.4, net -100%.

**Root cause:** los costos de rebalanceo (0.56% por bloque = fee+slippage x 2 piernas) destruyen la señal antes de medirla. A N=8 (8h hold), eso equivale a ~6% anual solo en costos. El retorno bruto del long-short es ~0.1-0.2% por bloque → ratio señal/costo de ~20-35% del break-even. El gross signal SI existe (eje 1 gross loss 78% implica eje 1b gross win 78%), pero es demasiado pequeño para superar los costos a esta frecuencia.

**Implicacion para sub-hora:** el cross-sectional tiene la misma limitacion estructural en 5m/15m — el costo por bloque es identico en absoluto (0.56%) pero los bloques son mas cortos = mas rebalanceos = peor. El cross-sectional requeriria costos institucionales (~10-20x menores) o una señal mucho mas fuerte. **El eje 1b cierra el cross-sectional definitivamente en 1h y sub-hora.**

### Eje 2 — OFI Contrarian condicionado por regimen HMM (2026-06-25) — RECHAZADA M4

| Eje | Hipotesis | TF | Activos | Baseline IS (sin filtro) | Mejor config filtrada | Estado |
|---|---|---|---|---|---|---|
| 2 (regimen) | OFI Contrarian (Long OFI bottom-15%) gateado por regimen HMM 4h | 1h | BTC, ETH, SOL | Sharpe medio 1.188 (hold=8) | Trend+hold=6: Sharpe 0.827 (-0.36 vs baseline) | RECHAZADA M4 |

**Grid:** compatible_regimes in {all, MeanRev, Trend, MR+Trend, MR+Squeeze}, hold in {4,6,8} — 15 configs. IS 2021-2024.
**Gate pre-registrado:** Sharpe filtrado >= 0.5 AND mejora > 0.15 vs baseline AND >= 2 activos con trades >= 30.
**Resultado:** Sharpe PASA (0.827) pero mejora FALLA (−0.361 — el filtro DEGRADA). OOS no corrido.
**Script:** `Trading.Research/m4_ofi_regime.py`

**Diagnostico:**
- El regimen NO aisle el edge — el OFI Contrarian funciona en TODOS los regimenes. Filtrar a Trend descarta señales validas en Squeeze (32-37% del IS BTC/ETH) y HighVol. La hipotesis "el rebote post-exhaustion-of-sellers vive en ciertos regimenes" es falsa.
- MeanReverting: 0 trades en los 3 activos. El HMM 4h casi nunca clasifica cripto en 2021-2024 como MeanReverting — domina Trend (52-56%) + Squeeze (32-37%). El régimen MeanReverting prácticamente no existe en este universo/período.
- SOLUSDT con proxy BTC: HighVolatility 51% (el modelo BTC no mapea bien a SOL; el proxy genera distribución de regimenes distorsionada).
- El edge IS de OFI Contrarian (Sharpe ~1.19 sin costos) es real pero no sobrevive OOS por razones que el regimen no explica. Probablemente beta de bull-market IS vs bear/flat OOS 2025.

### Eje 3 — OFI Contrarian sub-hora 5m/15m — RECHAZADA (limitacion estructural de costos)

**2026-06-25 (M4 a costo 0):** 54/54 configs PASS — ver tabla original abajo.
**2026-06-26 (Capa A ADR-056, costos reales):** 0/54 configs PASS. Eje rechazado.

| Eje | TF | Activos | Mejor config IS (sin costos) | Sharpe BTC/ETH/SOL sin costos | configs PASS sin costos | Estado |
|---|---|---|---|---|---|---|
| 3 (sub-hora 5m) | 5m | BTC, ETH, SOL | window=48, thr=0.85, hold=6 | +1.553 / +1.012 / +1.923 | 27/27 (100%) | **RECHAZADA — muere por costos** |
| 3 (sub-hora 15m) | 15m | BTC, ETH, SOL | window=12, thr=0.75, hold=6 | +1.511 / +1.287 / +1.803 | 27/27 (100%) | **RECHAZADA — muere por costos** |

**Sensibilidad a costos (Capa A, ADR-056) — IS 2021-2024:**

| Costos RT | configs PASS | Sharpe tipico BTC 5m | Sharpe tipico BTC 15m |
|---|---|---|---|
| 0.000% (M4 original, COST_RT=0.0) | 54/54 (100%) | +1.0 a +1.5 | +1.0 a +1.5 |
| 0.120% (fee 0.04%+slip 0.02% por lado) | **0/54 (0%)** | -44 a -13 | -15 a -4 |

**Root cause:** la estrategia genera ~47 trades/dia en 5m (hasta 69k trades en 4 anos). Con 0.12% RT de costo, el breakeven del retorno medio por trade es ~0.12%, pero la media bruta IS es apenas ~0.015-0.025% (1-2 bps por barra de 5m o 15m). Ratio señal/costo: ~15-20% del break-even. El edge bruto existe pero es ~5-6x mas chico que los costos. El Sharpe con costos es catastroficamente negativo (peor que los ejes 1/1b).

**Comparacion con eje 1/1b:** los ejes cross-seccionales (ratio señal/costo ~20-35%) tenian el mismo problema pero menos agudo. El eje 3 sub-hora es peor: mas frecuencia = mas costos acumulados = peor ratio.

**OOS e IS/OOS/MC:** no corridos. El gate estadístico de costos IS falla categoricamente (Sharpe IS < -4 en todas las configs). Correr OOS o MC seria infructuoso — el eje muere en la primera capa.

**Veredicto:** RECHAZADA por limitacion estructural de costos. No procede a Capa B (implementacion Lean).

**Script M4 original (sin costos):** `Trading.Research/m4_ofi_contrarian_subhora.py`
**Harness Capa A (con costos):** `Trading.Research/layer_a_validate.py` + `ofi_contrarian_subhora_spec.json`
**Diagnostico original (2026-06-25):** el M4 usó COST_RT=0.0 (violacion del estandar ADR-040 de 0.04% RT). La señal es real pero mucho mas chica que los costos reales. El alto trade count (47/dia) es la causa raiz: mas trades = mas costos = el edge no alcanza.

---

## TS10 — TS1 Trend-following GATEADO por regimen HMM {Trend} — Capa A (2026-06-27) — NO-GO

**Config:** TS1 momentum lookback=48h, hold=24h, long-only, costos reales 0.12% RT. Gate = regimen `{Trend}` del HMM 4h nativo por activo (SOL con proxy BTC). IS 2021-2024 / OOS 2025-2026.

**Grilla pre-registrada:** 2 brazos (ungated vs Trend-gated), decidida por mecanismo (II.4-A), sin barrido de regimenes.

| Activo | Brazo | Sharpe IS | #Trades IS | Sharpe OOS | #Trades OOS |
|---|---|---:|---:|---:|---:|
| BTC | ungated | +0.216 | 1019 | −1.201 | 355 |
| BTC | gated{Trend} | +0.246 | 605 | −1.375 | 160 |
| ETH | ungated | +0.335 | 1027 | −0.522 | 355 |
| ETH | gated{Trend} | +0.019 | 600 | −0.734 | 212 |
| SOL (proxy BTC) | ungated | +1.512 | 993 | −1.426 | 343 |
| SOL (proxy BTC) | gated{Trend} | +0.106 | 480 | −0.695 | 250 |

**Distribucion de regimen IS (4h):** BTC 55% Trend / 33% Squeeze / 12% HighVol; ETH 53/36/11; SOL (proxy BTC) 44% Trend / 4% Squeeze / 52% HighVol (distorsionada — el modelo BTC no mapea SOL, ver eje-2).

**Veredicto:** 0/3 brazos pasan el gate IS (>=0.5 en >=2/3 activos). El gate HMM `{Trend}` **no rescata** trend: es **inerte en BTC** (d+0.03), **degrada ETH** (d-0.316) y **destruye SOL** (d-1.406). La prediccion de mecanismo de §IV (trend rinde mejor en regimen Trend) queda **falsada**. Es la segunda confirmacion independiente (n=2, con eje-2/OFI) de que el gating HMM no aisla edge en este universo. El clasificador no tiene estado MeanReverting (2/4 estados son Trend), por lo que no existe el "off" que el mecanismo requiere. El pulso IS de SOL (+1.51) era sesgo de activo / beta de bull-market (OOS -1.43), no mecanismo robusto cross-asset.

**Causalidad del gate:** verificada sin lookahead (resample `closed="right", label="right"`; la etiqueta del bloque 4h que cierra en T solo aplica a barras >= T).

**Cierre:** S1 trend-following DESCARTADA. No pasa a Capa B (C#). No se entrena HMM propio de SOL (seria un solo-passer / infra especulativa).

**Script:** `Trading.Research/layer_a_trend_s1.py` (funcion `_try_load_hmm_regime`). Exportador de regimenes: `Trading.Research/export_hmm_regimes.py`. CSVs: `Trading.Models/regime/hmm_regimes_{btc,eth,sol}usdt.csv`.

---

## S1 — Trend Following (2026-06-27) — RECHAZADO OOS universalmente

Familia de 10 hipótesis de tendencia (long-only, mecanismo subreacción/continuación). Evaluadas con Capa A ADR-056: costos 0.12% RT, IS 2021-2024, OOS 2025-01→2026-06-09, activos BTC/ETH/SOL.

**Script:** `Trading.Research/layer_a_trend_s1.py`
**Grilla:** 38 configs totales × 3 activos = 114 evaluaciones individuales para testeo múltiple.

### Tabla maestra IS (Sharpe con costos 0.12% RT):

| ID | Hipótesis | TF | Configs PASS/Total | BTC Sharpe (mejor) | ETH Sharpe (mejor) | SOL Sharpe (mejor) | Gate IS |
|---|---|---|---|---|---|---|---|
| TS1 | Time-Series Momentum (retorno_L > 0) | 1h | 2/6 | +0.531 | +0.509 | +1.625 | PASS (2 configs) |
| TS2 | Cruce de medias móviles | 1h/4h | 1/6 | +0.692 | +0.558 | +1.145 | PASS (1 config, 4h) |
| TS3 | Breakout Donchian | 4h | 1/3 | +0.558 | +0.816 | +0.796 | PASS (1 config) |
| TS4 | Precio vs MA larga | 4h | 2/4 | +0.098 | +0.664 | +1.568 | PASS (2 configs) |
| TS5 | Momentum escalado por vol | 4h | 1/3 | -0.082 | +0.513 | +1.336 | PASS (1 config) |
| TS6 | Breakout de canal (hold fijo) | 4h/1d | 4/6 | +0.776 | +0.915 | +1.399 | PASS (4 configs: 1 en 4h, 3 en 1d) |
| TS7 | MACD (EMA rápida − EMA lenta > 0) | 4h | 0/3 | +0.045 | +0.437 | +1.456 | FAIL todas |
| TS8 | Acuerdo multi-timeframe (4h AND 1d) | multi | 0/2 | +0.124 | +0.068 | +1.665 | FAIL todas |
| TS9 | Momentum con skip | 4h | 0/3 | +0.033 | -0.037 | +1.392 | FAIL todas |
| TS10 | TS1 + gate HMM Trend | 1h | 0/2 | +0.216 | +0.335 | +1.512 | FAIL (HMM no disponible = TS1 vanilla, lb=48) |

**Resumen IS:** 11/38 configs PASS (28.9%). Hipótesis con señal: 6/10 (TS1, TS2, TS3, TS4, TS5, TS6).

### Análisis de robustez:

El análisis por hipótesis muestra **meseta parcial** — 6 de 10 formas de expresar tendencia tienen al menos una config que pasa el gate IS. Sin embargo, hay un patrón estructural crítico:

- **SOL domina**: en prácticamente todas las configs que pasan, SOL Sharpe IS es +0.7 a +1.7 pero BTC y ETH raramente superan +0.5.
- **BTC es el activo más débil**: la mayoría de configs PASS lo deben a SOL+ETH o SOL+BTC (nunca ETH+BTC solos).
- **El gate 2/3 es frágil**: la mayoría de configs que pasan tienen 2/3 activos (no 3/3). Quitando SOL, el edge de tendencia en BTC y ETH sería casi inexistente en IS.

### OOS (2025-01 → 2026-06): COLAPSO UNIVERSAL

| Config PASS IS | BTC OOS | ETH OOS | SOL OOS | Veredicto |
|---|---|---|---|---|
| TS1 lookback=48 h=48 | -1.065 | -0.445 | -1.002 | FAIL OOS |
| TS1 lookback=96 h=48 | -1.557 | -0.969 | -1.085 | FAIL OOS |
| TS2 fast=5 slow=20 4h h=12 | -0.879 | -0.668 | -0.644 | FAIL OOS |
| TS3 lb_entry=40 lb_exit=20 4h h=10 | -0.356 | -0.418 | -0.595 | FAIL OOS |
| TS4 lookback=100 thr=0.000 4h | -1.091 | -0.659 | -0.831 | FAIL OOS |
| TS4 lookback=100 thr=0.005 4h | -1.064 | -0.664 | -0.850 | FAIL OOS |
| TS5 lookback=60 thr=0.5 4h | -1.762 | -0.560 | -1.057 | FAIL OOS |
| TS6 lookback=40 h=10 4h | +0.619 | -0.157 | -0.791 | FAIL OOS |
| TS6 lookback=20 h=5 1d | N/A (T<30) | N/A | N/A | FAIL OOS (T insuf.) |
| TS6 lookback=40 h=10 1d | N/A (T<30) | N/A | N/A | FAIL OOS (T insuf.) |
| TS6 lookback=10 h=3 1d | -0.476 | -1.524 | -1.679 | FAIL OOS |

**0/11 configs sobreviven OOS.** Sharpes OOS típicos: -0.5 a -1.8. Gate1+Gate2 no corridos (early exit por OOS categórico). La única config con BTC OOS positivo (TS6-4h +0.619) falla ETH y SOL.

### Diagnóstico del colapso OOS:

El OOS 2025-2026 incluye un período bajista/lateral sostenido en cripto (H1 2025: BTC -15%, ETH -45% desde picos). El trend-following ES exactamente lo que la predicción de falla anticipaba: rinde mal en mercados sin tendencia sostenida. El IS 2021-2024 tuvo un bull market de 2021 y recovery 2023-2024 que infló las métricas IS. El OOS expone que el edge IS era régimen-dependiente.

**Nota de sobreajuste vigilado:** no se detectó ninguna config con Sharpe alto en "todo régimen" IS — el patrón es exactamente el esperado (rendimiento heterogéneo IS + colapso OOS en régimen desfavorable).

**Veredicto:** RECHAZADO. No procede a Capa B. El mecanismo de tendencia no tiene edge creíble que justifique implementación C# en el universo BTC/ETH/SOL con los costos actuales.

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
- Script: `Trading.Research/m4_lead_lag_btc_eth.py`

### H1 — RSI Mean Reversion condicionado por HMM Squeeze
- Hipótesis: RSI(14) < umbral en régimen HMM Squeeze (baja vol, sin trend) filtra señales falsas de oversold durante downtrends fuertes.
- M4 Binance 4h 2020-2025 sobre BTC/ETH/BNB, thresholds RSI 25/30/35, hold 8/12 barras:
  - Win rate alto: 55-69% en casi todos los configs. El condicionamiento HMM sí filtra algo.
  - Sharpe negativo en todos los 18 configs (0/18). Mejor resultado: ETH RSI<25 hold=8b → Sharpe +0.343 con 19 trades.
  - Problema estructural: RSI<25 en Squeeze produce 12-19 trades en 5 años (~3/año). Insuficiente frecuencia para compensar la varianza de los retornos por trade.
  - El retorno medio por trade es positivo en los thresholds más extremos pero la std es ~3-5x el mean → Sharpe inevitablemente bajo.
- Clasificación de régimen: centroide más cercano en espacio de features HMM escaladas (replica exacta de FeatureExtractor.cs). BNB usa modelo BTC como proxy.
- Script: `Trading.Research/m4_rsi_hmm_squeeze.py`

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
- Scripts: `Trading.Research/m4_atr_compression_breakout.py` (grid original), diagnóstico A inline en sesión.
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
- Script: `Trading.Research/m4_atr_tbr.py`.

### CVD Bullish Divergence (CvdBullishDivergenceStrategy)
- Hipótesis: cuando precio hace nuevo mínimo N-barra pero CVD no lo confirma, hay buying hidden → Long.
- M4 IS 2021-2024 (Long-only): 9/9 configs pasan, BTC +1.74 / ETH +0.52 / SOL +0.95 (3/3 activos). lookback=24, hold=6.
- Implementada: `CvdBullishDivergenceStrategy.cs`, 10 tests unitarios, registrada en StrategyFactory.
- Backtest QC IS 2021-2024 (SL=10%, TP=15%, MaxBars=6, sin OPS-2): Sharpe -1.854, DD 30.7%, expectancy -0.022%/trade.
- Root cause del gap M4↔QC: M4 trata cada señal como trade independiente aunque sean solapadas. Durante una caída de precio de 12h donde CVD se recupera gradualmente, M4 registra ~12 trades de entrada a distintos precios; QC entra solo una vez y mantiene la posición. Los "12 trades" de M4 en la recuperación tienen todos buen retorno → Sharpe inflado. QC captura solo el primero. La expectancy trade-by-trade es negativa: Average Win 0.15% × 48% win rate + Average Loss -0.18% × 52% = -0.022% por trade.
- Conclusión: el edge observado en M4 era un artefacto metodológico de señales solapadas, no alpha real.
- Scripts: `Trading.Research/m4_cvd_divergence.py`.

### Funding Rate Positioning (FRP)
- Hipótesis: z-score del funding rate (ventanas 14/30/60d) en extremo → mercado overcrowded → señal contraria.
- Test 1 bidireccional (BTC/ETH/SOL, diario, 2020-2025, 54 configs): BTC 8/54, ETH 0/54, SOL 11/54. Gate: 27/54 en 2/3 activos — FAIL.
- Test 2 SHORT-only (crowded longs → short): BTC 0/54, ETH 3/54, SOL 5/54. Peor que bidireccional — FAIL.
- Hallazgo clave: el edge en BTC estaba en el lado LONG (funding muy negativo → short squeeze → precio sube), no en el SHORT como postulaba la teoría.
- El filtro de tendencia (MA) es condición necesaria pero no suficiente: sin MA, todas las configs son negativas.
- Ventana de 60d sistemáticamente peor: demasiado lenta para capturar eventos de crowding.
- ETH sin señal en ninguna dirección, posiblemente por cambio estructural post-Merge (2022).
- Diagnóstico: el mecanismo de desapalancamiento existe pero opera en timeframes sub-diarios (horas), donde los datos históricos de order book/funding granular no están disponibles sin costo.
- Script: `Trading.Research/m4_funding_rate_positioning.py`

### OFI Momentum (candidata A — rechazada M4)
- Hipótesis: OFI en top percentil del historial reciente indica compra institucional agresiva → precio continúa subiendo (momentum de flujo).
- M4 IS 2021-2024 (1h, BTC/ETH/SOL, grid 27 configs: window=[24,48,96], thr=[0.75,0.80,0.85], hold=[4,6,8]):
  - 0/27 configs pasan el gate. Sharpe negativo en casi todos.
- Diagnóstico direccional (window=48, thr=0.80, hold=6):
  - Short cuando OFI bajo (vendedores agresivos): BTC -0.220, ETH -0.935, SOL -1.203 → precio SUBE después de venta agresiva.
  - Long cuando OFI alto (compradores agresivos): BTC +0.010, ETH +0.120, SOL +0.775 → edge débil, solo SOL relevante.
- Hallazgo: el OFI en 1h es mean-reverting, no momentum. Buying pressure gets absorbed (precio ya subió con la compra agresiva). La señal contraria (buy the dip after heavy selling) tiene mucho más edge.
- Script: `Trading.Research/m4_ofi_momentum.py`

---

## Hipótesis de Microestructura (Hito E — batch 2, sesión 2026-06-11)

Evaluación de 10 hipótesis basadas en datos AggTrades (OFI, CVD, ArrivalRate, MeanTradeSize, BuySellRatio, PriceReturn).
Período IS: 2021-2024. Período OOS: 2025-2026-06-09. Activos: BTCUSDT, ETHUSDT, SOLUSDT. Timeframe: 1h.

| ID | Hipótesis | Implementación | M4 Sharpe (BTC/ETH/SOL) | QC IS Sharpe | QC OOS Sharpe | Estado |
|---|---|---|---|---|---|---|
| H1 | VWAP Deviation — Long cuando (close-vwap)/vwap < -1.5% | VwapDeviationStrategy | ✅ PASS (≥2/3) | -0.369 | — | âŒ FAIL IS |
| H2 | Trade Count Spike — Long cuando ArrivalRate en P95 y PriceReturn plano | TradeCountSpikeStrategy | ✅ PASS (≥2/3) | -1.553 | — | âŒ FAIL IS |
| H3 | CVD Sell Exhaustion — Long cuando close=min(47b) y CvdDelta>0 | CvdSellExhaustionStrategy | ✅ PASS (≥2/3) | 2.178 | 1.718 | ✅ APROBADA |
| H4 | CVD Structure Shift — Long cuando CVD cambia de negativo a positivo | — (M4 FAIL) | âŒ FAIL | — | — | âŒ |
| H5 | Trade Size Institutional — Long cuando MeanTradeSize en P90 y BSR>1.02 | TradeSizeInstitutionalStrategy | ✅ PASS (≥2/3) | 3.985 | 4.186 | ✅ APROBADA |
| H6 | CVD-OFI Divergence — Long cuando CVD positivo pero OFI negativo | — (M4 FAIL) | âŒ FAIL | — | — | âŒ |
| H7 | Arrival Rate Momentum — Long cuando ArrivalRate acelerando | — (M4 FAIL) | âŒ FAIL | — | — | âŒ |
| H8 | Bid-Ask Imbalance — Long cuando BuySellRatio en percentil extremo | — (M4 FAIL) | âŒ FAIL | — | — | âŒ |
| H9 | Trade Count Spike Short — Short cuando ArrivalRate spike y return positivo | — (M4 FAIL) | âŒ FAIL | — | — | âŒ |
| H10 | Selling Climax — Long cuando SellingPressure extrema (ArrivalRate+return<-0.3%) | SellingClimaxStrategy | ✅ PASS (≥2/3) | -5.128 (SL=30%) | — | âŒ FAIL IS |

### Notas por hipótesis

**H1 — VwapDeviation**: Sin filtro de dirección. Entra en "dips" que en enero 2021 eran caídas de -10-14%. OPS-2/ConsecutiveLossesMonitor la mata antes de que pueda recuperar. Sharpe IS=-0.369, WR=50%.

**H2 — TradeCountSpike**: ArrivalRate spike + PriceReturn plano. El filtro `|return| < 0.5%` previene entradas en los crashes de enero 2021, pero la estrategia igual fue matada por OPS-2 en febrero 2022. Sharpe IS=-1.553, WR=58%. No hay dirección de flujo que proteja.

**H3 — CvdSellExhaustion**: Condición `close ≤ min(47b)` AND `CvdDelta > 0` proporciona dos filtros naturales: (a) precio en mínimo local genuino, (b) flujo neto positivo (compradores superan vendedores netos). El CvdDelta > 0 durante crashes donde los vendedores dominan protege de las caídas de enero 2021. OPS-2 mató ETHUSDT en sep-2023 y BTC/SOL en ene-2022, pero antes habían sido rentables. IS Sharpe=2.178, OOS Sharpe=1.718, CAGR OOS=30.4%, P(Sharpe<0)=1%. **APROBADA Hito G**.

**H5 — TradeSizeInstitutional**: MeanTradeSize en P90 + BuySellRatio > 1.02. El filtro BSR > 1.02 previene entrada cuando hay presión vendedora neta — mecanismo de protección clave durante el crash de enero 2021 (BSR colapsa <1 durante pánico). IS Sharpe=3.985, OOS Sharpe=4.186 (OOS mejor que IS — señal robusta), CAGR OOS=97%, MaxDD=5.9%, P(Sharpe<0)=0%. **APROBADA Hito G — resultado extraordinario**.

**H10 — SellingClimax**: ArrivalRate spike + PriceReturn < -0.3%. Contrarian puro sin filtro de flujo. Con SL=5%: Sharpe=-3.183 (caídas de enero 2021 >5% causan stop masivos). Con SL=30%: Sharpe=-5.128 (permite pérdidas enormes en crashes). No existe SL viable para este tipo de señal contrarian sin filtro de flujo.

**H4/H6/H7/H8/H9**: Rechazadas en M4. Hipótesis de momentum de CVD/OFI/ArrivalRate o bid-ask imbalance no mostraron Sharpe ≥ 0.5 en ≥2/3 activos en el grid de parámetros evaluado (scripts: `Trading.Research/m4_micro_*.py`).

### OFI Contrarian — Long-only (candidata B — APROBADA QC IS)
- Hipótesis: cuando OFI está en el percentil inferior de su distribución reciente (vendedores agresivos), el mercado está sobre-vendido localmente y el precio rebota en las próximas N horas.
- M4 IS 2021-2024 (1h, BTC/ETH/SOL, grid 27 configs: window=[24,48,96], thr=[0.75,0.80,0.85], hold=[4,6,8]):
  - 25/27 configs pasan el gate (93%).
  - Mejor: window=24, threshold=0.85, hold=8: BTC +0.869, ETH +1.475, SOL +1.367 (media +1.237).
- Análisis anual (window=24, thr=0.85, hold=8):
  - 2021: BTC +1.353, ETH +2.618, SOL +2.793 (bull market fuerte)
  - 2022: BTC -0.863, ETH +0.919, SOL -0.586 (bear market: Long-only pierde en BTC/SOL)
  - 2023: BTC +1.653, ETH +0.844, SOL +1.863 (recovery)
  - 2024: BTC +1.744, ETH +1.100, SOL +1.126 (bull market)
- Win rate M4: 50-52%, expectancy BTC +0.060%/trade, ETH +0.130%, SOL +0.179%.
- **QC IS 2021-2024 (SL=10%, TP=15%, MaxBars=8, Risk=1%, NullStrategyHealthMonitor):**
  - Sharpe: **0.503** — PASA M1 (≥0.5). Sortino: 0.702.
  - CAGR: 11.69%, Net Profit: +55.7% ($100k→$155,663).
  - Max Drawdown: **41.1%** (alto; esperado con 3 activos cripto correlacionados).
  - Win Rate: 44%, Avg Win: +1.48%, Avg Loss: -1.01%, P/L Ratio: 1.46.
  - Trades cerrados: 640, expectancy portfolio: +0.078.
  - Kill switch: 1 vez en 2024-08-05 (8 pérdidas consecutivas), cooling-off 1 día. Luego recuperó.
  - Fees: ≈1.28 fijo (negligible); slippage 0.2% round-trip ya embedido en fills.
  - Estado backtest: Completed (2021-01-01 → 2024-12-31).
- Implementada: `OfiContrarianStrategy.cs`, 7 tests unitarios, registrada en StrategyFactory.
- Scripts: `Trading.Research/m4_ofi_contrarian.py`, `Trading.Research/m4_ofi_momentum.py`.
- ADR: ADR-038.

---

## Momentum Cross-Sectional / Time-Series — Universo ancho — Capa A (2026-06-27) — NO-GO

**Tesis del test:** trend murio en 3 majors por falta de edge (no costos); el momentum es cross-sectional y 3 activos correlacionados no pueden expresarlo → re-test sobre universo ancho. Mecanismo re-testeado en el setting donde deberia vivir.

**Universo:** 196 simbolos perp USDT Binance, panel diario 2020-2026 (desde klines 1m locales). Anti-survivorship: incluye nombres colapsados (LUNA/USTC −99.9% presentes en el panel, castigaron carteras 2022). Sesgo residual documentado y OPTIMISTA: no incluye perps removidos pre-2021 (habrian sido filtrados por baja liquidez) y no modela impacto de mercado → ambos inflan el resultado, por lo que el NO-GO es robusto. Filtro de liquidez point-in-time ADV-30d > $5M (~110 elegibles/rebalanceo). Costos escalonados: majors 0.12% RT, mid/small 0.22% RT. Causalidad verificada en 3 niveles (elegibilidad, senal, hold sin solape con la ventana de ranking).

**Grilla pre-registrada long-only (8 configs):** L ∈ {30d, 90d} × H ∈ {7d, 30d} × seleccion ∈ {top-decil, top-5}.

| Brazo | Configs PASS IS (Sharpe >= 0.5) | Rango Sharpe IS | Rango Sharpe OOS | Estado |
|---|---|---|---|---|
| Long-only (grilla principal) | 1/8 (solo L=30d/H=7d/top-5 = 0.526, marginal) | 0.526 a negativo | −1.1 a −3.6 | NO-GO |
| Time-series (diagnostico) | N/A (diagnostico, no grilla principal) | hasta 0.70 (L=90d/H=30d) pero N=47-48 periodos | −2.1 a −3.4 | NO-GO |
| Long-Short (diagnostico) | N/A | peor que long-only | — | NO-GO |

**Resultado detallado:**
- **1/8 configs pasa Sharpe IS >= 0.5** (solo L=30d/H=7d/top-5 = 0.526, marginal). **SIN meseta**: el Sharpe IS decae monotonamente con L.
- **OOS colapsa en las 8 configs** (Sharpe OOS −1.1 a −3.6), incluida la unica que paso IS.
- Brazo time-series (diagnostico): IS hasta 0.70 (L=90d/H=30d) pero N=47-48 periodos y OOS −2.1 a −3.4.
- Brazo L/S (diagnostico): peor que long-only (la pata corta se destruye con los rallies de alts — confirma I.3 de ROADMAP-STRATEGIES).

**Veredicto: NO-GO.** El criterio decisivo es la ausencia de meseta (juicio IS, no contaminado por OOS): aun sobre un universo optimista, el edge es 0.3-0.5 sin plateau. Falla de mecanismo: rankear por retorno bruto en un universo donde todo correlaciona con BTC ≈ rankear por beta×mercado → elige alta-beta que revierte. El colapso OOS confirma la prediccion (correlacion → 1 en crashes mata el cross-sectional).

**Hilo vivo senalado por el director (NO condenado por este test, es otra senal):** momentum residual / beta-neutral (remover el factor mercado, rankear por residuo idiosincratico) — neutral por construccion, podria llenar el carril del carry. Pendiente de decision de research-direction.

**Script:** `Trading.Research/m4_momentum_cross_sectional.py`

---

## Eje Volatilidad — Capa A (2026-06-27) — NO-GO (3/3 hipotesis)

**Triage estructural (Fase 0):** el carril de vol neutral al mercado (producto que reemplazaria al carry) esta BLOQUEADO por el venue — no hay opciones / variance swaps / indice de vol tradeable en Binance; toda expresion sin opciones muere por el muro de costos o disuelve su mecanismo. Solo sobrevivieron al triage hipotesis vol-as-signal (direccional, baja frecuencia) y vol-as-sizing (infra de riesgo). Costos 0.12% RT, IS 2021-2024 / OOS 2025-2026, frecuencia diaria, causalidad verificada (RV con shift, sin lookahead).

| Hipotesis | Configs PASS IS | Resultado | Estado |
|---|---|---|---|
| H-V1 (spike de vol → rebote, long-only) | 0/8 | Mecanismo falsado; evento raro (T=3-17 en 4 anos) | NO-GO |
| H-V2 (compresion de vol → breakout) | 2/8 | Sin meseta; edge concentrado en SOL (T bajo = ruido); OOS colapsa | NO-GO |
| H-V3 (vol-targeting 1/RV, diagnostico de infra) | N/A (no sleeve) | Deteriora Sharpe OOS en 3/3 activos vs buy-and-hold | NO-GO |

**H-V1 (spike de vol → rebote, long-only):** 0/8 configs PASS. Sharpe IS medio BTC −0.21 / ETH +0.05 / SOL +0.13. Mecanismo FALSADO: los spikes de vol en cripto son crashes con continuacion, no capitulaciones que rebotan (BTC sistematicamente negativo). Evento demasiado raro (T=3-17 en 4 anos) — con MIN_TRADES=30 estandar todas serian N/A. NO-GO por falta de edge Y de poder estadistico.

**H-V2 (compresion de vol → breakout):** 2/8 PASS IS pero SIN meseta. El "edge" se concentra en SOL solo (Sharpe IS 0.53-1.03 pero T=9-17 = ruido estadistico); BTC negativo 6/8; OOS colapsa. Mismo patron de sesgo de activo / pico-de-un-solo-passer que mato a trend. Redundante con breakout/trend ya rechazado. NO-GO.

**H-V3 (vol-targeting 1/RV, diagnostico de infra — NO sleeve):** deteriora el Sharpe OOS en los 3 activos vs buy-and-hold (Δ −0.14 BTC / −0.26 ETH / −0.11 SOL). El escalado ingenuo compra exposicion en la calma de baja vol que precede los crashes. Leccion de infra (especifica de estos 3 majors / este periodo, no veredicto universal): la capa de riesgo I.4 NO debe usar inverse-vol simple aca.

**Veredicto:** eje volatilidad DESCARTADO en Capa A. No pasa a C#. El hueco del carril neutral (carry) queda abierto — la vol no lo lleno (bloqueo de venue). Script: `Trading.Research/layer_a_vol_v1.py`.

---

## Momentum Residual / Beta-Neutral — Universo ancho — Capa A (2026-06-27) — NO-GO (override del director)

**Hipotesis:** el momentum crudo falla porque rankear por retorno bruto ≈ rankear por beta×mercado; el edge conductual viviria en el residuo idiosincratico ε tras remover el factor BTC (Blitz-Huij-Martens, residual momentum). Mismo universo/datos/costos/particion que el momentum crudo (apples-to-apples); unica variable: senal crudo→residual. β-window=90d, skip=7d, ambos fijos. Causalidad verificada (β y residuos solo con datos ≤ t; skip evita solape con el hold; ADV point-in-time).

> **NOTA:** el script `m4_momentum_residual.py` IMPRIME "GATE LONG-ONLY: PASS / >> GO". El director (Opus) lo **OVERRIDE a NO-GO**: el gate long-only del script mide una cartera long de alts con beta de mercado ~1.2 en bull market; el gate VALIDO es el beta-neutral, que FALLA.

| Vista | Configs PASS IS | Sharpe IS medio | OOS | Gate |
|---|---|---|---|---|
| Long-only residual | 5/8 (meseta SI, no concentrado) | +0.506 | 0/8 positivas (−0.85 a −2.95, media −2.03) | (enganoso — incluye beta) |
| Beta-neutral (long + hedge short BTC) | 2/8 | ~−0.2 (rango −0.636 a +0.106) | −0.67 a −1.80 | **FAIL** |

- **Long-only residual:** 5/8 Sharpe IS≥0.5, meseta SI, no concentrado (8/8). MEJORA vs crudo (1/8 sin meseta) — residualizar SI afina el ranking in-sample. PERO **OOS colapsa 0/8**, identico al crudo (delta OOS +0.067 ≈ 0).
- **Beta-neutral (test REAL del carril):** beta neutralizada correctamente (pre 1.20 → post 0.00) pero Sharpe IS DESTRUIDO (2/8 positivas, media negativa). GATE NEUTRAL: **FAIL**.
- **Descomposicion decisiva:** el Sharpe IS 0.51 del long-only era ~enteramente **beta de mercado** (cartera long de alts β≈1.2 en bull 2021-2024), NO alfa residual. Al hedgear la beta (el punto entero de un residual neutral) el edge desaparece (~−0.2). La "meseta" del long-only es meseta de exposicion a beta, no de alfa. La diferencia long-only − neutral (~0.7 Sharpe) ES la beta.
- **OOS identico al crudo** confirma la prediccion de falla pre-registrada: fuga de beta / sin momentum idiosincratico persistente.
- **L/S residual (diagnostico):** sin edge IS (todas ≤0 salvo ruido). Dos configs L=90/H=7 con OOS positivo (+1.10, +1.28) pero IS negativo = ruido; nunca se elige por OOS.

**Veredicto: NO-GO. Cierra TODA la familia momentum** (crudo + residual; cross-sectional + TS; long-only + neutral). El carril neutral (que dejo vacio el carry) sigue vacio. El "GO" impreso por el script es un artefacto de gatear el long-only con beta incluida; el gate valido (neutral) falla. **Con esto quedan descartados por Capa A todos los ejes accesibles probados** (carry, reversion micro ×4, trend majors+gated, vol ×3, momentum crudo+residual) → pendiente reevaluacion estructural. Script: `Trading.Research/m4_momentum_residual.py`.

---

## Lead-Lag Gap — short del laggard en caída de BTC — Universo ancho — Capa A (2026-06-30) — NO-GO

**Hipótesis (director):** en cada movimiento BAJISTA importante de BTC, shortear la(s) alt(s) que todavía NO acompañaron la caída (gap de reacción = residuo CAPM sobre la ventana del evento), esperando catch-up bajista. State-contingent: cualquiera de las ~100 puede ser candidata, distinta por evento — NO se busca una moneda persistentemente lenta.

**Setup:** panel ~196 perps USDT-M (reusa `m4_momentum_cross_sectional`, anti-survivorship LUNA2/USTC), base 15m, top-100 por ADV-pit (piso $5M). Eventos BTC `z<−k` (σ rolling 30d). Gap = `cumret_coin_W − β_pit·cumret_BTC_W`, acumulado sobre W (no una sola vela). Dedup `refractory=H`. Causalidad verificada en 3 niveles: β-pit termina en `t−W`, ventana del evento `[t−W,t]`, catch-up `(t,t+H]` sin solape. Grilla pre-registrada `W∈{1h,2h,4h} × k∈{2.0,2.5,3.0}σ × H∈{1h,2h,4h}` = 27 configs. IS 2021-2024. **BRUTO (sin costos).**

**Resultado: 0/27 configs con short P&L > 0. NO-GO en Etapa 1-3, antes de costos.**

| Métrica | Resultado (27 configs, universo 196) |
|---|---|
| Short P&L bucket top-gap | NEGATIVO en 27/27 (−1 a −164 bps); win rate 33-48% |
| skip-1 (entrada 1 barra tarde) | NEGATIVO en 27/27 |
| Terciles de liquidez (low / high) | ambos NEGATIVOS — NO es ranciedad |
| stale_frac (top-gap sin print de entrada) | 0.00 (el filtro ADV ya remueve la cola rancia) |
| IC (Spearman gap~fwd) | NEGATIVO en 27/27 (−0.05 a −0.08), débil pero consistente |
| Spread market-neutral (top−bottom) | POSITIVO en 27/27 (+21 a +59 bps) |

**Diagnóstico:** el short direccional está FALSADO estructuralmente. Un evento `z<−k` bajista de BTC es un mínimo local que REVIERTE — tras la venta agresiva todo el complejo rebota (el short P&L empeora monótonamente con H y k = más rebote). Es el mismo mecanismo de **OFI Contrarian** (rebote post-exhaustion): la hipótesis pelea de frente contra el único edge real del proyecto. **Problema de timing inherente:** un "movimiento importante" solo es identificable DESPUÉS de ocurrido, que es exactamente el punto de máxima presión de reversión; el lag existe (IC negativo) pero está dominado por la reversión a la que estás forzado a entrar.

**La única pieza con edge bruto** (spread market-neutral: long lo ya-caído / short el laggard, +30-50 bps/evento) es una RE-DERIVACIÓN del cross-sectional reversion ya CERRADO por costos en Eje 1/1b: long+short de ilíquidos ≈ 0.44% RT en fees + slippage de crash >> 30-50 bps brutos. Sin rescate.

**Veredicto: NO-GO.** No procede a Etapa 4 (costos). Tercera confirmación independiente (con OFI-OOS y eje-1b) de que el carril reversión-tras-caída no sobrevive costos en este venue. Script: `Trading.Research/layer_a_lead_lag_gap.py`.

---

## Screening M4 — 4 hipótesis nuevas (reversión media, carry funding, baja vol, asimetría) — 2026-07-01

Screening de cuatro hipótesis clásicas de literatura (§3.9, §8.2.1, §3.4, §9.5) sobre la infra M4 existente (`m4_shared.py`, harness Hito E: 1h, IS 2021-01-01→2024-12-31, fee 0.04% RT, position tracking obligatorio, gate Sharpe≥0.5 en ≥2/3 activos, MIN_TRADES=30). Objetivo: buscar candidatas nuevas para llenar los carriles que Capa A dejó vacíos (ver [[project_inflexion_estructural]]).

| Estrategia | Gate M4 | Mejor config | Sharpe BTC/ETH/SOL | Estado |
|---|---|---|---|---|
| Reversión a la media (z-score vs MA, contrarian) | ❌ 0/36 configs | window=48, thr=2.50, hold=6 | −0.975 / −0.950 / **+0.063** | NO-GO |
| Carry funding alto-menos-bajo (bidireccional) | ⚠️ 2/27 configs | z_thr=2.0, z_window=60, hold=7 | **+0.800** / **+0.988** / −1.011 | NO-GO (pass mecánico frágil) |
| Anomalía de baja volatilidad (contrarian de régimen) | ❌ 0/54 configs | vol_w=48, rank_w=96, thr=0.75, hold=8 | −1.457 / **−0.204** / −1.401 | NO-GO |
| Prima de asimetría / skewness (contrarian y momentum) | ❌ 0/72 configs (36+36) | momentum: skew_w=24, rank_w=96, thr=0.85, hold=8 | −0.247 / +0.019 / **+0.929** | NO-GO |

**Reversión a la media** (`m4_reversion_media.py`): las 36 configs dan Sharpe negativo en BTC y ETH sin excepción; SOL roza cero en 3 configs pero nunca positivo relevante. El contrarian puro contra un mercado en tendencia (bull 2021-2024) pierde sistemáticamente — confirma, con un mecanismo distinto, el mismo patrón que ya cerró el eje reversión-tras-caída (lead-lag) y el eje cross-sectional invertido: en cripto majors 2021-2024 comprar la caída y vender la subida es la apuesta perdedora, no la ganadora. Ningún parámetro de ventana (24-168h) ni threshold (1.5-2.5σ) rescata el mecanismo.

**Carry funding alto-menos-bajo** (`m4_carry_funding.py`): pasa el gate mecánico en 2/27 configs (7.4%), ambas en la esquina extrema del grid (z_threshold=2.0 —el valor más alto probado—, z_window=60 —el más largo—, hold=5 o 7). BTC y ETH alcanzan +0.55/+0.80 y +0.97/+0.99 respectivamente, pero **SOL es negativo en las 27/27 configs sin excepción** (−0.15 a −1.24), y el N de trades en las configs que pasan es marginal (35-48, apenas por encima de MIN_TRADES=30). No hay meseta: mover z_threshold de 2.0 a 1.5 o z_window de 60 a 30 destruye el pass. Mismo patrón que ATR Compression H2 y el cross-sectional (pico aislado, no plataforma) — históricamente esos picos no sobrevivieron a backtest/OOS. **Veredicto: NO-GO** pese al pass mecánico — no cumple el criterio de robustez (meseta + generalización a 3/3 activos) que el proyecto exige antes de pasar a implementación. Nota metodológica: el Sharpe mide únicamente el retorno de precio en la ventana de hold, no el ingreso de funding en sí (misma simplificación que `m4_funding_rate_positioning.py`).

**Anomalía de baja volatilidad** (`m4_baja_volatilidad.py`): las 54 configs dan Sharpe negativo en los 3 activos sin ninguna excepción (peor resultado de las 4 hipótesis — 0 valores positivos en 162 celdas BTC/ETH/SOL). Tanto el brazo "long en régimen de baja vol" como el "short en régimen de alta vol" pierden. La anomalía de baja volatilidad (que en equities depende de restricciones de apalancamiento institucional, Frazzini-Pedersen) no tiene el mecanismo estructural equivalente en perpetuos cripto sin restricción de leverage — evidencia consistente con el H-V3 de Capa A (vol-targeting también deterioró Sharpe OOS).

**Prima de asimetría** (`m4_asimetria.py`): modo contrarian (short skew alto / long skew bajo) falla limpio — 0/36, siempre negativo, mismo patrón de "comprar la caída pierde" que reversión a la media. Modo momentum (long skew alto / short skew bajo) tiene señal parcial: SOL pasa el umbral individual en 12/36 configs (hasta +0.93), pero BTC es negativo en 33/36 y ETH oscila cerca de cero — nunca coinciden 2/3 activos en la misma config. Sesgo de un solo activo (SOL), mismo patrón que H-V2 de Capa A (edge concentrado en SOL con volumen/liquidez distinta a BTC/ETH) — no generaliza.

**Patrón cruzado a las 4 hipótesis:** todo lo que apuesta contrarian direccional puro contra el mercado (reversión a la media, skew contrarian) pierde limpio; todo lo que depende de un solo activo para pasar (carry en BTC/ETH pero no SOL, skew-momentum en SOL pero no BTC/ETH) es un pico aislado, no una meseta cross-asset — el mismo criterio de rechazo usado en Capa A (H-V2, cross-sectional, momentum residual). Ninguna de las 4 pasa a implementación C#. Scripts: `Trading.Research/m4_reversion_media.py`, `m4_carry_funding.py`, `m4_baja_volatilidad.py`, `m4_asimetria.py`.

---

## Eje 3 — Re-test a costos MAKER (OFI Contrarian sub-hora) — 2026-07-03 — NO-GO

**Hipótesis:** el eje 3 (OFI Contrarian 5m/15m) pasaba M4 54/54 a costo 0.0 pero moría 0/54 a costo taker real (0.12% RT, ADR-056). La entrada de reversión (comprar tras venta agresiva agotada) es naturalmente maker (se postea bid debajo del mercado) — el lever propuesto en la inflexión estructural del 2026-06-27 es probar si el modelo de costos taker (que asume ejecución agresiva) estaba sobre-penalizando un mecanismo que en la práctica se ejecutaría con fee maker.

**Modelo (conservador, no costo-0 disfrazado):** precio límite = `close[t-1] × (1 − 0.05%)`; fill condicional real (`low[t] <= limite`, no 100% asumido); fee maker 0.02%/lado en entrada y en salida; **haircut de selección adversa de 5bp** sobre el retorno del fill (Glosten-Milgrom: cuando un maker-bid se llena es porque el flujo era tóxico y el precio sigue cayendo). Costo RT efectivo del modelo ≈ 0.09% (vs 0.12% taker). Misma grilla pre-registrada de 54 configs (`ofi_window×threshold×hold×timeframe`), mismo IS 2021-2024, mismo position tracking. Causalidad verificada (señal shift+rolling, límite con close previo, fill con low de la barra de señal, exit con close futuro sin solape).

| Escenario | PASS/54 | Sharpe típico |
|---|---|---|
| M4 original (0.00% RT) | 54/54 | positivo por construcción |
| Capa A taker (0.12% RT, ADR-056) | 0/54 | −4 a −44 |
| **Maker modelo (este re-test, ~0.09% RT efectivo)** | **0/54** | **−0.13 a −20** |

**Fill-rate** (no es el problema): mediana 59.7%-88.1% según activo/TF, todos razonables — el modelo maker sí ejecuta.

**Diagnóstico:** el edge bruto del eje 3 no sobrevive a NINGÚN costo neto positivo, ni siquiera al escenario de ejecución más optimista-pero-defendible (maker con fill real + haircut de selección adversa). Los Sharpes catastróficos (hasta −20 en 5m hold=3) muestran que el "edge" a costo 0 es indistinguible de ruido de alta frecuencia — miles de trades/año con edge por trade menor al spread/fee mínimo posible en el venue. La mejor config (15m, thr=0.85, hold=12) reduce la magnitud (BTC −2.2 / ETH −1.5 / SOL −0.13) pero sigue negativa en 3/3 activos.

**Veredicto: NO-GO.** El lever "ejecución maker" queda descartado para el eje 3 específicamente — no era un problema de modelo de costos (taker vs maker), era ausencia de edge neto real. Esto no reabre eje 3 bajo ningún supuesto de ejecución razonable. Script: `Trading.Research/layer_a_reversion_maker.py`.

