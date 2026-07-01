"""
M4 — Anomalia de baja volatilidad (Sec 3.4), time-series por activo (Hito E screening)

Hipotesis:
  La anomalia de baja volatilidad (Ang et al. 2006, Frazzini-Pedersen "Betting
  Against Beta") predice que los activos/regimenes de baja volatilidad realizada
  tienen mejor retorno ajustado por riesgo hacia adelante que los de alta
  volatilidad. Se testea en version time-series por activo (no hay panel amplio
  para cross-section real con solo 3 activos):

    Long  cuando la vol realizada reciente esta en el percentil BAJO de su
          propia historia (regimen de calma -> se espera continuacion/drift
          positivo con menor riesgo).
    Short cuando la vol realizada esta en el percentil ALTO (regimen turbulento
          -> se espera que el activo underperforme, simetrico a la anomalia).

Senal:
  realized_vol = std(price_return) sobre `vol_window` barras (rolling, causal)
  vol_pct      = percentil de realized_vol[t] dentro de las `rank_window` barras
                 previas (rolling_percentile_rank de m4_shared, shift(1) interno
                 -> sin lookahead)
  Long:  vol_pct <= (1 - threshold)   [percentil bajo -> baja vol]
  Short: vol_pct >= threshold          [percentil alto -> alta vol]

  Position tracking: ON.

Grid:
  vol_window  in [12, 24, 48]     (horas: ventana de calculo de vol realizada)
  rank_window in [96, 168]        (horas: ventana de ranking percentil, ~4d y ~1sem)
  threshold   in [0.75, 0.80, 0.85]
  hold        in [4, 6, 8]
  Total: 3 x 2 x 3 x 3 = 54 configs

Gate M4: Sharpe >= 0.5 en >= 2/3 activos para alguna config.
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "Anomalia de baja volatilidad (vol realizada, contrarian de regimen)"
VOL_WINDOW_VALUES  = [12, 24, 48]
RANK_WINDOW_VALUES = [96, 168]
THRESHOLD_VALUES   = [0.75, 0.80, 0.85]
HOLD_VALUES        = [4, 6, 8]


def compute_realized_vol(price_return: pd.Series, vol_window: int) -> pd.Series:
    """Vol realizada causal: std de price_return[t-vol_window+1 .. t]."""
    return price_return.rolling(vol_window, min_periods=vol_window).std(ddof=1)


def compute_signals(df: pd.DataFrame, vol_window: int, rank_window: int, threshold: float) -> pd.Series:
    realized_vol = compute_realized_vol(df["price_return"].astype(float), vol_window)
    vol_pct = rolling_percentile_rank(realized_vol, rank_window)

    signals = pd.Series(0, index=df.index, dtype=int)
    signals[vol_pct <= (1 - threshold)] = 1    # baja vol -> Long
    signals[vol_pct >= threshold]       = -1   # alta vol -> Short
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: vol_window={VOL_WINDOW_VALUES}, rank_window={RANK_WINDOW_VALUES}, "
          f"threshold={THRESHOLD_VALUES}, hold={HOLD_VALUES}")
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
    header = (f"{'vol_w':>6}  {'rank_w':>6}  {'thr':>5}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for vol_window, rank_window, threshold, hold in product(
        VOL_WINDOW_VALUES, RANK_WINDOW_VALUES, THRESHOLD_VALUES, HOLD_VALUES
    ):
        row = {"vol_window": vol_window, "rank_window": rank_window,
               "threshold": threshold, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe", "_eth_sharpe", "_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], vol_window, rank_window, threshold)
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
        print(f"{vol_window:>6}  {rank_window:>6}  {threshold:>5.2f}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
