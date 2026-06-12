"""
H4 — CVD Velocity / Acceleration (Long-only)
Hipótesis: cuando la aceleración del flujo neto (delta de cvd_delta) está en el
percentil superior de su distribución reciente, los compradores están
acelerando → señal de momentum de flujo → precio continúa subiendo.
Δcvd_delta[t] = cvd_delta[t] - cvd_delta[t-1]
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "H4 — CVD Velocity (Long-only, Momentum)"
ACCEL_WINDOWS = [6, 12, 24]
ACCEL_PCTS    = [0.80, 0.85, 0.90]
HOLD_VALUES   = [2, 4, 6]   # momentum decae rápido → holds cortos


def compute_signals(df: pd.DataFrame, accel_w: int, accel_pct: float) -> pd.Series:
    vol = df["volume"].astype(float).replace(0.0, float("nan"))
    cvd_delta_norm = df["cvd_delta"].astype(float) / vol
    accel = cvd_delta_norm.diff()
    accel_pct_rank = rolling_percentile_rank(accel, accel_w)
    signals = pd.Series(0, index=df.index, dtype=int)
    signals[accel_pct_rank >= accel_pct] = 1
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: accel_w={ACCEL_WINDOWS}, accel_pct={ACCEL_PCTS}, hold={HOLD_VALUES}")
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
    header = (f"{'accel_w':>8}  {'a_pct':>6}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for accel_w, accel_pct, hold in product(ACCEL_WINDOWS, ACCEL_PCTS, HOLD_VALUES):
        row = {"accel_w": accel_w, "accel_pct": accel_pct, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], accel_w, accel_pct)
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
        print(f"{accel_w:>8}  {accel_pct:>6.2f}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
