"""
M4 pure-signal backtest: OFI Contrarian sub-hora (5m y 15m) — Eje 3

Hipótesis:
  El edge de microestructura OFI Contrarian vive en sub-hora (5m/15m), no en 1h.
  La literatura ubica el edge de order-flow exhaustion en timeframes cortos.
  Long-only cuando OFI está en el percentil inferior de su distribución reciente
  (muchos vendedores agresivos → rebote esperado).

Señal:
  ofi_pct = percentil de ofi[t] dentro de `window` barras previas (excluye t)
  Long: ofi_pct < (1 - threshold)  — extremo vendedor => rebote esperado
  Flat: resto (Long-only: sin Short)

  Position tracking: ON (suprime señales mientras hay posición abierta).

Grid:
  ofi_window ∈ [12, 24, 48]      (barras de lookback)
  threshold  ∈ [0.75, 0.80, 0.85]
  hold_bars  ∈ [3, 6, 12]        (barras de hold)
  timeframe  ∈ ["5m", "15m"]
  Total: 3 × 3 × 3 × 2 = 54 configs

COST_RT = 0.0 (pure signal test — sin costos, idéntico a m4_ofi_contrarian.py).

Gate M4: Sharpe >= 0.5 en >= 2 de 3 activos para al menos una configuración
         (pre-registrado: idéntico al gate de m4_ofi_contrarian.py).
"""

import pandas as pd
import numpy as np
from pathlib import Path
from itertools import product

# ── Configuración ──────────────────────────────────────────────────────────────

DATA_DIR  = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\AggTrades\features")
ASSETS    = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
IS_START  = "2021-01-01"
IS_END    = "2024-12-31"

OFI_WINDOW_VALUES = [12, 24, 48]
THRESHOLD_VALUES  = [0.75, 0.80, 0.85]
HOLD_VALUES       = [3, 6, 12]
TIMEFRAMES        = ["5m", "15m"]
SHARPE_THRESHOLD  = 0.5
ASSETS_NEEDED     = 2
MIN_TRADES        = 30

# ── Helpers ────────────────────────────────────────────────────────────────────

def load_features(asset: str, timeframe: str) -> pd.DataFrame:
    path = DATA_DIR / f"{asset}_{timeframe}_features.parquet"
    df = pd.read_parquet(path)
    df["bar"] = pd.to_datetime(df["bar"], utc=True)
    df = df.set_index("bar").sort_index()
    return df


def compute_ofi_percentile(ofi: pd.Series, window: int) -> pd.Series:
    """Percentil de ofi[t] dentro de las `window` barras previas (excluye barra actual)."""
    shifted = ofi.shift(1)
    return shifted.rolling(window, min_periods=window).apply(
        lambda x: float((x[:-1] < x[-1]).sum()) / max(len(x) - 1, 1),
        raw=True
    )


def compute_signals(df: pd.DataFrame, ofi_window: int, threshold: float) -> pd.Series:
    ofi = df["ofi"].astype(float)
    ofi_pct = compute_ofi_percentile(ofi, ofi_window)
    signals = pd.Series(0, index=df.index, dtype=int)
    signals[ofi_pct < (1 - threshold)] = 1
    return signals


def filter_overlapping_signals(signals: pd.Series, hold: int) -> pd.Series:
    """Suprime señales mientras hay posición abierta (position tracking)."""
    arr = signals.values.copy()
    in_position_until = -1
    for i in range(len(arr)):
        if arr[i] != 0:
            if i <= in_position_until:
                arr[i] = 0
            else:
                in_position_until = i + hold - 1
    return pd.Series(arr, index=signals.index, dtype=int)


def compute_sharpe(signals: pd.Series, close: pd.Series, hold: int) -> tuple[float, int, float]:
    """Retorna (sharpe_anualizado, num_trades, mean_return_pct) con position tracking."""
    signals = filter_overlapping_signals(signals, hold)
    forward_ret = close.shift(-hold) / close - 1

    mask = (signals != 0) & forward_ret.notna()
    num_trades = int(mask.sum())

    if num_trades < MIN_TRADES:
        return float("nan"), num_trades, float("nan")

    trade_returns = signals[mask].astype(float) * forward_ret[mask]
    mean_r = float(trade_returns.mean())
    std_r  = float(trade_returns.std(ddof=1))

    if std_r == 0:
        return float("nan"), num_trades, mean_r * 100

    years           = (close.index[-1] - close.index[0]).total_seconds() / (365.25 * 86400)
    trades_per_year = num_trades / years
    sharpe          = mean_r / std_r * np.sqrt(trades_per_year)
    return float(sharpe), num_trades, mean_r * 100


# ── Grid por timeframe ─────────────────────────────────────────────────────────

