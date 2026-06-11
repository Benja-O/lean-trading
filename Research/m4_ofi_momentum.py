"""
M4 pure-signal backtest: OFI Momentum (Hito E — candidata A)

Hipótesis:
  El Order Flow Imbalance (OFI) mide la presión neta de órdenes agresivas en un bar.
  Cuando el OFI del bar actual está en el extremo superior de su distribución reciente
  (percentil alto), hay compra institucional agresiva y el precio debería continuar subiendo
  en el corto plazo (momentum de flujo).
  Cuando está en el extremo inferior, presión vendedora → Short.

  Base académica: Chordia & Subrahmanyam (2004), Cont et al. (2014) — OFI predice
  retornos de corto plazo con significancia estadística en múltiples mercados.

Señal:
  ofi_pct = percentil de ofi[t] dentro de ventana ofi[t-window..t-1]
  Long:  ofi_pct > threshold   (e.g., OFI en top 20% de su historia reciente)
  Short: ofi_pct < 1-threshold (e.g., OFI en bottom 20%)
  Flat:  resto

  IMPORTANTE: position tracking activo — si hay trade abierto, señales nuevas son ignoradas
  hasta t+hold. Esto evita el artefacto de señales solapadas que invalidó CvdBullishDivergence.

Grid:
  ofi_window ∈ [24, 48, 96]   (1d / 2d / 4d en TF 1h)
  threshold  ∈ [0.75, 0.80, 0.85]  (percentil mínimo para disparar señal)
  hold_bars  ∈ [4, 6, 8]
  Total: 3 × 3 × 3 = 27 configs por asset

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
    """
    Percentil de ofi[t] dentro de las `window` barras previas (sin incluir la barra actual).
    Retorna serie en [0, 1]. NaN donde no hay ventana completa.
    """
    shifted = ofi.shift(1)  # ventana de barras previas
    return shifted.rolling(window, min_periods=window).apply(
        lambda x: float((x[:-1] < x[-1]).sum()) / max(len(x) - 1, 1),
        raw=True
    )


def compute_signals(df: pd.DataFrame, ofi_window: int, threshold: float) -> pd.Series:
    """
    Señal OFI momentum basada en percentil rolling:
      Long  (+1): ofi_pct > threshold
      Short (-1): ofi_pct < 1 - threshold
      Flat  ( 0): resto / ventana incompleta
    """
    ofi = df["ofi"].astype(float)
    ofi_pct = compute_ofi_percentile(ofi, ofi_window)

    signals = pd.Series(0, index=df.index, dtype=int)
    signals[ofi_pct > threshold]         =  1
    signals[ofi_pct < (1 - threshold)]   = -1
    return signals


def filter_overlapping_signals(signals: pd.Series, hold: int) -> pd.Series:
    """
    Suprime señales disparadas mientras hay una posición abierta.
    Si se entra en t, cualquier señal en [t+1, t+hold-1] es ignorada.
    Crítico para evitar inflación de Sharpe en señales frecuentes o persistentes.
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
    Aplica position tracking antes de evaluar.
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
    print("=" * 78)
    print("M4 — OFI Momentum (Hito E — candidata A)")
    print(f"IS: {IS_START} -> {IS_END}  |  TF: 1h")
    print(f"Assets: {', '.join(ASSETS)}")
    print(f"Grid: window={OFI_WINDOW_VALUES}, threshold={THRESHOLD_VALUES}, hold={HOLD_VALUES}")
    print(f"Gate: Sharpe >= {SHARPE_THRESHOLD} en >= {ASSETS_NEEDED}/3 activos")
    print("Position tracking: ON (señales solapadas suprimidas)")
    print("=" * 78)

    data: dict[str, pd.DataFrame] = {}
    for asset in ASSETS:
        try:
            df = load_features(asset)
            df = df.loc[IS_START:IS_END]
            data[asset] = df
            ofi_avail = df["ofi"].notna().sum()
            print(f"  {asset}: {len(df):,} barras  |  OFI no-nulo: {ofi_avail:,} "
                  f"({df.index[0].date()} - {df.index[-1].date()})")
        except FileNotFoundError:
            print(f"  {asset}: ARCHIVO NO ENCONTRADO -- {DATA_DIR / f'{asset}_1h_features.csv'}")
        except KeyError:
            print(f"  {asset}: COLUMNA 'ofi' NO ENCONTRADA en el CSV")
    print()

    if not data:
        print("❌ No se pudo cargar ningún asset. Abortando M4.")
        return

    # Verificar que OFI exista y no esté completamente en 0
    for asset, df in list(data.items()):
        if "ofi" not in df.columns or df["ofi"].abs().max() == 0:
            print(f"  WARN {asset}: OFI es todo cero o faltante — verificar pipeline de features")

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

    # ── Diagnóstico de frecuencia de señales ───────────────────────────────────
    print()
    print("Frecuencia de señales post-filtro (window=48, threshold=0.80, hold=6):")
    for asset in ASSETS:
        if asset not in data:
            continue
        df     = data[asset]
        sigs   = compute_signals(df, ofi_window=48, threshold=0.80)
        fil    = filter_overlapping_signals(sigs, hold=6)
        n_long  = int((fil ==  1).sum())
        n_short = int((fil == -1).sum())
        n_raw   = int((sigs != 0).sum())
        years   = (df.index[-1] - df.index[0]).total_seconds() / (365.25 * 86400)
        pct_kept = (n_long + n_short) / max(n_raw, 1) * 100
        print(f"  {asset}: {n_long} Long + {n_short} Short = {n_long+n_short} filtradas "
              f"({pct_kept:.0f}% de {n_raw} raw)  [{(n_long+n_short)/years:.0f}/año]")

    # ── Análisis direccional (mejor config BTC o default) ──────────────────────
    print()
    best_btc_config = (48, 0.80, 6)  # default; se actualiza si hay passing configs
    if passing_configs:
        sorted_pass = sorted(
            passing_configs,
            key=lambda r: r.get("_BTCUSDT_sharpe", float("-inf")),
            reverse=True
        )
        best = sorted_pass[0]
        best_btc_config = (best["ofi_window"], best["threshold"], best["hold"])

    w_d, t_d, h_d = best_btc_config
    print(f"Análisis direccional — window={w_d}, threshold={t_d:.2f}, hold={h_d}:")
    print(f"  {'asset':>8}  {'Short-only':>18}  {'Long-only':>18}  {'Bidireccional':>18}")
    print(f"  {'-'*8}  {'-'*18}  {'-'*18}  {'-'*18}")
    for asset in ASSETS:
        if asset not in data:
            continue
        df    = data[asset]
        sigs  = compute_signals(df, ofi_window=w_d, threshold=t_d)
        close = df["close"].astype(float)
        years = (close.index[-1] - close.index[0]).total_seconds() / (365.25 * 86400)
        fwd   = close.shift(-h_d) / close - 1
        results_dir = {}
        for lbl, dir_cond in [("Short", sigs == -1), ("Long", sigs == 1), ("Both", sigs != 0)]:
            dir_sigs = sigs.where(dir_cond, 0)
            filtered = filter_overlapping_signals(dir_sigs, h_d)
            mask = (filtered != 0) & fwd.notna()
            if mask.sum() < MIN_TRADES:
                results_dir[lbl] = f"N/A(T={int(mask.sum())})"
                continue
            tr  = filtered[mask].astype(float) * fwd[mask]
            m, s = tr.mean(), tr.std(ddof=1)
            if s == 0:
                results_dir[lbl] = "N/A"
                continue
            sh = m / s * np.sqrt(mask.sum() / years)
            flag = "*" if sh >= SHARPE_THRESHOLD else " "
            results_dir[lbl] = f"{flag}{sh:+.3f}(T={int(mask.sum())})"
        print(f"  {asset:>8}  {results_dir.get('Short','N/A'):>18}  "
              f"{results_dir.get('Long','N/A'):>18}  {results_dir.get('Both','N/A'):>18}")

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
        print("  Opciones: revisar features OFI, ajustar grid, o descartar candidata.")
    print("=" * 78)


if __name__ == "__main__":
    main()
