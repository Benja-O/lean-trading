#!/usr/bin/env python3
"""
Layer A — Lead-Lag Gap (BTC down-move catch-up) — Etapas 1-3 (BRUTO, sin costos)
================================================================================

HIPOTESIS (aclarada por el director):
  No buscar una moneda PERSISTENTEMENTE lenta. En cada movimiento BAJISTA
  importante de BTC, escanear las ~100 alts mas liquidas, identificar la(s) que
  TODAVIA no acompanaron la caida (gap de reaccion sin llenar), y shortearlas
  esperando catch-up. Cada evento -> otra moneda candidata. State-contingent.

SENAL POR EVENTO (gap de reaccion = residuo CAPM sobre la ventana):
  Para cada moneda en el evento t:
    cumret_coin_W = close_coin[t] / close_coin[t-W] - 1     (reaccion realizada)
    cumret_btc_W  = close_btc[t]  / close_btc[t-W]  - 1     (shock de BTC)
    expected      = beta_pit * cumret_btc_W                 (reaccion esperada)
    gap           = cumret_coin_W - expected                (residuo)
  En un down-move (cumret_btc_W < 0): gap POSITIVO = la moneda cayo MENOS de lo
  que su beta predice = laggard = candidata a SHORT (deberia completar la caida).

LO QUE EL TEST FALSA (no la persistencia de la moneda, sino la relacion):
  "gap grande sin llenar en t  ->  catch-up bajista en [t, t+H]"

TRES VENTANAS (todas explicitas, anti-lookahead):
  - beta_pit : estimada sobre BETA_WIN barras que TERMINAN en t-W (antes del evento)
  - W        : ventana del shock de BTC y de la reaccion acumulada [t-W, t]
  - H        : horizonte de catch-up, estrictamente DESPUES de t  -> [t, t+H]

DEDUP DE EVENTOS (refractory = H):
  Tras disparar un evento en t, se suprimen nuevos disparos hasta t+H. Asi tres
  caidas separadas = 3 eventos (3 laggards distintos), pero un unico deslizamiento
  continuo = 1 evento (no re-entrada en el mismo movimiento).

ETAPAS (este script = 1-3, a BRUTO sin costos):
  1. Eventos BTC (z-score en sigma) + gap por moneda.
  2. Predictividad del gap: bucket top-gap vs resto; short P&L; IC (Spearman gap~fwd).
  3. Ejecutabilidad (el filtro que mata casi todo):
       (a) buckets de liquidez (ADV terciles) -> el edge, ¿vive en liquidas o solo
           en la cola rancia?
       (b) skip-1 / skip-2 barras -> ¿queda catch-up en barras ENTRABLES o se
           consumio en la barra del evento?
       (c) stale-check -> ¿hubo print/volumen real para shortear?

GATE (Etapas 1-3, bruto). Procede a Etapa 4 (costos) SOLO si:
  - >= MIN_EVENTS eventos por config.
  - Short P&L del bucket top-gap > 0 con t-stat > 2, con MESETA en la grilla (W,k,H).
  - IC consistentemente negativo (gap alto -> fwd bajo).
  - Sobrevive skip-1 (sigue positivo entrando 1 barra tarde).
  - NO concentrado en el tercil ILIQUIDO (funciona tambien en mid/high ADV).

Universo / datos: reusa el panel de ~196 perps USDT-M de Binance (klines 1m locales,
anti-survivorship con LUNA2/USTC/etc.), recortado a las 100 mas liquidas por ADV
point-in-time. Base TF = 15m (W/H expresados en multiplos de 15m).

IS:  2021-01-01 -> 2024-12-31
OOS: 2025-01-01 -> 2026-06-26   (solo reporte; NO selecciona params)
"""

import sys
import argparse
import zipfile
import warnings
from pathlib import Path
from itertools import product

import numpy as np
import pandas as pd

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")
warnings.filterwarnings("ignore")

# ── Configuracion ───────────────────────────────────────────────────────────────

KLINES_1M_DIR = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\1m")
BASE_TF       = "15min"
CACHE_DIR     = Path(rf"F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\{BASE_TF}_leadlag")

EXCLUDE = {"1000BONKUSDT-copia"}
BTC     = "BTCUSDT"

