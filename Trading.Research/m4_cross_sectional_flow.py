"""
M4 pure-signal backtest: Cross-Sectional Order-Flow — long-short dollar-neutral entre activos

Hipótesis:
  El desbalance de flujo *relativo* entre activos predice el retorno *relativo*: el activo
  con mayor presión compradora inusual (vs su propia norma reciente) supera al de menor.
  Un portafolio market-neutral cosecha ese spread y elimina el confound de beta de bull-market
  que destruyó a OfiContrarian en OOS (era long-beta disfrazado, no alpha de flujo).

Diseño (ADR Opus 2026-06-25):
  1. Inner-join de los 3 CSV por `bar` (hora exacta) — solo horas con los 3 presentes.
  2. Z-score rolling POR ACTIVO (window W) → rank cross-sectional del z-score.
     La pregunta es "¿quién está más inusualmente presionado *respecto de sí mismo*?"
     El crudo compararía entre escalas de notional distintas → sesgo hacia BTC.
  3. Long el activo de rank máximo, Short el de rank mínimo, flat el del medio. Dollar-neutral.
  4. Retornos en bloques NO SOLAPADOS de N horas (anti-inflación de señales solapadas,
     el artefacto que infló CvdBullishDiv en M4). Una sola serie de retornos de portafolio.
  5. Costos por las 2 piernas en cada rebalanceo (fee + slippage × 2).

Gate pre-registrado (pre-ver números):
  - Sharpe IS ≥ 0.5
  - ≥ ⅔ de las configs del grid con Sharpe > 0 (robustez — no una celda con suerte)
  - Beta del portafolio vs BTC ≈ 0 (prueba de neutralidad real, no long encubierto)
  GO → tocar OOS 2025 (touch-once). NO-GO → kill barato, eje 2 (régimen).

Grid:
  metric  ∈ [ofi, cvd_delta, buy_sell_ratio, composite]
  W       ∈ [24, 48, 96]
  N       ∈ [4, 6, 8]
  Total: 4 × 3 × 3 = 36 configs
"""

import pandas as pd
import numpy as np
from pathlib import Path
from itertools import product

# ── Configuración ───────────────────────────────────────────────────────────────

DATA_DIR = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\AggTrades\features")
ASSETS   = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]

IS_START  = "2021-01-01"
IS_END    = "2024-12-31"
OOS_START = "2025-01-01"
OOS_END   = "2025-12-31"

FEE_ONEWAY    = 0.0004   # 4 bps taker Binance Futures
SLIPPAGE_OW   = 0.001    # 0.1% slippage one-way (conservador en cripto 1h)
COST_ROUNDTRIP_PER_LEG = 2 * (FEE_ONEWAY + SLIPPAGE_OW)  # open + close × (fee + slip)
COST_PER_REBALANCE     = 2 * COST_ROUNDTRIP_PER_LEG       # 2 piernas

METRIC_VALUES = ["ofi", "cvd_delta", "buy_sell_ratio", "composite"]
W_VALUES      = [24, 48, 96]
N_VALUES      = [4, 6, 8]

# Gate pre-registrado (no modificar después de ver resultados)
SHARPE_GATE          = 0.5
FRAC_POSITIVE_GATE   = 2 / 3   # ≥ ⅔ configs con Sharpe > 0
BETA_TOLERANCE       = 0.3     # |beta vs BTC| < 0.3 para considerarlo neutral


# ── Carga de datos ───────────────────────────────────────────────────────────────

def load_features(asset: str) -> pd.DataFrame:
    path = DATA_DIR / f"{asset}_1h_features.csv"
    df = pd.read_csv(path)
    df["bar"] = pd.to_datetime(df["bar"], utc=True)
    df = df.set_index("bar").sort_index()
    df["close"] = df["close"].astype(float)
    for col in ["ofi", "cvd_delta", "buy_sell_ratio"]:
        df[col] = pd.to_numeric(df[col], errors="coerce")
    return df[["open", "high", "low", "close", "volume",
               "ofi", "cvd_delta", "buy_sell_ratio", "cvd",
               "arrival_rate", "mean_trade_size", "price_return"]]


def build_panel() -> dict[str, pd.DataFrame]:
    return {a: load_features(a) for a in ASSETS}


# ── Score / z-score por activo ────────────────────────────────────────────────────

def rolling_zscore(series: pd.Series, window: int) -> pd.Series:
    mu  = series.shift(1).rolling(window).mean()
    std = series.shift(1).rolling(window).std()
    return (series - mu) / std.replace(0, np.nan)


