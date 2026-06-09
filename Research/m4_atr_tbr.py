# -*- coding: utf-8 -*-
"""
M4 validation: ATR Compression + Taker Buy Ratio (H8 candidate)

Hipotesis: breakout de compresion ATR es mas confiable cuando el volumen agresivo
(taker buy) confirma la direccion del rompimiento. El TBR filtra fakeouts
donde el precio rompe pero el flujo no acompana.

Extension de H2 (ATR Compression Breakout):
  H2 usaba solo OHLCV, 4h, 2020-2025. Fallo en backtest completo: SL 2% incompatible
  con hold 12h. Esta candidata usa 1h (mejor granularidad para TBR) y agrega
  confirmacion de flujo agresivo como filtro de entrada.

Disenio M4:
  - Compresion: ATR(14) < percentil P del ATR rolling 100 barras
  - Long:  close > max(close[-lookback:]) AND TBR_rolling > tbr_thr
  - Short: close < min(close[-lookback:]) AND TBR_rolling < (1 - tbr_thr)
  - TBR_rolling: media del Taker Buy Ratio en la ventana `lookback`
  - Sin SL/TP — solo hold fijo (test de pureza de senal)
  - Timeframe: 1h, activos: BTC, ETH, BNB, periodo 2021-2024 IS / 2025 OOS
  - Gate M4: >= 2/3 activos con mayoria de configs Sharpe >= 0.5

Grid:
  - ATR percentiles: [15, 20, 25]
  - Lookback precio: [10, 20]
  - Hold: [3, 4, 6] barras 1h
  - TBR threshold: [0.52, 0.55, 0.58]
  Total: 3 x 2 x 3 x 3 = 54 configs
"""

import os
import numpy as np
import pandas as pd
from binance.client import Client


# ─── ATR(14) — Wilder smoothing ───────────────────────────────────────────────

def compute_atr(highs: np.ndarray, lows: np.ndarray, closes: np.ndarray,
                period: int = 14) -> np.ndarray:
    n = len(closes)
    tr  = np.full(n, np.nan)
    atr = np.full(n, np.nan)
    for i in range(1, n):
        tr[i] = max(highs[i] - lows[i],
                    abs(highs[i] - closes[i - 1]),
                    abs(lows[i]  - closes[i - 1]))
    atr[period] = np.nanmean(tr[1:period + 1])
    for i in range(period + 1, n):
        atr[i] = (atr[i - 1] * (period - 1) + tr[i]) / period
    return atr


# ─── Rolling percentile ───────────────────────────────────────────────────────

def rolling_percentile(values: np.ndarray, window: int, pct: float) -> np.ndarray:
    n = len(values)
    result = np.full(n, np.nan)
    for i in range(window - 1, n):
        result[i] = np.percentile(values[i - window + 1:i + 1], pct)
    return result


# ─── Data download ────────────────────────────────────────────────────────────

CACHE_DIR = os.path.join(os.path.dirname(__file__), "data", "atr_tbr_1h")

def download_1h_ohlcv(symbol: str, start_date: str, end_date: str) -> pd.DataFrame:
    os.makedirs(CACHE_DIR, exist_ok=True)
    cache_path = os.path.join(CACHE_DIR, f"{symbol}_1h_{start_date[:4]}_{end_date[:4]}.csv")

    if os.path.exists(cache_path):
        print(f"  {symbol}: usando cache local ({cache_path})")
        df = pd.read_csv(cache_path, index_col='time', parse_dates=True)
        return df

    print(f"  {symbol}: descargando 1h OHLCV+TBR {start_date} -> {end_date}...")
    client = Client()
    klines = client.get_historical_klines(symbol, '1h', start_date, end_date)
    df = pd.DataFrame(klines, columns=[
        'time', 'open', 'high', 'low', 'close', 'volume',
        'close_time', 'quote_volume', 'trades',
        'taker_buy_base', 'taker_buy_quote', 'ignore'
    ])
    df['time'] = pd.to_datetime(df['time'], unit='ms', utc=True)
    for col in ['open', 'high', 'low', 'close', 'volume', 'taker_buy_base']:
        df[col] = df[col].astype(float)
    df['tbr'] = df['taker_buy_base'] / df['volume']
    df = df.set_index('time')[['open', 'high', 'low', 'close', 'volume', 'tbr']].sort_index()
    df.to_csv(cache_path)
    print(f"    {len(df)} barras guardadas en cache")
    return df


# ─── M4 signal test ───────────────────────────────────────────────────────────

