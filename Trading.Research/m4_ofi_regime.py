"""
M4 pure-signal backtest: OFI Contrarian condicionado por regimen HMM

Hipotesis:
  OfiContrarian (Long cuando OFI en percentil bajo = vendedores agresivos se agotan)
  tuvo IS Sharpe 0.503 genuino pero OOS -0.703. El desglose anual del M4 original
  mostro que el edge vivio en años bulllish (2021, 2023, 2024) y colapso en bear
  (2022: Sharpe -0.863 BTC). Hipotesis: el edge existe solo en regimenes donde el
  precio responde al flujo de compradores — MeanReverting o Trend — y no en
  HighVolatility donde el momentum baja aplasta cualquier señal contraria.

Diseño:
  - Señal base: OFI Contrarian (window=24, threshold=0.15, hold=8) — parametros del
    M4 original, sin re-seleccion (anti-p-hacking).
  - Regimen: HMM de 4h (Trend / MeanReverting / HighVolatility / Squeeze). Los modelos
    entrenados son BTCUSDT y ETHUSDT. SOLUSDT usa BTC como proxy (mismo que BNB en
    m4_rsi_hmm_squeeze.py, justificado: correlacion alta, sin modelo propio).
  - Puente 4h→1h: clasificar a 4h (resampleo close 1h → 4h), propagar la etiqueta
    de cada barra 4h a sus 4 barras 1h constitutivas (forward-fill por bloque).
  - Position tracking: ON (no re-entrar mientras hay posicion abierta).

Grid M4:
  compatible_regimes en {
    "all"           — sin filtro (baseline, reproduce OfiContrarian original),
    "MeanReverting" — solo regimen lateral,
    "Trend"         — solo tendencia,
    "MR+Trend"      — MeanReverting OR Trend (excluye HighVol y Squeeze),
    "MR+Squeeze"    — MeanReverting OR Squeeze (baja volatilidad),
  }
  hold_bars in {4, 6, 8}
  Total: 5 × 3 = 15 configs

Gate pre-registrado (pre-ver numeros):
  - Sharpe IS >= 0.5 en al menos UNA config de regimen filtrado (no "all")
  - Trades >= 30 (poder estadistico minimo)
  - La config ganadora debe tener Sharpe IS materialmente > "all" (no ruido):
    diferencia > 0.15 — si el regimen no aporta, no justifica la complejidad.
  - Si pasa IS: correr OOS 2025 sobre la misma config (touch-once).

IS: 2021-2024. OOS: 2025.
Activos: BTCUSDT, ETHUSDT, SOLUSDT.
"""

import json
import numpy as np
import pandas as pd
from pathlib import Path
from itertools import product

# ── Configuracion ────────────────────────────────────────────────────────────────

DATA_DIR  = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\AggTrades\features")
MODEL_DIR = Path(r"F:\DesarrolloTrading\QuantConnect\Lean\Trading.Models\regime")

ASSETS = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
MODEL_MAP = {
    "BTCUSDT": MODEL_DIR / "BTCUSDT-perp-binance.hmm.json",
    "ETHUSDT": MODEL_DIR / "ETHUSDT-perp-binance.hmm.json",
    "SOLUSDT": MODEL_DIR / "BTCUSDT-perp-binance.hmm.json",  # proxy BTC
}

IS_START  = "2021-01-01"
IS_END    = "2024-12-31"
OOS_START = "2025-01-01"
OOS_END   = "2025-12-31"

# Parametros fijos del OfiContrarian (del M4 original — sin re-seleccionar)
OFI_WINDOW  = 24
OFI_LOW_PCT = 0.15   # entrar si percentil < 0.15

# Pure signal test sin costos — replica la metodologia de m4_ofi_contrarian.py.
# Los costos (0.28% round-trip) son identicos para todas las configs y no afectan
# la pregunta de investigacion: ¿el regimen mejora la señal cruda?
COST_RT = 0.0

REGIME_CONFIGS = {
    "all":        None,                              # sin filtro (baseline)
    "MeanRev":    {"MeanReverting"},
    "Trend":      {"Trend"},
    "MR+Trend":   {"MeanReverting", "Trend"},
    "MR+Squeeze": {"MeanReverting", "Squeeze"},
}
HOLD_VALUES = [4, 6, 8]