IS_START  = "2021-01-01"
IS_END    = "2024-12-31"
OOS_START = "2025-01-01"
OOS_END   = "2026-06-26"

# Barras por dia a 15m
BARS_PER_DAY = 96

# Universo elegible (point-in-time)
ADV_MIN_USD   = 5_000_000     # piso ADV diario en USD para entrar al universo
N_UNIVERSE    = 100           # top-N mas liquidas por ADV en cada evento
ADV_WIN_BARS  = 30 * BARS_PER_DAY   # 30 dias para ADV-pit

# Estimacion de beta point-in-time
BETA_WIN_BARS = 30 * BARS_PER_DAY   # 30 dias de retornos 15m
# Normalizacion de la sigma del evento
NORM_WIN_BARS = 30 * BARS_PER_DAY

# ── Grilla pre-registrada ───────────────────────────────────────────────────────
# W (ventana del evento/gap) y H (catch-up) en BARRAS de 15m.
W_GRID = [4, 8, 16]      # 1h, 2h, 4h
K_GRID = [2.0, 2.5, 3.0] # umbral del shock en sigmas (down-move)
H_GRID = [4, 8, 16]      # 1h, 2h, 4h

TOP_N        = 5         # bucket top-gap = 5 monedas mas "atrasadas"
MIN_EVENTS   = 30        # poder estadistico minimo por config
SKIP_GRID    = [0, 1, 2] # barras de retraso de entrada para el test de ejecutabilidad

# Columnas klines Binance (sin header en datos pre-2022)
KLINE_COLS = [
    "open_time", "open", "high", "low", "close", "volume",
    "close_time", "quote_volume", "n_trades",
    "taker_buy_vol", "taker_buy_qvol", "ignore",
]


# ── Lectura de datos (reusa la logica de m4_momentum_cross_sectional) ───────────

def _read_zip_1m(path: Path):
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
                    usecols=[0, 1, 2, 3, 4, 5, 7],
                    dtype={0: "int64", 1: "float64", 2: "float64",
                           3: "float64", 4: "float64", 5: "float64", 7: "float64"},
                )
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
                needed = ["open_time", "open", "high", "low", "close", "volume", "quote_volume"]
                df = df[[c for c in needed if c in df.columns]]
            else:
                df.columns = ["open_time", "open", "high", "low", "close", "volume", "quote_volume"]
        return df
    except Exception:
        return None


def load_symbol_1m_to_tf(symbol: str, tf: str):
    """Lee los ZIPs 1m de un simbolo y los resamplea a `tf` (UTC, label=right)."""
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
    raw["ts"] = pd.to_datetime(raw["open_time"], unit="ms", utc=True)
    raw = raw.set_index("ts")

    # label="right", closed="left": la barra de 15m que CIERRA en T agrega [T-15m, T)
    # -> el timestamp del indice es el fin de barra, igual que la convencion del proyecto.
    out = raw.resample(tf, label="right", closed="left").agg(
        open=("open", "first"),
        high=("high", "max"),
        low=("low", "min"),
        close=("close", "last"),
        volume=("volume", "sum"),
        quote_volume=("quote_volume", "sum"),
    ).dropna(subset=["close"])
    return out


def build_universe_cache(symbols, tf, force_rebuild=False, max_symbols=None):
    """Carga o genera los parquets `tf` por simbolo. dict symbol -> DataFrame."""
    CACHE_DIR.mkdir(parents=True, exist_ok=True)
    if max_symbols:
        # BTC es obligatorio (referencia del lead-lag); algunos majors liquidos
        # garantizan universo elegible en el smoke test.
        forced = [s for s in (BTC, "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT") if s in symbols]
        rest = [s for s in symbols if s not in forced]
        symbols = forced + rest[:max(0, max_symbols - len(forced))]

    universe = {}
    failed = []
    print(f"\nCargando datos {tf} para {len(symbols)} simbolos (cache: {CACHE_DIR})...")
    for i, sym in enumerate(symbols, 1):
        cache_path = CACHE_DIR / f"{sym}_{tf}.parquet"
        if cache_path.exists() and not force_rebuild:
            try:
                df = pd.read_parquet(cache_path)
                df.index = pd.to_datetime(df.index, utc=True)
                universe[sym] = df
                if i % 25 == 0:
                    print(f"  {i}/{len(symbols)} (cache)...")
                continue
            except Exception:
                pass

        df = load_symbol_1m_to_tf(sym, tf)
        if df is None or len(df) < BETA_WIN_BARS:
            failed.append(sym)
            continue
        df.to_parquet(cache_path)
        universe[sym] = df
        if i % 25 == 0:
            print(f"  {i}/{len(symbols)} (built from 1m)...")

    print(f"  OK: {len(universe)} simbolos | Fallidos/cortos: {len(failed)}: {failed[:8]}")
    return universe


