"""
H10 — Selling Climax (Long-only)
Hipótesis: cuando el número de trades (arrival_rate) está en el percentil
superior Y el precio cae en la misma barra, es un clímax de venta: los
vendedores están exhaustos, el precio rebota en las próximas barras.
Diferencia de H2: H2 busca absorción (precio neutro); H10 busca clímax
(precio cae bruscamente en alto volumen de trades).
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "H10 — Selling Climax (Long-only)"
VEL_WINDOWS  = [24, 48, 96]
VEL_PCTS     = [0.85, 0.90, 0.95]
NEG_RET_THRS = [-0.003, -0.005, -0.008]   # price_return < thr (precio cae al menos X%)
HOLD_VALUES  = [4, 6, 8]


def compute_signals(df: pd.DataFrame, vel_w: int, vel_pct: float, neg_ret_thr: float) -> pd.Series:
    arrival = df["arrival_rate"].astype(float)
    price_ret = df["price_return"].astype(float)
    arr_rank = rolling_percentile_rank(arrival, vel_w)

    signals = pd.Series(0, index=df.index, dtype=int)
    signals[(arr_rank >= vel_pct) & (price_ret < neg_ret_thr)] = 1
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: vel_w={VEL_WINDOWS}, vel_pct={VEL_PCTS}, neg_ret_thr={NEG_RET_THRS}, hold={HOLD_VALUES}")
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
    header = (f"{'vel_w':>6}  {'v_pct':>5}  {'ret_thr':>7}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for vel_w, vel_pct, neg_ret_thr, hold in product(VEL_WINDOWS, VEL_PCTS, NEG_RET_THRS, HOLD_VALUES):
        row = {"vel_w": vel_w, "vel_pct": vel_pct, "neg_ret_thr": neg_ret_thr, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], vel_w, vel_pct, neg_ret_thr)
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
        print(f"{vel_w:>6}  {vel_pct:>5.2f}  {neg_ret_thr:>7.3f}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
