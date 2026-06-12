"""
H1 — VWAP Deviation (Long-only)
Hipótesis: close muy por debajo del VWAP de las N horas previas indica presión
vendedora retail contra acumulación institucional → rebote esperado.
VWAP rolling = sum(close_i * volume_i) / sum(volume_i) sobre ventana N.
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED, MIN_TRADES)

NAME = "H1 — VWAP Deviation (Long-only)"
VWAP_WINDOWS  = [24, 48, 96]
DEV_THRS      = [0.010, 0.015, 0.020]   # close < vwap * (1 - dev_thr)
HOLD_VALUES   = [4, 6, 8]


def compute_rolling_vwap(df: pd.DataFrame, window: int) -> pd.Series:
    pv = df["close"].astype(float) * df["volume"].astype(float)
    return pv.rolling(window, min_periods=window).sum() / df["volume"].astype(float).rolling(window, min_periods=window).sum()


def compute_signals(df: pd.DataFrame, vwap_window: int, dev_thr: float) -> pd.Series:
    vwap = compute_rolling_vwap(df, vwap_window)
    close = df["close"].astype(float)
    dev = (close - vwap) / vwap
    signals = pd.Series(0, index=df.index, dtype=int)
    signals[dev < -dev_thr] = 1
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: vwap_window={VWAP_WINDOWS}, dev_thr={DEV_THRS}, hold={HOLD_VALUES}")
    print("=" * 78)

    data = {}
    for asset in ASSETS:
        try:
            df = load_features(asset).loc[IS_START:IS_END]
            data[asset] = df
            print(f"  {asset}: {len(df):,} barras")
        except FileNotFoundError:
            print(f"  {asset}: NO ENCONTRADO")
    print()

    col_w = 22
    header = (f"{'vwap_w':>8}  {'dev':>6}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for vwap_w, dev_thr, hold in product(VWAP_WINDOWS, DEV_THRS, HOLD_VALUES):
        row = {"vwap_w": vwap_w, "dev_thr": dev_thr, "hold": hold}
        n_pass = 0
        cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], vwap_w, dev_thr)
            sh, n, _ = compute_sharpe(sig, data[asset]["close"].astype(float), hold)
            row[key] = sh; row[f"{key}_n"] = n
            flag = "*" if not np.isnan(sh) and sh >= SHARPE_THRESHOLD else " "
            cell = f"N/A (T={n})" if np.isnan(sh) else f"{flag}{sh:+.3f} (T={n})"
            cells.append(f"{cell:>{col_w}}")
            if not np.isnan(sh) and sh >= SHARPE_THRESHOLD: n_pass += 1
        row["assets_passing"] = n_pass
        row["gate"] = "PASS" if n_pass >= ASSETS_NEEDED else "FAIL"
        valid = [row[k] for k in ["_btc_sharpe","_eth_sharpe","_sol_sharpe"] if not np.isnan(row[k])]
        row["_sum_sharpe"] = sum(valid)
        all_r.append(row)
        if n_pass >= ASSETS_NEEDED: passing.append(row)
        print(f"{vwap_w:>8}  {dev_thr:>6.3f}  {hold:>4}  {cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
