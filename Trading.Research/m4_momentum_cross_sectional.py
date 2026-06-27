#!/usr/bin/env python3
"""
M4 Gate — Momentum Cross-Sectional + Time-Series (Capa A)
=========================================================
Universo: perps USDT-M de Binance, datos 1m resampleados a 1d.
Fuente: klines 1m locales en F:\\Mis Documentos\\Cripto monedas\\Trading\\Data\\Velas\\1m\\

Anti-survivorship:
  - Usamos TODOS los símbolos disponibles localmente (incluyendo delisted/muertos).
  - Los datos 1m en disco incluyen monedas deslistadas (LUNA2USDT, USTCUSDT, ETHWUSDT, etc.)
    hasta la fecha en que dejan de tener datos.
  - Sesgo residual documentado al final.

Modelo de costos ESCALONADO:
  - Majors (BTC/ETH/BNB/SOL/XRP/ADA/DOGE/BNBUSDT): 0.12% RT
  - Mid/small-caps: 0.22% RT

Grilla pre-registrada (8 configs):
  L ∈ {30d, 90d} × H ∈ {7d, 30d} × selección ∈ {top-decil, top-5}
  Brazo LONG-ONLY (principal) + L/S diagnóstico.

Causalidad:
  - Ranking en t usa retornos HASTA t (inclusive, retorno del período [t-L, t]).
  - Cartera se forma al cierre de t → mantenida H días.
  - Universe eligibility en t usa ADV-30d hasta t.
  - NO hay lookahead: retorno del período hold [t+1, t+H] es la variable objetivo.

IS: 2021-01-01 → 2024-12-31
OOS: 2025-01-01 → 2026-06-26

Gate:
  Sharpe IS >= 0.5 neto de costos escalonados
  MESETA sobre L/H (no un solo passer)
  Edge NO concentrado en <3 nombres
"""

import sys
import zipfile
import warnings
from pathlib import Path
from datetime import date, timedelta

import numpy as np
import pandas as pd

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")
warnings.filterwarnings("ignore")

# ── Configuración ──────────────────────────────────────────────────────────────

KLINES_1M_DIR = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\1m")
DATA_DIR_1D   = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\1d_momentum")

IS_START  = "2021-01-01"
IS_END    = "2024-12-31"
OOS_START = "2025-01-01"
OOS_END   = "2026-06-26"

# Símbolos a excluir (duplicados o problemas de datos)
EXCLUDE = {"1000BONKUSDT-copia"}

# Majors (costo RT 0.12%), resto mid/small (0.22%)
MAJORS = {
    "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "XRPUSDT",
    "ADAUSDT", "DOGEUSDT", "AVAXUSDT", "LINKUSDT", "LTCUSDT",
    "BCHUSDT", "ETCUSDT",
}
COST_RT_MAJOR    = 0.0012  # 0.12% round-trip
COST_RT_MIDSMALL = 0.0022  # 0.22% round-trip

# ADV mínimo: $5M diarios promedio 30d para entrar al universo elegible
ADV_MIN_USD = 5_000_000

# Grilla pre-registrada
LOOKBACKS    = [30, 90]    # días
HOLD_PERIODS = [7, 30]     # días
SELECTIONS   = ["top_decil", "top_5"]

# Columnas klines Binance (sin header para datos pre-2022)
KLINE_COLS = [
    "open_time", "open", "high", "low", "close", "volume",
    "close_time", "quote_volume", "n_trades",
    "taker_buy_vol", "taker_buy_qvol", "ignore"
]


# ── Lectura de datos ───────────────────────────────────────────────────────────

