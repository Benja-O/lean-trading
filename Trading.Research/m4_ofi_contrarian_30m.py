"""
M4 — OFI Contrarian 30m: rescate del Eje 3 con timeframe mas largo y filtros mas selectivos

Hipotesis de rescate:
  Sub-hora 5m/15m (Eje 3) tenia edge bruto real (54/54 sin costos) pero muria por costos:
  ratio señal/costo ~15-20%. Root cause: ~47 trades/dia en 5m, retorno medio ~0.015-0.025%
  por trade vs break-even de 0.12%.

  Dos palancas para mejorar el ratio:
    1. Timeframe 30m: hold [3,6,12] barras = 1.5h / 3h / 6h vs 15-60min en sub-hora.
       El retorno bruto por trade escala con el horizonte; el costo por trade no.
    2. Threshold 0.90 / 0.95: solo señales en el percentil mas extremo → mayor rebote
       esperado + menos frecuencia → menos costos acumulados.

Costos: 0.12% RT desde el inicio (fee 0.04% + slippage 0.02% por lado).
        Leccion del Eje 3: nunca correr sin costos y luego sorprenderse.

Comparativo 15m incluido para verificar coherencia con Layer A (que dio Sharpe -15 a -4)
y para ver si thr=0.90/0.95 rescata algo en 15m.

Grid: 3 × 4 × 3 × 2 = 72 configs
  ofi_window ∈ [12, 24, 48]
  threshold  ∈ [0.80, 0.85, 0.90, 0.95]
  hold_bars  ∈ [3, 6, 12]
  timeframe  ∈ ["15m", "30m"]

Gate M4 (pre-registrado): Sharpe >= 0.5 en >= 2/3 activos CON COSTOS.

Si falla: el eje OFI Contrarian sub-hora no tiene rescate viable con costos taker.
          Proxima palanca: costos maker (~0.04-0.06% RT con limit orders).
"""

import pandas as pd
import numpy as np
from pathlib import Path
from itertools import product

# ── Configuracion ──────────────────────────────────────────────────────────────

DATA_DIR  = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\AggTrades\features")
ASSETS    = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
IS_START  = "2021-01-01"
IS_END    = "2024-12-31"

COST_RT   = 0.0012   # 0.12% round-trip (fee 0.04% + slippage 0.02% por lado, taker)

OFI_WINDOW_VALUES = [12, 24, 48]
THRESHOLD_VALUES  = [0.80, 0.85, 0.90, 0.95]   # extendido vs Eje 3 (llegaba a 0.85)
HOLD_VALUES       = [3, 6, 12]
TIMEFRAMES        = ["15m", "30m"]

SHARPE_THRESHOLD = 0.5
ASSETS_NEEDED    = 2
MIN_TRADES       = 30

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
    """Suprime señales mientras hay posicion abierta (position tracking)."""
    arr = signals.values.copy()
    in_position_until = -1
    for i in range(len(arr)):
        if arr[i] != 0:
            if i <= in_position_until:
                arr[i] = 0
            else:
                in_position_until = i + hold - 1
    return pd.Series(arr, index=signals.index, dtype=int)


def compute_sharpe(
    signals: pd.Series, close: pd.Series, hold: int
) -> tuple[float, int, float, float]:
    """Retorna (sharpe_anualizado, num_trades, mean_return_pct, trades_per_day)."""
    signals = filter_overlapping_signals(signals, hold)
    forward_ret = close.shift(-hold) / close - 1

    mask = (signals != 0) & forward_ret.notna()
    num_trades = int(mask.sum())

    years = (close.index[-1] - close.index[0]).total_seconds() / (365.25 * 86400)
    trades_per_day = (num_trades / years) / 365.25 if years > 0 else 0.0

    if num_trades < MIN_TRADES:
        return float("nan"), num_trades, float("nan"), trades_per_day

    trade_returns = signals[mask].astype(float) * forward_ret[mask] - COST_RT
    mean_r = float(trade_returns.mean())
    std_r  = float(trade_returns.std(ddof=1))

    if std_r == 0:
        return float("nan"), num_trades, mean_r * 100, trades_per_day

    trades_per_year = num_trades / years
    sharpe = mean_r / std_r * np.sqrt(trades_per_year)
    return float(sharpe), num_trades, mean_r * 100, trades_per_day


