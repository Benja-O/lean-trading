"""
H9 — Intraday OFI Nocturno (Long-only)
Hipótesis: el OFI Contrarian tiene más edge durante las horas de baja
liquidez UTC 00-06 (madrugada asiática), donde cada trade tiene mayor
impacto en precio y la señal de overselling es más predictiva.
Solo entra en esas horas; el hold puede extenderse más allá de la noche.
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "H9 — Intraday OFI Nocturno UTC 00-06 (Long-only)"
OFI_WINDOWS   = [24, 48]
OFI_THRS      = [0.75, 0.80, 0.85]
NIGHT_END_HRS = [4, 6]    # UTC: horas 0 a N-1 son "nocturnas"
HOLD_VALUES   = [4, 6, 8]


def compute_signals(df: pd.DataFrame, ofi_w: int, ofi_thr: float, night_end: int) -> pd.Series:
    ofi = df["ofi"].astype(float)
    ofi_rank = rolling_percentile_rank(ofi, ofi_w)

    is_night = df.index.hour < night_end

    signals = pd.Series(0, index=df.index, dtype=int)
    signals[(ofi_rank < (1 - ofi_thr)) & is_night] = 1
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: ofi_w={OFI_WINDOWS}, ofi_thr={OFI_THRS}, night_end={NIGHT_END_HRS}, hold={HOLD_VALUES}")
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
    header = (f"{'ofi_w':>6}  {'o_thr':>5}  {'n_end':>5}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for ofi_w, ofi_thr, night_end, hold in product(OFI_WINDOWS, OFI_THRS, NIGHT_END_HRS, HOLD_VALUES):
        row = {"ofi_w": ofi_w, "ofi_thr": ofi_thr, "night_end": night_end, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], ofi_w, ofi_thr, night_end)
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
        print(f"{ofi_w:>6}  {ofi_thr:>5.2f}  {night_end:>5}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
