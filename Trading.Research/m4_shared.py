"""
Utilidades compartidas para todos los scripts M4 de Hito E.
Carga de datos, filtro de solapamiento, cálculo de Sharpe.
"""

import pandas as pd
import numpy as np
from pathlib import Path

DATA_DIR = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\AggTrades\features")
ASSETS   = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
IS_START = "2021-01-01"
IS_END   = "2024-12-31"

SHARPE_THRESHOLD = 0.5
ASSETS_NEEDED    = 2
MIN_TRADES       = 30


def load_features(asset: str) -> pd.DataFrame:
    path = DATA_DIR / f"{asset}_1h_features.csv"
    df = pd.read_csv(path)
    df["bar"] = pd.to_datetime(df["bar"], utc=True)
    df = df.set_index("bar").sort_index()
    return df


def filter_overlapping_signals(signals: pd.Series, hold: int) -> pd.Series:
    """Suprime señales mientras hay posición abierta (position tracking obligatorio)."""
    arr = signals.values.copy()
    in_position_until = -1
    for i in range(len(arr)):
        if arr[i] != 0:
            if i <= in_position_until:
                arr[i] = 0
            else:
                in_position_until = i + hold - 1
    return pd.Series(arr, index=signals.index, dtype=int)


def compute_sharpe(signals: pd.Series, close: pd.Series, hold: int) -> tuple:
    """Retorna (sharpe, n_trades, mean_ret_pct) con position tracking y fees 0.04% RT."""
    signals = filter_overlapping_signals(signals, hold)
    forward_ret = close.shift(-hold) / close - 1
    fee = 0.0004  # 0.04% round-trip

    mask = (signals != 0) & forward_ret.notna()
    n_trades = int(mask.sum())

    if n_trades < MIN_TRADES:
        return float("nan"), n_trades, float("nan")

    trade_returns = signals[mask].astype(float) * forward_ret[mask] - fee
    mean_r = float(trade_returns.mean())
    std_r  = float(trade_returns.std(ddof=1))

    if std_r == 0:
        return float("nan"), n_trades, mean_r * 100

    years           = (close.index[-1] - close.index[0]).total_seconds() / (365.25 * 86400)
    trades_per_year = n_trades / years
    sharpe          = mean_r / std_r * np.sqrt(trades_per_year)
    return float(sharpe), n_trades, mean_r * 100


def rolling_percentile_rank(series: pd.Series, window: int) -> pd.Series:
    """Percentil de series[t] dentro de las `window` barras anteriores (excluye t)."""
    shifted = series.shift(1)
    return shifted.rolling(window, min_periods=window).apply(
        lambda x: float((x[:-1] < x[-1]).sum()) / max(len(x) - 1, 1),
        raw=True
    )


def print_grid_header(cols: list[str]) -> str:
    col_w = 22
    header = "  ".join(f"{c:>{8}}" for c in cols)
    header += f"  {'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  {'>=0.5':>6}  {'gate':>8}"
    print(header)
    print("-" * len(header))
    return header


def format_cell(sharpe: float, n_trades: int) -> str:
    col_w = 22
    if np.isnan(sharpe):
        return f"{'N/A (T=' + str(n_trades) + ')':>{col_w}}"
    flag = "*" if sharpe >= SHARPE_THRESHOLD else " "
    return f"{flag + f'{sharpe:+.3f} (T={n_trades})':>{col_w}}"


def summarize_results(passing_configs: list, all_results: list, name: str) -> None:
    print()
    print("=" * 78)
    if passing_configs:
        best = max(passing_configs, key=lambda r: r["_sum_sharpe"])
        print(f"M4 PASSED — {len(passing_configs)} config(s) superaron el gate — {name}")
        params = {k: v for k, v in best.items() if not k.startswith("_") and k not in ["assets_passing", "gate"]}
        print(f"Parámetros nominales: {params}")
        btc = best.get("_btc_sharpe", float("nan"))
        eth = best.get("_eth_sharpe", float("nan"))
        sol = best.get("_sol_sharpe", float("nan"))
        sharpes = [s for s in [btc, eth, sol] if not np.isnan(s)]
        print(f"Sharpes (BTC/ETH/SOL): {btc:+.3f} / {eth:+.3f} / {sol:+.3f}  (media: {np.mean(sharpes):+.3f})")
    else:
        print(f"M4 FAILED — ninguna config alcanzó Sharpe >= 0.5 en >= 2/3 activos — {name}")
    print("=" * 78)
