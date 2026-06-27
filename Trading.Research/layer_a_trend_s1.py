"""
Capa A - Familia TREND (S1): Gate estadístico Python para 10 hipótesis de tendencia.
ADR-056 D5: gramática crece a demanda. Esta familia agrega primitivos de trend
al lado de la familia OFI existente, sin modificarla.

Hipótesis (todas long-only, mecanismo = subreacción/continuación):
  TS1  - Time-Series Momentum: long si retorno_L > 0
  TS2  - Cruce de medias móviles: long si MA_rápida > MA_lenta
  TS3  - Breakout Donchian: long al superar máximo N barras; salida por mínimo M
  TS4  - Precio vs MA larga: long si close > MA_L × (1 + umbral)
  TS5  - Momentum escalado por vol: ret_L / vol_L > umbral
  TS6  - Breakout de canal (lógica de salida distinta de TS3)
  TS7  - MACD: long si (EMA_rápida − EMA_lenta) > 0
  TS8  - Acuerdo multi-timeframe: long si alcista en 4h Y en 1d
  TS9  - Momentum con skip: retorno[t-L, t-k] > 0
  TS10 - Trend + gate HMM: TS1 gateada por régimen Trend del HMM (Hito B)

Costos: 0.04% fee/lado + 0.02% slippage/lado = 0.12% RT (igual que eje 3).
IS: 2021-01-01 → 2024-12-31
OOS: 2025-01-01 → 2026-06-09
Activos: BTCUSDT, ETHUSDT, SOLUSDT
Gate M4: Sharpe >= 0.5 en >= 2/3 activos CON costos.
Long-only: sin shorts.

Uso:
    python layer_a_trend_s1.py
    python layer_a_trend_s1.py --oos          # corre OOS también para las que pasan IS
    python layer_a_trend_s1.py --hypothesis 2 # solo TS2
"""

import argparse
import sys
import warnings
from pathlib import Path
from typing import Optional

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

# ── Carga y resampleo de datos ────────────────────────────────────────────────

def load_ohlcv_1h(asset: str) -> pd.DataFrame:
    """Carga el parquet 1h con columnas OHLCV. Índice = DatetimeIndex UTC."""
    path = DATA_DIR / f"{asset}_1h_features.parquet"
    df = pd.read_parquet(path)
    df["bar"] = pd.to_datetime(df["bar"], utc=True)
    df = df.set_index("bar").sort_index()
    return df[["open", "high", "low", "close", "volume"]].copy()


def resample_ohlcv(df_1h: pd.DataFrame, tf: str) -> pd.DataFrame:
    """
    Resamplea OHLCV 1h a 4h o 1d.
    tf: '4h' | '1d'
    Usa closed='left', label='left' (la barra 4h 00:00 agrupa 00:00–03:00).
    Para 1d se ancla a UTC medianoche.
    """
    rule_map = {"4h": "4h", "1d": "1D"}
    rule = rule_map[tf]
    agg = {
        "open":   "first",
        "high":   "max",
        "low":    "min",
        "close":  "last",
        "volume": "sum",
    }
    resampled = df_1h.resample(rule, closed="left", label="left").agg(agg).dropna()
    return resampled


def load_all_assets(tf: str) -> dict[str, pd.DataFrame]:
    """Carga y (si necesario) resamplea para todos los activos."""
    result = {}
    for asset in ASSETS:
        df_1h = load_ohlcv_1h(asset)
        if tf == "1h":
            result[asset] = df_1h
        else:
            result[asset] = resample_ohlcv(df_1h, tf)
    return result


# ── Primitivos de señal (gramática de trend) ──────────────────────────────────

def sig_ts_momentum(close: pd.Series, lookback: int) -> pd.Series:
    """
    TS1 / TS9 base: retorno del período [t-lookback, t-1] > 0.
    Shift(1) para evitar lookahead.
    """
    ret = close.shift(1) / close.shift(lookback + 1) - 1
    return (ret > 0).astype(int)


def sig_ma_cross(close: pd.Series, fast: int, slow: int) -> pd.Series:
    """TS2: long si MA_fast > MA_slow (ambas calculadas sobre close.shift(1))."""
    c = close.shift(1)
    ma_fast = c.rolling(fast, min_periods=fast).mean()
    ma_slow = c.rolling(slow, min_periods=slow).mean()
    return (ma_fast > ma_slow).astype(int)


