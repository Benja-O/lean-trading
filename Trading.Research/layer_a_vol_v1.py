"""
Capa A - Familia VOLATILIDAD (V1): Gate estadístico Python para 3 hipótesis.

Hipótesis:
  H-V1 - Reversión por spike de vol (capitulación → rebote), long-only
  H-V2 - Compresión de vol → expansión (breakout)
  H-V3 - Vol-targeting como enhancer (diagnóstico, no cuenta para gate)

Costos: 0.04% fee/lado + 0.02% slippage/lado = 0.12% RT (igual que eje TREND/OFI).
IS:  2021-01-01 → 2024-12-31
OOS: 2025-01-01 → 2026-06-09
Activos: BTCUSDT, ETHUSDT, SOLUSDT
Gate M4: Sharpe IS >= 0.5 en >= 2/3 activos + meseta de configs.

Causalidad garantizada:
  - RV en día t = std de retornos log de los días {t-window+1 … t} inclusive.
    Se usa .shift(1) sobre la serie de retornos ANTES de aplicar rolling,
    de modo que el retorno del día t+1 NUNCA entra en el cálculo del día t.
    Equivalentemente: rv.shift(1) asegura que la señal del día t usa sólo
    información hasta el cierre de t-1 (la señal se genera al cierre de t-1
    y la entrada se ejecuta al cierre de t-1 = apertura de t).
    Ver función rv_causal() para la implementación.
  - El percentil rolling (expanding en H-V1, rolling en H-V2) se calcula
    con .rank(pct=True) o expanding/rolling sobre rv, siempre shifted,
    de modo que el percentil del día t depende sólo de {t-window…t-1}.
  - El break de canal en H-V2 compara close[t-1] con max/min de high/low
    en [t-lookback-1 … t-2] (doble shift), sin tocar t.
  - H-V3: el peso de escalado usa rv.shift(1) antes de dividir.

Uso:
    python layer_a_vol_v1.py
    python layer_a_vol_v1.py --oos       # incluye columnas OOS
    python layer_a_vol_v1.py --hv1       # solo H-V1
    python layer_a_vol_v1.py --hv2       # solo H-V2
    python layer_a_vol_v1.py --hv3       # solo H-V3 (diagnóstico vol-targeting)
"""

import argparse
import sys
import warnings
from pathlib import Path

import numpy as np
import pandas as pd

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")
warnings.filterwarnings("ignore")

# ── Constantes ────────────────────────────────────────────────────────────────

DATA_DIR  = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\AggTrades\features")
ASSETS    = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
IS_START  = "2021-01-01"
IS_END    = "2024-12-31"
OOS_START = "2025-01-01"
OOS_END   = "2026-06-09"

FEE_PER_SIDE      = 0.0004   # 0.04%
SLIPPAGE_PER_SIDE = 0.0002   # 0.02%
COST_PER_SIDE     = FEE_PER_SIDE + SLIPPAGE_PER_SIDE  # 0.06%
COST_RT           = 2 * COST_PER_SIDE                 # 0.12%

SHARPE_THRESHOLD = 0.5
ASSETS_NEEDED    = 2
MIN_TRADES       = 30


# ── Carga y resampleo de datos (idéntico a layer_a_trend_s1.py) ──────────────

def load_ohlcv_1h(asset: str) -> pd.DataFrame:
    """Carga el parquet 1h con columnas OHLCV. Índice = DatetimeIndex UTC."""
    path = DATA_DIR / f"{asset}_1h_features.parquet"
    df = pd.read_parquet(path)
    df["bar"] = pd.to_datetime(df["bar"], utc=True)
    df = df.set_index("bar").sort_index()
    return df[["open", "high", "low", "close", "volume"]].copy()


def resample_ohlcv(df_1h: pd.DataFrame) -> pd.DataFrame:
    """Resamplea OHLCV 1h a 1d (UTC medianoche)."""
    agg = {
        "open":   "first",
        "high":   "max",
        "low":    "min",
        "close":  "last",
        "volume": "sum",
    }
    return df_1h.resample("1D", closed="left", label="left").agg(agg).dropna()