def _read_zip_1m(path: Path) -> pd.DataFrame | None:
    """Lee un ZIP de klines 1m Binance. Retorna DataFrame o None si error."""
    try:
        with zipfile.ZipFile(path) as zf:
            fname = zf.namelist()[0]
            with zf.open(fname) as f:
                first_line = f.readline().decode("utf-8", errors="ignore")
            has_header = not first_line.split(",")[0].strip().lstrip("-").isdigit()

            with zf.open(fname) as f:
                df = pd.read_csv(
                    f,
                    header=0 if has_header else None,
                    names=None if has_header else KLINE_COLS,
                    usecols=[0, 1, 2, 3, 4, 5, 7],  # open_time,O,H,L,C,vol,quote_vol
                    dtype={0: "int64", 1: "float64", 2: "float64",
                           3: "float64", 4: "float64", 5: "float64", 7: "float64"},
                )
            # Normalizar nombres si tenía header
            if has_header:
                df.columns = [c.lower().replace(" ", "_") for c in df.columns]
                col_map = {}
                for c in df.columns:
                    if "open_time" in c or c == "open time":
                        col_map[c] = "open_time"
                    elif c == "open":
                        col_map[c] = "open"
                    elif c == "high":
                        col_map[c] = "high"
                    elif c == "low":
                        col_map[c] = "low"
                    elif c == "close":
                        col_map[c] = "close"
                    elif c == "volume":
                        col_map[c] = "volume"
                    elif "quote" in c and "volume" in c:
                        col_map[c] = "quote_volume"
                df = df.rename(columns=col_map)
                # Seleccionar sólo las que necesitamos
                needed = ["open_time", "open", "high", "low", "close", "volume", "quote_volume"]
                df = df[[c for c in needed if c in df.columns]]
            else:
                df.columns = ["open_time", "open", "high", "low", "close", "volume", "quote_volume"]

        return df
    except Exception as e:
        return None


def load_symbol_1m_to_daily(symbol: str) -> pd.DataFrame | None:
    """
    Lee todos los ZIPs mensuales de klines 1m de un símbolo y los agrega a 1d (UTC).
    Retorna DataFrame con índice DatetimeIndex (fecha UTC) y cols: open,high,low,close,volume,quote_volume.
    """
    sym_dir = KLINES_1M_DIR / symbol
    if not sym_dir.exists():
        return None

    zips = sorted(sym_dir.glob("*.zip"))
    if not zips:
        return None

    frames = []
    for zp in zips:
        df = _read_zip_1m(zp)
        if df is not None and not df.empty:
            frames.append(df)

    if not frames:
        return None

    raw = pd.concat(frames, ignore_index=True)
    raw = raw.drop_duplicates(subset=["open_time"]).sort_values("open_time")

    # Convertir timestamp ms a datetime UTC
    raw["ts"] = pd.to_datetime(raw["open_time"], unit="ms", utc=True)

    # Resamplear a 1d
    raw = raw.set_index("ts")
    daily = raw.resample("1D").agg(
        open=("open", "first"),
        high=("high", "max"),
        low=("low", "min"),
        close=("close", "last"),
        volume=("volume", "sum"),
        quote_volume=("quote_volume", "sum"),
    ).dropna(subset=["close"])

    return daily


# ── Construcción del universo + caché 1d ──────────────────────────────────────

def build_universe_cache(symbols: list[str], force_rebuild: bool = False) -> dict[str, pd.DataFrame]:
    """
    Carga o genera los parquets 1d para todos los símbolos.
    Retorna dict symbol -> DataFrame(date, open, high, low, close, volume, quote_volume).
    """
    DATA_DIR_1D.mkdir(parents=True, exist_ok=True)
    universe = {}
    failed = []

    print(f"\nCargando datos 1d para {len(symbols)} símbolos...")
    for i, sym in enumerate(symbols, 1):
        cache_path = DATA_DIR_1D / f"{sym}_1d.parquet"
        if cache_path.exists() and not force_rebuild:
            try:
                df = pd.read_parquet(cache_path)
                df.index = pd.to_datetime(df.index, utc=True)
                universe[sym] = df
                if i % 20 == 0:
                    print(f"  {i}/{len(symbols)} cargados desde caché...")
                continue
            except Exception:
                pass

        # Construir desde 1m
        df = load_symbol_1m_to_daily(sym)
        if df is None or len(df) < 30:
            failed.append(sym)
            continue

        df.to_parquet(cache_path)
        universe[sym] = df

        if i % 20 == 0:
            print(f"  {i}/{len(symbols)} procesados (built from 1m)...")

    print(f"  OK: {len(universe)} símbolos | Fallidos: {len(failed)}: {failed[:10]}")
    return universe


# ── Construcción del panel de retornos diarios ────────────────────────────────

