"""
H7 — Price-Volume Divergence (Short-only)
Hipótesis: cuando el precio sube pero el volumen comprador cae N barras
consecutivas, la suba es distribución (institucionales vendiendo en la
fortaleza de precio) → el precio revertirá → Short.
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "H7 — Price-Volume Divergence (Short-only)"
DIV_BARS    = [1, 2, 3]   # barras consecutivas de divergencia
HOLD_VALUES = [4, 6, 8]


def compute_signals(df: pd.DataFrame, div_bars: int) -> pd.Series:
    close = df["close"].astype(float)
    buy_vol = df["buy_volume"].astype(float)

    price_up    = close.diff() > 0
    buy_vol_dn  = buy_vol.diff() < 0

    divergence = price_up & buy_vol_dn

    if div_bars == 1:
        condition = divergence
    else:
        # Requiere div_bars consecutivos de divergencia
        condition = divergence.rolling(div_bars, min_periods=div_bars).min().astype(bool)

    signals = pd.Series(0, index=df.index, dtype=int)
    signals[condition] = -1   # Short
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: div_bars={DIV_BARS}, hold={HOLD_VALUES}")
    print("NOTA: Short-only — va contra el sesgo alcista cripto 2021-2024")
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
    header = (f"{'div_bars':>8}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for div_bars, hold in product(DIV_BARS, HOLD_VALUES):
        row = {"div_bars": div_bars, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], div_bars)
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
        print(f"{div_bars:>8}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