def run_timeframe(timeframe: str) -> tuple[list, list]:
    """Corre el grid completo para un timeframe. Retorna (all_results, passing_configs)."""
    print(f"\n{'=' * 78}")
    print(f"TF: {timeframe}  |  IS: {IS_START} -> {IS_END}  |  Long-only")
    print(f"Grid: window={OFI_WINDOW_VALUES}, threshold={THRESHOLD_VALUES}, hold={HOLD_VALUES}")
    print(f"{'=' * 78}")

    data: dict[str, pd.DataFrame] = {}
    for asset in ASSETS:
        try:
            df = load_features(asset, timeframe)
            df = df.loc[IS_START:IS_END]
            data[asset] = df
            print(f"  {asset}: {len(df):,} barras  ({df.index[0].date()} - {df.index[-1].date()})")
        except FileNotFoundError:
            print(f"  {asset}: ARCHIVO NO ENCONTRADO — {DATA_DIR / f'{asset}_{timeframe}_features.parquet'}")
    print()

    if not data:
        print("No se pudo cargar ningun asset.")
        return [], []

    col_w = 22
    header = (
        f"{'window':>6}  {'thr':>5}  {'hold':>4}  "
        f"{'BTCUSDT':>{col_w}}  {'ETHUSDT':>{col_w}}  {'SOLUSDT':>{col_w}}  "
        f"{'>=0.5':>6}  {'gate':>8}"
    )
    print(header)
    print("-" * len(header))

    all_results     = []
    passing_configs = []

    for ofi_window, threshold, hold in product(OFI_WINDOW_VALUES, THRESHOLD_VALUES, HOLD_VALUES):
        row: dict = {
            "ofi_window": ofi_window, "threshold": threshold,
            "hold": hold, "tf": timeframe,
        }
        assets_passing = 0

        for asset in ASSETS:
            if asset not in data:
                row[asset] = "N/A"
                row[f"_{asset}_sharpe"] = float("nan")
                continue

            df = data[asset]
            signals = compute_signals(df, ofi_window, threshold)
            sharpe, n_trades, mean_pct = compute_sharpe(
                signals, df["close"].astype(float), hold
            )

            if np.isnan(sharpe):
                cell = f"N/A (T={n_trades})"
            else:
                flag = "*" if sharpe >= SHARPE_THRESHOLD else " "
                cell = f"{flag}{sharpe:+.3f} (T={n_trades})"

            row[asset] = cell
            row[f"_{asset}_sharpe"] = sharpe
            row[f"_{asset}_n"]      = n_trades

            if not np.isnan(sharpe) and sharpe >= SHARPE_THRESHOLD:
                assets_passing += 1

        row["assets_passing"] = assets_passing
        row["gate"] = "PASS" if assets_passing >= ASSETS_NEEDED else "FAIL"
        all_results.append(row)
        if assets_passing >= ASSETS_NEEDED:
            passing_configs.append(row)

        btc_cell = row.get("BTCUSDT", "N/A")
        eth_cell = row.get("ETHUSDT", "N/A")
        sol_cell = row.get("SOLUSDT", "N/A")
        print(
            f"{ofi_window:>6}  {threshold:>5.2f}  {hold:>4}  "
            f"{btc_cell:>{col_w}}  {eth_cell:>{col_w}}  {sol_cell:>{col_w}}  "
            f"{assets_passing:>6}  {row['gate']:>8}"
        )

    if passing_configs:
        best = max(
            passing_configs,
            key=lambda r: sum(
                r.get(f"_{a}_sharpe", float("-inf"))
                for a in ASSETS
                if not np.isnan(r.get(f"_{a}_sharpe", float("nan")))
            ),
        )
        print(f"\n[{timeframe}] PASS — {len(passing_configs)} config(s). "
              f"Mejor: window={best['ofi_window']}, thr={best['threshold']:.2f}, hold={best['hold']}")
        btc_s = best.get("_BTCUSDT_sharpe", float("nan"))
        eth_s = best.get("_ETHUSDT_sharpe", float("nan"))
        sol_s = best.get("_SOLUSDT_sharpe", float("nan"))
        valid_s = [s for s in [btc_s, eth_s, sol_s] if not np.isnan(s)]
        print(f"[{timeframe}] Sharpes (BTC/ETH/SOL): "
              f"{btc_s:+.3f} / {eth_s:+.3f} / {sol_s:+.3f}  (media: {np.mean(valid_s):+.3f})")
    else:
        print(f"\n[{timeframe}] FAIL — ninguna config alcanzo Sharpe >= {SHARPE_THRESHOLD} en >= {ASSETS_NEEDED}/3 activos")

    return all_results, passing_configs


# ── Main ───────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 78)
    print("M4 — OFI Contrarian sub-hora: Long after extreme selling (Eje 3)")
    print(f"Assets: {', '.join(ASSETS)}")
    print(f"Timeframes: {TIMEFRAMES}  |  IS: {IS_START} -> {IS_END}  |  Long-only")
    print(f"Gate: Sharpe >= {SHARPE_THRESHOLD} en >= {ASSETS_NEEDED}/3 activos (pre-registrado)")
    print("Signal: Long when OFI_pct < (1 - threshold)  [extremo vendedor => rebote]")
    print("=" * 78)

    all_passing: list = []

    for timeframe in TIMEFRAMES:
        _, passing = run_timeframe(timeframe)
        all_passing.extend(passing)

    print()
    print("=" * 78)
    print("VEREDICTO GLOBAL")
    print("=" * 78)
    if all_passing:
        print(f"M4 PASSED -- {len(all_passing)} config(s) superaron el gate")
        best = max(
            all_passing,
            key=lambda r: sum(
                r.get(f"_{a}_sharpe", float("-inf"))
                for a in ASSETS
                if not np.isnan(r.get(f"_{a}_sharpe", float("nan")))
            ),
        )
        print(
            f"Mejor config global: TF={best['tf']}, window={best['ofi_window']}, "
            f"threshold={best['threshold']:.2f}, hold={best['hold']}"
        )
        btc_s = best.get("_BTCUSDT_sharpe", float("nan"))
        eth_s = best.get("_ETHUSDT_sharpe", float("nan"))
        sol_s = best.get("_SOLUSDT_sharpe", float("nan"))
        valid_s = [s for s in [btc_s, eth_s, sol_s] if not np.isnan(s)]
        print(f"Sharpes (BTC/ETH/SOL): "
              f"{btc_s:+.3f} / {eth_s:+.3f} / {sol_s:+.3f}  "
              f"(media: {np.mean(valid_s):+.3f})")
    else:
        print("M4 FAILED -- ninguna config alcanzo Sharpe >= 0.5 en >= 2/3 activos en ningun TF")
    print("=" * 78)


if __name__ == "__main__":
    main()