MIN_TRADES        = 30
SHARPE_GATE       = 0.5
MIN_IMPROVEMENT   = 0.15   # mejora minima sobre baseline para considerar el regimen util


# ── HMM helpers (replica de m4_rsi_hmm_squeeze.py) ───────────────────────────────

def load_hmm(path: Path) -> dict:
    with open(path) as f:
        return json.load(f)


def extract_features_4h(closes: np.ndarray) -> np.ndarray:
    """Replica exacta de FeatureExtractor.cs. Warm-up: 50 barras 4h."""
    rows = []
    for i in range(50, len(closes)):
        ret_log  = np.log(closes[i] / closes[i - 1])
        log_rets = np.log(closes[i - 20 + 1:i + 1] / closes[i - 20:i])
        vol_20   = log_rets.std(ddof=1)
        sma20    = closes[i - 19:i + 1].mean()
        sma50    = closes[i - 49:i + 1].mean()
        momentum = sma20 / sma50 - 1.0
        rows.append([ret_log, vol_20, momentum])
    return np.array(rows)


def classify_nearest_centroid(scaled: np.ndarray, model: dict) -> np.ndarray:
    means = np.array(model["EmissionMeans"])
    label_map = model["StateToRegimeLabel"]
    labels = []
    for feat in scaled:
        dists = np.sum((feat - means) ** 2, axis=1)
        labels.append(label_map[str(int(np.argmin(dists)))])
    return np.array(labels)


def build_regime_series_1h(df_1h: pd.DataFrame, model: dict) -> pd.Series:
    """
    Clasifica regimenes a 4h y propaga (ffill) a todas las barras 1h del bloque.
    Cada barra 4h [t, t+4h) asigna su etiqueta a las 4 barras 1h dentro del bloque.
    """
    # Resampleo close 1h → 4h (ultima vela del bloque = close al cierre de la 4h)
    close_4h = df_1h["close"].resample("4h").last().dropna()

    closes = close_4h.values
    if len(closes) < 51:
        return pd.Series("Unknown", index=df_1h.index)

    feats_raw    = extract_features_4h(closes)
    feats_scaled = (feats_raw - np.array(model["FeatureScalerMeans"])) / np.array(model["FeatureScalerStdDevs"])
    labels_4h    = classify_nearest_centroid(feats_scaled, model)

    # Alinear con el indice 4h (los primeros 50 no tienen clasificacion → Unknown)
    idx_4h = close_4h.index[50:]
    regime_4h = pd.Series(labels_4h, index=idx_4h)

    # Propagar a 1h: reindexar y rellenar hacia adelante (cada etiqueta rige hasta la siguiente)
    regime_1h = regime_4h.reindex(df_1h.index, method="ffill")
    regime_1h = regime_1h.fillna("Unknown")
    return regime_1h


# ── Carga de datos ────────────────────────────────────────────────────────────────

def load_features_1h(asset: str) -> pd.DataFrame:
    path = DATA_DIR / f"{asset}_1h_features.csv"
    df = pd.read_csv(path)
    df["bar"] = pd.to_datetime(df["bar"], utc=True)
    df = df.set_index("bar").sort_index()
    df["close"] = df["close"].astype(float)
    df["ofi"]   = pd.to_numeric(df["ofi"], errors="coerce")
    return df


# ── Señal OFI Contrarian con filtro de regimen ────────────────────────────────────

