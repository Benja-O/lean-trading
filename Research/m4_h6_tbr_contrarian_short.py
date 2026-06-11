"""
H6 — TBR Extremo Contrarian (Short-only)
Hipótesis: cuando el OFI (proxy del TBR) está en el percentil superior de su
distribución reciente (compra agresiva masiva), el precio ya subió con esas
compras y la presión compradora se agota → rebote bajista.
Análogo inverso al OFI Contrarian (que Long cuando OFI bajo).
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "H6 — TBR/OFI Extremo Contrarian Short (Short-only)"
OFI_WINDOWS  = [24, 48, 96]
THRESHOLDS   = [0.75, 0.80, 0.85]
HOLD_VALUES  = [4, 6, 8]


def compute_signals(df: pd.DataFrame, ofi_window: int, threshold: float) -> pd.Series:
    ofi = df["ofi"].astype(float)
    ofi_pct = rolling_percentile_rank(ofi, ofi_window)
    signals = pd.Series(0, index=df.index, dtype=int)
    signals[ofi_pct >= threshold] = -1   # Short when extreme buying
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: ofi_window={OFI_WINDOWS}, threshold={THRESHOLDS}, hold={HOLD_VALUES}")
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
    header = (f"{'ofi_w':>6}  {'thr':>5}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for ofi_w, thr, hold in product(OFI_WINDOWS, THRESHOLDS, HOLD_VALUES):
        row = {"ofi_w": ofi_w, "thr": thr, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], ofi_w, thr)
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
        print(f"{ofi_w:>6}  {thr:>5.2f}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