def sig_donchian(
    close: pd.Series, high: pd.Series, low: pd.Series,
    lookback_entry: int, lookback_exit: int, hold: int
) -> pd.Series:
    """
    TS3 - Breakout Donchian.
    Señal de ENTRADA: close[t-1] > max(high[t-lookback_entry : t-1]).
    Señal de SALIDA (para la simulación): close < min(low[t-lookback_exit : t])
    OR barras transcurridas >= hold.
    Retorna señal de entrada (0/1); la salida se maneja en simulate_trades_flexible.
    """
    h_shift = high.shift(1)
    breakout_level = h_shift.rolling(lookback_entry, min_periods=lookback_entry).max().shift(1)
    entry = (close.shift(1) > breakout_level).astype(int)
    return entry


def sig_price_vs_ma(close: pd.Series, lookback: int, threshold: float) -> pd.Series:
    """TS4: long si close > MA_lookback × (1 + threshold)."""
    c = close.shift(1)
    ma = c.rolling(lookback, min_periods=lookback).mean()
    return (close.shift(1) > ma * (1 + threshold)).astype(int)


def sig_vol_adjusted_momentum(close: pd.Series, lookback: int, threshold: float) -> pd.Series:
    """TS5: ret_L / vol_L > threshold. Usando retornos log."""
    log_ret = np.log(close / close.shift(1))
    cum_ret = log_ret.rolling(lookback, min_periods=lookback).sum().shift(1)
    vol     = log_ret.rolling(lookback, min_periods=lookback).std(ddof=1).shift(1)
    signal  = cum_ret / vol.replace(0, np.nan)
    return (signal > threshold).astype(int)


def sig_channel_breakout(close: pd.Series, lookback: int) -> pd.Series:
    """
    TS6 - Breakout de canal (hold fijo vs salida por mínimo de canal en TS3).
    Señal en t: close[t-1] > max(close[t-lookback-1 : t-2])
    Doble shift para que la comparación sea contra el canal previo a la barra de señal.
    Lógica: la barra anterior (t-1) rompe el máximo de las lookback barras previas a ella.
    """
    prev_close  = close.shift(1)
    channel_max = close.shift(2).rolling(lookback, min_periods=lookback).max()
    return (prev_close > channel_max).astype(int)


def sig_macd(close: pd.Series, fast: int, slow: int) -> pd.Series:
    """TS7: long si EMA_fast − EMA_slow > 0."""
    c = close.shift(1)
    ema_fast = c.ewm(span=fast, adjust=False, min_periods=fast).mean()
    ema_slow = c.ewm(span=slow, adjust=False, min_periods=slow).mean()
    return ((ema_fast - ema_slow) > 0).astype(int)


def sig_multi_tf(
    close_4h: pd.Series, close_1d: pd.Series,
    lookback_4h: int, lookback_1d: int
) -> pd.Series:
    """
    TS8 - Multi-timeframe: long si tendencia alcista en 4h Y en 1d.
    Tendencia = MA_fast > MA_slow en cada TF.
    Retorna serie indexada en 4h que solo señala cuando ambos TF acuerdan.
    """
    # MA en 4h
    c4 = close_4h.shift(1)
    fast_4h = c4.rolling(lookback_4h, min_periods=lookback_4h).mean()
    slow_4h = c4.rolling(lookback_4h * 3, min_periods=lookback_4h * 3).mean()
    trend_4h = (fast_4h > slow_4h)

    # MA en 1d resampleada/forward-filled a 4h
    c1d = close_1d.shift(1)
    fast_1d = c1d.rolling(lookback_1d, min_periods=lookback_1d).mean()
    slow_1d = c1d.rolling(lookback_1d * 3, min_periods=lookback_1d * 3).mean()
    trend_1d_raw = (fast_1d > slow_1d)
    trend_1d = trend_1d_raw.reindex(close_4h.index, method="ffill")

    return (trend_4h & trend_1d).astype(int)


def sig_momentum_skip(close: pd.Series, lookback: int, skip: int) -> pd.Series:
    """
    TS9 - Momentum con skip de las últimas k barras.
    Retorno = close[t-1-skip] / close[t-1-skip-lookback] - 1.
    Long si > 0. Evita el momentum de corto plazo (reversión).
    """
    shifted_new = close.shift(1 + skip)
    shifted_old = close.shift(1 + skip + lookback)
    ret = shifted_new / shifted_old - 1
    return (ret > 0).astype(int)


def sig_ts1_with_hmm_gate(
    close: pd.Series, lookback: int,
    hmm_regime_series: Optional[pd.Series]
) -> pd.Series:
    """
    TS10 - TS1 gateada por régimen HMM = Trend.
    Si hmm_regime_series es None, retorna solo TS1 (para diagnóstico).
    """
    ts1 = sig_ts_momentum(close, lookback)
    if hmm_regime_series is None:
        return ts1
    trend_gate = (hmm_regime_series == "Trend").astype(int)
    trend_gate = trend_gate.reindex(close.index, method="ffill").fillna(0).astype(int)
    return (ts1 & trend_gate).astype(int)


