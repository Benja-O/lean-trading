# -*- coding: utf-8 -*-
"""
M4 — Carry alto-menos-bajo via funding rate (Sec 8.2.1), diario (Hito E screening)

Hipotesis (carry, distinta de Funding Rate Positioning ya probado):
  El funding rate de un perpetuo es el pago periodico entre longs y shorts.
  Un carry trade puro cobra ese pago: cuando el funding es muy positivo, ser
  SHORT recibe funding de los longs; cuando el funding es muy negativo, ser
  LONG recibe funding de los shorts. A diferencia de FRP (m4_funding_rate_
  positioning.py), que apuesta a un mecanismo de REVERSION DE PRECIO por
  desapalancamiento de longs sobrecargados (short-only, con filtro de
  tendencia MA), este script:
    - es BIDIRECCIONAL (long en funding extremo negativo, short en extremo
      positivo — el carry trade clasico "high-minus-low"),
    - NO usa filtro de tendencia (el carry no depende de la direccion del
      precio, solo del signo/magnitud del funding),
    - mide el Sharpe sobre el retorno de PRECIO en el periodo de hold (misma
      convencion que el resto del harness M4 de este repo — el ingreso de
      funding en si no se modela explicitamente; es una simplificacion ya
      usada en m4_funding_rate_positioning.py).

Señal:
  funding_daily = promedio de las 3 liquidaciones UTC/dia (aggregate_funding_daily)
  z             = z-score de funding_daily sobre `z_window` dias
  Long:  z <= -z_threshold   (funding muy negativo -> shorts pagan a longs)
  Short: z >= +z_threshold   (funding muy positivo -> longs pagan a shorts)

  Position tracking: ON (no reentrada hasta cerrar el trade anterior).

Grid:
  z_threshold in [1.0, 1.5, 2.0]
  z_window    in [14, 30, 60]  dias
  hold_days   in [3, 5, 7]
  Total: 3 x 3 x 3 = 27 configs

Gate M4: Sharpe >= 0.5 en >= 2/3 activos para alguna config.
"""

import numpy as np
import pandas as pd
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).parent))
from m4_funding_rate_positioning import (
    download_funding_rates, download_daily_closes, aggregate_funding_daily,
    compute_zscore,
)

SYMBOLS = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
IS_START = "2021-01-01"
IS_END   = "2024-12-31"

Z_THRESHOLDS = [1.0, 1.5, 2.0]
Z_WINDOWS    = [14, 30, 60]
HOLD_DAYS    = [3, 5, 7]

SHARPE_THRESHOLD = 0.5
ASSETS_NEEDED    = 2
MIN_TRADES       = 30


def test_carry(closes: pd.Series, funding_daily: pd.Series,
                z_threshold: float, z_window: int, hold_days: int) -> dict:
    """Carry bidireccional: Long funding muy negativo, Short funding muy positivo."""
    combined = pd.DataFrame({"close": closes, "funding": funding_daily}).dropna(
        subset=["close", "funding"]
    )
    fr = combined["funding"]
    cl = combined["close"]
    z = compute_zscore(fr, z_window)

    z_arr = z.values
    cl_arr = cl.values
    n = len(cl_arr)

    trades = []
    i = 0
    while i < n - hold_days:
        zi = z_arr[i]
        if np.isnan(zi):
            i += 1
            continue

        direction = 0
        if zi <= -z_threshold:
            direction = 1    # funding muy negativo -> Long (cobra carry)
        elif zi >= z_threshold:
            direction = -1   # funding muy positivo -> Short (cobra carry)

        if direction != 0:
            entry = cl_arr[i]
            exit_ = cl_arr[i + hold_days]
            ret = direction * (exit_ - entry) / entry
            trades.append(ret)
            i += hold_days + 1
        else:
            i += 1

    if len(trades) < MIN_TRADES:
        return {"sharpe": np.nan, "win_rate": np.nan, "mean_return": np.nan, "trades": len(trades)}

    returns = np.array(trades)
    mean_ret = returns.mean()
    std_ret = returns.std(ddof=1)
    win_rate = (returns > 0).mean()

    years = n / 365.0
    trades_per_year = len(trades) / years
    sharpe = (mean_ret / std_ret * np.sqrt(trades_per_year)) if std_ret > 0 else 0.0

    return {"sharpe": sharpe, "win_rate": win_rate, "mean_return": mean_ret,
            "std_return": std_ret, "trades": len(trades)}


