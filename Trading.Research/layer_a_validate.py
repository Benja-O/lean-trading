"""
Capa A -Gate estadístico Python (ADR-056): IS/OOS + costos + Trading.Analytics

Uso:
    python layer_a_validate.py --spec <spec.json> [--output <dir>] [--analytics-exe <path>]

Ejemplo (eje 3 config central):
    python layer_a_validate.py --spec ofi_contrarian_subhora_spec.json --output validation_output

El harness:
1. Lee un spec declarativo (A1) -grammar definida en este archivo.
2. Parte IS/OOS, corre simulación con costos realistas (A2).
3. Emite trade-logs CSV en formato Trading.Analytics (A3).
4. Llama Trading.Analytics CLI para Gate1 + Gate2 Monte Carlo (A4).
5. Imprime veredicto final.

Formato CSV de salida (compatible con LeanTransactionLogParser de Trading.Analytics):
    DateTime,Symbol,Buy|Sell,Quantity,Price
    Quantity se normaliza para que EntryPrice * Quantity = 1.0 USDT
    (= el PnL del pairer equivale al retorno fraccional del trade)

Costos por defecto (parametrizables en el spec o via --fee / --slippage):
    Fee taker:  0.04% por lado (0.08% round-trip)
    Slippage:   0.02% por lado (0.04% round-trip)
    Total RT:   0.12% round-trip

ADR-056 D5: la gramática crece a demanda. Solo se implementan los primitivos
que el eje 3 usa: feature=OFI, transform=percentile, direction=long, exit=hold_bars.
"""

import argparse
import json
import subprocess
import sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

from pathlib import Path
from itertools import product

import numpy as np
import pandas as pd

# ── Defaults ──────────────────────────────────────────────────────────────────

ANALYTICS_EXE_DEFAULT = str(
    Path(__file__).parent.parent / "Trading.Analytics" / "bin" / "Release" / "net10.0" / "Trading.Analytics.exe"
)
DATA_DIR_DEFAULT = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\AggTrades\features")

IS_START_DEFAULT  = "2021-01-01"
IS_END_DEFAULT    = "2024-12-31"
OOS_START_DEFAULT = "2025-01-01"
OOS_END_DEFAULT   = "2026-06-09"

FEE_PER_SIDE_DEFAULT      = 0.0004   # 0.04% taker
SLIPPAGE_PER_SIDE_DEFAULT = 0.0002   # 0.02% por lado
MIN_TRADES = 30

# ── Grammar de spec (A1) ──────────────────────────────────────────────────────
#
# Spec mínimo que el eje 3 necesita (ADR-056 D5: solo lo que se usa):
#
# {
#   "name": "OFI Contrarian sub-hora",
#   "feature": "ofi",                 # columna del parquet
#   "transform": "percentile",        # único transform soportado
#   "window": 24,                     # lookback en barras
#   "threshold": 0.85,                # signal cuando pct < (1 - threshold)
#   "direction": "long",              # único direction soportado
#   "exit": {"type": "hold_bars", "bars": 6},
#   "timeframe": "15m",               # "5m" | "15m" | "1h" | etc.
#   "symbols": ["BTCUSDT", "ETHUSDT", "SOLUSDT"],
#   "is_start":  "2021-01-01",        # opcional, sobreescribe default
#   "is_end":    "2024-12-31",
#   "oos_start": "2025-01-01",
#   "oos_end":   "2026-06-09",
#   "fee_per_side":      0.0004,      # opcional
#   "slippage_per_side": 0.0002       # opcional
# }

# ── Signal engine (A2) ────────────────────────────────────────────────────────

def load_features(data_dir: Path, asset: str, timeframe: str) -> pd.DataFrame:
    path = data_dir / f"{asset}_{timeframe}_features.parquet"
    df = pd.read_parquet(path)
    df["bar"] = pd.to_datetime(df["bar"], utc=True)
    return df.set_index("bar").sort_index()