# ── Paneles ──────────────────────────────────────────────────────────────────────

def build_panels(universe):
    """close_panel, ret_panel, qvol_panel (DatetimeIndex tf x simbolos)."""
    closes = {s: df["close"] for s, df in universe.items()}
    qvols  = {s: df["quote_volume"] for s, df in universe.items()}
    close_panel = pd.DataFrame(closes).sort_index()
    qvol_panel  = pd.DataFrame(qvols).sort_index()
    ret_panel   = close_panel.pct_change()
    return close_panel, ret_panel, qvol_panel


def compute_beta_panel(ret_panel, win):
    """
    Beta rolling point-in-time de cada simbolo vs BTC sobre `win` barras.
    beta_t usa retornos en (t-win, t].  cov = E[r*rb]-E[r]E[rb] ; var = E[rb^2]-E[rb]^2.
    """
    rb = ret_panel[BTC]
    mean_rb     = rb.rolling(win).mean()
    var_rb      = rb.rolling(win).var(ddof=0)
    mean_r      = ret_panel.rolling(win).mean()
    mean_r_rb   = ret_panel.mul(rb, axis=0).rolling(win).mean()
    cov         = mean_r_rb.sub(mean_r.mul(mean_rb, axis=0))
    beta_panel  = cov.div(var_rb.replace(0, np.nan), axis=0)
    return beta_panel


# ── Eventos BTC (z-score en sigma, dedup refractory=H) ──────────────────────────

def detect_btc_events(close_panel, ret_panel, w, k, h):
    """
    Posiciones (indices enteros) de eventos bajistas de BTC:
      cumret_W[t] = close_btc[t]/close_btc[t-W] - 1
      sigma_W[t]  = std_1bar(ret_btc, NORM_WIN)[t-1] * sqrt(W)   (point-in-time)
      z[t] = cumret_W[t] / sigma_W[t]   ;   evento bajista si z[t] < -k
    Dedup: tras un evento en i, suprimir hasta i+h.
    """
    close_btc = close_panel[BTC]
    rb        = ret_panel[BTC]

    cumret_w = close_btc / close_btc.shift(w) - 1.0
    sigma_1b = rb.rolling(NORM_WIN_BARS).std(ddof=1).shift(1)
    sigma_w  = sigma_1b * np.sqrt(w)
    z        = cumret_w / sigma_w.replace(0, np.nan)

    z_vals = z.values
    n = len(z_vals)
    events = []
    refractory_until = -1
    for i in range(n):
        if i <= refractory_until:
            continue
        zv = z_vals[i]
        if np.isfinite(zv) and zv < -k:
            events.append(i)
            refractory_until = i + h
    return events, z


# ── Gap + forward returns por evento ────────────────────────────────────────────

def _spearman(a, b):
    """Spearman = Pearson de rangos. a,b arrays alineados (sin NaN)."""
    if len(a) < 4:
        return np.nan
    ra = pd.Series(a).rank().values
    rb = pd.Series(b).rank().values
    if np.std(ra) == 0 or np.std(rb) == 0:
        return np.nan
    return float(np.corrcoef(ra, rb)[0, 1])