def load_all_daily() -> dict[str, pd.DataFrame]:
    """Carga y resamplea a 1d para los 3 activos."""
    result = {}
    for asset in ASSETS:
        df_1h = load_ohlcv_1h(asset)
        result[asset] = resample_ohlcv(df_1h)
    return result


# ── Primitivo de volatilidad realizada causal ─────────────────────────────────

def rv_causal(close: pd.Series, window: int) -> pd.Series:
    """
    Volatilidad realizada CAUSAL: std de retornos log sobre `window` días.

    Causalidad: log_ret[t] = log(close[t]/close[t-1]).
    rv_raw[t]  = std(log_ret[t-window+1 … t], ddof=1)
               = rolling(window).std() sobre log_ret, sin shift.
    rv[t]      = rv_raw.shift(1)  ← la señal del día t usa RV calculada
                 hasta el cierre de t-1.  close[t] NUNCA entra en rv[t].

    Retorna la serie shifted (ya causal), lista para comparar contra
    percentil y construir señales del día t.
    """
    log_ret = np.log(close / close.shift(1))
    rv_raw  = log_ret.rolling(window, min_periods=window).std(ddof=1)
    return rv_raw.shift(1)   # causal: rv[t] = RV hasta cierre de t-1


def rv_percentile_expanding(rv: pd.Series) -> pd.Series:
    """
    Percentil expanding causal de rv.
    percentil[t] = rank de rv[t] entre {rv[0]…rv[t-1]}.
    Implementado: expanding().rank(pct=True).shift(1) con mínimo 20 obs.
    """
    return rv.expanding(min_periods=20).rank(pct=True).shift(1)


def rv_percentile_rolling(rv: pd.Series, window: int) -> pd.Series:
    """
    Percentil rolling causal de rv sobre ventana `window`.
    percentil[t] = rank de rv[t] en {rv[t-window]…rv[t-1]}.
    Implementado: rolling(window).rank(pct=True).shift(1).
    """
    return rv.rolling(window, min_periods=window).rank(pct=True).shift(1)


# ── Simulador (reutiliza lógica de layer_a_trend_s1.py) ──────────────────────

def filter_overlapping(signals: pd.Series, hold: int) -> pd.Series:
    """Suprime señales mientras hay posición abierta."""
    arr = signals.values.copy()
    in_pos_until = -1
    for i in range(len(arr)):
        if arr[i] != 0:
            if i <= in_pos_until:
                arr[i] = 0
            else:
                in_pos_until = i + hold - 1
    return pd.Series(arr, index=signals.index, dtype=int)


def simulate(
    close: pd.Series,
    signals_raw: pd.Series,
    hold: int,
    cost_per_side: float,
    start: str,
    end: str,
) -> tuple[list[float], int]:
    """
    Simula trades con position tracking y costos.
    Entrada al cierre del día de señal (índice i), salida al cierre i+hold.
    Retorna (lista de retornos netos por trade, n_trades).
    """
    close   = close.loc[start:end]
    signals = signals_raw.loc[start:end]
    signals = filter_overlapping(signals, hold)

    vals = close.values
    sigs = signals.values
    n    = len(vals)

    returns = []
    for i in range(n):
        if sigs[i] == 0:
            continue
        j = i + hold
        if j >= n:
            break
        ep = vals[i] * (1.0 + cost_per_side)   # entry price con slippage+fee
        xp = vals[j] * (1.0 - cost_per_side)   # exit price con slippage+fee
        returns.append((xp - ep) / ep)

    return returns, len(returns)


def compute_sharpe(returns: list[float], period_years: float) -> float:
    """Sharpe anualizado desde lista de retornos por trade."""
    if len(returns) < MIN_TRADES:
        return float("nan")
    r = np.array(returns)
    mean_r = float(np.mean(r))
    std_r  = float(np.std(r, ddof=1))
    if std_r == 0:
        return float("nan")
    trades_per_year = len(r) / period_years
    return mean_r / std_r * np.sqrt(trades_per_year)


def period_years(start: str, end: str) -> float:
    s = pd.Timestamp(start, tz="UTC")
    e = pd.Timestamp(end,   tz="UTC")
    return (e - s).total_seconds() / (365.25 * 86400)


