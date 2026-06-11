"""
H3 — CVD Sell Exhaustion (Long-only, CLEAN — sin solapamiento)
Hipótesis: cuando el precio hace nuevo mínimo de N barras pero el flujo
neto de la barra (cvd_delta) es positivo (compradores netos), los vendedores
están exhaustos y el precio rebota.
Diferencia vs CVD Bullish Divergence original: usa cvd_delta intra-barra
(no CVD acumulativo), y position tracking correcto desde el inicio.
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "H3 — CVD Sell Exhaustion (Long-only)"
LOOKBACKS   = [12, 24, 48]
CVD_THRS    = [0.0, 0.1, 0.2]   # cvd_delta / volume > thr (normalizado)
HOLD_VALUES = [4, 6, 8]


def compute_signals(df: pd.DataFrame, lookback: int, cvd_thr: float) -> pd.Series:
    close = df["close"].astype(float)
    vol   = df["volume"].astype(float).replace(0.0, float("nan"))
    cvd_delta_norm = df["cvd_delta"].astype(float) / vol   # normalizado por volumen

    price_at_n_low = close == close.rolling(lookback, min_periods=lookback).min()
    buying_pressure = cvd_delta_norm > cvd_thr

    signals = pd.Series(0, index=df.index, dtype=int)
    signals[price_at_n_low & buying_pressure] = 1
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: lookback={LOOKBACKS}, cvd_thr={CVD_THRS}, hold={HOLD_VALUES}")
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
    header = (f"{'lookback':>8}  {'cvd_thr':>7}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for lookback, cvd_thr, hold in product(LOOKBACKS, CVD_THRS, HOLD_VALUES):
        row = {"lookback": lookback, "cvd_thr": cvd_thr, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], lookback, cvd_thr)
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
        print(f"{lookback:>8}  {cvd_thr:>7.2f}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
