"""
H2 — Trade Count Spike + Absorption (Long-only)
Hipótesis: salto brusco en número de trades (arrival_rate > P90) con movimiento
de precio mínimo indica absorción institucional de la venta retail → rebote.
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "H2 — Trade Count Spike + Absorption (Long-only)"
SPIKE_WINDOWS = [24, 48, 96]
SPIKE_PCTS    = [0.90, 0.95]
MAX_RET_THRS  = [0.003, 0.005, 0.008]   # abs(price_return) < thr
HOLD_VALUES   = [4, 6, 8]


def compute_signals(df: pd.DataFrame, spike_w: int, spike_pct: float, max_ret: float) -> pd.Series:
    arr_rate = df["arrival_rate"].astype(float)
    price_ret = df["price_return"].astype(float).abs()
    arr_pct = rolling_percentile_rank(arr_rate, spike_w)
    signals = pd.Series(0, index=df.index, dtype=int)
    signals[(arr_pct >= spike_pct) & (price_ret < max_ret)] = 1
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: spike_w={SPIKE_WINDOWS}, spike_pct={SPIKE_PCTS}, max_ret={MAX_RET_THRS}, hold={HOLD_VALUES}")
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
    header = (f"{'spike_w':>8}  {'s_pct':>6}  {'max_r':>6}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for spike_w, spike_pct, max_ret, hold in product(SPIKE_WINDOWS, SPIKE_PCTS, MAX_RET_THRS, HOLD_VALUES):
        row = {"spike_w": spike_w, "spike_pct": spike_pct, "max_ret": max_ret, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], spike_w, spike_pct, max_ret)
            sh, n, _ = compute_sharpe(sig, data[asset]["close"].astype(float), hold)
            row[key] = sh
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
        print(f"{spike_w:>8}  {spike_pct:>6.2f}  {max_ret:>6.3f}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
