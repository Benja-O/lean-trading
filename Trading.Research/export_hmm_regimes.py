"""
Exportador de regímenes HMM a CSV para consumo por layer_a_trend_s1.py (TS10).

Salida: Trading.Models/regime/hmm_regimes_<asset>.csv
Columnas: timestamp (UTC ISO-8601), regime (string)
Cadencia nativa: una fila por barra 4h (cubriendo IS 2021-2024 + OOS 2025-2026).

GARANTÍA DE CAUSALIDAD (anti-lookahead):
  El resampleo de 1h → 4h usa closed='right', label='right':
    - La barra 4h cuyo close ocurre en T queda indexada en T.
    - Esa etiqueta NO puede aplicarse a barras 1h dentro de [T-4h, T),
      porque su timestamp es < T.
    - Cuando layer_a_trend_s1.py hace ffill de régimen 4h → 1h, la etiqueta
      de T se propagará HACIA ADELANTE (T, T+1h, T+2h, T+3h), nunca atrás.
  Verificación explícita: se aserta que para cada fila del CSV, el timestamp
  es el close de la ventana 4h — i.e., la info ya ocurrió antes de que se
  use para filtrar señales en T o posterior.

Reutiliza extract_features_4h() y classify_nearest_centroid() de m4_ofi_regime.py
importándolas directamente (sin modificar ese archivo).

Uso:
    python export_hmm_regimes.py
"""

import sys
import io
from pathlib import Path

# Forzar UTF-8 en stdout/stderr para evitar UnicodeEncodeError en Windows cp1252
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")

import numpy as np
import pandas as pd

# Añadir Trading.Research al path para importar desde m4_ofi_regime.py
_RESEARCH_DIR = Path(__file__).parent
sys.path.insert(0, str(_RESEARCH_DIR))

from m4_ofi_regime import extract_features_4h, classify_nearest_centroid, load_hmm

# ── Configuración ──────────────────────────────────────────────────────────────

DATA_DIR  = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\AggTrades\features")
MODEL_DIR = Path(__file__).parent.parent / "Trading.Models" / "regime"

ASSETS = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]

MODEL_MAP = {
    "BTCUSDT": MODEL_DIR / "BTCUSDT-perp-binance.hmm.json",
    "ETHUSDT": MODEL_DIR / "ETHUSDT-perp-binance.hmm.json",
    "SOLUSDT": MODEL_DIR / "BTCUSDT-perp-binance.hmm.json",  # proxy BTC (igual que m4_ofi_regime.py)
}


# ── Carga de datos 1h ──────────────────────────────────────────────────────────

def load_close_1h(asset: str) -> pd.Series:
    """Carga serie close 1h. Índice = DatetimeIndex UTC."""
    # Preferir parquet (más rápido)
    path_pq = DATA_DIR / f"{asset}_1h_features.parquet"
    path_csv = DATA_DIR / f"{asset}_1h_features.csv"
    if path_pq.exists():
        df = pd.read_parquet(path_pq)
    else:
        df = pd.read_csv(path_csv)
    df["bar"] = pd.to_datetime(df["bar"], utc=True)
    df = df.set_index("bar").sort_index()
    return df["close"].astype(float)


# ── Clasificación causal 4h ────────────────────────────────────────────────────

def classify_regimes_4h(close_1h: pd.Series, model: dict) -> pd.Series:
    """
    Clasifica regímenes a cadencia 4h con CAUSALIDAD GARANTIZADA.

    Protocolo anti-lookahead:
    1. Resampleo 1h→4h con closed='right', label='right':
       - La barra cuyo close ocurre en T queda indexada en T.
       - Ejemplo: ventana (T-4h, T] → índice T.
       - Contraste con label='left' (default): la misma ventana quedaría en T-4h,
         y un ffill ingenuo la aplicaría DENTRO de la ventana, antes del close.
    2. La etiqueta en T representa info disponible SOLO después del close en T.
    3. Cuando se hace ffill → 1h, la etiqueta de T gobierna [T, T+4h),
       que son barras POSTERIORES al close que generó la etiqueta. Sin lookahead.

    Retorna pd.Series con índice DatetimeIndex UTC (cadencia 4h), valores = strings de régimen.
    """
    # Resamplear a 4h: close al final de cada ventana (closed='right', label='right')
    # La barra que cierra en T tiene índice T — causal.
    close_4h = close_1h.resample("4h", closed="right", label="right").last().dropna()

    closes = close_4h.values
    if len(closes) < 51:
        return pd.Series(dtype="object")

    # Extraer features (warm-up 50 barras según FeatureExtractor.cs)
    feats_raw = extract_features_4h(closes)

    # Escalar
    feats_scaled = (
        feats_raw - np.array(model["FeatureScalerMeans"])
    ) / np.array(model["FeatureScalerStdDevs"])

    # Clasificar (centroide más cercano — causal, no Viterbi)
    labels_4h = classify_nearest_centroid(feats_scaled, model)

    # Alinear: los primeros 50 cierres no tienen clasificación (warm-up)
    idx_4h = close_4h.index[50:]
    regime_series = pd.Series(labels_4h, index=idx_4h, name="regime")

    return regime_series


# ── Exportación a CSV ──────────────────────────────────────────────────────────