def test_atr_tbr(
    closes:   np.ndarray,
    highs:    np.ndarray,
    lows:     np.ndarray,
    tbr:      np.ndarray,
    atr_pct:  float,
    lookback: int,
    hold:     int,
    tbr_thr:  float,
    atr_window: int = 100,
) -> dict:
    """
    Senal pura: ATR comprimido + rompimiento de rango confirmado por TBR.

    Long:  close > max(close[i-lookback:i]) AND ATR < P(atr_pct) AND tbr_roll > tbr_thr
    Short: close < min(close[i-lookback:i]) AND ATR < P(atr_pct) AND tbr_roll < (1-tbr_thr)
    tbr_roll = media(tbr[i-lookback:i])

    Anticipa: una barra en compresion con precio rompiendo y flujo agresivo
    confirmando → la senal es real, no un fakeout.
    """
    atr           = compute_atr(highs, lows, closes)
    atr_threshold = rolling_percentile(atr, atr_window, atr_pct)

    n      = len(closes)
    trades = []
    i      = max(atr_window, lookback)

    while i < n - hold:
        if np.isnan(atr[i]) or np.isnan(atr_threshold[i]):
            i += 1
            continue

        if atr[i] >= atr_threshold[i]:   # no hay compresion
            i += 1
            continue

        price_window = closes[i - lookback: i]
        tbr_window   = tbr[i - lookback: i]
        if len(price_window) < lookback or np.any(np.isnan(tbr_window)):
            i += 1
            continue

        range_high   = np.max(price_window)
        range_low    = np.min(price_window)
        tbr_rolling  = np.mean(tbr_window)
        entry        = closes[i]
        exit_        = closes[i + hold]

        if entry > range_high and tbr_rolling > tbr_thr:
            ret = (exit_ - entry) / entry
            trades.append(('long', ret))
            i += hold + 1
        elif entry < range_low and tbr_rolling < (1.0 - tbr_thr):
            ret = (entry - exit_) / entry
            trades.append(('short', ret))
            i += hold + 1
        else:
            i += 1

    if len(trades) < 10:
        return {'sharpe': np.nan, 'win_rate': np.nan, 'trades': len(trades)}

    returns         = np.array([r for _, r in trades])
    mean_ret        = returns.mean()
    std_ret         = returns.std(ddof=1)
    win_rate        = (returns > 0).mean()
    years           = n / (24 * 365)      # 24 barras 1h / dia
    trades_per_year = len(trades) / years
    sharpe          = (mean_ret / std_ret * np.sqrt(trades_per_year)) if std_ret > 0 else 0.0

    long_count  = sum(1 for d, _ in trades if d == 'long')
    short_count = sum(1 for d, _ in trades if d == 'short')

    return {
        'sharpe':       round(sharpe, 4),
        'win_rate':     round(win_rate, 4),
        'mean_return':  round(mean_ret, 6),
        'std_return':   round(std_ret, 6),
        'trades':       len(trades),
        'long_trades':  long_count,
        'short_trades': short_count,
    }


# ─── Main ─────────────────────────────────────────────────────────────────────

