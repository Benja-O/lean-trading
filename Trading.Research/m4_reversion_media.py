"""
M4 — Reversion a la media (Sec 3.9), time-series por activo (Hito E screening)

Hipotesis:
  El precio oscila alrededor de una media movil de corto/mediano plazo.
  Cuando el z-score de close vs esa media esta en un extremo, el precio
  tiende a revertir hacia la media en las N barras siguientes.

  Contrarian:
    Long  cuando z <= -threshold  (precio muy por debajo de la media)
    Short cuando z >= +threshold  (precio muy por encima de la media)

Senal:
  ma      = media movil simple de `close` sobre `window` barras (shift(1) implicito
            porque z-score usa solo datos hasta t-1 vs close[t] -- ver compute_signals)
  z       = (close[t] - ma[t-1]) / std[t-1]   (media y std calculadas con barras
            PREVIAS a t, sin lookahead)
  Long:  z <= -threshold
  Short: z >= +threshold

  Position tracking: ON (filter_overlapping_signals de m4_shared).

Grid:
  window    in [24, 48, 96, 168]   (horas: 1d, 2d, 4d, 1 semana)
  threshold in [1.5, 2.0, 2.5]
  hold      in [4, 6, 8]
  Total: 4 x 3 x 3 = 36 configs

Gate M4: Sharpe >= 0.5 en >= 2/3 activos para alguna config.
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, summarize_results,
                        ASSETS, IS_START, IS_END, SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "Reversion a la media (z-score vs MA, contrarian bidireccional)"
WINDOW_VALUES    = [24, 48, 96, 168]
THRESHOLD_VALUES = [1.5, 2.0, 2.5]
HOLD_VALUES      = [4, 6, 8]


def compute_zscore(close: pd.Series, window: int) -> pd.Series:
    """z-score de close[t] vs media/std de las `window` barras previas (sin lookahead)."""
    ma  = close.shift(1).rolling(window, min_periods=window).mean()
    std = close.shift(1).rolling(window, min_periods=window).std(ddof=1)
    return (close - ma) / std


def compute_signals(df: pd.DataFrame, window: int, threshold: float) -> pd.Series:
    z = compute_zscore(df["close"].astype(float), window)
    signals = pd.Series(0, index=df.index, dtype=int)
    signals[z <= -threshold] = 1
    signals[z >= threshold]  = -1
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: window={WINDOW_VALUES}, threshold={THRESHOLD_VALUES}, hold={HOLD_VALUES}")
    print(f"Gate: Sharpe >= {SHARPE_THRESHOLD} en >= {ASSETS_NEEDED}/3 activos")
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
    header = (f"{'window':>6}  {'thr':>5}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for window, threshold, hold in product(WINDOW_VALUES, THRESHOLD_VALUES, HOLD_VALUES):
        row = {"window": window, "threshold": threshold, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe", "_eth_sharpe", "_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], window, threshold)
            sh, n, _ = compute_sharpe(sig, data[asset]["close"].astype(float), hold)
            row[key] = sh
            flag = "*" if not np.isnan(sh) and sh >= SHARPE_THRESHOLD else " "
            cell = f"N/A (T={n})" if np.isnan(sh) else f"{flag}{sh:+.3f} (T={n})"
            cells.append(f"{cell:>{col_w}}")
            if not np.isnan(sh) and sh >= SHARPE_THRESHOLD: n_pass += 1
        row["assets_passing"] = n_pass
        row["gate"] = "PASS" if n_pass >= ASSETS_NEEDED else "FAIL"
        valid = [row[k] for k in ["_btc_sharpe", "_eth_sharpe", "_sol_sharpe"] if not np.isnan(row[k])]
        row["_sum_sharpe"] = sum(valid)
        all_r.append(row)
        if n_pass >= ASSETS_NEEDED: passing.append(row)
        print(f"{window:>6}  {threshold:>5.2f}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
