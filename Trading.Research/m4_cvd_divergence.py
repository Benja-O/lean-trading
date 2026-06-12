"""
M4 pure-signal backtest: CVD Divergence Strategy (Hito E)

Hipótesis:
  Bearish: precio cierra nuevo máximo de N barras PERO CVD acumulativo no confirma ese máximo
           → presión vendedora oculta → señal Short.
  Bullish: precio cierra nuevo mínimo de N barras PERO CVD acumulativo no confirma ese mínimo
           → presión compradora oculta → señal Long.

Señal exacta (replica la lógica de CvdDivergenceStrategy.cs):
  bearish = close[t] > max(close[t-N..t-1])  AND  cvd[t] < max(cvd[t-N..t-1])
  bullish = close[t] < min(close[t-N..t-1])  AND  cvd[t] > min(cvd[t-N..t-1])

Grid:
  lookback_bars ∈ [12, 24, 48]   (0.5d / 1d / 2d en TF 1h)
  hold_bars     ∈ [4, 6, 8]      (4h / 6h / 8h de holding)
  assets        : BTCUSDT, ETHUSDT, SOLUSDT
  período IS    : 2021-01-01 → 2024-12-31

Gate M4: Sharpe ≥ 0.5 en ≥ 2 de 3 activos para al menos una configuración.
Si ninguna config pasa el gate → estrategia rechazada, no se implementa en C#.
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

LOOKBACK_VALUES  = [12, 24, 48]
HOLD_VALUES      = [4, 6, 8]
SHARPE_THRESHOLD = 0.5
ASSETS_NEEDED    = 2   # ≥ 2 de 3 activos deben pasar el gate
MIN_TRADES       = 30  # mínimo de operaciones para que el Sharpe sea válido

# ── Helpers ────────────────────────────────────────────────────────────────────

def load_features(asset: str) -> pd.DataFrame:
    path = DATA_DIR / f"{asset}_1h_features.csv"
    df = pd.read_csv(path)
    df["bar"] = pd.to_datetime(df["bar"], utc=True)
    df = df.set_index("bar").sort_index()
    return df


def compute_signals(df: pd.DataFrame, lookback: int) -> pd.Series:
    """
    Replica la lógica de CvdDivergenceStrategy.EvaluateSignal:
      - Ventana de lookback barras PREVIAS (shift(1) + rolling(lookback))
      - Bearish → -1 (Short): close > price_max_prev AND cvd < cvd_max_prev
      - Bullish → +1 (Long):  close < price_min_prev AND cvd > cvd_min_prev
      - Neutral → 0 (Flat)
    """
    close = df["close"].astype(float)
    cvd   = df["cvd"].astype(float)

    # Extremos de las N barras anteriores (sin incluir la barra actual)
    prev      = close.shift(1)
    price_max = prev.rolling(lookback, min_periods=lookback).max()
    price_min = prev.rolling(lookback, min_periods=lookback).min()

    prev_cvd  = cvd.shift(1)
    cvd_max   = prev_cvd.rolling(lookback, min_periods=lookback).max()
    cvd_min   = prev_cvd.rolling(lookback, min_periods=lookback).min()

    bearish = (close > price_max) & (cvd < cvd_max)
    bullish = (close < price_min) & (cvd > cvd_min)

    signals = pd.Series(0, index=df.index, dtype=int)
    signals[bearish] = -1  # Short
    signals[bullish] =  1  # Long
    # En el caso (imposible matemáticamente) de ambos simultáneos, bearish gana
    return signals


def filter_overlapping_signals(signals: pd.Series, hold: int) -> pd.Series:
    """
    Suprime señales disparadas mientras hay una posición abierta.
    Si se entra en t, cualquier señal en [t+1, t+hold-1] es ignorada.
    Esto replica el comportamiento real del sistema: una sola posición por activo.
    """
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
    """
    Retorna (sharpe_anualizado, num_trades, mean_return_pct).

    Retorno por operación: señal × (close[t+hold] / close[t] - 1).
    Sharpe anualizado: mean / std × sqrt(trades_por_año).
    NOTA: se filtra señales solapadas antes de evaluar (position tracking).
    """
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


# ── Main ───────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 72)
    print("M4 — CVD Divergence Strategy (Hito E)")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h")
    print(f"Assets: {', '.join(ASSETS)}")
    print(f"Grid: lookback={LOOKBACK_VALUES}, hold={HOLD_VALUES}")
    print(f"Gate: Sharpe >= {SHARPE_THRESHOLD} en >= {ASSETS_NEEDED}/3 activos")
    print("=" * 72)

    # Cargar y filtrar período IS
    data: dict[str, pd.DataFrame] = {}
    for asset in ASSETS:
        try:
            df = load_features(asset)
            df = df.loc[IS_START:IS_END]
            data[asset] = df
            print(f"  {asset}: {len(df):,} barras 1h  ({df.index[0].date()} - {df.index[-1].date()})")
        except FileNotFoundError:
            print(f"  {asset}: ARCHIVO NO ENCONTRADO -- {DATA_DIR / f'{asset}_1h_features.csv'}")
    print()

    if not data:
        print("❌ No se pudo cargar ningún asset. Abortando M4.")
        return

    # ── Grid de pruebas ────────────────────────────────────────────────────────
    col_w = 22
    header = (
        f"{'lookback':>8}  {'hold':>4}  "
        f"{'BTCUSDT':>{col_w}}  {'ETHUSDT':>{col_w}}  {'SOLUSDT':>{col_w}}  "
        f"{'>=0.5':>6}  {'gate':>8}"
    )
    print(header)
    print("-" * len(header))

    all_results = []
    passing_configs = []

    for lookback, hold in product(LOOKBACK_VALUES, HOLD_VALUES):
        row: dict = {"lookback": lookback, "hold": hold}
        assets_passing = 0

        for asset in ASSETS:
            if asset not in data:
                row[asset] = "N/A"
                row[f"_{asset}_sharpe"] = float("nan")
                continue

            df = data[asset]
            signals = compute_signals(df, lookback)
            sharpe, n_trades, mean_pct = compute_sharpe(signals, df["close"].astype(float), hold)

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
            f"{lookback:>8}  {hold:>4}  "
            f"{btc_cell:>{col_w}}  {eth_cell:>{col_w}}  {sol_cell:>{col_w}}  "
            f"{assets_passing:>6}  {row['gate']:>8}"
        )

    # ── Diagnóstico de frecuencia de señales ───────────────────────────────────
    print()
    print("Frecuencia de señales (lookback=24, IS completo):")
    for asset in ASSETS:
        if asset not in data:
            continue
        df     = data[asset]
        sigs   = compute_signals(df, lookback=24)
        n_long  = int((sigs == 1).sum())
        n_short = int((sigs == -1).sum())
        n_total = n_long + n_short
        years   = (df.index[-1] - df.index[0]).total_seconds() / (365.25 * 86400)
        print(f"  {asset}: {n_long} Long + {n_short} Short = {n_total} senales "
              f"({n_total/years:.0f}/anio)")

    # -- Analisis direccional (Short-only vs Long-only, mejor config BTC) ------
    print()
    print("Analisis direccional -- lookback=48, hold=4 (mejor config BTC):")
    print(f"  {'asset':>8}  {'Short-only':>16}  {'Long-only':>16}  {'Bidireccional':>16}")
    print(f"  {'-'*8}  {'-'*16}  {'-'*16}  {'-'*16}")
    for asset in ASSETS:
        if asset not in data:
            continue
        df    = data[asset]
        sigs  = compute_signals(df, lookback=48)
        close = df["close"].astype(float)
        fwd   = close.shift(-4) / close - 1
        years = (close.index[-1] - close.index[0]).total_seconds() / (365.25 * 86400)
        results_dir = {}
        for lbl, raw_mask in [("Short", sigs == -1), ("Long", sigs == 1), ("Both", sigs != 0)]:
            dir_sigs = sigs.where(raw_mask, 0)
            filtered  = filter_overlapping_signals(dir_sigs, 4)
            mask = (filtered != 0) & fwd.notna()
            if mask.sum() < MIN_TRADES:
                results_dir[lbl] = "N/A"
                continue
            tr = filtered[mask].astype(float) * fwd[mask]
            m, s = tr.mean(), tr.std(ddof=1)
            if s == 0:
                results_dir[lbl] = "N/A"
                continue
            sh = m / s * np.sqrt(mask.sum() / years)
            flag = "*" if sh >= SHARPE_THRESHOLD else " "
            results_dir[lbl] = f"{flag}{sh:+.3f}(T={int(mask.sum())})"
        print(f"  {asset:>8}  {results_dir.get('Short','N/A'):>16}  "
              f"{results_dir.get('Long','N/A'):>16}  {results_dir.get('Both','N/A'):>16}")

    # -- Veredicto -------------------------------------------------------------
    print()
    print("=" * 72)
    if passing_configs:
        print(f"M4 PASSED -- {len(passing_configs)} config(s) superaron el gate")
        sorted_configs = sorted(
            passing_configs,
            key=lambda r: sum(
                r.get(f"_{a}_sharpe", float("-inf"))
                for a in ASSETS
                if not np.isnan(r.get(f"_{a}_sharpe", float("nan")))
            ),
            reverse=True,
        )
        best = sorted_configs[0]
        print(f"Parametros nominales: lookback={best['lookback']}, hold={best['hold']}")
    else:
        print("M4 FAILED -- ninguna config alcanzo Sharpe >= 0.5 en >= 2/3 activos")
        print("   Ver analisis direccional arriba para diagnostico.")
    print("=" * 72)


if __name__ == "__main__":
    main()