# ── H-V1: Reversión por spike de vol ─────────────────────────────────────────

def hv1_signal(
    close: pd.Series,
    rv_window: int,
    trigger_pct: float,
    hold: int,
) -> pd.Series:
    """
    Señal H-V1 (long-only):
      Día t: rv_causal >= percentil trigger AND retorno sobre rv_window < 0
      → LONG al cierre de t, hold días.

    Causalidad:
      rv[t]         = std(log_ret[t-rv_window … t-1])  (rv_causal con .shift(1))
      pct[t]        = expanding rank de rv hasta t-1     (rv_percentile_expanding)
      ret_window[t] = close[t-1] / close[t-rv_window-1] - 1  (shift(1))
      Ninguno de los tres usa close[t] ni datos futuros.
    """
    rv   = rv_causal(close, rv_window)
    pct  = rv_percentile_expanding(rv)

    # Retorno del activo sobre la ventana del spike (causal: close[t-1]/close[t-rv_window-1]-1)
    ret_window = close.shift(1) / close.shift(rv_window + 1) - 1

    spike   = pct >= trigger_pct        # spike de vol
    selloff = ret_window < 0            # el movimiento es negativo (sell-off)

    signal = (spike & selloff).astype(int)
    return signal


def hv1_signal_symmetric(
    close: pd.Series,
    rv_window: int,
    trigger_pct: float,
    hold: int,
) -> tuple[pd.Series, pd.Series]:
    """
    Versión simétrica (diagnóstico): fade del signo del movimiento.
    long  si spike AND ret < 0 (igual que base)
    short si spike AND ret > 0  (fade de subida)
    Retorna (long_sig, short_sig). Solo diagnóstico, no cuenta para gate.
    """
    rv   = rv_causal(close, rv_window)
    pct  = rv_percentile_expanding(rv)
    ret_window = close.shift(1) / close.shift(rv_window + 1) - 1

    spike      = pct >= trigger_pct
    long_sig   = (spike & (ret_window < 0)).astype(int)
    short_sig  = (spike & (ret_window > 0)).astype(int)
    return long_sig, short_sig


HV1_CONFIGS = [
    {"rv_window": 10, "trigger_pct": 0.90, "hold": 3},
    {"rv_window": 10, "trigger_pct": 0.90, "hold": 5},
    {"rv_window": 10, "trigger_pct": 0.95, "hold": 3},
    {"rv_window": 10, "trigger_pct": 0.95, "hold": 5},
    {"rv_window": 20, "trigger_pct": 0.90, "hold": 3},
    {"rv_window": 20, "trigger_pct": 0.90, "hold": 5},
    {"rv_window": 20, "trigger_pct": 0.95, "hold": 3},
    {"rv_window": 20, "trigger_pct": 0.95, "hold": 5},
]  # 8 configs exactas según grilla pre-registrada


# ── H-V2: Compresión de vol → expansión (breakout) ───────────────────────────

def hv2_signal(
    close: pd.Series,
    high: pd.Series,
    low: pd.Series,
    rv_window: int,          # lookback para RV y canal
    bottom_pct: float,       # percentil de compresión (0.10 o 0.20)
    hold: int,
) -> pd.Series:
    """
    Señal H-V2 (long-only: break al alza; short-only: break a la baja).
    Aquí implementamos LONG (break al alza) per grilla:
      Día t: rv[t] <= percentil bottom (compresión)
             AND close[t-1] > max(high[t-lookback-1 … t-2])  ← break al alza

    Causalidad:
      rv[t]           = rv_causal (hasta t-1)
      pct_bottom[t]   = rolling rank de rv hasta t-1
      canal_max[t]    = max(high[t-lookback-1 … t-2])  (doble shift)
      Ninguno usa close[t] ni high[t].
    """
    rv      = rv_causal(close, rv_window)
    pct     = rv_percentile_rolling(rv, window=rv_window)

    # Canal de ruptura: doble shift para que [t-lookback-1 … t-2]
    canal_max = high.shift(2).rolling(rv_window, min_periods=rv_window).max()
    canal_min = low.shift(2).rolling(rv_window, min_periods=rv_window).min()

    compression = pct <= bottom_pct

    # Long signal: break al alza
    prev_close  = close.shift(1)
    break_up    = prev_close > canal_max
    break_down  = prev_close < canal_min

    # Dirección del break define la señal; tomamos break_up (long) como señal única
    # (break_down se podría usar como short, aquí solo long per especificación)
    long_signal = (compression & break_up).astype(int)
    return long_signal


