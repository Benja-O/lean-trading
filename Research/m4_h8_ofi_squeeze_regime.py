"""
H8 — OFI Contrarian + Squeeze Regime Proxy (Long-only)
Hipótesis: el OFI Contrarian tiene más edge en régimen de baja volatilidad
(Squeeze en HMM) donde el precio es más sensible a los flujos de órdenes.
Proxy de Squeeze: ATR normalizado en el percentil inferior de su historial.
El QC backtest usará el HMM real; el M4 usa ATR como aproximación.
"""
import sys; sys.path.insert(0, str(__import__('pathlib').Path(__file__).parent))
import numpy as np
import pandas as pd
from itertools import product
from m4_shared import (load_features, compute_sharpe, rolling_percentile_rank,
                        summarize_results, ASSETS, IS_START, IS_END,
                        SHARPE_THRESHOLD, ASSETS_NEEDED)

NAME = "H8 — OFI Contrarian + Squeeze Proxy ATR (Long-only)"
OFI_WINDOWS  = [24, 48, 96]
OFI_THRS     = [0.75, 0.80, 0.85]
ATR_WINDOWS  = [24, 48]
ATR_PCTS     = [0.25, 0.33]   # ATR < P25 o P33 = régimen Squeeze
HOLD_VALUES  = [4, 6, 8]


def compute_atr(df: pd.DataFrame, window: int) -> pd.Series:
    high  = df["high"].astype(float)
    low   = df["low"].astype(float)
    close = df["close"].astype(float)
    prev_close = close.shift(1)
    tr = pd.concat([high - low, (high - prev_close).abs(), (low - prev_close).abs()], axis=1).max(axis=1)
    return tr.rolling(window, min_periods=window).mean()


def compute_signals(df: pd.DataFrame, ofi_w: int, ofi_thr: float, atr_w: int, atr_pct: float) -> pd.Series:
    ofi = df["ofi"].astype(float)
    ofi_rank = rolling_percentile_rank(ofi, ofi_w)

    atr = compute_atr(df, atr_w)
    atr_rank = rolling_percentile_rank(atr, atr_w * 4)   # comparar ATR contra ventana más larga

    ofi_signal  = ofi_rank < (1 - ofi_thr)     # OFI bajo = vendedores extremos
    squeeze_proxy = atr_rank < atr_pct          # ATR bajo = régimen tranquilo

    signals = pd.Series(0, index=df.index, dtype=int)
    signals[ofi_signal & squeeze_proxy] = 1
    return signals


def main():
    print("=" * 78)
    print(f"M4 — {NAME}")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Fee: 0.04% RT")
    print(f"Grid: ofi_w={OFI_WINDOWS}, ofi_thr={OFI_THRS}, atr_w={ATR_WINDOWS}, atr_pct={ATR_PCTS}, hold={HOLD_VALUES}")
    print("NOTA: ATR<Pxx es proxy de HMM Squeeze. El QC backtest usará el HMM real.")
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
    header = (f"{'ofi_w':>6}  {'o_thr':>5}  {'atr_w':>5}  {'a_pct':>5}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for ofi_w, ofi_thr, atr_w, atr_pct, hold in product(OFI_WINDOWS, OFI_THRS, ATR_WINDOWS, ATR_PCTS, HOLD_VALUES):
        row = {"ofi_w": ofi_w, "ofi_thr": ofi_thr, "atr_w": atr_w, "atr_pct": atr_pct, "hold": hold}
        n_pass = 0; cells = []
        for key, asset in zip(["_btc_sharpe","_eth_sharpe","_sol_sharpe"], ASSETS):
            if asset not in data:
                row[key] = float("nan"); cells.append(f"{'N/A':>{col_w}}"); continue
            sig = compute_signals(data[asset], ofi_w, ofi_thr, atr_w, atr_pct)
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
        print(f"{ofi_w:>6}  {ofi_thr:>5.2f}  {atr_w:>5}  {atr_pct:>5.2f}  {hold:>4}  "
              f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    summarize_results(passing, all_r, NAME)


if __name__ == "__main__":
    main()