def compute_percentile_signal(df: pd.DataFrame, feature_col: str,
                               window: int, threshold: float) -> pd.Series:
    """
    Replica exacta de compute_ofi_percentile + compute_signals del script M4.

    Calcula el percentil de feature[t] dentro de las `window` barras previas
    (shift(1) para excluir la barra actual -sin lookahead).

    Signal = 1 (Long) cuando percentile < (1 - threshold).
    """
    feature = df[feature_col].astype(float)
    shifted = feature.shift(1)
    pct = shifted.rolling(window, min_periods=window).apply(
        lambda x: float((x[:-1] < x[-1]).sum()) / max(len(x) - 1, 1),
        raw=True,
    )
    signals = pd.Series(0, index=df.index, dtype=int)
    signals[pct < (1.0 - threshold)] = 1
    return signals


def filter_overlapping_signals(signals: pd.Series, hold: int) -> pd.Series:
    """
    Suprime señales mientras hay posición abierta (position tracking).
    Replica exacta de filter_overlapping_signals del script M4.
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


def simulate_trades(
    df: pd.DataFrame,
    spec: dict,
    fee_per_side: float,
    slippage_per_side: float,
    start: str,
    end: str,
) -> pd.DataFrame:
    """
    Simula los trades para un activo en un período.
    Retorna DataFrame de trades cerrados con costos aplicados.

    Retorna columnas:
        entry_time, exit_time, entry_price_gross, exit_price_gross,
        entry_price_net, exit_price_net, return_net, symbol
    """
    feature_col = spec["feature"]      # ej. "ofi"
    transform   = spec["transform"]    # solo "percentile" soportado
    window      = spec["window"]
    threshold   = spec["threshold"]
    hold        = spec["exit"]["bars"]
    symbol      = df.attrs.get("symbol", "UNKNOWN")

    if transform != "percentile":
        raise NotImplementedError(f"transform '{transform}' no implementado. Solo 'percentile' soportado.")

    df_period = df.loc[start:end]
    if df_period.empty:
        return pd.DataFrame()

    close = df_period["close"].astype(float)

    signals_raw = compute_percentile_signal(df_period, feature_col, window, threshold)
    signals = filter_overlapping_signals(signals_raw, hold)

    entry_times   = []
    exit_times    = []
    entry_prices_gross = []
    exit_prices_gross  = []
    entry_prices_net   = []
    exit_prices_net    = []
    returns_net        = []

    cost_per_side_total = fee_per_side + slippage_per_side  # total por lado
    cost_rt = 2 * cost_per_side_total                       # round-trip

    idx  = df_period.index
    vals = close.values
    sig  = signals.values

    for i in range(len(idx)):
        if sig[i] == 0:
            continue
        exit_i = i + hold
        if exit_i >= len(idx):
            break  # posición abierta al final del período → descarta

        entry_price = vals[i]
        exit_price  = vals[exit_i]

        # Costos aplicados al precio (entry sube, exit baja -long)
        ep_net = entry_price * (1.0 + cost_per_side_total)
        xp_net = exit_price  * (1.0 - cost_per_side_total)
        ret_net = (xp_net - ep_net) / ep_net

        entry_times.append(idx[i])
        exit_times.append(idx[exit_i])
        entry_prices_gross.append(entry_price)
        exit_prices_gross.append(exit_price)
        entry_prices_net.append(ep_net)
        exit_prices_net.append(xp_net)
        returns_net.append(ret_net)

    if not entry_times:
        return pd.DataFrame()

    return pd.DataFrame({
        "symbol":              symbol,
        "entry_time":          entry_times,
        "exit_time":           exit_times,
        "entry_price_gross":   entry_prices_gross,
        "exit_price_gross":    exit_prices_gross,
        "entry_price_net":     entry_prices_net,
        "exit_price_net":      exit_prices_net,
        "return_net":          returns_net,
    })


def compute_sharpe_from_trades(trades: pd.DataFrame) -> tuple[float, int]:
    """
    Calcula Sharpe anualizado desde el DataFrame de trades (retorno por trade).
    Usa la misma fórmula que el script M4: Sharpe = mean/std * sqrt(trades/year).
    """
    if trades.empty:
        return float("nan"), 0

    r = trades["return_net"].values
    n = len(r)
    if n < MIN_TRADES:
        return float("nan"), n

    mean_r = float(np.mean(r))
    std_r  = float(np.std(r, ddof=1))
    if std_r == 0:
        return float("nan"), n

    t0 = trades["entry_time"].min()
    t1 = trades["exit_time"].max()
    years = (t1 - t0).total_seconds() / (365.25 * 86400)
    if years <= 0:
        return float("nan"), n

    trades_per_year = n / years
    sharpe = mean_r / std_r * np.sqrt(trades_per_year)
    return float(sharpe), n


# ── Emisión de trade-log CSV (A3) ─────────────────────────────────────────────

def trades_to_fills_csv(trades: pd.DataFrame) -> list[str]:
    """
    Convierte trades a fills en formato LeanTransactionLogParser:
        DateTime,Symbol,Buy|Sell,Quantity,Price

    Quantity normalizado: 1 / entry_price_net
    → entry_price_net * Quantity = 1.0 (nominal 1 USDT por trade)
    → PnlUsdt del TradePairer = (exit_price_net - entry_price_net) * Quantity
                               = (exit_price_net/entry_price_net - 1)
                               = return_net (aproximado, sin el denominador)

    NOTA: se usan precios netos de costos para que el PnL de Trading.Analytics
    ya incorpore fees y slippage sin tocar el código C#.
    """
    lines = []
    for _, row in trades.iterrows():
        sym    = row["symbol"]
        qty    = 1.0 / row["entry_price_net"]   # normalización nominal
        ep     = row["entry_price_net"]
        xp     = row["exit_price_net"]
        et     = row["entry_time"]
        xt     = row["exit_time"]

        # formato DateTime: compatible con DateTime.Parse C# (ISO 8601)
        et_str = et.strftime("%Y-%m-%dT%H:%M:%S")
        xt_str = xt.strftime("%Y-%m-%dT%H:%M:%S")

        lines.append(f"{et_str},{sym},Buy,{qty:.10f},{ep:.8f}")
        lines.append(f"{xt_str},{sym},Sell,{qty:.10f},{xp:.8f}")
    return sorted(lines)  # sort by datetime (Buy+Sell intercalados, el pairer los reordena)


def write_fills_csv(path: Path, all_trades: list[pd.DataFrame]) -> None:
    """Escribe el CSV de fills para Trading.Analytics."""
    lines = []
    for trades in all_trades:
        if not trades.empty:
            lines.extend(trades_to_fills_csv(trades))
    # Sort global por timestamp (el pairer hace FIFO, necesita orden)
    lines.sort()
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


# ── Harness principal (A2 + A4) ───────────────────────────────────────────────

def run_period(
    spec: dict,
    data_dir: Path,
    fee: float,
    slip: float,
    start: str,
    end: str,
    label: str,
) -> tuple[list[pd.DataFrame], dict[str, float]]:
    """
    Corre la simulación para todos los símbolos en un período.
    Retorna (lista de DataFrames de trades, dict de Sharpe por símbolo).
    """
    symbols    = spec["symbols"]
    timeframe  = spec["timeframe"]
    all_trades = []
    sharpes    = {}

    print(f"\n--- {label}: {start} -> {end} ---")
    for sym in symbols:
        try:
            df = load_features(data_dir, sym, timeframe)
            df.attrs["symbol"] = sym
        except FileNotFoundError as e:
            print(f"  [{sym}] ARCHIVO NO ENCONTRADO: {e}")
            continue

        trades = simulate_trades(df, spec, fee, slip, start, end)
        sharpe, n = compute_sharpe_from_trades(trades)
        sharpes[sym] = sharpe

        mean_net = float(trades["return_net"].mean() * 100) if not trades.empty else float("nan")
        if not np.isnan(sharpe):
            print(f"  [{sym}] trades={n:>5}  mean_net={mean_net:+.4f}%  sharpe={sharpe:+.3f}")
        else:
            print(f"  [{sym}] trades={n:>5}  (insuficiente -N<{MIN_TRADES})")
        all_trades.append(trades)

    return all_trades, sharpes


def call_trading_analytics(
    analytics_exe: str,
    is_csv: Path,
    oos_csv: Path,
    strategy_name: str,
    output_dir: Path,
) -> tuple[bool, bool]:
    """
    Llama Trading.Analytics CLI y parsea el código de retorno.
    Retorna (gate1_passed, gate2_passed) -ambos True si exit code 0.
    """
    cmd = [
        analytics_exe,
        "--is-log",   str(is_csv),
        "--oos-log",  str(oos_csv),
        "--strategy", strategy_name,
        "--output",   str(output_dir),
    ]
    print(f"\nLlamando Trading.Analytics...")
    print(f"  CMD: {' '.join(cmd)}")
    result = subprocess.run(cmd, capture_output=False, text=True)
    passed = result.returncode == 0
    return passed, passed


# ── M4 grid scan (sensibilidad a costos) ─────────────────────────────────────

def run_m4_cost_sensitivity(
    data_dir: Path,
    fee: float,
    slip: float,
    is_start: str,
    is_end: str,
) -> None:
    """
    Corre el grid completo del eje 3 (54 configs) con costos reales.
    Replica la lógica de m4_ofi_contrarian_subhora.py pero con costos.
    Reporta cuántos configs pasan el gate M4 vs el 54/54 a costo 0.
    """
    ASSETS             = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
    OFI_WINDOW_VALUES  = [12, 24, 48]
    THRESHOLD_VALUES   = [0.75, 0.80, 0.85]
    HOLD_VALUES        = [3, 6, 12]
    TIMEFRAMES         = ["5m", "15m"]
    SHARPE_THRESHOLD   = 0.5
    ASSETS_NEEDED      = 2

    total_cost_rt = (fee + slip) * 2
    print(f"\n{'=' * 80}")
    print(f"M4 COST SENSITIVITY -Eje 3: OFI Contrarian sub-hora")
    print(f"Costos: fee={fee*100:.3f}%/lado, slippage={slip*100:.3f}%/lado, RT={total_cost_rt*100:.3f}%")
    print(f"IS: {is_start} -> {is_end}")
    print(f"Gate: Sharpe >= {SHARPE_THRESHOLD} en >= {ASSETS_NEEDED}/3 activos")
    print(f"{'=' * 80}")

    # Pre-carga datos
    data: dict[str, dict[str, pd.DataFrame]] = {}
    for tf in TIMEFRAMES:
        data[tf] = {}
        for asset in ASSETS:
            try:
                df = load_features(data_dir, asset, tf)
                df.attrs["symbol"] = asset
                data[tf][asset] = df.loc[is_start:is_end]
                n = len(data[tf][asset])
                print(f"  [{tf}] {asset}: {n:,} barras ({data[tf][asset].index[0].date()} -> {data[tf][asset].index[-1].date()})")
            except FileNotFoundError:
                print(f"  [{tf}] {asset}: NO ENCONTRADO")

    results_pass  = 0
    results_total = 0
    passing_configs = []

    for tf in TIMEFRAMES:
        print(f"\n  TF={tf}")
        col_w = 24
        header = (
            f"  {'win':>4} {'thr':>5} {'hold':>4}  "
            f"{'BTCUSDT':>{col_w}}  {'ETHUSDT':>{col_w}}  {'SOLUSDT':>{col_w}}  "
            f"{'>=0.5':>6}  {'gate':>8}"
        )
        print(header)
        print("  " + "-" * (len(header) - 2))

        for ofi_window, threshold, hold in product(OFI_WINDOW_VALUES, THRESHOLD_VALUES, HOLD_VALUES):
            spec = {
                "feature": "ofi",
                "transform": "percentile",
                "window": ofi_window,
                "threshold": threshold,
                "direction": "long",
                "exit": {"type": "hold_bars", "bars": hold},
                "timeframe": tf,
                "symbols": ASSETS,
            }
            assets_passing = 0
            cells = {}

            for asset in ASSETS:
                if asset not in data.get(tf, {}):
                    cells[asset] = "N/A"
                    continue
                df = data[tf][asset]
                trades = simulate_trades(df, spec, fee, slip, is_start, is_end)
                sharpe, n = compute_sharpe_from_trades(trades)
                if np.isnan(sharpe):
                    cells[asset] = f"N/A (T={n})"
                else:
                    flag = "*" if sharpe >= SHARPE_THRESHOLD else " "
                    cells[asset] = f"{flag}{sharpe:+.3f} (T={n})"
                    if sharpe >= SHARPE_THRESHOLD:
                        assets_passing += 1

            gate = "PASS" if assets_passing >= ASSETS_NEEDED else "FAIL"
            results_total += 1
            if gate == "PASS":
                results_pass += 1
                passing_configs.append({
                    "tf": tf, "window": ofi_window, "threshold": threshold, "hold": hold,
                    "assets_passing": assets_passing,
                })

            print(
                f"  {ofi_window:>4} {threshold:>5.2f} {hold:>4}  "
                f"{cells.get('BTCUSDT', 'N/A'):>{col_w}}  "
                f"{cells.get('ETHUSDT', 'N/A'):>{col_w}}  "
                f"{cells.get('SOLUSDT', 'N/A'):>{col_w}}  "
                f"{assets_passing:>6}  {gate:>8}"
            )

    print(f"\n{'=' * 80}")
    print(f"RESUMEN SENSIBILIDAD A COSTOS")
    print(f"{'=' * 80}")
    print(f"  Con COST_RT=0.0 (M4 original):  54/54 configs PASS (100%)")
    print(f"  Con costos reales ({total_cost_rt*100:.2f}% RT): {results_pass}/{results_total} configs PASS ({results_pass/results_total*100:.1f}%)")
    print()

    return passing_configs


# ── CLI principal (A4) ────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Capa A -gate estadístico Python (ADR-056)"
    )
    parser.add_argument("--spec",          required=True,  help="Archivo JSON con la especificación de la hipótesis")
    parser.add_argument("--output",        default="layer_a_output", help="Directorio de salida")
    parser.add_argument("--analytics-exe", default=ANALYTICS_EXE_DEFAULT, help="Path al ejecutable Trading.Analytics")
    parser.add_argument("--data-dir",      default=str(DATA_DIR_DEFAULT), help="Directorio de features parquet")
    parser.add_argument("--fee",           type=float, default=None, help="Fee por lado (sobreescribe spec)")
    parser.add_argument("--slippage",      type=float, default=None, help="Slippage por lado (sobreescribe spec)")
    parser.add_argument("--m4-only",       action="store_true", help="Solo corre el grid M4 de sensibilidad a costos (sin IS/OOS/MC)")

    args = parser.parse_args()

    # Carga spec
    spec_path = Path(args.spec)
    with open(spec_path, encoding="utf-8") as f:
        spec = json.load(f)

    data_dir = Path(args.data_dir)
    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)

    # Costos (spec > args > default)
    fee  = args.fee  if args.fee  is not None else spec.get("fee_per_side",      FEE_PER_SIDE_DEFAULT)
    slip = args.slippage if args.slippage is not None else spec.get("slippage_per_side", SLIPPAGE_PER_SIDE_DEFAULT)

    # Períodos
    is_start  = spec.get("is_start",  IS_START_DEFAULT)
    is_end    = spec.get("is_end",    IS_END_DEFAULT)
    oos_start = spec.get("oos_start", OOS_START_DEFAULT)
    oos_end   = spec.get("oos_end",   OOS_END_DEFAULT)

    strategy_name = spec.get("name", spec_path.stem)

    # ── Paso 1: M4 sensibilidad a costos (siempre se corre) ──────────────────
    passing_configs = run_m4_cost_sensitivity(data_dir, fee, slip, is_start, is_end)

    if args.m4_only:
        return

    # Early-exit si ninguna config pasa M4 con costos -IS/OOS/MC son redundantes
    if not passing_configs:
        print(f"\n{'=' * 80}")
        print(f"VEREDICTO FINAL -{strategy_name}")
        print(f"{'=' * 80}")
        print(f"  M4 CON COSTOS: 0 configs PASS -EARLY EXIT")
        print(f"  El eje muere por costos estructurales antes de IS/OOS/MC.")
        print(f"  No procede a Capa B. (IS/OOS/MC no corridos -redundantes.)")
        print(f"{'=' * 80}")
        sys.exit(1)

    # ── Paso 2: IS/OOS con costos ────────────────────────────────────────────
    print(f"\n{'=' * 80}")
    print(f"IS/OOS CON COSTOS -{strategy_name}")
    print(f"Fee: {fee*100:.3f}%/lado | Slippage: {slip*100:.3f}%/lado | RT: {(fee+slip)*2*100:.3f}%")
    print(f"{'=' * 80}")

    is_trades_list,  is_sharpes  = run_period(spec, data_dir, fee, slip, is_start,  is_end,    "IS")
    oos_trades_list, oos_sharpes = run_period(spec, data_dir, fee, slip, oos_start, oos_end,   "OOS")

    # Escribe CSVs
    is_csv  = output_dir / f"{spec_path.stem}_is_fills.csv"
    oos_csv = output_dir / f"{spec_path.stem}_oos_fills.csv"
    write_fills_csv(is_csv,  is_trades_list)
    write_fills_csv(oos_csv, oos_trades_list)
    print(f"\nFills escritos:")
    print(f"  IS:  {is_csv}")
    print(f"  OOS: {oos_csv}")

    # Estadísticas rápidas IS
    is_all  = pd.concat([t for t in is_trades_list  if not t.empty], ignore_index=True) if is_trades_list  else pd.DataFrame()
    oos_all = pd.concat([t for t in oos_trades_list if not t.empty], ignore_index=True) if oos_trades_list else pd.DataFrame()

    def summarize(trades: pd.DataFrame, label: str) -> None:
        if trades.empty:
            print(f"  {label}: sin trades")
            return
        r = trades["return_net"]
        n = len(r)
        mean_r = r.mean()
        std_r  = r.std(ddof=1)
        pos_r  = (r > 0).sum() / n
        total_cost_rt = (fee + slip) * 2
        print(f"  {label}: N={n}, mean={mean_r*100:+.4f}%, std={std_r*100:.4f}%, "
              f"winrate={pos_r:.1%}, cost_rt={total_cost_rt*100:.3f}%")

    print()
    summarize(is_all,  "IS  agg")
    summarize(oos_all, "OOS agg")

    # ── Paso 3: Trading.Analytics (Gate 1 + Gate 2 MC) ───────────────────────
    analytics_exe = args.analytics_exe
    if not Path(analytics_exe).exists():
        print(f"\n[ADVERTENCIA] Trading.Analytics no encontrado en: {analytics_exe}")
        print("  Intentando buscar en directorio de build Debug...")
        debug_exe = str(
            Path(__file__).parent.parent / "Trading.Analytics" / "bin" / "Debug" / "net10.0" / "Trading.Analytics.exe"
        )
        if Path(debug_exe).exists():
            analytics_exe = debug_exe
            print(f"  Usando: {analytics_exe}")
        else:
            print("  No se puede llamar Trading.Analytics. Verificar que el proyecto esté compilado.")
            return

    passed, _ = call_trading_analytics(
        analytics_exe, is_csv, oos_csv, strategy_name, output_dir
    )

    # ── Veredicto final ───────────────────────────────────────────────────────
    print(f"\n{'=' * 80}")
    print(f"VEREDICTO FINAL -{strategy_name}")
    print(f"{'=' * 80}")
    print(f"  IS  Sharpes:  {', '.join(f'{k}={v:+.3f}' for k, v in is_sharpes.items())}")
    print(f"  OOS Sharpes:  {', '.join(f'{k}={v:+.3f}' for k, v in oos_sharpes.items())}")
    print(f"  Gate 1 + Gate 2 (Trading.Analytics): {'PASS' if passed else 'FAIL'}")
    if passed:
        print(f"  => EJE SOBREVIVE: procede a Capa B (implementación Lean)")
    else:
        print(f"  => EJE RECHAZADO: no procede a Capa B")
    print(f"{'=' * 80}")

    sys.exit(0 if passed else 1)


if __name__ == "__main__":
    main()