def add_composite(df: pd.DataFrame, window: int) -> pd.Series:
    z_ofi  = rolling_zscore(df["ofi"],           window)
    z_cvd  = rolling_zscore(df["cvd_delta"],      window)
    z_bsr  = rolling_zscore(df["buy_sell_ratio"],  window)
    return (z_ofi + z_cvd + z_bsr) / 3


def compute_scores(panel: dict[str, pd.DataFrame], metric: str, window: int) -> pd.DataFrame:
    scores = {}
    for asset, df in panel.items():
        if metric == "composite":
            scores[asset] = add_composite(df, window)
        else:
            scores[asset] = rolling_zscore(df[metric], window)
    return pd.DataFrame(scores).dropna(how="any")


# ── Construcción del portafolio ───────────────────────────────────────────────────

def build_portfolio_returns(
    panel: dict[str, pd.DataFrame],
    scores: pd.DataFrame,
    hold: int,
    period_start: str,
    period_end: str,
) -> tuple[pd.Series, pd.Series]:
    """
    Retorna (portfolio_returns, btc_returns) en bloques NO solapados de `hold` horas.
    Long el activo de score máximo, short el de score mínimo, flat el del medio.
    Dollar-neutral: 1 unidad nocional por pierna.
    """
    # Precios de cierre alineados
    closes = pd.DataFrame({a: panel[a]["close"] for a in ASSETS})
    closes = closes.loc[scores.index]

    # Filtro de período
    start = pd.Timestamp(period_start, tz="UTC")
    end   = pd.Timestamp(period_end,   tz="UTC") + pd.Timedelta(hours=23)
    idx   = scores.index[(scores.index >= start) & (scores.index <= end)]

    if len(idx) < hold + 1:
        return pd.Series(dtype=float), pd.Series(dtype=float)

    port_rets = []
    btc_rets  = []

    # Bloques no solapados: de i en i+hold (rebalanceo cada N horas)
    i = 0
    while i + hold < len(idx):
        entry_ts = idx[i]
        exit_ts  = idx[i + hold]

        if entry_ts not in scores.index or exit_ts not in closes.index:
            i += hold
            continue

        row_scores = scores.loc[entry_ts]
        if row_scores.isna().any():
            i += hold
            continue

        ranks = row_scores.rank()       # 1=min (short), 3=max (long)
        long_asset  = ranks.idxmax()
        short_asset = ranks.idxmin()

        entry_long  = closes.loc[entry_ts, long_asset]
        exit_long   = closes.loc[exit_ts,  long_asset]
        entry_short = closes.loc[entry_ts, short_asset]
        exit_short  = closes.loc[exit_ts,  short_asset]

        if entry_long <= 0 or entry_short <= 0:
            i += hold
            continue

        ret_long  =  (exit_long  - entry_long)  / entry_long
        ret_short = -(exit_short - entry_short) / entry_short  # short: ganamos si baja

        # Dollar-neutral: 0.5 nocional por pierna → suma a 1 nocional total
        port_ret = 0.5 * ret_long + 0.5 * ret_short - COST_PER_REBALANCE
        port_rets.append((exit_ts, port_ret))

        # BTC buy-and-hold en el mismo bloque (para calcular beta)
        entry_btc = closes.loc[entry_ts, "BTCUSDT"]
        exit_btc  = closes.loc[exit_ts,  "BTCUSDT"]
        btc_rets.append((exit_ts, (exit_btc - entry_btc) / entry_btc))

        i += hold

    if not port_rets:
        return pd.Series(dtype=float), pd.Series(dtype=float)

    ts_idx, p_vals = zip(*port_rets)
    _, b_vals      = zip(*btc_rets)
    return (
        pd.Series(p_vals, index=pd.DatetimeIndex(ts_idx)),
        pd.Series(b_vals, index=pd.DatetimeIndex(ts_idx)),
    )


# ── Métricas ─────────────────────────────────────────────────────────────────────

def sharpe(returns: pd.Series, periods_per_year: float) -> float:
    if len(returns) < 10 or returns.std() == 0:
        return np.nan
    return (returns.mean() / returns.std()) * np.sqrt(periods_per_year)


def beta_vs_btc(port: pd.Series, btc: pd.Series) -> float:
    aligned = pd.concat([port, btc], axis=1).dropna()
    if len(aligned) < 10:
        return np.nan
    cov = np.cov(aligned.iloc[:, 0], aligned.iloc[:, 1])
    return cov[0, 1] / cov[1, 1] if cov[1, 1] != 0 else np.nan