def run_config(close_panel, ret_panel, qvol_panel, beta_panel,
               w, k, h, start_dt, end_dt):
    """
    Corre una config (W,k,H) sobre [start_dt, end_dt]. Retorna metricas Etapas 1-3.
    """
    idx = close_panel.index
    cv  = close_panel.values
    qv  = qvol_panel.values
    bv  = beta_panel.values
    cols = list(close_panel.columns)
    btc_col = cols.index(BTC)

    events_all, _ = detect_btc_events(close_panel, ret_panel, w, k, h)

    # Limitar a eventos cuya t cae en [start, end] y con margenes de indice validos
    lo = idx.searchsorted(start_dt)
    hi = idx.searchsorted(end_dt, side="right")
    min_i = max(w, BETA_WIN_BARS, ADV_WIN_BARS) + 1
    events = [it for it in events_all
              if lo <= it < hi and it - w >= 0 and it - 1 >= min_i and it + h < len(idx)]

    # Acumuladores
    short_pnl_top   = []   # short P&L medio del bucket top-gap por evento (skip=0)
    short_pnl_bot   = []   # short P&L medio del bucket bottom-gap (placebo)
    ic_list         = []   # IC (Spearman gap~fwd) por evento  -> esperado NEGATIVO
    pnl_by_skip     = {s: [] for s in SKIP_GRID}
    pnl_by_tertile  = {"low": [], "mid": [], "high": []}
    stale_frac_list = []   # fraccion de candidatos top-gap sin volumen en barra de entrada
    n_eligible_list = []

    for it in events:
        # ── Universo elegible point-in-time (ADV trailing 30d, top-N) ──
        adv_window = qv[it - ADV_WIN_BARS:it, :]
        adv = np.nanmean(adv_window, axis=0) * BARS_PER_DAY  # ADV diario USD
        elig = np.where((adv >= ADV_MIN_USD) & np.isfinite(adv))[0]
        elig = elig[elig != btc_col]
        if len(elig) < TOP_N * 2 + 2:
            continue
        # top-N por ADV
        if len(elig) > N_UNIVERSE:
            order = np.argsort(adv[elig])[::-1][:N_UNIVERSE]
            elig = elig[order]

        # ── Gap (residuo CAPM sobre [t-W, t]) ──
        p_now  = cv[it, elig]
        p_prev = cv[it - w, elig]
        beta_e = bv[it - w, elig]   # beta-pit: ventana que termina en t-W
        valid  = np.isfinite(p_now) & np.isfinite(p_prev) & (p_prev > 0) & np.isfinite(beta_e)
        if valid.sum() < TOP_N * 2 + 2:
            continue
        e          = elig[valid]
        cumret_coin = p_now[valid] / p_prev[valid] - 1.0
        cumret_btc  = cv[it, btc_col] / cv[it - w, btc_col] - 1.0
        expected    = beta_e[valid] * cumret_btc
        gap         = cumret_coin - expected   # >0 en down-move = laggard = SHORT

        adv_e = adv[e]

        # ── Forward returns (catch-up) para skip variants ──
        p_exit = cv[it + h, e]
        fwd0   = p_exit / cv[it, e] - 1.0       # entrada en t
        ok0    = np.isfinite(fwd0)

        # Ranking por gap descendente: top = mas laggard (mas residuo positivo)
        order_gap = np.argsort(gap)
        bot_ix = order_gap[:TOP_N]          # gap mas chico (ya cayo de mas) -> placebo
        top_ix = order_gap[-TOP_N:]         # gap mas grande -> candidatos SHORT

        # short P&L = -fwd (gano si el precio baja)
        top_fwd = fwd0[top_ix]
        bot_fwd = fwd0[bot_ix]
        if np.isfinite(top_fwd).sum() >= 1:
            short_pnl_top.append(-np.nanmean(top_fwd))
        if np.isfinite(bot_fwd).sum() >= 1:
            short_pnl_bot.append(-np.nanmean(bot_fwd))

        # IC cross-sectional: gap vs fwd (esperado negativo)
        ic_mask = ok0
        if ic_mask.sum() >= 4:
            ic_list.append(_spearman(gap[ic_mask], fwd0[ic_mask]))

        n_eligible_list.append(len(e))

        # ── Etapa 3a: skip-1 / skip-2 (entrada tardia, exit fijo en t+H) ──
        for s in SKIP_GRID:
            if it + s >= len(idx):
                continue
            p_entry_s = cv[it + s, e[top_ix]]
            fwd_s = p_exit[top_ix] / p_entry_s - 1.0
            if np.isfinite(fwd_s).sum() >= 1:
                pnl_by_skip[s].append(-np.nanmean(fwd_s))

        # ── Etapa 3b: buckets de liquidez (terciles de ADV dentro del evento) ──
        # Re-rankear por gap DENTRO de cada tercil de ADV, tomar top-N//? -> usamos
        # el bucket top-gap restringido a cada tercil para ver donde vive el edge.
        adv_terciles = np.quantile(adv_e, [1/3, 2/3])
        for name, mask in (
            ("low",  adv_e <= adv_terciles[0]),
            ("mid",  (adv_e > adv_terciles[0]) & (adv_e <= adv_terciles[1])),
            ("high", adv_e > adv_terciles[1]),
        ):
            if mask.sum() < TOP_N:
                continue
            gap_t = gap.copy()
            gap_t[~mask] = -np.inf  # solo candidatos de este tercil pueden ser top
            top_t = np.argsort(gap_t)[-TOP_N:]
            fwd_t = fwd0[top_t]
            if np.isfinite(fwd_t).sum() >= 1:
                pnl_by_tertile[name].append(-np.nanmean(fwd_t))

        # ── Etapa 3c: stale-check (¿hubo volumen para shortear en la barra de entrada?) ──
        vol_entry = qv[it, e[top_ix]]
        stale = (~np.isfinite(vol_entry)) | (vol_entry <= 0)
        stale_frac_list.append(float(np.mean(stale)))

    n_events = len(short_pnl_top)

    def _tstat(x):
        x = np.array([v for v in x if np.isfinite(v)], dtype=float)
        if len(x) < 2 or x.std(ddof=1) == 0:
            return np.nan, np.nan, len(x)
        return float(x.mean()), float(x.mean() / (x.std(ddof=1) / np.sqrt(len(x)))), len(x)

    top_mean, top_t, _ = _tstat(short_pnl_top)
    bot_mean, _, _     = _tstat(short_pnl_bot)
    ic_mean, ic_t, _   = _tstat(ic_list)

    skip_means = {s: (np.nanmean(pnl_by_skip[s]) if pnl_by_skip[s] else np.nan)
                  for s in SKIP_GRID}
    tert_means = {nm: (np.nanmean(v) if v else np.nan) for nm, v in pnl_by_tertile.items()}

    return {
        "w": w, "k": k, "h": h,
        "n_events": n_events,
        "top_pnl_bps": top_mean * 1e4 if np.isfinite(top_mean) else np.nan,
        "top_tstat": top_t,
        "spread_bps": (top_mean - bot_mean) * 1e4 if np.isfinite(top_mean) and np.isfinite(bot_mean) else np.nan,
        "ic_mean": ic_mean,
        "ic_tstat": ic_t,
        "win_rate": float(np.mean([1.0 if v > 0 else 0.0 for v in short_pnl_top])) if short_pnl_top else np.nan,
        "skip0_bps": skip_means[0] * 1e4 if np.isfinite(skip_means[0]) else np.nan,
        "skip1_bps": skip_means[1] * 1e4 if np.isfinite(skip_means[1]) else np.nan,
        "skip2_bps": skip_means[2] * 1e4 if np.isfinite(skip_means[2]) else np.nan,
        "tert_low_bps":  tert_means["low"]  * 1e4 if np.isfinite(tert_means["low"])  else np.nan,
        "tert_mid_bps":  tert_means["mid"]  * 1e4 if np.isfinite(tert_means["mid"])  else np.nan,
        "tert_high_bps": tert_means["high"] * 1e4 if np.isfinite(tert_means["high"]) else np.nan,
        "stale_frac": float(np.nanmean(stale_frac_list)) if stale_frac_list else np.nan,
        "n_eligible_avg": float(np.nanmean(n_eligible_list)) if n_eligible_list else np.nan,
    }