HV2_CONFIGS = [
    {"rv_window": 20, "bottom_pct": 0.10, "hold": 5},
    {"rv_window": 20, "bottom_pct": 0.10, "hold": 10},
    {"rv_window": 20, "bottom_pct": 0.20, "hold": 5},
    {"rv_window": 20, "bottom_pct": 0.20, "hold": 10},
    {"rv_window": 40, "bottom_pct": 0.10, "hold": 5},
    {"rv_window": 40, "bottom_pct": 0.10, "hold": 10},
    {"rv_window": 40, "bottom_pct": 0.20, "hold": 5},
    {"rv_window": 40, "bottom_pct": 0.20, "hold": 10},
]  # 8 configs exactas según grilla pre-registrada


# ── H-V3: Vol-targeting como enhancer ────────────────────────────────────────

def hv3_run(
    data_daily: dict[str, pd.DataFrame],
    target_vol: float = 0.20,   # target diario anualizado
    cost_per_side: float = COST_PER_SIDE,
    rv_window: int = 20,
) -> None:
    """
    Vol-targeting: peso_t = min(1, target_vol / RV_t).
    RV_t es causal (rv_causal).
    Comparamos Sharpe escalado vs buy-and-hold (neto de costos por rebalanceo).

    Costos del vol-targeting: se aplican cuando el peso cambia,
    proporcionales al cambio de peso × 2 × COST_PER_SIDE.
    """
    is_years  = period_years(IS_START, IS_END)
    oos_years = period_years(OOS_START, OOS_END)

    # RV diaria → anualizar (sqrt(252))
    rv_scale = np.sqrt(252)

    print(f"\n{'='*72}")
    print(f"H-V3  Vol-Targeting Enhancer  |  target_vol={target_vol:.0%}  rv_window={rv_window}d")
    print(f"{'='*72}")
    print(f"  {'Asset':<10}  {'Period':<5}  {'Sharpe BH':>10}  {'Sharpe VT':>10}  "
          f"{'Turnover/yr':>12}  {'Cost drag':>10}")
    print("-" * 72)

    for asset in ASSETS:
        df = data_daily[asset]

        for period, start, end, yrs in [
            ("IS",  IS_START,  IS_END,  is_years),
            ("OOS", OOS_START, OOS_END, oos_years),
        ]:
            close = df["close"].loc[start:end]
            if len(close) < 60:
                print(f"  {asset:<10}  {period:<5}  {'N/A':>10}  {'N/A':>10}  {'N/A':>12}  {'N/A':>10}")
                continue

            # Retornos diarios (log para mayor precisión)
            log_ret = np.log(close / close.shift(1)).dropna()

            # Buy-and-hold Sharpe (sin costos, es referencia)
            bh_sharpe = (log_ret.mean() / log_ret.std(ddof=1)) * np.sqrt(252)

            # Vol realizada causal sobre toda la serie (para el período)
            close_full = df["close"]
            rv_ann = rv_causal(close_full, rv_window) * rv_scale   # anualizada
            rv_ann_period = rv_ann.loc[start:end]

            # Peso vol-targeting causal (usa rv[t] que es causal hasta t-1)
            weight = (target_vol / rv_ann_period).clip(upper=1.0).fillna(1.0)

            # Retornos del día t (del activo, con el peso del día t)
            # ret[t] = log(close[t]/close[t-1]) * weight[t]
            # weight[t] usa rv[t] = RV hasta t-1 → causal OK
            ret_period = np.log(close / close.shift(1)).loc[start:end].dropna()
            weight_aligned = weight.reindex(ret_period.index).ffill().fillna(1.0)

            # Costos de rebalanceo: |delta_weight[t]| * 2 * cost_per_side
            delta_w  = weight_aligned.diff().abs().fillna(0.0)
            cost_day = delta_w * 2 * cost_per_side
            turnover_yr = delta_w.mean() * 252

            # Retorno neto del vol-targeting
            vt_ret_net = ret_period * weight_aligned - cost_day

            vt_sharpe = (vt_ret_net.mean() / vt_ret_net.std(ddof=1)) * np.sqrt(252)
            cost_drag  = cost_day.mean() * 252   # costo anual por rebalanceo

            print(f"  {asset:<10}  {period:<5}  {bh_sharpe:>+10.3f}  {vt_sharpe:>+10.3f}  "
                  f"{turnover_yr:>12.3f}  {cost_drag:>10.4f}")

    print()