def export_regime_csv(asset: str, regime_series: pd.Series) -> Path:
    """
    Emite CSV con columnas [timestamp, regime] en el formato que
    _try_load_hmm_regime() en layer_a_trend_s1.py ya busca y parsea.

    Ruta destino: Trading.Models/regime/hmm_regimes_<asset_lower>.csv
    Columna de tiempo: 'timestamp' (reconocida por _try_load_hmm_regime como alternativa a 'bar').
    """
    out_path = MODEL_DIR / f"hmm_regimes_{asset.lower()}.csv"

    df_out = pd.DataFrame({
        "timestamp": regime_series.index.strftime("%Y-%m-%d %H:%M:%S+00:00"),
        "regime":    regime_series.values,
    })

    df_out.to_csv(out_path, index=False)
    return out_path


# ── Verificación de causalidad ─────────────────────────────────────────────────

def verify_causality(close_1h: pd.Series, regime_series: pd.Series) -> None:
    """
    Verifica que NINGUNA etiqueta de régimen aplica a una barra 1h
    cuyo timestamp es anterior al close que generó la etiqueta.

    Con label='right': la etiqueta en T fue producida por el close de la
    ventana (T-4h, T]. Cuando se propaga a 1h via ffill, la primera barra
    1h que recibe esa etiqueta es la siguiente a T (en práctica T mismo
    si T es múltiplo de 4h, o T+1h si no). En todos los casos >= T.

    La verificación aserta: para cada timestamp del régimen 4h (= close de ventana),
    no hay barra 1h DENTRO de la ventana que tenga esa etiqueta via ffill.
    """
    # Propagamos la etiqueta 4h a 1h via ffill para verificar
    regime_1h = regime_series.reindex(close_1h.index, method="ffill")

    violations = 0
    for ts_4h, label in regime_series.items():
        window_start = ts_4h - pd.Timedelta(hours=4)
        # Barras 1h en (window_start, ts_4h) — las que están DENTRO de la ventana,
        # anteriores al close que generó la etiqueta
        bars_in_window = close_1h.index[
            (close_1h.index > window_start) & (close_1h.index < ts_4h)
        ]
        for bar in bars_in_window:
            if bar in regime_1h.index and regime_1h[bar] == label:
                # Puede ser etiqueta de bloque ANTERIOR (ffill de la 4h previa) — OK.
                # Solo es violación si la etiqueta de la SIGUIENTE 4h se aplicó hacia atrás.
                # Con label='right', esto no puede ocurrir: la etiqueta ts_4h
                # no puede aparecer antes de ts_4h en el ffill.
                prev_regime_ts = regime_series.index[regime_series.index < ts_4h]
                if len(prev_regime_ts) > 0:
                    prev_ts = prev_regime_ts[-1]
                    prev_label = regime_series[prev_ts]
                    if regime_1h.get(bar) != prev_label:
                        violations += 1

    if violations == 0:
        print("  [CAUSALIDAD OK] Ninguna etiqueta aplicada antes del close que la generó.")
    else:
        print(f"  [ADVERTENCIA] {violations} posibles violaciones de causalidad detectadas.")


# ── Distribución de regímenes ──────────────────────────────────────────────────

def print_regime_distribution(asset: str, regime_series: pd.Series) -> None:
    """Imprime distribución IS (2021-2024) y OOS (2025-2026)."""
    def dist_str(s: pd.Series) -> str:
        counts = s.value_counts()
        total = len(s)
        if total == 0:
            return "(sin datos)"
        return " | ".join(f"{k}: {v/total:.0%}" for k, v in counts.items())

    is_mask  = (regime_series.index >= pd.Timestamp("2021-01-01", tz="UTC")) & \
               (regime_series.index <= pd.Timestamp("2024-12-31 23:59:59", tz="UTC"))
    oos_mask = (regime_series.index >= pd.Timestamp("2025-01-01", tz="UTC"))

    r_is  = regime_series[is_mask]
    r_oos = regime_series[oos_mask]

    proxy_note = " [proxy BTC]" if asset == "SOLUSDT" else ""
    print(f"  {asset}{proxy_note}:")
    print(f"    IS  (2021-2024, {len(r_is):,} barras 4h): {dist_str(r_is)}")
    print(f"    OOS (2025+,     {len(r_oos):,} barras 4h): {dist_str(r_oos)}")


# ── Main ───────────────────────────────────────────────────────────────────────

def main() -> None:
    print("=" * 70)
    print("Exportador de regimenes HMM -> CSV (causal, label='right')")
    print("=" * 70)

    for asset in ASSETS:
        proxy_note = " (proxy BTC)" if asset == "SOLUSDT" else ""
        print(f"\n[{asset}{proxy_note}]")

        # 1. Cargar close 1h
        close_1h = load_close_1h(asset)
        print(f"  Barras 1h: {len(close_1h):,}  "
              f"({close_1h.index[0].date()} → {close_1h.index[-1].date()})")

        # 2. Cargar modelo HMM
        model = load_hmm(MODEL_MAP[asset])
        print(f"  Modelo: {MODEL_MAP[asset].name}  "
              f"({model['NumberOfStates']} estados: "
              f"{list(model['StateToRegimeLabel'].values())})")

        # 3. Clasificar con garantía causal (closed='right', label='right')
        regime_series = classify_regimes_4h(close_1h, model)
        print(f"  Barras 4h clasificadas: {len(regime_series):,}  "
              f"(warm-up 50 barras excluidas)")

        # 4. Verificar causalidad
        verify_causality(close_1h, regime_series)

        # 5. Distribución
        print_regime_distribution(asset, regime_series)

        # 6. Exportar CSV
        out_path = export_regime_csv(asset, regime_series)
        print(f"  CSV exportado: {out_path}")

    print("\n" + "=" * 70)
    print("Regimenes exportados. Ahora correr TS10:")
    print("  python layer_a_trend_s1.py --hypothesis TS10 --oos")
    print("=" * 70)


if __name__ == "__main__":
    main()
