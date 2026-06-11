"""
H5 — Trade Size Asymmetry / Institutional Signal (Long-only)
Hipótesis: cuando el tamaño medio de trade está en el percentil superior
(institucionales operando en bloques grandes) Y el ratio buy/sell es positivo
(compran más que venden), hay acumulación institucional silenciosa → Long.
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "H5 — Trade Size Institutional (Long-only)"
SIZE_WINDOWS  = [24, 48, 96]
SIZE_PCTS     = [0.75, 0.85, 0.90]
BSR_THRS      = [1.02, 1.05, 1.10]   # buy_sell_ratio > thr (compras > ventas)
HOLD_VALUES   = [4, 6, 8]


def compute_signals(df: pd.DataFrame, size_w: int, size_pct: float, bsr_thr: float) -> pd.Series:
    mean_size = df["mean_trade_size"].astype(float)
    bsr       = df["buy_sell_ratio"].astype(float)
    size_rank = rolling_percentile_rank(mean_size, size_w)
    signals = pd.Series(0, index=df.index, dtype=int)
    signals[(size_rank >= size_pct) & (bsr > bsr_thr)] = 1
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: size_w={SIZE_WINDOWS}, size_pct={SIZE_PCTS}, bsr_thr={BSR_THRS}, hold={HOLD_VALUES}")
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
    header = (f"{'size_w':>7}  {'s_pct':>6}  {'bsr':>5}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for size_w, size_pct, bsr_thr, hold in product(SIZE_WINDOWS, SIZE_PCTS, BSR_THRS, HOLD_VALUES):
        row = {"size_w": size_w, "size_pct": size_pct, "bsr_thr": bsr_thr, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], size_w, size_pct, bsr_thr)
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
        print(f"{size_w:>7}  {size_pct:>6.2f}  {bsr_thr:>5.2f}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