def run_ofi_contrarian(
    df: pd.DataFrame,
    regimes: pd.Series,
    hold: int,
    compatible: set | None,
    period_start: str,
    period_end: str,
) -> dict:
    """
    Backtest OFI Contrarian en bloques no-solapados con filtro de regimen opcional.
    compatible=None -> sin filtro (baseline).
    """
    start = pd.Timestamp(period_start, tz="UTC")
    end   = pd.Timestamp(period_end,   tz="UTC") + pd.Timedelta(hours=23)
    mask  = (df.index >= start) & (df.index <= end)
    df_p  = df.loc[mask].copy()
    reg_p = regimes.loc[mask]

    closes = df_p["close"].values
    ofis   = df_p["ofi"].values
    regs   = reg_p.values
    n      = len(df_p)

    # OFI rolling window (shift=1: solo historia previa, sin lookahead)
    ofi_series = pd.Series(ofis)
    # Replica exacta de m4_ofi_contrarian.py: percentil de ofi[t-1]
    # dentro de las OFI_WINDOW-1 barras previas (x[:-1] vs x[-1], raw=True).
    ofi_history = ofi_series.shift(1).rolling(OFI_WINDOW, min_periods=OFI_WINDOW).apply(
        lambda x: float((x[:-1] < x[-1]).sum()) / max(len(x) - 1, 1), raw=True
    )
    ofi_pct = ofi_history.values

    trades = []
    in_position = False
    bars_held   = 0
    entry_price = 0.0
    entry_idx   = -1

    i = 0
    while i < n:
        if in_position:
            bars_held += 1
            if bars_held >= hold:
                ret = (closes[i] - entry_price) / entry_price - COST_RT
                trades.append(ret)
                in_position = False
                bars_held   = 0
            i += 1
            continue

        # Condicion de entrada
        if np.isnan(ofi_pct[i]):
            i += 1
            continue

        regime_ok = (compatible is None) or (regs[i] in compatible)
        if ofi_pct[i] < OFI_LOW_PCT and regime_ok:
            in_position = True
            bars_held   = 0
            entry_price = closes[i]
            entry_idx   = i

        i += 1

    if len(trades) < 5:
        return {"sharpe": np.nan, "net_pct": np.nan, "n_trades": len(trades), "win_rate": np.nan}

    rets = np.array(trades)
    # Annualize: trades por año en la ventana
    years = (pd.Timestamp(period_end, tz="UTC") - pd.Timestamp(period_start, tz="UTC")).days / 365.25
    tpy   = len(trades) / years
    s     = (rets.mean() / rets.std(ddof=1)) * np.sqrt(tpy) if rets.std(ddof=1) > 0 else np.nan
    return {
        "sharpe":   round(s, 3),
        "net_pct":  round(((1 + rets).prod() - 1) * 100, 2),
        "n_trades": len(trades),
        "win_rate": round((rets > 0).mean(), 3),
    }


# ── Runner ────────────────────────────────────────────────────────────────────────

def run_grid(
    panel: dict,
    regimes: dict,
    period_start: str,
    period_end: str,
    label: str,
) -> pd.DataFrame:
    rows = []
    for (reg_name, compatible), hold in product(REGIME_CONFIGS.items(), HOLD_VALUES):
        sharpes, trades_list = [], []
        for asset in ASSETS:
            r = run_ofi_contrarian(
                panel[asset], regimes[asset],
                hold, compatible, period_start, period_end
            )
            sharpes.append(r["sharpe"])
            trades_list.append(r["n_trades"])
        # Gate de activos: >= 2/3 con Sharpe valido y trades suficientes
        valid = [(s, t) for s, t in zip(sharpes, trades_list)
                 if not np.isnan(s) and t >= MIN_TRADES]
        mean_s = np.nanmean(sharpes) if sharpes else np.nan
        rows.append({
            "regime":    reg_name,
            "hold":      hold,
            "sharpe_btc": sharpes[0],
            "sharpe_eth": sharpes[1],
            "sharpe_sol": sharpes[2],
            "sharpe_mean": round(mean_s, 3),
            "trades_btc": trades_list[0],
            "trades_eth": trades_list[1],
            "trades_sol": trades_list[2],
            "assets_ok":  len(valid),
        })

    df = pd.DataFrame(rows).sort_values("sharpe_mean", ascending=False)
    print(f"\n{'='*80}")
    print(f"  {label} -- OFI Contrarian condicionado por regimen HMM")
    print(f"{'='*80}")
    print(df.to_string(index=False))
    return df


def print_regime_distribution(regimes: dict):
    print("\nDistribucion de regimenes (IS 2021-2024):")
    for asset in ASSETS:
        start = pd.Timestamp(IS_START, tz="UTC")
        end   = pd.Timestamp(IS_END,   tz="UTC") + pd.Timedelta(hours=23)
        r = regimes[asset]
        r_is = r[(r.index >= start) & (r.index <= end)]
        counts = r_is.value_counts()
        total  = len(r_is)
        parts  = [f"{k}: {v/total:.0%}" for k, v in counts.items()]
        print(f"  {asset}: {' | '.join(parts)}")