# ── Reporte de eventos (preview Etapa 1, "lo que ves en el chart") ──────────────

def preview_events(close_panel, ret_panel, qvol_panel, beta_panel, w, k, h, start_dt, end_dt, n=8):
    events, z = detect_btc_events(close_panel, ret_panel, w, k, h)
    idx = close_panel.index
    lo = idx.searchsorted(start_dt); hi = idx.searchsorted(end_dt, side="right")
    events = [it for it in events if lo <= it < hi and it - w >= 0 and it + h < len(idx)]
    print(f"\n  Preview de eventos BTC (W={w}b/{w*15}m, k={k}sigma, H={h}b): "
          f"{len(events)} eventos en el periodo")
    print(f"  {'fecha (fin W, UTC)':>22}  {'z':>7}  {'ret_BTC_W':>10}  {'top laggards (gap%)':>40}")
    btc_col = list(close_panel.columns).index(BTC)
    cv = close_panel.values; bv = beta_panel.values; qv = qvol_panel.values
    cols = list(close_panel.columns)
    shown = 0
    for it in events:
        if shown >= n:
            break
        cumret_btc = cv[it, btc_col] / cv[it - w, btc_col] - 1.0
        adv = np.nanmean(qv[it - ADV_WIN_BARS:it, :], axis=0) * BARS_PER_DAY
        elig = np.where((adv >= ADV_MIN_USD) & np.isfinite(adv))[0]
        elig = elig[elig != btc_col]
        if len(elig) < TOP_N:
            continue
        p_now = cv[it, elig]; p_prev = cv[it - w, elig]; beta_e = bv[it - w, elig]
        valid = np.isfinite(p_now) & np.isfinite(p_prev) & (p_prev > 0) & np.isfinite(beta_e)
        e = elig[valid]
        gap = (p_now[valid] / p_prev[valid] - 1.0) - beta_e[valid] * cumret_btc
        top = np.argsort(gap)[-3:][::-1]
        names = ", ".join(f"{cols[e[j]].replace('USDT','')}({gap[j]*100:+.1f})" for j in top)
        print(f"  {str(idx[it]):>22}  {z.values[it]:>7.2f}  {cumret_btc*100:>9.2f}%  {names:>40}")
        shown += 1