def main():
    print("=" * 78)
    print("M4 — Carry alto-menos-bajo via funding rate (bidireccional, diario)")
    print(f"IS: {IS_START} -> {IS_END}")
    print(f"Grid: z_threshold={Z_THRESHOLDS}, z_window={Z_WINDOWS}, hold_days={HOLD_DAYS}")
    print(f"Gate: Sharpe >= {SHARPE_THRESHOLD} en >= {ASSETS_NEEDED}/3 activos")
    print("=" * 78)

    start_ms = int(pd.Timestamp(IS_START, tz="UTC").timestamp() * 1000)
    end_ms = int(pd.Timestamp(IS_END, tz="UTC").timestamp() * 1000)

    price_data = {}
    funding_data = {}
    for sym in SYMBOLS:
        print(f"Descargando {sym}...")
        price_data[sym] = download_daily_closes(sym, start_ms, end_ms)
        fr_raw = download_funding_rates(sym, start_ms, end_ms)
        funding_data[sym] = aggregate_funding_daily(fr_raw)
        print(f"  Precios: {len(price_data[sym])} dias  Funding: {len(funding_data[sym])} dias")
    print()

    col_w = 22
    header = (f"{'z_thr':>5}  {'z_win':>5}  {'hold':>4}  "
              f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}")
    print(header); print("-" * len(header))

    passing, all_r = [], []
    for z_threshold in Z_THRESHOLDS:
        for z_window in Z_WINDOWS:
            for hold_days in HOLD_DAYS:
                row = {"z_threshold": z_threshold, "z_window": z_window, "hold_days": hold_days}
                n_pass = 0
                cells = []
                for key, sym in zip(["_btc_sharpe", "_eth_sharpe", "_sol_sharpe"], SYMBOLS):
                    res = test_carry(price_data[sym], funding_data[sym], z_threshold, z_window, hold_days)
                    sh, n = res["sharpe"], res["trades"]
                    row[key] = sh
                    flag = "*" if not np.isnan(sh) and sh >= SHARPE_THRESHOLD else " "
                    cell = f"N/A (T={n})" if np.isnan(sh) else f"{flag}{sh:+.3f} (T={n})"
                    cells.append(f"{cell:>{col_w}}")
                    if not np.isnan(sh) and sh >= SHARPE_THRESHOLD:
                        n_pass += 1
                row["assets_passing"] = n_pass
                row["gate"] = "PASS" if n_pass >= ASSETS_NEEDED else "FAIL"
                valid = [row[k] for k in ["_btc_sharpe", "_eth_sharpe", "_sol_sharpe"] if not np.isnan(row[k])]
                row["_sum_sharpe"] = sum(valid)
                all_r.append(row)
                if n_pass >= ASSETS_NEEDED:
                    passing.append(row)
                print(f"{z_threshold:>5.2f}  {z_window:>5}  {hold_days:>4}  "
                      f"{cells[0]}  {cells[1]}  {cells[2]}  {n_pass:>6}  {row['gate']:>8}")

    print()
    print("=" * 78)
    if passing:
        best = max(passing, key=lambda r: r["_sum_sharpe"])
        print(f"M4 PASSED — {len(passing)} config(s) superaron el gate")
        print(f"Parametros nominales: z_threshold={best['z_threshold']}, "
              f"z_window={best['z_window']}, hold_days={best['hold_days']}")
        btc, eth, sol = best["_btc_sharpe"], best["_eth_sharpe"], best["_sol_sharpe"]
        sharpes = [s for s in [btc, eth, sol] if not np.isnan(s)]
        print(f"Sharpes (BTC/ETH/SOL): {btc:+.3f} / {eth:+.3f} / {sol:+.3f}  (media: {np.mean(sharpes):+.3f})")
    else:
        print("M4 FAILED — ninguna config alcanzo Sharpe >= 0.5 en >= 2/3 activos")
    print("=" * 78)


if __name__ == "__main__":
    main()