def evaluate_gate(is_results: pd.DataFrame) -> tuple[bool, str | None]:
    baseline = is_results[is_results["regime"] == "all"]["sharpe_mean"].values
    baseline_s = baseline[0] if len(baseline) > 0 else np.nan

    filtered = is_results[is_results["regime"] != "all"]
    best_row  = filtered.iloc[0] if len(filtered) > 0 else None

    print(f"\n{'-'*80}")
    print("  GATE PRE-REGISTRADO (IS 2021-2024)")
    print(f"{'-'*80}")
    print(f"  Baseline (sin regimen, hold=8): Sharpe medio = {baseline_s:.3f}")

    if best_row is None:
        print("  Sin configs filtradas. NO-GO.")
        return False, None

    best_s      = best_row["sharpe_mean"]
    improvement = best_s - baseline_s
    trades_ok   = (best_row["assets_ok"] >= 2)

    print(f"  Mejor config filtrada: regime={best_row.regime} hold={best_row.hold}")
    print(f"    Sharpe medio = {best_s:.3f}  (gate >= {SHARPE_GATE})")
    print(f"    Mejora vs baseline = +{improvement:.3f}  (gate > {MIN_IMPROVEMENT})")
    print(f"    Activos con trades >= {MIN_TRADES}: {int(best_row.assets_ok)}/3  (gate >= 2)")

    sharpe_ok = best_s >= SHARPE_GATE
    improv_ok = improvement > MIN_IMPROVEMENT
    trades_ok_g = trades_ok

    print(f"\n  Sharpe >= {SHARPE_GATE}          : {'PASA' if sharpe_ok  else 'FALLA'}")
    print(f"  Mejora > {MIN_IMPROVEMENT}          : {'PASA' if improv_ok  else 'FALLA'}")
    print(f"  >= 2 activos con trades  : {'PASA' if trades_ok_g else 'FALLA'}")

    go = sharpe_ok and improv_ok and trades_ok_g
    best_key = f"{best_row.regime}_hold{int(best_row.hold)}"
    print(f"\n  VEREDICTO IS: {'GO -- correr OOS config ' + best_key if go else 'NO-GO -- kill, pasar a eje 3'}")
    return go, best_key if go else None


# ── Main ─────────────────────────────────────────────────────────────────────────

def main():
    print("Cargando features 1h...")
    panel = {}
    for asset in ASSETS:
        panel[asset] = load_features_1h(asset)
        print(f"  {asset}: {len(panel[asset]):,} barras")

    print("\nClasificando regimenes HMM (4h)...")
    regimes = {}
    for asset in ASSETS:
        model = load_hmm(MODEL_MAP[asset])
        regimes[asset] = build_regime_series_1h(panel[asset], model)
        proxy = " (proxy BTC)" if asset == "SOLUSDT" else ""
        print(f"  {asset}{proxy}: regimen clasificado")

    print_regime_distribution(regimes)

    # IS
    is_results = run_grid(panel, regimes, IS_START, IS_END, "IS 2021-2024")

    # Gate
    go, best_config = evaluate_gate(is_results)

    if go:
        print(f"\nCorriendo OOS 2025 (touch-once, config: {best_config})...")
        oos_results = run_grid(panel, regimes, OOS_START, OOS_END, "OOS 2025")

        # Extraer la config ganadora en OOS
        reg_name, hold_str = best_config.rsplit("_hold", 1)
        hold_val = int(hold_str)
        oos_row = oos_results[
            (oos_results["regime"] == reg_name) & (oos_results["hold"] == hold_val)
        ]
        if not oos_row.empty:
            r = oos_row.iloc[0]
            print(f"\n  Config {best_config} en OOS:")
            print(f"    Sharpe medio OOS = {r.sharpe_mean:.3f}")
            print(f"    BTC: {r.sharpe_btc:.3f} | ETH: {r.sharpe_eth:.3f} | SOL: {r.sharpe_sol:.3f}")
    else:
        print("\n  IS no pasa el gate. OOS no se corre.")
        print("  Documentar en strategy_experiments.md y pasar a eje 3 (sub-hora).")


if __name__ == "__main__":
    main()