# ── Gate ─────────────────────────────────────────────────────────────────────────

def evaluate_gate(df_is):
    """Gate Etapas 1-3 (bruto). Ver docstring del modulo."""
    valid = df_is[df_is["n_events"] >= MIN_EVENTS].copy()
    n_cfg = len(df_is)

    sig_positive = valid[(valid["top_pnl_bps"] > 0) & (valid["top_tstat"] > 2)]
    meseta = len(sig_positive) >= max(2, n_cfg // 2)

    ic_negative = (valid["ic_mean"] < 0).mean() if len(valid) else 0.0
    ic_ok = ic_negative >= 0.5

    survives_skip1 = ((valid["skip1_bps"] > 0).sum() >= max(2, len(valid) // 2)) if len(valid) else False

    # No concentrado en iliquidas: el tercil high o mid debe contribuir positivo
    # en una mayoria de configs (si solo 'low' es positivo -> artefacto de ranciedad).
    liquid_ok = (((valid["tert_high_bps"] > 0) | (valid["tert_mid_bps"] > 0)).sum()
                 >= max(2, len(valid) // 2)) if len(valid) else False

    gate = bool(meseta and ic_ok and survives_skip1 and liquid_ok)
    return {
        "gate": gate,
        "n_valid_configs": len(valid),
        "n_sig_positive": len(sig_positive),
        "meseta": meseta,
        "ic_negative_frac": ic_negative,
        "ic_ok": ic_ok,
        "survives_skip1": survives_skip1,
        "liquid_ok": liquid_ok,
    }


# ── Main ─────────────────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--max-symbols", type=int, default=None,
                    help="limitar #simbolos (smoke test)")
    ap.add_argument("--rebuild", action="store_true", help="forzar rebuild de cache")
    args = ap.parse_args()

    print("=" * 78)
    print("LAYER A — LEAD-LAG GAP (BTC down-move catch-up) — Etapas 1-3 (BRUTO)")
    print(f"Base TF: {BASE_TF}  |  IS: {IS_START} -> {IS_END}  |  OOS: {OOS_START} -> {OOS_END}")
    print(f"Grilla: W{W_GRID} x k{K_GRID} x H{H_GRID} = {len(W_GRID)*len(K_GRID)*len(H_GRID)} configs")
    print("=" * 78)

    # ── Universo ──
    sym_dirs = [d for d in KLINES_1M_DIR.iterdir() if d.is_dir() and d.name not in EXCLUDE]
    symbols = sorted(d.name for d in sym_dirs)
    print(f"  Simbolos en disco: {len(symbols)}")

    universe = build_universe_cache(symbols, BASE_TF, force_rebuild=args.rebuild,
                                    max_symbols=args.max_symbols)
    if BTC not in universe:
        print(f"  ERROR: {BTC} no esta en el universo. Abortando.")
        return False

    close_panel, ret_panel, qvol_panel = build_panels(universe)
    print(f"  Panel: {len(close_panel):,} barras x {len(close_panel.columns)} simbolos")
    print(f"  Rango: {close_panel.index[0]} -> {close_panel.index[-1]}")

    print("\n  Calculando beta rolling point-in-time (puede tardar)...")
    beta_panel = compute_beta_panel(ret_panel, BETA_WIN_BARS)

    is_start = pd.Timestamp(IS_START, tz="UTC"); is_end = pd.Timestamp(IS_END, tz="UTC")
    oos_start = pd.Timestamp(OOS_START, tz="UTC"); oos_end = pd.Timestamp(OOS_END, tz="UTC")

    # ── Preview Etapa 1 (config central) ──
    preview_events(close_panel, ret_panel, qvol_panel, beta_panel,
                   w=8, k=2.5, h=16, start_dt=is_start, end_dt=oos_end, n=10)

    # ── Grilla IS ──
    print("\n" + "=" * 78)
    print("ETAPAS 1-3 — GRILLA IS (bruto, sin costos)")
    print("=" * 78)
    rows_is = []
    for w, k, h in product(W_GRID, K_GRID, H_GRID):
        rows_is.append(run_config(close_panel, ret_panel, qvol_panel, beta_panel,
                                  w, k, h, is_start, is_end))
    df_is = pd.DataFrame(rows_is)

    pd.set_option("display.float_format", "{:.2f}".format)
    pd.set_option("display.max_columns", 30)
    pd.set_option("display.width", 200)

    cols_show = ["w", "k", "h", "n_events", "top_pnl_bps", "top_tstat", "spread_bps",
                 "ic_mean", "win_rate", "skip1_bps", "tert_low_bps", "tert_high_bps", "stale_frac"]
    print(df_is[cols_show].to_string(index=False))

    # ── Gate ──
    g = evaluate_gate(df_is)
    print("\n" + "=" * 78)
    print("GATE ETAPAS 1-3 (bruto)")
    print("=" * 78)
    print(f"  Configs validas (>= {MIN_EVENTS} eventos): {g['n_valid_configs']}/{len(df_is)}")
    print(f"  Configs con short P&L > 0 y t-stat > 2:    {g['n_sig_positive']}  -> meseta: {g['meseta']}")
    print(f"  IC negativo (gap alto -> fwd bajo):        {g['ic_negative_frac']*100:.0f}% configs  -> ok: {g['ic_ok']}")
    print(f"  Sobrevive skip-1 (entrada 1 barra tarde):  {g['survives_skip1']}")
    print(f"  Edge NO solo en iliquidas (mid/high>0):    {g['liquid_ok']}")
    print(f"\n  GATE: {'PASS -> procede a Etapa 4 (costos)' if g['gate'] else 'FAIL -> NO-GO (no construir Etapa 4)'}")

    # ── OOS (solo reporte si hay alguna config viva en IS) ──
    if g["n_sig_positive"] > 0:
        print("\n" + "=" * 78)
        print("OOS (solo reporte — NO selecciona params)")
        print("=" * 78)
        rows_oos = []
        for _, r in df_is[df_is["top_tstat"] > 2].iterrows():
            rows_oos.append(run_config(close_panel, ret_panel, qvol_panel, beta_panel,
                                       int(r["w"]), float(r["k"]), int(r["h"]),
                                       oos_start, oos_end))
        if rows_oos:
            df_oos = pd.DataFrame(rows_oos)
            print(df_oos[["w", "k", "h", "n_events", "top_pnl_bps", "top_tstat",
                          "ic_mean", "skip1_bps"]].to_string(index=False))

    print("\n" + "=" * 78)
    print("NOTAS")
    print("=" * 78)
    print("  - Bruto: SIN costos. Un PASS aqui solo habilita Etapa 4 (shorts + costos")
    print("    reales 0.12%/0.22% RT + slippage de crash + funding/borrow).")
    print("  - stale_frac alto en top-gap = el gap mas grande lo dan monedas sin print")
    print("    (precio rancio) -> el laggard es artefacto, no edge atrapable.")
    print("  - Si el edge cae con skip-1 o solo vive en tert_low -> NO-GO (no ejecutable).")
    print("=" * 78)
    return g["gate"]


if __name__ == "__main__":
    ok = main()
    sys.exit(0 if ok else 1)