# ── Simulación con costos (motor común) ───────────────────────────────────────

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
    Simula trades con posición tracking y costos.
    Retorna (lista de retornos netos por trade, n_trades).
    """
    close   = close.loc[start:end]
    signals = signals_raw.loc[start:end]
    signals = filter_overlapping(signals, hold)

    vals = close.values
    sigs = signals.values
    idx  = close.index
    n    = len(vals)

    returns = []
    for i in range(n):
        if sigs[i] == 0:
            continue
        j = i + hold
        if j >= n:
            break
        ep = vals[i] * (1.0 + cost_per_side)
        xp = vals[j] * (1.0 - cost_per_side)
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


# ── Runner de una hipótesis ───────────────────────────────────────────────────

def run_hypothesis(
    name: str,
    configs: list[dict],
    signal_fn,          # callable(df_asset, config) -> pd.Series  (señal cruda)
    tf: str,
    hold_key: str = "hold",
    oos: bool = False,
) -> list[dict]:
    """
    Evalúa todas las configs de una hipótesis sobre los 3 activos.
    signal_fn: recibe (df_dict, asset, config) donde df_dict contiene
               los DataFrames indexados por asset y timeframe.
    Retorna lista de resultados.
    """
    # Carga datos para el timeframe requerido
    if tf == "multi":
        # TS8 necesita 4h y 1d
        data_4h = load_all_assets("4h")
        data_1d = load_all_assets("1d")
        data = {"4h": data_4h, "1d": data_1d}
    else:
        data = {tf: load_all_assets(tf)}

    is_years  = period_years(IS_START,  IS_END)
    oos_years = period_years(OOS_START, OOS_END)

    results = []
    total_n = 0

    print(f"\n{'='*72}")
    print(f"{name}  |  TF={tf}  |  {len(configs)} configs  |  costos={COST_RT*100:.2f}% RT")
    print(f"{'='*72}")
    col_w = 22
    header = (
        f"  {'cfg':>4}  {'params':<28}  "
        f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  "
        f"{'pass':>5}  gate"
    )
    print(header)
    print("-" * len(header))

    for cfg in configs:
        hold = cfg[hold_key]
        sharpes_is  = {}
        sharpes_oos = {}
        ntrades_is  = {}
        assets_pass = 0

        for asset in ASSETS:
            # Generación de señal
            sig = signal_fn(data, asset, cfg)
            if sig is None or sig.empty:
                sharpes_is[asset] = float("nan")
                ntrades_is[asset] = 0
                continue

            # IS
            primary_close = _get_close(data, tf, asset)
            rets_is, n_is = simulate(primary_close, sig, hold, COST_PER_SIDE, IS_START, IS_END)
            sh_is = compute_sharpe(rets_is, is_years)
            sharpes_is[asset]  = sh_is
            ntrades_is[asset]  = n_is
            total_n += 1

            if not np.isnan(sh_is) and sh_is >= SHARPE_THRESHOLD:
                assets_pass += 1

            # OOS (solo si solicitado)
            if oos:
                rets_oos, _ = simulate(primary_close, sig, hold, COST_PER_SIDE, OOS_START, OOS_END)
                sharpes_oos[asset] = compute_sharpe(rets_oos, oos_years)

        gate = "PASS" if assets_pass >= ASSETS_NEEDED else "FAIL"

        # Formateo
        params_str = _format_params(cfg, hold_key)
        cells = []
        for asset in ASSETS:
            sh = sharpes_is.get(asset, float("nan"))
            n  = ntrades_is.get(asset, 0)
            if np.isnan(sh):
                cells.append(f"{'N/A (T='+str(n)+')':>{col_w}}")
            else:
                flag = "*" if sh >= SHARPE_THRESHOLD else " "
                cells.append(f"{flag}{sh:+.3f}(T={n}):>{col_w}".format(
                    f"{flag}{sh:+.3f}(T={n})"))
        # Re-formateo más limpio
        cells = []
        for asset in ASSETS:
            sh = sharpes_is.get(asset, float("nan"))
            n  = ntrades_is.get(asset, 0)
            if np.isnan(sh):
                cell = f"N/A(T={n})"
            else:
                flag = "*" if sh >= SHARPE_THRESHOLD else " "
                cell = f"{flag}{sh:+.3f}(T={n})"
            cells.append(f"{cell:>{col_w}}")

        cfg_idx = configs.index(cfg) + 1
        print(
            f"  {cfg_idx:>4}  {params_str:<28}  "
            f"{'  '.join(cells)}  {assets_pass:>5}  {gate}"
        )

        results.append({
            "name":       name,
            "config":     cfg,
            "tf":         tf,
            "assets_pass_is": assets_pass,
            "gate_is":    gate,
            "sharpes_is": sharpes_is,
            "ntrades_is": ntrades_is,
            "sharpes_oos": sharpes_oos,
            "n_configs_counted": len(ASSETS),
        })

    return results, total_n


def _get_close(data: dict, tf: str, asset: str) -> pd.Series:
    """Extrae la serie de cierre del diccionario de datos."""
    if tf == "multi":
        return data["4h"][asset]["close"]
    return data[tf][asset]["close"]


def _format_params(cfg: dict, hold_key: str) -> str:
    """Formatea los parámetros de una config como string corto."""
    parts = []
    skip_keys = {hold_key, "name"}
    for k, v in cfg.items():
        if k in skip_keys:
            continue
        if isinstance(v, float):
            parts.append(f"{k}={v:.3f}")
        else:
            parts.append(f"{k}={v}")
    parts.append(f"h={cfg[hold_key]}")
    return " ".join(parts)[:28]


# ── Definición de hipótesis ───────────────────────────────────────────────────

def define_hypotheses() -> list[dict]:
    """
    Retorna lista de dicts con metadatos de cada hipótesis.
    Grilla chica y pre-registrada: 2-3 lookbacks × 1-2 TFs.
    Total de configuraciones: contadas para N de testeo múltiple.
    """

    hypotheses = []

    # ── TS1: Time-Series Momentum ────────────────────────────────────────────
    # Lookback en barras 1h. 3 lookbacks × 2 holds × 1 TF = 6 configs × 3 activos = 18
    ts1_configs = []
    for lb in [24, 48, 96]:           # 1d, 2d, 4d en 1h
        for hold in [24, 48]:         # 1d, 2d de hold
            ts1_configs.append({"lookback": lb, "hold": hold})

    def ts1_signal(data, asset, cfg):
        close = data["1h"][asset]["close"]
        return sig_ts_momentum(close, cfg["lookback"])

    hypotheses.append({
        "id": "TS1", "name": "TS1-Momentum", "tf": "1h",
        "configs": ts1_configs, "signal_fn": ts1_signal,
    })

    # ── TS2: Cruce de medias móviles ─────────────────────────────────────────
    # 3 pares fast/slow × 2 TFs (1h, 4h) = 6 configs
    ts2_configs = [
        {"fast": 20, "slow": 50,  "hold": 24},
        {"fast": 10, "slow": 40,  "hold": 24},
        {"fast": 5,  "slow": 20,  "hold": 12},
    ]

    def ts2_signal_1h(data, asset, cfg):
        close = data["1h"][asset]["close"]
        return sig_ma_cross(close, cfg["fast"], cfg["slow"])

    def ts2_signal_4h(data, asset, cfg):
        close = data["4h"][asset]["close"]
        return sig_ma_cross(close, cfg["fast"], cfg["slow"])

    hypotheses.append({
        "id": "TS2a", "name": "TS2-MACross-1h", "tf": "1h",
        "configs": ts2_configs, "signal_fn": ts2_signal_1h,
    })
    hypotheses.append({
        "id": "TS2b", "name": "TS2-MACross-4h", "tf": "4h",
        "configs": ts2_configs, "signal_fn": ts2_signal_4h,
    })

    # ── TS3: Breakout Donchian ───────────────────────────────────────────────
    # Salida por mínimo de canal. 3 configs × 1 TF (4h) = 3 configs
    ts3_configs = [
        {"lb_entry": 20, "lb_exit": 10, "hold": 5},
        {"lb_entry": 40, "lb_exit": 20, "hold": 10},
        {"lb_entry": 10, "lb_exit": 5,  "hold": 3},
    ]

    def ts3_signal(data, asset, cfg):
        df   = data["4h"][asset]
        close, high, low = df["close"], df["high"], df["low"]
        return sig_donchian(close, high, low, cfg["lb_entry"], cfg["lb_exit"], cfg["hold"])

    hypotheses.append({
        "id": "TS3", "name": "TS3-Donchian-4h", "tf": "4h",
        "configs": ts3_configs, "signal_fn": ts3_signal,
    })

    # ── TS4: Precio vs MA larga ──────────────────────────────────────────────
    # 2 lookbacks × 2 umbral × 1 TF (4h) = 4 configs
    ts4_configs = [
        {"lookback": 50,  "threshold": 0.0,   "hold": 6},
        {"lookback": 100, "threshold": 0.0,   "hold": 6},
        {"lookback": 50,  "threshold": 0.005, "hold": 6},
        {"lookback": 100, "threshold": 0.005, "hold": 6},
    ]

    def ts4_signal(data, asset, cfg):
        close = data["4h"][asset]["close"]
        return sig_price_vs_ma(close, cfg["lookback"], cfg["threshold"])

    hypotheses.append({
        "id": "TS4", "name": "TS4-PriceVsMA-4h", "tf": "4h",
        "configs": ts4_configs, "signal_fn": ts4_signal,
    })

    # ── TS5: Momentum escalado por vol ───────────────────────────────────────
    # 3 lookbacks × 1 TF (4h) = 3 configs
    ts5_configs = [
        {"lookback": 20, "threshold": 0.5, "hold": 5},
        {"lookback": 40, "threshold": 0.5, "hold": 5},
        {"lookback": 60, "threshold": 0.5, "hold": 5},
    ]

    def ts5_signal(data, asset, cfg):
        close = data["4h"][asset]["close"]
        return sig_vol_adjusted_momentum(close, cfg["lookback"], cfg["threshold"])

    hypotheses.append({
        "id": "TS5", "name": "TS5-VolAdjMom-4h", "tf": "4h",
        "configs": ts5_configs, "signal_fn": ts5_signal,
    })

    # ── TS6: Breakout de canal (hold fijo vs salida por mínimo en TS3) ──────
    # 3 configs × 2 TFs (4h, 1d) = 6 configs
    ts6_configs = [
        {"lookback": 20, "hold": 5},
        {"lookback": 40, "hold": 10},
        {"lookback": 10, "hold": 3},
    ]

    def ts6_signal_4h(data, asset, cfg):
        close = data["4h"][asset]["close"]
        return sig_channel_breakout(close, cfg["lookback"])

    def ts6_signal_1d(data, asset, cfg):
        close = data["1d"][asset]["close"]
        return sig_channel_breakout(close, cfg["lookback"])

    hypotheses.append({
        "id": "TS6a", "name": "TS6-ChannelBO-4h", "tf": "4h",
        "configs": ts6_configs, "signal_fn": ts6_signal_4h,
    })
    hypotheses.append({
        "id": "TS6b", "name": "TS6-ChannelBO-1d", "tf": "1d",
        "configs": ts6_configs, "signal_fn": ts6_signal_1d,
    })

    # ── TS7: MACD ────────────────────────────────────────────────────────────
    # 3 pares EMA × 1 TF (4h) = 3 configs
    ts7_configs = [
        {"fast": 12, "slow": 26, "hold": 6},
        {"fast": 8,  "slow": 21, "hold": 6},
        {"fast": 5,  "slow": 13, "hold": 4},
    ]

    def ts7_signal(data, asset, cfg):
        close = data["4h"][asset]["close"]
        return sig_macd(close, cfg["fast"], cfg["slow"])

    hypotheses.append({
        "id": "TS7", "name": "TS7-MACD-4h", "tf": "4h",
        "configs": ts7_configs, "signal_fn": ts7_signal,
    })

    # ── TS8: Multi-timeframe ─────────────────────────────────────────────────
    # 2 configs de lookback 4h × 1 = 2 configs
    ts8_configs = [
        {"lookback_4h": 10, "lookback_1d": 10, "hold": 5},
        {"lookback_4h": 20, "lookback_1d": 20, "hold": 5},
    ]

    def ts8_signal(data, asset, cfg):
        close_4h = data["4h"][asset]["close"]
        close_1d = data["1d"][asset]["close"]
        return sig_multi_tf(close_4h, close_1d, cfg["lookback_4h"], cfg["lookback_1d"])

    hypotheses.append({
        "id": "TS8", "name": "TS8-MultiTF", "tf": "multi",
        "configs": ts8_configs, "signal_fn": ts8_signal,
    })

    # ── TS9: Momentum con skip ───────────────────────────────────────────────
    # 3 lookbacks × 1 skip × 1 TF (4h) = 3 configs
    ts9_configs = [
        {"lookback": 20, "skip": 5,  "hold": 5},
        {"lookback": 40, "skip": 10, "hold": 5},
        {"lookback": 60, "skip": 5,  "hold": 5},
    ]

    def ts9_signal(data, asset, cfg):
        close = data["4h"][asset]["close"]
        return sig_momentum_skip(close, cfg["lookback"], cfg["skip"])

    hypotheses.append({
        "id": "TS9", "name": "TS9-MomSkip-4h", "tf": "4h",
        "configs": ts9_configs, "signal_fn": ts9_signal,
    })

    # ── TS10: TS1 + gate HMM ─────────────────────────────────────────────────
    # 2 configs: con y sin gate (diagnóstico de pulso ADR-056 II.4)
    # HMM no disponible inline → se anota como pendiente y se corre TS1 como proxy
    ts10_configs = [
        {"lookback": 48, "hold": 24, "hmm_gate": False},
        {"lookback": 48, "hold": 24, "hmm_gate": True},  # con gate → pendiente
    ]

    def ts10_signal(data, asset, cfg):
        close = data["1h"][asset]["close"]
        if cfg["hmm_gate"]:
            # Intentar cargar régimen HMM si está disponible
            hmm_series = _try_load_hmm_regime(asset)
            return sig_ts1_with_hmm_gate(close, cfg["lookback"], hmm_series)
        else:
            return sig_ts_momentum(close, cfg["lookback"])

    hypotheses.append({
        "id": "TS10", "name": "TS10-MomHMM-1h", "tf": "1h",
        "configs": ts10_configs, "signal_fn": ts10_signal,
    })

    return hypotheses


def _try_load_hmm_regime(asset: str) -> Optional[pd.Series]:
    """
    Intenta cargar la serie de régimen HMM desde el modelo entrenado.
    Si no está disponible, retorna None (gate desactivado).
    """
    # El HMM vive en Trading.Models/ — se verifica si hay un archivo de regímenes
    models_dir = Path(__file__).parent.parent / "Trading.Models"
    possible_paths = [
        models_dir / f"hmm_regime_{asset.lower()}.parquet",
        models_dir / f"hmm_regimes_{asset.lower()}.csv",
        models_dir / "hmm_regimes.csv",
    ]
    for p in possible_paths:
        if p.exists():
            try:
                if p.suffix == ".parquet":
                    df = pd.read_parquet(p)
                else:
                    df = pd.read_csv(p)
                # Intentar parsear como serie temporal con columna 'regime'
                if "regime" in df.columns and ("bar" in df.columns or "timestamp" in df.columns):
                    time_col = "bar" if "bar" in df.columns else "timestamp"
                    df[time_col] = pd.to_datetime(df[time_col], utc=True)
                    return df.set_index(time_col)["regime"]
            except Exception:
                pass
    return None  # No disponible → TS10 corre sin gate como TS1 pura


# ── Carga de datos con cache ──────────────────────────────────────────────────

_data_cache: dict[str, dict] = {}


def load_all_assets_cached(tf: str) -> dict[str, pd.DataFrame]:
    global _data_cache
    if tf not in _data_cache:
        _data_cache[tf] = load_all_assets(tf)
    return _data_cache[tf]


# ── Resumen final ─────────────────────────────────────────────────────────────

def print_master_table(all_results: list[dict]) -> None:
    """Imprime la tabla maestra de las 10 hipótesis."""
    print(f"\n{'='*90}")
    print("TABLA MAESTRA — S1 Trend Following — IS 2021-2024 — costos 0.12% RT")
    print(f"{'='*90}")
    print(f"  {'ID':<8} {'Nombre':<22} {'TF':<6} {'cfgs':>4} "
          f"  {'BTC Sharpe':>10} {'ETH Sharpe':>10} {'SOL Sharpe':>10} "
          f"  {'trades':>7}  Gate")
    print("-" * 90)

    pass_count = 0
    total_configs = 0

    for r in all_results:
        name     = r["name"]
        cfg      = r["config"]
        tf       = r["tf"]
        sh_is    = r["sharpes_is"]
        n_is     = r["ntrades_is"]
        gate     = r["gate_is"]
        hyp_id   = r.get("id", "?")

        btc_sh = sh_is.get("BTCUSDT", float("nan"))
        eth_sh = sh_is.get("ETHUSDT", float("nan"))
        sol_sh = sh_is.get("SOLUSDT", float("nan"))
        total_trades = sum(n_is.values())
        total_configs += 1

        if gate == "PASS":
            pass_count += 1
            gate_str = "PASS (*)"
        else:
            gate_str = "FAIL"

        def fmt_sh(s):
            if np.isnan(s):
                return "    N/A   "
            mark = "*" if s >= SHARPE_THRESHOLD else " "
            return f"{mark}{s:+.3f}    "

        params = _format_params(cfg, "hold")

        print(
            f"  {hyp_id:<8} {name:<22} {tf:<6} {total_configs:>4}"
            f"  {fmt_sh(btc_sh):>10} {fmt_sh(eth_sh):>10} {fmt_sh(sol_sh):>10}"
            f"  {total_trades:>7}  {gate_str}"
        )

    print(f"\n  Total configs evaluadas (N para testeo múltiple): {total_configs} × 3 activos")
    print(f"  Configs PASS IS: {pass_count}/{total_configs} ({pass_count/max(total_configs,1)*100:.1f}%)")


def print_robustness_analysis(all_results: list[dict]) -> None:
    """Analiza si el mecanismo es robusto (meseta) o puntual (pico)."""
    print(f"\n{'='*72}")
    print("ANÁLISIS DE ROBUSTEZ — ¿meseta o pico?")
    print(f"{'='*72}")

    # Agrupar por hipótesis base (TS1..TS10)
    from collections import defaultdict
    by_hypothesis: dict[str, list] = defaultdict(list)
    for r in all_results:
        hid = r.get("id", "?").rstrip("ab")  # TS2a/TS2b → TS2
        by_hypothesis[hid].append(r)

    hypothesis_pass_rate = {}
    for hid, results in sorted(by_hypothesis.items()):
        total   = len(results)
        passing = sum(1 for r in results if r["gate_is"] == "PASS")
        avg_btc = np.nanmean([r["sharpes_is"].get("BTCUSDT", float("nan")) for r in results])
        avg_eth = np.nanmean([r["sharpes_is"].get("ETHUSDT", float("nan")) for r in results])
        avg_sol = np.nanmean([r["sharpes_is"].get("SOLUSDT", float("nan")) for r in results])
        hypothesis_pass_rate[hid] = passing / total if total > 0 else 0.0

        status = "PASS" if passing > 0 else "FAIL"
        print(f"  {hid:<6}: {passing:>2}/{total} configs pasan  "
              f"Sharpe medio BTC={avg_btc:+.3f} ETH={avg_eth:+.3f} SOL={avg_sol:+.3f}  [{status}]")

    n_hyp_with_signal = sum(1 for v in hypothesis_pass_rate.values() if v > 0)
    total_hyp = len(hypothesis_pass_rate)

    print(f"\n  Hipótesis con alguna config PASS: {n_hyp_with_signal}/{total_hyp}")
    if n_hyp_with_signal >= total_hyp * 0.6:
        print("  => MESETA: el mecanismo de tendencia es robusto (aparece en la mayoría)")
    elif n_hyp_with_signal >= 2:
        print("  => SEÑAL PARCIAL: el mecanismo aparece pero no es universal")
    else:
        print("  => PICO O NADA: edge de tendencia no es reproducible cross-expresión")


# ── Main ──────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description="Capa A - Familia Trend S1 (10 hipótesis)")
    parser.add_argument("--oos",        action="store_true", help="Correr OOS para configs que pasan IS")
    parser.add_argument("--hypothesis", type=str, default=None,
                        help="Solo correr esta hipótesis por ID (ej: TS2a, TS3, TS7)")
    args = parser.parse_args()

    hypotheses = define_hypotheses()

    if args.hypothesis:
        hypotheses = [h for h in hypotheses if h["id"] == args.hypothesis]
        if not hypotheses:
            print(f"[ERROR] Hipótesis '{args.hypothesis}' no encontrada.")
            sys.exit(1)

    # Precargar datos para todos los TFs necesarios
    tfs_needed = set(h["tf"] for h in hypotheses)
    print(f"Precargando datos para TFs: {tfs_needed}")
    for tf in tfs_needed:
        if tf == "multi":
            load_all_assets_cached("4h")
            load_all_assets_cached("1d")
        else:
            load_all_assets_cached(tf)
    print("Datos cargados.")

    all_results = []
    total_N = 0

    for hyp in hypotheses:
        tf = hyp["tf"]
        if tf == "multi":
            data = {"4h": _data_cache["4h"], "1d": _data_cache["1d"]}
        elif tf in _data_cache:
            data = {tf: _data_cache[tf]}
        else:
            data = {tf: load_all_assets_cached(tf)}

        # Adaptamos run_hypothesis para usar el cache
        results, n = _run_hypothesis_inner(
            hyp["id"], hyp["name"], hyp["configs"], hyp["signal_fn"],
            tf, data, oos=args.oos
        )
        all_results.extend(results)
        total_N += n

    print_master_table(all_results)
    print_robustness_analysis(all_results)

    # Veredicto OOS si aplica
    passing = [r for r in all_results if r["gate_is"] == "PASS"]
    if args.oos and passing:
        print(f"\n{'='*72}")
        print("OOS (2025-01-01 → 2026-06-09) — solo configs que pasan IS")
        print(f"{'='*72}")
        for r in passing:
            name = r["name"]
            sh_oos = r["sharpes_oos"]
            btc_oos = sh_oos.get("BTCUSDT", float("nan"))
            eth_oos = sh_oos.get("ETHUSDT", float("nan"))
            sol_oos = sh_oos.get("SOLUSDT", float("nan"))
            params = _format_params(r["config"], "hold")
            print(f"  {name:<22} {params:<28}  "
                  f"BTC={btc_oos:+.3f}  ETH={eth_oos:+.3f}  SOL={sol_oos:+.3f}")

    print(f"\n  Total de evaluaciones individuales (N): {total_N} × activos")
    print(f"  Costos aplicados: {COST_RT*100:.3f}% RT ({FEE_PER_SIDE*100:.3f}% fee + {SLIPPAGE_PER_SIDE*100:.3f}% slippage por lado)")
    print(f"  IS: {IS_START} → {IS_END}  |  Long-only  |  ADR-056 / ROADMAP-STRATEGIES §IV")


def _run_hypothesis_inner(
    hyp_id: str,
    name: str,
    configs: list[dict],
    signal_fn,
    tf: str,
    data: dict,
    oos: bool,
) -> tuple[list[dict], int]:
    """Runner interno que usa el cache de datos."""
    is_years  = period_years(IS_START,  IS_END)
    oos_years = period_years(OOS_START, OOS_END)

    results = []
    total_n = 0

    print(f"\n{'='*72}")
    print(f"{hyp_id} — {name}  |  TF={tf}  |  {len(configs)} configs  |  costos={COST_RT*100:.2f}% RT")
    print(f"{'='*72}")
    col_w = 24
    header = (
        f"  {'#':>3}  {'params':<30}  "
        f"{'BTC':>{col_w}}  {'ETH':>{col_w}}  {'SOL':>{col_w}}  "
        f"{'pass':>5}  gate"
    )
    print(header)
    print("-" * len(header))

    for i, cfg in enumerate(configs, 1):
        hold = cfg.get("hold", 6)
        sharpes_is  = {}
        sharpes_oos = {}
        ntrades_is  = {}
        assets_pass = 0

        for asset in ASSETS:
            try:
                sig = signal_fn(data, asset, cfg)
            except Exception as e:
                print(f"  [ERROR señal {hyp_id} {asset}]: {e}")
                sharpes_is[asset] = float("nan")
                ntrades_is[asset] = 0
                continue

            if sig is None or len(sig) == 0:
                sharpes_is[asset] = float("nan")
                ntrades_is[asset] = 0
                continue

            # Obtener close del TF primario
            if tf == "multi":
                primary_close = data["4h"][asset]["close"]
            else:
                primary_close = data[tf][asset]["close"]

            # IS
            try:
                rets_is, n_is = simulate(primary_close, sig, hold, COST_PER_SIDE, IS_START, IS_END)
                sh_is = compute_sharpe(rets_is, is_years)
            except Exception as e:
                print(f"  [ERROR IS {hyp_id} {asset}]: {e}")
                sh_is, n_is = float("nan"), 0

            sharpes_is[asset] = sh_is
            ntrades_is[asset] = n_is
            total_n += 1

            if not np.isnan(sh_is) and sh_is >= SHARPE_THRESHOLD:
                assets_pass += 1

            # OOS
            if oos:
                try:
                    rets_oos, _ = simulate(primary_close, sig, hold, COST_PER_SIDE, OOS_START, OOS_END)
                    sharpes_oos[asset] = compute_sharpe(rets_oos, oos_years)
                except Exception:
                    sharpes_oos[asset] = float("nan")

        gate = "PASS" if assets_pass >= ASSETS_NEEDED else "FAIL"

        params_str = _format_params(cfg, "hold")
        cells = []
        for asset in ASSETS:
            sh = sharpes_is.get(asset, float("nan"))
            n  = ntrades_is.get(asset, 0)
            if np.isnan(sh):
                cell = f"N/A(T={n})"
            else:
                flag = "*" if sh >= SHARPE_THRESHOLD else " "
                cell = f"{flag}{sh:+.3f}(T={n})"
            cells.append(f"{cell:>{col_w}}")

        gate_mark = " (***PASS***)" if gate == "PASS" else ""
        print(
            f"  {i:>3}  {params_str:<30}  "
            f"{'  '.join(cells)}  {assets_pass:>5}  {gate}{gate_mark}"
        )

        results.append({
            "id":         hyp_id,
            "name":       name,
            "config":     cfg,
            "tf":         tf,
            "assets_pass_is": assets_pass,
            "gate_is":    gate,
            "sharpes_is": sharpes_is,
            "ntrades_is": ntrades_is,
            "sharpes_oos": sharpes_oos,
        })

    n_pass = sum(1 for r in results if r["gate_is"] == "PASS")
    print(f"\n  {hyp_id}: {n_pass}/{len(configs)} configs PASS")

    return results, total_n


if __name__ == "__main__":
    main()
