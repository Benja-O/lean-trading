"""
M4 — Prima de asimetria / skewness (Sec 9.5), time-series por activo (Hito E screening)

Hipotesis:
  La literatura de "lottery demand" / asimetria (Boyer-Mitton-Vorkink,
  Bali-Cakici-Whitelaw) sugiere que los activos con skewness reciente alta
  (retornos con cola derecha gorda, tipo loteria) son sobre-demandados por
  inversores con preferencia por asimetria positiva, lo que COMPRIME su
  retorno esperado hacia adelante ("contrarian": short high-skew / long
  low-skew). La hipotesis rival ("momentum") es que un evento de skew alto
  refleja informacion real (ej. breakout) y el activo continua. Se testean
  ambas direcciones explicitamente porque el signo del efecto no esta
  pre-decidido para cripto intraday.

Senal:
  skew         = skewness rolling de price_return sobre `skew_window` barras
                 (causal: solo usa datos hasta la barra actual)
  skew_pct     = percentil de skew[t] dentro de las `rank_window` barras previas
                 (rolling_percentile_rank de m4_shared, sin lookahead)

  Modo "contrarian":
    Short cuando skew_pct >= threshold        (skew alto -> se espera compresion)
    Long  cuando skew_pct <= (1 - threshold)   (skew bajo/negativo -> prima esperada)

  Modo "momentum":
    Long  cuando skew_pct >= threshold         (skew alto -> continuacion)
    Short cuando skew_pct <= (1 - threshold)   (skew bajo -> continuacion bajista)

  Position tracking: ON.

Grid:
  skew_window in [24, 48, 96]
  rank_window in [96, 168]
  threshold   in [0.75, 0.85]
  hold        in [4, 6, 8]
  mode        in ["contrarian", "momentum"]
  Total: 3 x 2 x 2 x 3 x 2 = 72 configs

Gate M4: Sharpe >= 0.5 en >= 2/3 activos para alguna config (evaluado por modo).
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "Prima de asimetria / skewness (contrarian vs momentum)"
SKEW_WINDOW_VALUES = [24, 48, 96]
RANK_WINDOW_VALUES = [96, 168]
THRESHOLD_VALUES   = [0.75, 0.85]
HOLD_VALUES        = [4, 6, 8]
MODES              = ["contrarian", "momentum"]


def compute_skew(price_return: pd.Series, skew_window: int) -> pd.Series:
    """Skewness rolling causal de price_return sobre `skew_window` barras."""
    return price_return.rolling(skew_window, min_periods=skew_window).skew()


def compute_signals(df: pd.DataFrame, skew_window: int, rank_window: int,
                     threshold: float, mode: str) -> pd.Series:
    skew = compute_skew(df["price_return"].astype(float), skew_window)
    skew_pct = rolling_percentile_rank(skew, rank_window)

    signals = pd.Series(0, index=df.index, dtype=int)
    if mode == "contrarian":
        signals[skew_pct >= threshold]       = -1  # skew alto -> Short
        signals[skew_pct <= (1 - threshold)] = 1   # skew bajo -> Long
    else:  # momentum
        signals[skew_pct >= threshold]       = 1   # skew alto -> Long
        signals[skew_pct <= (1 - threshold)] = -1  # skew bajo -> Short
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: skew_window={SKEW_WINDOW_VALUES}, rank_window={RANK_WINDOW_VALUES}, "
          f"threshold={THRESHOLD_VALUES}, hold={HOLD_VALUES}, mode={MODES}")
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

    for mode in MODES:
        print()
        print(f"--- modo: {mode} ---")
        header = (f"{'skew_w':>6}  {'rank_w':>6}  {'thr':>5}  {'hold':>4}  "
                  f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
        print(header); print("-" * len(header))

        passing, all_r = [], []
        for skew_window, rank_window, threshold, hold in product(
            SKEW_WINDOW_VALUES, RANK_WINDOW_VALUES, THRESHOLD_VALUES, HOLD_VALUES
        ):
            row = {"skew_window": skew_window, "rank_window": rank_window,
                   "threshold": threshold, "hold": hold, "mode": mode}
            n_pass = 0; cells = []
            for key, asset in zip(["_btc_sharpe", "_eth_sharpe", "_sol_sharpe"], ASSETS):
                if asset not in data:
                    row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
                sig = compute_signals(data[asset], skew_window, rank_window, threshold, mode)
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
            print(f"{skew_window:>6}  {rank_window:>6}  {threshold:>5.2f}  {hold:>4}  "
                  f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

        summarize_results(passing, all_r, f"{NAME} — modo {mode}")


if __name__ == "__main__":
    main()