# ── Runner por hipótesis ──────────────────────────────────────────────────────

def run_hypothesis(
    name: str,
    hyp_id: str,
    configs: list[dict],
    data_daily: dict[str, pd.DataFrame],
    signal_fn,
    oos: bool = False,
) -> list[dict]:
    """
    Evalúa todas las configs sobre los 3 activos.
    signal_fn: callable(df, cfg) -> pd.Series  (señal cruda, índice diario)
    df tiene columnas open/high/low/close/volume con índice DatetimeIndex UTC.
    """
    is_years  = period_years(IS_START, IS_END)
    oos_years = period_years(OOS_START, OOS_END)

    results = []

    print(f"\n{'='*72}")
    print(f"{hyp_id} — {name}  |  {len(configs)} configs  |  costos={COST_RT*100:.2f}% RT")
    print(f"{'='*72}")
    col_w = 22
    header = (
        f"  {'#':>3}  {'params':<35}  "
        f"{'BTC IS':>{col_w}}  {'ETH IS':>{col_w}}  {'SOL IS':>{col_w}}  "
        f"{'pass':>5}  gate"
    )
    if oos:
        header += f"  {'BTC OOS':>{col_w}}  {'ETH OOS':>{col_w}}  {'SOL OOS':>{col_w}}"
    print(header)
    print("-" * len(header))

    for i, cfg in enumerate(configs, 1):
        hold = cfg["hold"]
        sharpes_is  = {}
        sharpes_oos = {}
        ntrades_is  = {}
        assets_pass = 0

        for asset in ASSETS:
            df = data_daily[asset]
            try:
                sig = signal_fn(df, cfg)
            except Exception as e:
                print(f"  [ERROR señal {hyp_id} {asset}]: {e}")
                sharpes_is[asset] = float("nan")
                ntrades_is[asset] = 0
                continue

            close = df["close"]

            # IS
            try:
                rets_is, n_is = simulate(close, sig, hold, COST_PER_SIDE, IS_START, IS_END)
                sh_is = compute_sharpe(rets_is, is_years)
            except Exception as e:
                print(f"  [ERROR IS {hyp_id} {asset}]: {e}")
                sh_is, n_is = float("nan"), 0

            sharpes_is[asset] = sh_is
            ntrades_is[asset] = n_is

            if not np.isnan(sh_is) and sh_is >= SHARPE_THRESHOLD:
                assets_pass += 1

            # OOS
            if oos:
                try:
                    rets_oos, _ = simulate(close, sig, hold, COST_PER_SIDE, OOS_START, OOS_END)
                    sharpes_oos[asset] = compute_sharpe(rets_oos, oos_years)
                except Exception:
                    sharpes_oos[asset] = float("nan")

        gate = "PASS" if assets_pass >= ASSETS_NEEDED else "FAIL"

        # Formateo de fila
        def fmt(sh, n):
            if np.isnan(sh):
                return f"N/A(T={n})"
            flag = "*" if sh >= SHARPE_THRESHOLD else " "
            return f"{flag}{sh:+.3f}(T={n})"

        def fmt_oos(sh):
            if np.isnan(sh):
                return "N/A"
            flag = "*" if sh >= SHARPE_THRESHOLD else " "
            return f"{flag}{sh:+.3f}"

        params_str = " ".join(f"{k}={v}" for k, v in cfg.items() if k != "hold")
        params_str += f" h={hold}"
        params_str = params_str[:35]

        cells_is = [f"{fmt(sharpes_is.get(a, float('nan')), ntrades_is.get(a, 0)):>{col_w}}" for a in ASSETS]
        gate_mark = " (***PASS***)" if gate == "PASS" else ""

        row = (
            f"  {i:>3}  {params_str:<35}  "
            f"{'  '.join(cells_is)}  {assets_pass:>5}  {gate}{gate_mark}"
        )
        if oos:
            cells_oos = [f"{fmt_oos(sharpes_oos.get(a, float('nan'))):>{col_w}}" for a in ASSETS]
            row += f"  {'  '.join(cells_oos)}"
        print(row)

        results.append({
            "id":             hyp_id,
            "name":           name,
            "config":         cfg,
            "assets_pass_is": assets_pass,
            "gate_is":        gate,
            "sharpes_is":     sharpes_is,
            "ntrades_is":     ntrades_is,
            "sharpes_oos":    sharpes_oos,
        })

    n_pass = sum(1 for r in results if r["gate_is"] == "PASS")
    print(f"\n  {hyp_id}: {n_pass}/{len(configs)} configs PASS IS")
    return results


