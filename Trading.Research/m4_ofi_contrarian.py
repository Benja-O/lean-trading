"""
M4 pure-signal backtest: OFI Contrarian — Long after extreme selling (Hito E)

Motivación:
  m4_ofi_momentum.py mostró que la señal de Short cuando OFI está en el percentil
  bajo (vendedores agresivos) tiene Sharpe CONSISTENTEMENTE NEGATIVO en todos los
  activos: BTC -0.220, ETH -0.935, SOL -1.203 a (window=48, threshold=0.80, hold=6).
  Esto implica que el precio SUBE después de presión vendedora extrema → el short
  pierde, el long gana. Mean reversion del flujo.

Hipótesis:
  Cuando el OFI de la hora actual está en el percentil inferior de su distribución
  reciente (muchos vendedores agresivos), el mercado está sobre-vendido localmente.
  Los compradores responden, el precio rebota en las próximas N horas.
  → Long-only: entry cuando OFI_pct < (1 - threshold)

  El lado Short (vender cuando OFI está en top = muchos compradores) va en contra
  del sesgo alcista del mercado cripto 2021-2024 y probablemente no tiene edge.
  Esta estrategia es intencionalmente Long-only.

Señal:
  ofi_pct = percentil de ofi[t] dentro de ventana ofi[t-window..t-1]
  Long:  ofi_pct < (1 - threshold)  — extremo vendedor → rebote esperado
  Flat:  resto (no Short — evitar sesgo bajista en mercado estructuralmente alcista)

  Position tracking: ON.

Grid:
  ofi_window ∈ [24, 48, 96]
  threshold  ∈ [0.75, 0.80, 0.85]  (más alto = señal más extrema y menos frecuente)
  hold_bars  ∈ [4, 6, 8]
  Total: 3 × 3 × 3 = 27 configs

Gate M4: Sharpe ≥ 0.5 en ≥ 2 de 3 activos para al menos una configuración.
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

OFI_WINDOW_VALUES = [24, 48, 96]
THRESHOLD_VALUES  = [0.75, 0.80, 0.85]
HOLD_VALUES       = [4, 6, 8]
SHARPE_THRESHOLD  = 0.5
ASSETS_NEEDED     = 2
MIN_TRADES        = 30

# ── Helpers ────────────────────────────────────────────────────────────────────

def load_features(asset: str) -> pd.DataFrame:
    path = DATA_DIR / f"{asset}_1h_features.csv"
    df = pd.read_csv(path)
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
    """
    Long-only contrarian: entra Long cuando OFI está en el percentil bajo
    (presión vendedora extrema → rebote esperado).
    """
    ofi = df["ofi"].astype(float)
    ofi_pct = compute_ofi_percentile(ofi, ofi_window)

    signals = pd.Series(0, index=df.index, dtype=int)
    signals[ofi_pct < (1 - threshold)] = 1   # Long when extreme selling
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


# ── Main ───────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 78)
    print("M4 — OFI Contrarian: Long after extreme selling (Hito E)")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h  |  Long-only")
    print(f"Assets: {', '.join(ASSETS)}")
    print(f"Grid: window={OFI_WINDOW_VALUES}, threshold={THRESHOLD_VALUES}, hold={HOLD_VALUES}")
    print(f"Gate: Sharpe >= {SHARPE_THRESHOLD} en >= {ASSETS_NEEDED}/3 activos")
    print("Signal: Long when OFI_pct < (1 - threshold)  [extremo vendedor => rebote]")
    print("=" * 78)

    data: dict[str, pd.DataFrame] = {}
    for asset in ASSETS:
        try:
            df = load_features(asset)
            df = df.loc[IS_START:IS_END]
            data[asset] = df
            print(f"  {asset}: {len(df):,} barras  ({df.index[0].date()} - {df.index[-1].date()})")
        except FileNotFoundError:
            print(f"  {asset}: ARCHIVO NO ENCONTRADO")
    print()

    if not data:
        print("❌ No se pudo cargar ningún asset.")
        return

    col_w = 22
    header = (
        f"{'window':>6}  {'thr':>5}  {'hold':>4}  "
        f"{'BTCUSDT':>{col_w}}  {'ETHUSDT':>{col_w}}  {'SOLUSDT':>{col_w}}  "
        f"{'>=0.5':>6}  {'gate':>8}"
    )
    print(header)
    print("-" * len(header))

    all_results    = []
    passing_configs = []

    for ofi_window, threshold, hold in product(OFI_WINDOW_VALUES, THRESHOLD_VALUES, HOLD_VALUES):
        row: dict = {"ofi_window": ofi_window, "threshold": threshold, "hold": hold}
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

    # ── Análisis de retornos por año ───────────────────────────────────────────
    print()
    best_config = (48, 0.80, 6)  # default
    if passing_configs:
        sorted_pass = sorted(
            passing_configs,
            key=lambda r: sum(
                r.get(f"_{a}_sharpe", float("-inf"))
                for a in ASSETS
                if not np.isnan(r.get(f"_{a}_sharpe", float("nan")))
            ),
            reverse=True,
        )
        best = sorted_pass[0]
        best_config = (best["ofi_window"], best["threshold"], best["hold"])

    w_b, t_b, h_b = best_config
    print(f"Análisis anual — window={w_b}, threshold={t_b:.2f}, hold={h_b}:")
    for asset in ASSETS:
        if asset not in data:
            continue
        df    = data[asset]
        sigs  = compute_signals(df, w_b, t_b)
        fil   = filter_overlapping_signals(sigs, h_b)
        close = df["close"].astype(float)
        fwd   = close.shift(-h_b) / close - 1
        df_a  = pd.DataFrame({"sig": fil, "fwd": fwd})
        mask  = (df_a["sig"] != 0) & df_a["fwd"].notna()
        df_a  = df_a[mask].copy()
        if df_a.empty:
            print(f"  {asset}: sin trades")
            continue
        df_a["ret"] = df_a["sig"].astype(float) * df_a["fwd"]
        df_a["year"] = df_a.index.year
        by_year = df_a.groupby("year")["ret"].agg(["mean", "std", "count"])
        print(f"  {asset}:")
        for yr, row_y in by_year.iterrows():
            if row_y["std"] > 0 and row_y["count"] >= 5:
                sharpe_yr = row_y["mean"] / row_y["std"] * np.sqrt(row_y["count"])
                print(f"    {yr}: Sharpe {sharpe_yr:+.3f}  "
                      f"(T={int(row_y['count'])}, mean={row_y['mean']*100:+.3f}%)")
            else:
                print(f"    {yr}: N/A (T={int(row_y['count'])})")

    # ── Win rate y expectancy ──────────────────────────────────────────────────
    print()
    print(f"Win rate y expectancy — window={w_b}, threshold={t_b:.2f}, hold={h_b}:")
    for asset in ASSETS:
        if asset not in data:
            continue
        df    = data[asset]
        sigs  = compute_signals(df, w_b, t_b)
        fil   = filter_overlapping_signals(sigs, h_b)
        close = df["close"].astype(float)
        fwd   = close.shift(-h_b) / close - 1
        mask  = (fil != 0) & fwd.notna()
        if mask.sum() < MIN_TRADES:
            print(f"  {asset}: insuficientes trades")
            continue
        rets = fil[mask].astype(float) * fwd[mask]
        wins  = (rets > 0).sum()
        total = len(rets)
        mean_win  = rets[rets > 0].mean() * 100 if (rets > 0).any() else 0
        mean_loss = rets[rets < 0].mean() * 100 if (rets < 0).any() else 0
        wr = wins / total * 100
        expectancy = rets.mean() * 100
        print(f"  {asset}: WR={wr:.1f}%  mean_win={mean_win:+.3f}%  "
              f"mean_loss={mean_loss:+.3f}%  expectancy={expectancy:+.4f}%  T={total}")

    # ── Veredicto ──────────────────────────────────────────────────────────────
    print()
    print("=" * 78)
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
        print(
            f"Parámetros nominales: window={best['ofi_window']}, "
            f"threshold={best['threshold']:.2f}, hold={best['hold']}"
        )
        btc_s = best.get("_BTCUSDT_sharpe", float("nan"))
        eth_s = best.get("_ETHUSDT_sharpe", float("nan"))
        sol_s = best.get("_SOLUSDT_sharpe", float("nan"))
        sharpes = [s for s in [btc_s, eth_s, sol_s] if not np.isnan(s)]
        print(f"Sharpes (BTC/ETH/SOL): "
              f"{btc_s:+.3f} / {eth_s:+.3f} / {sol_s:+.3f}  "
              f"(media: {np.mean(sharpes):+.3f})")
    else:
        print("M4 FAILED -- ninguna config alcanzó Sharpe >= 0.5 en >= 2/3 activos")
    print("=" * 78)


if __name__ == "__main__":
    main()