def net_profit(returns: pd.Series) -> float:
    return (1 + returns).prod() - 1


# ── Runner del grid ───────────────────────────────────────────────────────────────

def run_grid(panel: dict, period_start: str, period_end: str, label: str) -> pd.DataFrame:
    # Períodos aproximados por año para anualizar el Sharpe
    hold_hours_sample = N_VALUES[0]
    periods_per_year  = 8760 / hold_hours_sample  # ajusta por hold dentro del loop

    rows = []
    for metric, W, N in product(METRIC_VALUES, W_VALUES, N_VALUES):
        scores = compute_scores(panel, metric, W)
        ann_factor = 8760 / N
        port, btc = build_portfolio_returns(panel, scores, N, period_start, period_end)
        if port.empty:
            continue
        s = sharpe(port, ann_factor)
        b = beta_vs_btc(port, btc)
        rows.append({
            "metric": metric, "W": W, "N": N,
            "sharpe": round(s, 3),
            "net_pct": round(net_profit(port) * 100, 2),
            "n_blocks": len(port),
            "beta_btc": round(b, 3),
            "win_rate": round((port > 0).mean(), 3),
        })

    df = pd.DataFrame(rows).sort_values("sharpe", ascending=False)
    print(f"\n{'='*70}")
    print(f"  {label} -- Cross-Sectional Order-Flow ({period_start} a {period_end})")
    print(f"{'='*70}")
    print(df.to_string(index=False))
    return df


# ── Gate ─────────────────────────────────────────────────────────────────────────

def evaluate_gate(is_results: pd.DataFrame) -> bool:
    best_sharpe   = is_results["sharpe"].max()
    frac_positive = (is_results["sharpe"] > 0).mean()
    best_row      = is_results.iloc[0]
    beta          = best_row["beta_btc"]

    print(f"\n{'-'*70}")
    print("  GATE PRE-REGISTRADO (IS 2021-2024)")
    print(f"{'-'*70}")
    print(f"  Mejor Sharpe IS      : {best_sharpe:.3f}  (gate >= {SHARPE_GATE})")
    print(f"  Configs con Sharpe>0 : {frac_positive:.1%}  (gate >= {FRAC_POSITIVE_GATE:.1%})")
    print(f"  Beta vs BTC (mejor)  : {beta:.3f}    (gate |beta| < {BETA_TOLERANCE})")

    sharpe_ok   = best_sharpe   >= SHARPE_GATE
    positive_ok = frac_positive >= FRAC_POSITIVE_GATE
    beta_ok     = abs(beta)     <  BETA_TOLERANCE

    print(f"\n  Sharpe >= {SHARPE_GATE}       : {'PASA' if sharpe_ok   else 'FALLA'}")
    print(f"  >= 2/3 configs > 0  : {'PASA' if positive_ok else 'FALLA'}")
    print(f"  Beta neutral        : {'PASA' if beta_ok     else 'FALLA'}")

    go = sharpe_ok and positive_ok and beta_ok
    print(f"\n  VEREDICTO IS: {'GO -- correr OOS' if go else 'NO-GO -- kill barato'}")
    return go


# ── Main ─────────────────────────────────────────────────────────────────────────

def main():
    print("Cargando features...")
    panel = build_panel()
    for asset, df in panel.items():
        print(f"  {asset}: {len(df):,} barras  "
              f"({df.index.min().date()} a {df.index.max().date()})")

    # IS
    is_results = run_grid(panel, IS_START, IS_END, "IS 2021-2024")

    # Gate — touch-once: OOS solo si IS pasa
    go = evaluate_gate(is_results)

    if go:
        oos_results = run_grid(panel, OOS_START, OOS_END, "OOS 2025")
        oos_best = oos_results.iloc[0]
        print(f"\n  OOS mejor config: metric={oos_best.metric} W={oos_best.W} N={oos_best.N}")
        print(f"  Sharpe OOS: {oos_best.sharpe:.3f}  |  Net: {oos_best.net_pct:.1f}%  "
              f"|  Beta: {oos_best.beta_btc:.3f}")
    else:
        print("\n  IS no pasa el gate. OOS no se corre. "
              "Documentar en strategy_experiments.md y pasar a eje 2 (régimen).")


if __name__ == "__main__":
    main()