def plateau_analysis(results: list[dict], hyp_id: str) -> bool:
    """
    ¿Meseta? La mayoría de configs del mismo signo (>= 50% pasan gate).
    Retorna True si hay meseta.
    """
    n_pass  = sum(1 for r in results if r["gate_is"] == "PASS")
    n_total = len(results)
    ratio   = n_pass / n_total if n_total > 0 else 0.0

    print(f"\n  Análisis de meseta {hyp_id}: {n_pass}/{n_total} configs PASS "
          f"({ratio*100:.0f}%) → {'MESETA' if ratio >= 0.50 else 'PICO o NADA'}")

    # Sharpes medios por activo
    for asset in ASSETS:
        vals = [r["sharpes_is"].get(asset, float("nan")) for r in results]
        mean_v = np.nanmean(vals) if vals else float("nan")
        print(f"    {asset}: Sharpe IS medio = {mean_v:+.3f}")

    return ratio >= 0.50


# ── H-V1 diagnóstico simétrico ────────────────────────────────────────────────

def run_hv1_symmetric_diagnostic(data_daily: dict[str, pd.DataFrame]) -> None:
    """
    Diagnóstico opcional: versión simétrica de H-V1.
    Compara long (fade sell-off) vs short (fade rally) en el spike.
    Solo imprime, no cuenta para gate.
    """
    is_years = period_years(IS_START, IS_END)
    cfg = {"rv_window": 20, "trigger_pct": 0.90, "hold": 5}

    print(f"\n{'='*72}")
    print("H-V1 DIAGNÓSTICO SIMÉTRICO (no cuenta para gate)")
    print(f"  Config: rv_window={cfg['rv_window']} trigger={cfg['trigger_pct']} hold={cfg['hold']}")
    print(f"{'='*72}")
    print(f"  {'Asset':<10}  {'Long IS':>10}  {'Short IS':>10}  {'#Long':>8}  {'#Short':>8}")
    print("-" * 55)

    for asset in ASSETS:
        df    = data_daily[asset]
        close = df["close"]
        long_sig, short_sig = hv1_signal_symmetric(
            close, cfg["rv_window"], cfg["trigger_pct"], cfg["hold"]
        )
        rl, nl = simulate(close, long_sig,  cfg["hold"], COST_PER_SIDE, IS_START, IS_END)
        rs, ns = simulate(close, short_sig, cfg["hold"], COST_PER_SIDE, IS_START, IS_END)
        sl = compute_sharpe(rl, is_years)
        ss = compute_sharpe(rs, is_years)

        def fmt(s):
            return f"{s:+.3f}" if not np.isnan(s) else "N/A"

        print(f"  {asset:<10}  {fmt(sl):>10}  {fmt(ss):>10}  {nl:>8}  {ns:>8}")