def build_returns_panel(universe: dict[str, pd.DataFrame]) -> pd.DataFrame:
    """
    Construye panel de retornos diarios y ADV (quote_volume en USD).
    Retorna: (returns_df, adv_df) con DatetimeIndex (UTC, freq=1D) y columnas=símbolos.
    """
    closes = {}
    qvols  = {}
    for sym, df in universe.items():
        closes[sym] = df["close"]
        qvols[sym]  = df["quote_volume"]

    close_panel = pd.DataFrame(closes).sort_index()
    qvol_panel  = pd.DataFrame(qvols).sort_index()

    # Retorno simple diario
    returns_panel = close_panel.pct_change()

    return close_panel, returns_panel, qvol_panel


# ── Backtest momentum cross-sectional ─────────────────────────────────────────

def momentum_backtest(
    close_panel: pd.DataFrame,
    returns_panel: pd.DataFrame,
    qvol_panel: pd.DataFrame,
    lookback: int,
    hold: int,
    selection: str,  # "top_decil" o "top_5"
    start: str,
    end: str,
    long_short: bool = False,
) -> dict:
    """
    Backtest de momentum cross-sectional con causalidad estricta.

    En cada fecha de rebalanceo t (cada `hold` días):
      1. Universo elegible: símbolos con ADV-30d > ADV_MIN_USD en t.
      2. Momentum signal: retorno acumulado de [t-lookback, t] para cada elegible.
      3. Ranking: top por momentum signal.
      4. Portfolio: igualmente ponderado, hold H días.
      5. Retorno del portfolio: [t+1, t+H] (cierra en t+H).
      6. Costos aplicados al entrar y salir (escalonado por símbolo).

    Retorna dict con métricas.
    """
    # Recortar al período + warmup
    start_dt = pd.Timestamp(start, tz="UTC")
    end_dt   = pd.Timestamp(end,   tz="UTC")
    warmup_start = start_dt - pd.Timedelta(days=lookback + 40)

    # Filtrar panel
    cp = close_panel.loc[warmup_start:end_dt].copy()
    rp = returns_panel.loc[warmup_start:end_dt].copy()
    qp = qvol_panel.loc[warmup_start:end_dt].copy()

    dates = cp.loc[start_dt:end_dt].index
    if len(dates) == 0:
        return {"error": "sin fechas en período"}

    # Fechas de rebalanceo (cada H días)
    rebal_dates = dates[::hold]

    portfolio_returns = []
    rebal_records = []
    trade_log = {}  # sym -> list of returns

    for rebal_idx, rebal_t in enumerate(rebal_dates):
        # Buscar fecha de salida
        exit_idx_in_dates = dates.get_loc(rebal_t) + hold
        if exit_idx_in_dates >= len(dates):
            break
        exit_t = dates[exit_idx_in_dates]

        # ── Universo elegible en rebal_t ──────────────────────────────────────
        # ADV-30d hasta rebal_t (point-in-time)
        adv_window = qp.loc[:rebal_t].tail(30)
        adv_30d = adv_window.mean()

        # Solo símbolos con datos hasta rebal_t y ADV suficiente
        eligible_mask = (adv_30d >= ADV_MIN_USD) & (adv_30d.notna())
        eligible_syms = adv_30d[eligible_mask].index.tolist()

        if len(eligible_syms) < 5:
            continue

        # ── Momentum signal en rebal_t ────────────────────────────────────────
        # Retorno acumulado de [rebal_t - lookback, rebal_t]
        mom_window = cp.loc[:rebal_t].tail(lookback + 1)
        if len(mom_window) < lookback + 1:
            continue

        price_start = mom_window.iloc[0]
        price_end   = mom_window.iloc[-1]
        mom_signal  = (price_end / price_start - 1.0).reindex(eligible_syms)
        mom_signal  = mom_signal.dropna()

        if len(mom_signal) < 5:
            continue

        # ── Selección ─────────────────────────────────────────────────────────
        n_eligible = len(mom_signal)
        if selection == "top_decil":
            n_select = max(1, n_eligible // 10)
        else:  # top_5
            n_select = min(5, n_eligible)

        top_syms  = mom_signal.nlargest(n_select).index.tolist()
        bot_syms  = mom_signal.nsmallest(n_select).index.tolist() if long_short else []

        # ── Retorno del período hold [rebal_t+1, exit_t] ─────────────────────
        # Usamos retorno de close(rebal_t) a close(exit_t)
        # Esto equivale a: entrar al cierre de rebal_t, salir al cierre de exit_t
        hold_rets_long  = []
        hold_rets_short = []

        for sym in top_syms:
            if sym not in cp.columns:
                continue
            p_entry = cp.loc[rebal_t, sym] if rebal_t in cp.index else np.nan
            p_exit  = cp.loc[exit_t,   sym] if exit_t  in cp.index else np.nan
            if np.isnan(p_entry) or np.isnan(p_exit) or p_entry <= 0:
                continue

            gross_ret = p_exit / p_entry - 1.0
            cost_rt   = COST_RT_MAJOR if sym in MAJORS else COST_RT_MIDSMALL
            net_ret   = gross_ret - cost_rt  # Long: gross - costo RT

            hold_rets_long.append(net_ret)
            if sym not in trade_log:
                trade_log[sym] = []
            trade_log[sym].append(net_ret)

        for sym in bot_syms:
            if sym not in cp.columns:
                continue
            p_entry = cp.loc[rebal_t, sym] if rebal_t in cp.index else np.nan
            p_exit  = cp.loc[exit_t,   sym] if exit_t  in cp.index else np.nan
            if np.isnan(p_entry) or np.isnan(p_exit) or p_entry <= 0:
                continue

            gross_ret = p_exit / p_entry - 1.0
            cost_rt   = COST_RT_MAJOR if sym in MAJORS else COST_RT_MIDSMALL
            # Short: -gross - costo RT
            net_ret   = -gross_ret - cost_rt

            hold_rets_short.append(net_ret)

        # Portfolio: igualmente ponderado
        all_rets = hold_rets_long + hold_rets_short
        if not all_rets:
            continue

        port_ret = np.mean(all_rets)
        portfolio_returns.append({
            "date": rebal_t,
            "exit": exit_t,
            "n_long": len(top_syms),
            "n_short": len(bot_syms),
            "port_ret": port_ret,
            "n_eligible": n_eligible,
        })

        rebal_records.append({
            "date": rebal_t,
            "top_syms": top_syms,
            "n_eligible": n_eligible,
        })

    if len(portfolio_returns) < 5:
        return {
            "sharpe": np.nan, "n_trades": 0, "mean_ret": np.nan,
            "turnover": np.nan, "trade_log": {}
        }

    rets_df = pd.DataFrame(portfolio_returns)
    r = rets_df["port_ret"].values
    n = len(r)

    mean_r = float(np.mean(r))
    std_r  = float(np.std(r, ddof=1))

    if std_r == 0:
        sharpe = np.nan
    else:
        # Frecuencia de rebalanceo: hold/365.25 años por período
        periods_per_year = 365.25 / hold
        sharpe = mean_r / std_r * np.sqrt(periods_per_year)

    # Turnover: número de cambios en top_syms / período
    avg_turnover = np.nan
    if len(rebal_records) >= 2:
        turnovers = []
        for i in range(1, len(rebal_records)):
            prev = set(rebal_records[i-1]["top_syms"])
            curr = set(rebal_records[i]["top_syms"])
            if len(prev | curr) > 0:
                turnovers.append(len(prev ^ curr) / len(prev | curr))
        avg_turnover = float(np.mean(turnovers)) if turnovers else np.nan

    return {
        "sharpe": float(sharpe),
        "n_trades": n,
        "mean_ret": float(mean_r),
        "std_ret": float(std_r),
        "win_rate": float((r > 0).mean()),
        "turnover": float(avg_turnover),
        "trade_log": trade_log,
        "n_eligible_avg": float(rets_df["n_eligible"].mean()),
    }


# ── Time-Series momentum (diagnóstico) ────────────────────────────────────────

def timeseries_momentum_backtest(
    close_panel: pd.DataFrame,
    returns_panel: pd.DataFrame,
    qvol_panel: pd.DataFrame,
    lookback: int,
    hold: int,
    start: str,
    end: str,
) -> dict:
    """
    Brazo time-series: para cada activo, long si momentum(L) > 0, hold H días.
    Igualmente ponderado sobre los activos con señal positiva.
    """
    start_dt = pd.Timestamp(start, tz="UTC")
    end_dt   = pd.Timestamp(end,   tz="UTC")
    warmup_start = start_dt - pd.Timedelta(days=lookback + 40)

    cp = close_panel.loc[warmup_start:end_dt].copy()
    qp = qvol_panel.loc[warmup_start:end_dt].copy()

    dates = cp.loc[start_dt:end_dt].index
    if len(dates) == 0:
        return {"error": "sin fechas"}

    rebal_dates = dates[::hold]
    portfolio_returns = []

    for rebal_t in rebal_dates:
        exit_idx = dates.get_loc(rebal_t) + hold
        if exit_idx >= len(dates):
            break
        exit_t = dates[exit_idx]

        adv_30d = qp.loc[:rebal_t].tail(30).mean()
        eligible_mask = (adv_30d >= ADV_MIN_USD) & adv_30d.notna()
        eligible_syms = adv_30d[eligible_mask].index.tolist()

        mom_window = cp.loc[:rebal_t].tail(lookback + 1)
        if len(mom_window) < lookback + 1:
            continue

        price_start = mom_window.iloc[0]
        price_end   = mom_window.iloc[-1]
        mom_signal  = (price_end / price_start - 1.0).reindex(eligible_syms).dropna()

        # Long solo si momentum propio > 0
        long_syms = mom_signal[mom_signal > 0].index.tolist()

        hold_rets = []
        for sym in long_syms:
            p_entry = cp.loc[rebal_t, sym] if rebal_t in cp.index else np.nan
            p_exit  = cp.loc[exit_t,   sym] if exit_t  in cp.index else np.nan
            if np.isnan(p_entry) or np.isnan(p_exit) or p_entry <= 0:
                continue
            gross_ret = p_exit / p_entry - 1.0
            cost_rt   = COST_RT_MAJOR if sym in MAJORS else COST_RT_MIDSMALL
            hold_rets.append(gross_ret - cost_rt)

        if hold_rets:
            portfolio_returns.append(np.mean(hold_rets))

    if len(portfolio_returns) < 5:
        return {"sharpe": np.nan, "n_trades": 0}

    r = np.array(portfolio_returns)
    std_r = np.std(r, ddof=1)
    periods_per_year = 365.25 / hold
    sharpe = float(np.mean(r) / std_r * np.sqrt(periods_per_year)) if std_r > 0 else np.nan

    return {"sharpe": sharpe, "n_trades": len(r)}


# ── Análisis de concentración ──────────────────────────────────────────────────

def analyze_concentration(trade_log: dict) -> dict:
    """
    ¿El edge está concentrado en <3 nombres?
    trade_log: dict sym -> list of returns
    """
    if not trade_log:
        return {"top_3_share": np.nan, "concentrated": True}

    sym_pnl = {sym: np.sum(rets) for sym, rets in trade_log.items() if rets}
    if not sym_pnl:
        return {"top_3_share": np.nan, "concentrated": True}

    total_pnl = sum(max(v, 0) for v in sym_pnl.values())
    if total_pnl <= 0:
        return {"top_3_share": np.nan, "concentrated": True, "n_contributors": 0}

    sorted_syms = sorted(sym_pnl.items(), key=lambda x: x[1], reverse=True)
    top_3_pnl = sum(max(v, 0) for _, v in sorted_syms[:3])
    top_3_share = top_3_pnl / total_pnl

    return {
        "top_3_share": top_3_share,
        "concentrated": top_3_share > 0.6,
        "n_contributors": len([v for v in sym_pnl.values() if v > 0]),
        "top_syms": [s for s, _ in sorted_syms[:5]],
    }


# ── ADV stats ─────────────────────────────────────────────────────────────────

def compute_adv_stats(qvol_panel: pd.DataFrame, start: str, end: str) -> dict:
    """Estadísticas de ADV-30d promedio durante el IS."""
    start_dt = pd.Timestamp(start, tz="UTC")
    end_dt   = pd.Timestamp(end,   tz="UTC")
    qp = qvol_panel.loc[start_dt:end_dt]

    # ADV promedio por símbolo en IS
    adv_by_sym = qp.mean().sort_values(ascending=False)
    total_syms = len(adv_by_sym.dropna())
    above_5m   = (adv_by_sym >= ADV_MIN_USD).sum()
    above_50m  = (adv_by_sym >= 50_000_000).sum()
    above_500m = (adv_by_sym >= 500_000_000).sum()

    return {
        "total_syms": total_syms,
        "above_5m":   int(above_5m),
        "above_50m":  int(above_50m),
        "above_500m": int(above_500m),
        "median_adv": float(adv_by_sym.median()),
        "mean_adv":   float(adv_by_sym.mean()),
        "top_10_syms": adv_by_sym.head(10).to_dict(),
    }


# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    print("=" * 72)
    print("M4 MOMENTUM CROSS-SECTIONAL — Capa A")
    print(f"IS: {IS_START} → {IS_END}  |  OOS: {OOS_START} → {OOS_END}")
    print("=" * 72)

    # ── Parte 1: Construcción del universo ────────────────────────────────────
    print("\n[1/4] Enumerando símbolos disponibles...")
    all_sym_dirs = [d for d in KLINES_1M_DIR.iterdir()
                    if d.is_dir() and d.name not in EXCLUDE]
    symbols = sorted([d.name for d in all_sym_dirs])
    print(f"  Símbolos en disco: {len(symbols)}")

    # Identificar delisted conocidos
    KNOWN_DELISTED = {
        "LUNA2USDT",   # LUNA 2.0 — crash mayo 2022
        "USTCUSDT",    # UST / Terra — crash mayo 2022
        "ETHWUSDT",    # ETH PoW — fork de Merge
        "1000LUNCUSDT", # LUNC
    }
    delisted_present = [s for s in symbols if s in KNOWN_DELISTED]
    print(f"  Deslistados conocidos presentes: {delisted_present}")

    # ── Parte 2: Carga y caché a 1d ──────────────────────────────────────────
    print("\n[2/4] Cargando/construyendo datos 1d (puede tardar)...")
    universe = build_universe_cache(symbols)
    print(f"  Universo final: {len(universe)} símbolos con datos válidos")

    # Panel
    close_panel, returns_panel, qvol_panel = build_returns_panel(universe)
    print(f"  Panel: {len(close_panel)} días × {len(close_panel.columns)} símbolos")
    print(f"  Rango: {close_panel.index[0].date()} → {close_panel.index[-1].date()}")

    # ── Parte 3: ADV stats ────────────────────────────────────────────────────
    print("\n[3/4] Estadísticas ADV...")
    adv_stats = compute_adv_stats(qvol_panel, IS_START, IS_END)
    print(f"  Total símbolos con datos IS: {adv_stats['total_syms']}")
    print(f"  ADV > $5M:   {adv_stats['above_5m']} símbolos")
    print(f"  ADV > $50M:  {adv_stats['above_50m']} símbolos")
    print(f"  ADV > $500M: {adv_stats['above_500m']} símbolos")
    print(f"  Mediana ADV IS: ${adv_stats['median_adv']/1e6:.1f}M")
    print(f"  Media ADV IS:   ${adv_stats['mean_adv']/1e6:.1f}M")
    print(f"  Top 10 por ADV:")
    for sym, adv in adv_stats["top_10_syms"].items():
        print(f"    {sym:20s} ${adv/1e6:>8.1f}M/día")

    # ── Parte 4: Test de momentum — grilla 8 configs ──────────────────────────
    print("\n[4/4] Ejecutando grilla de momentum (8 configs IS + OOS)...")
    print("=" * 72)

    results_grid = []
    ts_results   = []

    for lookback in LOOKBACKS:
        for hold in HOLD_PERIODS:
            for selection in SELECTIONS:
                label = f"L={lookback}d H={hold}d sel={selection}"

                # IS
                r_is = momentum_backtest(
                    close_panel, returns_panel, qvol_panel,
                    lookback, hold, selection,
                    IS_START, IS_END, long_short=False
                )
                # OOS
                r_oos = momentum_backtest(
                    close_panel, returns_panel, qvol_panel,
                    lookback, hold, selection,
                    OOS_START, OOS_END, long_short=False
                )
                # Concentración IS
                conc = analyze_concentration(r_is.get("trade_log", {}))

                results_grid.append({
                    "config": label,
                    "lookback": lookback,
                    "hold": hold,
                    "selection": selection,
                    "sharpe_is":  r_is.get("sharpe", np.nan),
                    "sharpe_oos": r_oos.get("sharpe", np.nan),
                    "n_trades_is":  r_is.get("n_trades", 0),
                    "n_trades_oos": r_oos.get("n_trades", 0),
                    "mean_ret_is": r_is.get("mean_ret", np.nan),
                    "turnover_is": r_is.get("turnover", np.nan),
                    "n_eligible_avg": r_is.get("n_eligible_avg", np.nan),
                    "top_3_share": conc.get("top_3_share", np.nan),
                    "concentrated": conc.get("concentrated", True),
                    "n_contributors": conc.get("n_contributors", 0),
                    "top_syms": conc.get("top_syms", []),
                })

        # Time-series diagnóstico (no cuenta para el gate)
        for hold in HOLD_PERIODS:
            ts_is = timeseries_momentum_backtest(
                close_panel, returns_panel, qvol_panel,
                lookback, hold, IS_START, IS_END
            )
            ts_oos = timeseries_momentum_backtest(
                close_panel, returns_panel, qvol_panel,
                lookback, hold, OOS_START, OOS_END
            )
            ts_results.append({
                "lookback": lookback, "hold": hold,
                "sharpe_is": ts_is.get("sharpe", np.nan),
                "sharpe_oos": ts_oos.get("sharpe", np.nan),
                "n_is": ts_is.get("n_trades", 0),
                "n_oos": ts_oos.get("n_trades", 0),
            })

    # ── L/S diagnóstico para cada config ──────────────────────────────────────
    ls_results = []
    for lookback in LOOKBACKS:
        for hold in HOLD_PERIODS:
            for selection in SELECTIONS:
                r_ls_is = momentum_backtest(
                    close_panel, returns_panel, qvol_panel,
                    lookback, hold, selection,
                    IS_START, IS_END, long_short=True
                )
                r_ls_oos = momentum_backtest(
                    close_panel, returns_panel, qvol_panel,
                    lookback, hold, selection,
                    OOS_START, OOS_END, long_short=True
                )
                ls_results.append({
                    "config": f"L={lookback}d H={hold}d sel={selection} [L/S]",
                    "sharpe_is":  r_ls_is.get("sharpe", np.nan),
                    "sharpe_oos": r_ls_oos.get("sharpe", np.nan),
                    "n_is":  r_ls_is.get("n_trades", 0),
                    "n_oos": r_ls_oos.get("n_trades", 0),
                })

    # ── Resultados ────────────────────────────────────────────────────────────
    rdf = pd.DataFrame(results_grid)
    print("\n" + "=" * 72)
    print("RESULTADOS GRILLA LONG-ONLY (8 configs pre-registradas)")
    print("=" * 72)
    pd.set_option("display.float_format", "{:.3f}".format)
    pd.set_option("display.max_columns", 20)
    pd.set_option("display.width", 120)

    display_cols = ["config", "sharpe_is", "sharpe_oos", "n_trades_is", "n_trades_oos",
                    "mean_ret_is", "turnover_is", "n_eligible_avg", "top_3_share", "concentrated"]
    print(rdf[display_cols].to_string(index=False))

    # Gate IS
    passers_is = rdf[rdf["sharpe_is"] >= 0.5]
    print(f"\n  Configs con Sharpe IS >= 0.5: {len(passers_is)}/8")

    # Meseta
    has_meseta = (
        rdf.groupby("lookback")["sharpe_is"].apply(lambda x: (x >= 0.5).any()).all()
        and
        rdf.groupby("hold")["sharpe_is"].apply(lambda x: (x >= 0.5).any()).all()
    )
    print(f"  Meseta (pass en múltiples L/H): {'SI' if has_meseta else 'NO'}")

    # Concentración
    not_concentrated = rdf[~rdf["concentrated"]].shape[0]
    print(f"  Configs NO concentradas en <3 nombres: {not_concentrated}/8")

    # ── Gate final ────────────────────────────────────────────────────────────
    gate_pass = (
        len(passers_is) >= 4 and  # mayoría de configs pasan
        has_meseta and
        not_concentrated >= 4
    )

    print("\n" + "=" * 72)
    print(f"GATE IS: {'PASS' if gate_pass else 'FAIL'}")
    print("=" * 72)

    # ── Time-series diagnóstico ───────────────────────────────────────────────
    print("\nBRAZO TIME-SERIES (diagnóstico, no cuenta para gate):")
    tsdf = pd.DataFrame(ts_results)
    print(tsdf.to_string(index=False))

    # ── L/S diagnóstico ───────────────────────────────────────────────────────
    print("\nBRAZO L/S (diagnóstico):")
    lsdf = pd.DataFrame(ls_results)
    print(lsdf[["config", "sharpe_is", "sharpe_oos", "n_is", "n_oos"]].to_string(index=False))

    # ── Cobertura del universo ────────────────────────────────────────────────
    print("\n" + "=" * 72)
    print("COBERTURA Y BIAS DE SUPERVIVENCIA")
    print("=" * 72)
    print(f"  Total símbolos procesados: {len(universe)}")
    print(f"  Deslistados conocidos incluidos: {[s for s in KNOWN_DELISTED if s in universe]}")
    print(f"  Rango de datos: {close_panel.index[0].date()} → {close_panel.index[-1].date()}")

    # Cobertura por símbolo
    coverages = {}
    for sym, df in universe.items():
        first = df.index[0].date()
        last  = df.index[-1].date()
        n_days = len(df)
        coverages[sym] = {"first": first, "last": last, "n_days": n_days}

    cov_df = pd.DataFrame(coverages).T.sort_values("first")
    print(f"\n  Cobertura de inicio:")
    print(f"  Antes de 2020-01: {(pd.to_datetime(cov_df['first']) < '2020-01-01').sum()} símbolos")
    print(f"  2020 - 2021:      {((pd.to_datetime(cov_df['first']) >= '2020-01-01') & (pd.to_datetime(cov_df['first']) < '2022-01-01')).sum()} símbolos")
    print(f"  2022 - 2024:      {((pd.to_datetime(cov_df['first']) >= '2022-01-01') & (pd.to_datetime(cov_df['first']) < '2025-01-01')).sum()} símbolos")
    print(f"  2025+:            {(pd.to_datetime(cov_df['first']) >= '2025-01-01').sum()} símbolos")

    # Muertos (último dato antes de 2025)
    dead_syms = cov_df[pd.to_datetime(cov_df["last"]) < pd.Timestamp("2025-01-01")]
    print(f"\n  Símbolos con último dato antes de 2025 (muertos/delisted): {len(dead_syms)}")
    if len(dead_syms) > 0:
        print("  " + ", ".join(dead_syms.index.tolist()))

    print("\n" + "=" * 72)
    print("SESGO DE SUPERVIVENCIA RESIDUAL")
    print("=" * 72)
    print("""
  FUENTE 1 (controlada): Los datos 1m en disco incluyen símbolos deslistados
  (LUNA2USDT, USTCUSDT, ETHWUSDT, 1000LUNCUSDT, etc.) con datos hasta su muerte.
  Estos se incluyen en el backtest hasta su última fecha disponible.

  FUENTE 2 (NO controlada): Binance Vision puede no tener datos 1m históricos
  para todos los perps que alguna vez existieron. Símbolos que fueron listados
  y deslistados ANTES de la fecha de inicio de los datos en disco no aparecen.
  Estimación: ~20-40 perps USDT-M de la era 2019-2020 podrían estar ausentes.
  Impacto en el backtest (IS 2021-2026): BAJO, ya que la mayoría murieron antes
  del período IS o tienen datos en disco.

  FUENTE 3 (NO controlada): Sesgo de LISTADO TARDÍO. Símbolos listados en 2023+
  con alto momentum retrospectivo (selection bias). Mitigado por el piso ADV-30d
  point-in-time: un símbolo nuevo entra al universo solo cuando ya tiene 30 días
  de historia y ADV > $5M.

  CONCLUSIÓN: El universo tiene sesgo de supervivencia RESIDUAL BAJO pero no nulo.
  Los resultados IS deben interpretarse con un margen de corrección hacia abajo.
    """)

    print("=" * 72)
    print("MODELO DE COSTOS USADO")
    print("=" * 72)
    print(f"  Majors ({', '.join(sorted(MAJORS))}): {COST_RT_MAJOR*100:.2f}% RT")
    print(f"  Mid/small-caps (resto): {COST_RT_MIDSMALL*100:.2f}% RT")
    print("  Los costos se aplican como reducción al retorno bruto del hold period.")
    print("  No se modela impacto de mercado (conservador para posiciones pequeñas).")

    return gate_pass, rdf, adv_stats, universe


if __name__ == "__main__":
    gate_pass, rdf, adv_stats, universe = main()
    sys.exit(0 if gate_pass else 1)