if __name__ == '__main__':
    print("M4: ATR Compression + Taker Buy Ratio (1h, bidireccional)")
    print("=" * 70)

    START_IS = '2021-01-01'
    END_IS   = '2024-12-31'
    SYMBOLS  = ['BTCUSDT', 'ETHUSDT', 'BNBUSDT']

    ATR_PERCENTILES = [15, 20, 25]
    LOOKBACKS       = [10, 20]
    HOLDS           = [3, 4, 6]
    TBR_THRESHOLDS  = [0.52, 0.55, 0.58]
    TOTAL_CONFIGS   = len(ATR_PERCENTILES) * len(LOOKBACKS) * len(HOLDS) * len(TBR_THRESHOLDS)

    print(f"\nGrid: {len(ATR_PERCENTILES)} ATR x {len(LOOKBACKS)} lookback x "
          f"{len(HOLDS)} hold x {len(TBR_THRESHOLDS)} TBR = {TOTAL_CONFIGS} configs")
    print(f"Periodo IS: {START_IS} -> {END_IS}\n")

    # Descargar datos
    print("Descargando datos 1h (columnas OHLCV + taker_buy_base)...")
    ohlcv = {}
    for sym in SYMBOLS:
        ohlcv[sym] = download_1h_ohlcv(sym, START_IS, END_IS)
        print(f"  {sym}: {len(ohlcv[sym])} barras OK")

    # Test de senal — grid completo
    print("\n" + "-" * 70)
    all_results = {}

    for sym in SYMBOLS:
        df = ohlcv[sym]
        closes = df['close'].values.astype(float)
        highs  = df['high'].values.astype(float)
        lows   = df['low'].values.astype(float)
        tbr    = df['tbr'].values.astype(float)

        all_results[sym] = {}
        print(f"\n{sym}:")
        print(f"  {'ATR':>5} {'look':>5} {'hold':>5} {'TBR_thr':>8}  "
              f"{'Sharpe':>8}  {'WinRate':>8}  {'Trades':>7}  Status")

        for pct in ATR_PERCENTILES:
            for lookback in LOOKBACKS:
                for hold in HOLDS:
                    for tbr_thr in TBR_THRESHOLDS:
                        res = test_atr_tbr(
                            closes, highs, lows, tbr,
                            atr_pct=pct, lookback=lookback,
                            hold=hold, tbr_thr=tbr_thr,
                        )
                        key    = (pct, lookback, hold, tbr_thr)
                        all_results[sym][key] = res
                        s      = res['sharpe']
                        wr     = res['win_rate']
                        status = '[PASS]' if (not np.isnan(s) and s >= 0.5) else '[FAIL]'
                        wr_str = f"{wr:.1%}" if not np.isnan(wr) else ' n/a'
                        print(f"  P{pct:>2}  {lookback:>4}b  {hold:>4}b  thr={tbr_thr:.2f}  "
                              f"Sharpe={s:+.3f}  Win={wr_str}  "
                              f"T={res['trades']:>4}  {status}")

    # ─── Resumen M4 ───────────────────────────────────────────────────────────
    print("\n" + "=" * 70)
    print("RESUMEN M4:")
    print()

    asset_results = {}
    for sym in SYMBOLS:
        n_pass = sum(
            1 for res in all_results[sym].values()
            if not np.isnan(res['sharpe']) and res['sharpe'] >= 0.5
        )
        pct_pass = n_pass / TOTAL_CONFIGS
        # Gate consistente con H2: mayoria de configs (>50%) pasa en ese activo
        passed = pct_pass > 0.50
        asset_results[sym] = {'n_pass': n_pass, 'pct': pct_pass, 'passed': passed}
        mark = '[PASS]' if passed else '[FAIL]'
        print(f"  {sym}: {n_pass}/{TOTAL_CONFIGS} configs Sharpe >= 0.5  ({pct_pass:.1%})  {mark}")

    print()
    assets_passing = sum(1 for v in asset_results.values() if v['passed'])

    if assets_passing >= 2:
        print(f"Activos pasando: {assets_passing}/3")
        print()
        print("[OK] M4 PASADO — ATR Compression + Taker Buy Ratio tiene edge en 1h")
        print()

        # Mostrar top configs (los que pasan en los 3 activos)
        print("Top configs (Sharpe medio de los 3 activos):")
        print(f"  {'ATR':>5} {'look':>5} {'hold':>5} {'TBR_thr':>8}  "
              f"{'BTC':>8}  {'ETH':>8}  {'BNB':>8}  {'Media':>8}")
        rows = []
        for pct in ATR_PERCENTILES:
            for lookback in LOOKBACKS:
                for hold in HOLDS:
                    for tbr_thr in TBR_THRESHOLDS:
                        key = (pct, lookback, hold, tbr_thr)
                        sharpes = [all_results[sym][key]['sharpe'] for sym in SYMBOLS]
                        if all(not np.isnan(s) for s in sharpes):
                            passes = sum(s >= 0.5 for s in sharpes)
                            rows.append((pct, lookback, hold, tbr_thr,
                                         sharpes[0], sharpes[1], sharpes[2],
                                         np.mean(sharpes), passes))
        rows.sort(key=lambda x: -x[7])
        for row in rows[:15]:
            p, lb, h, tt, s0, s1, s2, sm, ps = row
            cross = f"({ps}/3)"
            print(f"  P{p:>2}  {lb:>4}b  {h:>4}b  thr={tt:.2f}  "
                  f"{s0:>+8.3f}  {s1:>+8.3f}  {s2:>+8.3f}  {sm:>+8.3f}  {cross}")

        print()
        print("Parametros nominales recomendados para implementacion C#:")
        best = rows[0]
        print(f"  ATR<P{best[0]}, lookback={best[1]}b, hold={best[2]}b, TBR_thr={best[3]:.2f}")

    else:
        print(f"Activos pasando: {assets_passing}/3")
        print()
        print("[FAIL] M4 RECHAZADO — ATR Compression + TBR sin edge suficiente en 1h")

        # Diagnostico adicional
        print()
        print("Diagnostico — mejores configs por activo:")
        for sym in SYMBOLS:
            best_res = sorted(
                [(k, v) for k, v in all_results[sym].items() if not np.isnan(v['sharpe'])],
                key=lambda x: -x[1]['sharpe']
            )[:3]
            print(f"  {sym}:")
            for key, res in best_res:
                pct, lb, h, tt = key
                print(f"    P{pct} look={lb}b hold={h}b TBR={tt:.2f}: "
                      f"Sharpe={res['sharpe']:+.3f} Win={res['win_rate']:.1%} T={res['trades']}")