# ── Main ──────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description="Capa A - Familia Volatilidad V1")
    parser.add_argument("--oos",  action="store_true", help="Incluir columnas OOS")
    parser.add_argument("--hv1",  action="store_true", help="Solo H-V1")
    parser.add_argument("--hv2",  action="store_true", help="Solo H-V2")
    parser.add_argument("--hv3",  action="store_true", help="Solo H-V3 (diagnóstico)")
    args = parser.parse_args()

    run_all = not (args.hv1 or args.hv2 or args.hv3)

    print("Cargando datos diarios...")
    data_daily = load_all_daily()
    print(f"Datos cargados. Activos: {list(data_daily.keys())}")
    for asset, df in data_daily.items():
        print(f"  {asset}: {len(df)} barras ({df.index[0].date()} → {df.index[-1].date()})")

    print(f"\nIS:  {IS_START} → {IS_END}  ({period_years(IS_START, IS_END):.2f} años)")
    print(f"OOS: {OOS_START} → {OOS_END}  ({period_years(OOS_START, OOS_END):.2f} años)")
    print(f"Costos: {COST_RT*100:.3f}% RT")

    # ── H-V1 ─────────────────────────────────────────────────────────────────
    if run_all or args.hv1:
        def hv1_fn(df, cfg):
            return hv1_signal(df["close"], cfg["rv_window"], cfg["trigger_pct"], cfg["hold"])

        results_v1 = run_hypothesis(
            "Reversión por spike de vol (long-only)",
            "H-V1",
            HV1_CONFIGS,
            data_daily,
            hv1_fn,
            oos=args.oos,
        )
        plateau_v1 = plateau_analysis(results_v1, "H-V1")

        # Diagnóstico simétrico
        run_hv1_symmetric_diagnostic(data_daily)

    # ── H-V2 ─────────────────────────────────────────────────────────────────
    if run_all or args.hv2:
        def hv2_fn(df, cfg):
            return hv2_signal(
                df["close"], df["high"], df["low"],
                cfg["rv_window"], cfg["bottom_pct"], cfg["hold"],
            )

        results_v2 = run_hypothesis(
            "Compresión de vol → expansión (breakout long)",
            "H-V2",
            HV2_CONFIGS,
            data_daily,
            hv2_fn,
            oos=args.oos,
        )
        plateau_v2 = plateau_analysis(results_v2, "H-V2")

    # ── H-V3 ─────────────────────────────────────────────────────────────────
    if run_all or args.hv3:
        hv3_run(data_daily)

    # ── Veredicto final ───────────────────────────────────────────────────────
    print(f"\n{'='*72}")
    print("VEREDICTO FINAL — Eje Volatilidad — Capa A")
    print(f"{'='*72}")

    if run_all or args.hv1:
        n_pass_v1 = sum(1 for r in results_v1 if r["gate_is"] == "PASS")
        gate_v1   = n_pass_v1 >= ASSETS_NEEDED
        print(f"  H-V1: {n_pass_v1}/{len(HV1_CONFIGS)} configs PASS  |  "
              f"Meseta={'SI' if plateau_v1 else 'NO'}  |  "
              f"Gate Hipótesis={'PASS' if gate_v1 else 'FAIL'}")

    if run_all or args.hv2:
        n_pass_v2 = sum(1 for r in results_v2 if r["gate_is"] == "PASS")
        gate_v2   = n_pass_v2 >= ASSETS_NEEDED
        print(f"  H-V2: {n_pass_v2}/{len(HV2_CONFIGS)} configs PASS  |  "
              f"Meseta={'SI' if plateau_v2 else 'NO'}  |  "
              f"Gate Hipótesis={'PASS' if gate_v2 else 'FAIL'}")

    print(f"\n  N total de configs (para DSR/PBO): {len(HV1_CONFIGS) + len(HV2_CONFIGS)} configs × 3 activos")
    print(f"  Costos: {COST_RT*100:.3f}% RT siempre aplicados.")
    print(f"  Causalidad: rv_causal() = rolling std + .shift(1); canal = high.shift(2).rolling()")


if __name__ == "__main__":
    main()