# ── Grid por timeframe ─────────────────────────────────────────────────────────

def run_timeframe(timeframe: str) -> tuple[list, list]:
    print(f"\n{'=' * 90}")
    print(f"TF: {timeframe}  |  IS: {IS_START} -> {IS_END}  |  Long-only  |  COST_RT={COST_RT*100:.3f}%")
    print(f"Grid: window={OFI_WINDOW_VALUES}, threshold={THRESHOLD_VALUES}, hold={HOLD_VALUES}")
    print(f"{'=' * 90}")

    data: dict[str, pd.DataFrame] = {}
    for asset in ASSETS:
        try:
            df = load_features(asset, timeframe)
            df = df.loc[IS_START:IS_END]
            data[asset] = df
            print(f"  {asset}: {len(df):,} barras  ({df.index[0].date()} - {df.index[-1].date()})")
        except FileNotFoundError:
            print(f"  {asset}: ARCHIVO NO ENCONTRADO — {DATA_DIR / f'{asset}_{timeframe}_features.parquet'}")
        except Exception as ex:
            print(f"  {asset}: ERROR al cargar — {ex}")
    print()

    if not data:
        print("No se pudo cargar ningun asset.")
        return [], []

    col_w = 24
    header = (
        f"{'window':>6}  {'thr':>5}  {'hold':>4}  "
        f"{'BTCUSDT':>{col_w}}  {'ETHUSDT':>{col_w}}  {'SOLUSDT':>{col_w}}  "
        f"{'T/d(BTC)':>8}  {'>=0.5':>6}  {'gate':>6}"
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
        btc_tpd = float("nan")

        for asset in ASSETS:
            if asset not in data:
                row[asset] = "N/A"
                row[f"_{asset}_sharpe"] = float("nan")
                continue

            df = data[asset]
            signals = compute_signals(df, ofi_window, threshold)
            sharpe, n_trades, mean_pct, tpd = compute_sharpe(
                signals, df["close"].astype(float), hold
            )

            if asset == "BTCUSDT":
                btc_tpd = tpd

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
        row["_btc_tpd"] = btc_tpd
        all_results.append(row)
        if assets_passing >= ASSETS_NEEDED:
            passing_configs.append(row)

        tpd_str = f"{btc_tpd:.1f}" if not np.isnan(btc_tpd) else "N/A"
        print(
            f"{ofi_window:>6}  {threshold:>5.2f}  {hold:>4}  "
            f"{row.get('BTCUSDT', 'N/A'):>{col_w}}  "
            f"{row.get('ETHUSDT', 'N/A'):>{col_w}}  "
            f"{row.get('SOLUSDT', 'N/A'):>{col_w}}  "
            f"{tpd_str:>8}  {assets_passing:>6}  {row['gate']:>6}"
        )

    print()
    if passing_configs:
        best = max(
            passing_configs,
            key=lambda r: sum(
                r.get(f"_{a}_sharpe", float("-inf"))
                for a in ASSETS
                if not np.isnan(r.get(f"_{a}_sharpe", float("nan")))
            ),
        )
        print(f"[{timeframe}] PASS — {len(passing_configs)} config(s). "
              f"Mejor: window={best['ofi_window']}, thr={best['threshold']:.2f}, "
              f"hold={best['hold']}, T/dia(BTC)≈{best.get('_btc_tpd', float('nan')):.1f}")
        btc_s = best.get("_BTCUSDT_sharpe", float("nan"))
        eth_s = best.get("_ETHUSDT_sharpe", float("nan"))
        sol_s = best.get("_SOLUSDT_sharpe", float("nan"))
        valid_s = [s for s in [btc_s, eth_s, sol_s] if not np.isnan(s)]
        print(f"[{timeframe}] Sharpes (BTC/ETH/SOL): "
              f"{btc_s:+.3f} / {eth_s:+.3f} / {sol_s:+.3f}  (media: {np.mean(valid_s):+.3f})")
    else:
        best_by_sharpe = max(
            all_results,
            key=lambda r: sum(
                r.get(f"_{a}_sharpe", float("-inf"))
                for a in ASSETS
                if not np.isnan(r.get(f"_{a}_sharpe", float("nan")))
            ),
            default=None,
        )
        if best_by_sharpe:
            btc_s = best_by_sharpe.get("_BTCUSDT_sharpe", float("nan"))
            eth_s = best_by_sharpe.get("_ETHUSDT_sharpe", float("nan"))
            sol_s = best_by_sharpe.get("_SOLUSDT_sharpe", float("nan"))
            print(f"[{timeframe}] FAIL — ninguna config alcanzo Sharpe >= {SHARPE_THRESHOLD} "
                  f"en >= {ASSETS_NEEDED}/3 activos")
            print(f"[{timeframe}] Mejor combo (aun fallando): window={best_by_sharpe['ofi_window']}, "
                  f"thr={best_by_sharpe['threshold']:.2f}, hold={best_by_sharpe['hold']}")
            print(f"[{timeframe}] Sharpes (BTC/ETH/SOL): {btc_s:+.3f} / {eth_s:+.3f} / {sol_s:+.3f}")

    return all_results, passing_configs


# ── Main ───────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 90)
    print("M4 — OFI Contrarian 30m: rescate del Eje 3 con TF mas largo y filtros selectivos")
    print(f"Assets: {', '.join(ASSETS)}")
    print(f"Timeframes: {TIMEFRAMES}  |  IS: {IS_START} -> {IS_END}  |  Long-only")
    print(f"COST_RT = {COST_RT*100:.3f}%  (fee 0.04% + slip 0.02% por lado, taker)")
    print(f"Gate: Sharpe >= {SHARPE_THRESHOLD} en >= {ASSETS_NEEDED}/3 activos CON costos")
    print(f"Signal: Long when OFI_pct < (1 - threshold)  [extremo vendedor => rebote]")
    print(f"Referencia: Eje 3 sub-hora a costo 0 dio 54/54 PASS; con 0.12% RT dio 0/54.")
    print("=" * 90)

    all_passing: list = []

    for timeframe in TIMEFRAMES:
        _, passing = run_timeframe(timeframe)
        all_passing.extend(passing)

    print()
    print("=" * 90)
    print("VEREDICTO GLOBAL")
    print("=" * 90)

    if all_passing:
        print(f"M4 PASSED — {len(all_passing)} config(s) superaron el gate CON costos 0.12% RT")
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
            f"threshold={best['threshold']:.2f}, hold={best['hold']}, "
            f"T/dia(BTC)≈{best.get('_btc_tpd', float('nan')):.1f}"
        )
        btc_s = best.get("_BTCUSDT_sharpe", float("nan"))
        eth_s = best.get("_ETHUSDT_sharpe", float("nan"))
        sol_s = best.get("_SOLUSDT_sharpe", float("nan"))
        valid_s = [s for s in [btc_s, eth_s, sol_s] if not np.isnan(s)]
        print(f"Sharpes (BTC/ETH/SOL): "
              f"{btc_s:+.3f} / {eth_s:+.3f} / {sol_s:+.3f}  "
              f"(media: {np.mean(valid_s):+.3f})")
        print()
        print("SIGUIENTE PASO: Capa A con OOS 2025-2026 sobre las configs que pasaron.")
    else:
        print("M4 FAILED — ninguna config alcanzo Sharpe >= 0.5 en >= 2/3 activos CON costos")
        print()
        print("Diagnostico: si los Sharpes mas altos son > -1.0, el eje puede rescatarse")
        print("con costos maker (~0.04-0.06% RT con limit orders). Si son < -2.0, el")
        print("mecanismo no tiene suficiente edge para ningun nivel de costo razonable.")

    print("=" * 90)


if __name__ == "__main__":
    main()
