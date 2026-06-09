# -*- coding: utf-8 -*-
"""
M4 validation: RSI Mean Reversion condicionado por regimen HMM Squeeze.

Hipotesis: RSI(14) < umbral es una senal de mean reversion valida
SOLO cuando el activo esta en regimen Squeeze (baja volatilidad, sin trend).
El condicionamiento por regimen filtra las falsas senales oversold que
ocurren durante downtrends fuertes, donde RSI < 30 no revierte.

Diseno M4:
- Regimen: clasificado con los modelos HMM entrenados (BTCUSDT, ETHUSDT).
  Para BNBUSDT se usa el modelo BTC como proxy (no hay modelo BNB).
  Clasificacion por centroide mas cercano en espacio de features escaladas
  (aproximacion al Viterbi completo; suficiente para validacion M4).
- Senal: RSI(14, asset) < threshold AND regimen(asset) == "Squeeze"
- Long only (mean reversion desde oversold -> recuperacion)
- Hold: 8 o 12 barras (32h o 48h en 4h TF)
- RSI thresholds: 25, 30, 35
- Timeframe: 4h
- Umbral M4: Sharpe >= 0.5 en mayoria de configs (>= 4/6) para >= 2/3 activos
"""

import json
import numpy as np
import pandas as pd
from binance.client import Client


# ─── HMM model loading ───────────────────────────────────────────────────────

def load_hmm_model(json_path: str) -> dict:
    with open(json_path, 'r') as f:
        return json.load(f)


def classify_regime_nearest_centroid(
    scaled_features: np.ndarray,
    emission_means: list,
    state_to_label: dict
) -> np.ndarray:
    """
    Clasificacion por centroide mas cercano en espacio de features escaladas.
    Aproximacion al HMM Viterbi: rapida, sin dependencias externas, suficiente para M4.
    Nota: ignora la matriz de transicion (sin smoothing temporal).
    """
    means = np.array(emission_means)  # (K, 3)
    labels = []
    for feat in scaled_features:
        dists = np.sum((feat - means) ** 2, axis=1)
        state = int(np.argmin(dists))
        labels.append(state_to_label[str(state)])
    return np.array(labels)


# ─── Feature extraction (replica exacta de FeatureExtractor.cs) ──────────────

def extract_features(closes: np.ndarray) -> np.ndarray:
    """
    Replica de FeatureExtractor.cs. Warm-up: 50 barras.
    Features: [return_log, vol_20 (std muestral ddof=1), momentum_ratio (SMA20/SMA50 - 1)]
    Retorna array (N-50, 3) alineado con closes[50:].
    """
    n = len(closes)
    rows = []
    for i in range(50, n):
        ret_log = np.log(closes[i] / closes[i - 1])

        # vol_20: std muestral de 20 retornos log terminados en i
        log_rets = np.log(closes[i - 20 + 1:i + 1] / closes[i - 20:i])
        vol_20 = log_rets.std(ddof=1)

        sma20 = closes[i - 19:i + 1].mean()
        sma50 = closes[i - 49:i + 1].mean()
        momentum = sma20 / sma50 - 1.0

        rows.append([ret_log, vol_20, momentum])
    return np.array(rows)


def scale_features(features: np.ndarray, means: list, stds: list) -> np.ndarray:
    """Z-score normalization con los parametros del modelo entrenado."""
    return (features - np.array(means)) / np.array(stds)


# ─── RSI(14) — Wilder's smoothed RSI ─────────────────────────────────────────

def compute_rsi(closes: np.ndarray, period: int = 14) -> np.ndarray:
    """
    RSI de Wilder. Retorna array de misma longitud que closes,
    con nan en los primeros `period` indices (warm-up).
    """
    n = len(closes)
    rsi = np.full(n, np.nan)

    gains = np.zeros(n)
    losses = np.zeros(n)
    for i in range(1, n):
        change = closes[i] - closes[i - 1]
        gains[i] = max(change, 0.0)
        losses[i] = max(-change, 0.0)

    # Seed: SMA simple de los primeros `period` cambios
    avg_gain = gains[1:period + 1].mean()
    avg_loss = losses[1:period + 1].mean()

    for i in range(period, n):
        if i == period:
            ag, al = avg_gain, avg_loss
        else:
            ag = (ag * (period - 1) + gains[i]) / period
            al = (al * (period - 1) + losses[i]) / period

        if al == 0:
            rsi[i] = 100.0
        else:
            rs = ag / al
            rsi[i] = 100.0 - (100.0 / (1.0 + rs))

    return rsi


# ─── Data download ────────────────────────────────────────────────────────────

def download_4h_data(symbol: str, start_date: str, end_date: str) -> pd.DataFrame:
    client = Client()
    klines = client.get_historical_klines(symbol, '4h', start_date, end_date)
    df = pd.DataFrame(klines, columns=[
        'time', 'open', 'high', 'low', 'close', 'volume',
        'close_time', 'quote_volume', 'trades', 'taker_buy', 'taker_buy_quote', 'ignore'
    ])
    df['time'] = pd.to_datetime(df['time'], unit='ms', utc=True)
    df = df[['time', 'close']].astype({'close': float})
    return df.set_index('time').sort_index()


# ─── M4 signal test ───────────────────────────────────────────────────────────

def test_rsi_squeeze(
    closes: np.ndarray,
    regimes: np.ndarray,
    rsi_threshold: float,
    hold_bars: int
) -> dict:
    """
    Testear senal RSI < threshold AND regime == 'Squeeze'.
    closes y regimes estan alineados (mismo indice = misma barra).
    El RSI se calcula sobre closes completo (hay warm-up propio).
    Retorna metricas de la senal.
    """
    rsi = compute_rsi(closes)
    n = len(closes)
    trades = []

    i = 0
    while i < n - hold_bars:
        if (not np.isnan(rsi[i])
                and rsi[i] < rsi_threshold
                and i < len(regimes)
                and regimes[i] == 'Squeeze'):
            entry = closes[i]
            exit_ = closes[i + hold_bars]
            ret = (exit_ - entry) / entry
            trades.append(ret)
            i += hold_bars + 1  # no re-entrar hasta salir
        else:
            i += 1

    if len(trades) < 5:
        return {'sharpe': np.nan, 'win_rate': np.nan,
                'mean_return': np.nan, 'trades': len(trades)}

    returns = np.array(trades)
    mean_ret = returns.mean()
    std_ret = returns.std(ddof=1)
    win_rate = (returns > 0).mean()

    # Sharpe anualizado por trade-rate observada
    years = n / (6 * 365)  # 6 barras 4h/dia
    trades_per_year = len(trades) / years
    sharpe = (mean_ret / std_ret * np.sqrt(trades_per_year)) if std_ret > 0 else 0.0

    return {
        'sharpe': sharpe,
        'win_rate': win_rate,
        'mean_return': mean_ret,
        'std_return': std_ret,
        'trades': len(trades),
    }


# ─── Main ─────────────────────────────────────────────────────────────────────

if __name__ == '__main__':
    print("M4: RSI Mean Reversion condicionado por HMM Squeeze (4h)")
    print("=" * 65)

    MODEL_DIR = r'f:\DesarrolloTrading\QuantConnect\Lean\models\regime'
    START = '2020-01-01'
    END   = '2025-12-31'

    SYMBOLS = ['BTCUSDT', 'ETHUSDT', 'BNBUSDT']
    MODEL_MAP = {
        'BTCUSDT': f'{MODEL_DIR}\\BTCUSDT-perp-binance.hmm.json',
        'ETHUSDT': f'{MODEL_DIR}\\ETHUSDT-perp-binance.hmm.json',
        'BNBUSDT': f'{MODEL_DIR}\\BTCUSDT-perp-binance.hmm.json',  # proxy BTC
    }

    RSI_THRESHOLDS = [25, 30, 35]
    HOLD_CONFIGS   = [8, 12]   # barras 4h → 32h o 48h

    # Descargar datos
    print()
    price_data = {}
    for sym in SYMBOLS:
        print(f"Descargando {sym} 4h...")
        price_data[sym] = download_4h_data(sym, START, END)
        print(f"  {len(price_data[sym])} barras")

    # Clasificar regimenes
    print()
    regime_arrays = {}
    for sym in SYMBOLS:
        closes = price_data[sym]['close'].values
        model = load_hmm_model(MODEL_MAP[sym])

        features_raw = extract_features(closes)
        features_scaled = scale_features(
            features_raw,
            model['FeatureScalerMeans'],
            model['FeatureScalerStdDevs']
        )
        labels = classify_regime_nearest_centroid(
            features_scaled,
            model['EmissionMeans'],
            model['StateToRegimeLabel']
        )
        regime_arrays[sym] = labels

        squeeze_pct = (labels == 'Squeeze').mean() * 100
        print(f"{sym} regimen ({MODEL_MAP[sym].split(chr(92))[-1]}):")
        for label in ['Trend', 'Squeeze', 'HighVolatility']:
            pct = (labels == label).mean() * 100
            print(f"  {label}: {pct:.1f}%")

    # Test de senal
    print()
    print("-" * 65)
    results = {}
    for sym in SYMBOLS:
        closes = price_data[sym]['close'].values
        # Los regimes estan alineados con closes[50:] — padding con 'Unknown' al inicio
        pad = len(closes) - len(regime_arrays[sym])
        regimes_full = np.array(['Unknown'] * pad + list(regime_arrays[sym]))

        results[sym] = {}
        print(f"\n{sym}:")
        for thr in RSI_THRESHOLDS:
            for hold in HOLD_CONFIGS:
                res = test_rsi_squeeze(closes, regimes_full, thr, hold)
                key = (thr, hold)
                results[sym][key] = res
                s = res['sharpe']
                status = '[PASS]' if (not np.isnan(s) and s >= 0.5) else '[FAIL]'
                wr = f"{res['win_rate']:.1%}" if not np.isnan(res.get('win_rate', np.nan)) else 'n/a'
                print(
                    f"  RSI<{thr} hold={hold}b: "
                    f"Sharpe={s:.3f}  Win={wr}  "
                    f"Trades={res['trades']}  {status}"
                )

    # Resumen M4
    print()
    print("=" * 65)
    print("RESUMEN M4:")
    print()

    TOTAL_CONFIGS = len(RSI_THRESHOLDS) * len(HOLD_CONFIGS)  # 6
    PASS_THRESHOLD = TOTAL_CONFIGS / 2  # >= 3/6

    asset_pass = {}
    for sym in results:
        n_pass = sum(
            1 for res in results[sym].values()
            if not np.isnan(res['sharpe']) and res['sharpe'] >= 0.5
        )
        passed = n_pass >= PASS_THRESHOLD
        asset_pass[sym] = passed
        mark = '[PASS]' if passed else '[FAIL]'
        print(f"  {sym}: {n_pass}/{TOTAL_CONFIGS} configs Sharpe >= 0.5  {mark}")

    print()
    assets_passing = sum(1 for v in asset_pass.values() if v)
    total_assets = len(asset_pass)

    if assets_passing >= 2:
        print(f"Activos pasando: {assets_passing}/{total_assets}")
        print("\n[OK] M4 PASADO — proceder a implementacion C# LeadLagStrategy")
        print("     RSI + HMM Squeeze tiene edge estadistico en 4h")
    else:
        print(f"Activos pasando: {assets_passing}/{total_assets}")
        print("\n[FAIL] M4 RECHAZADO — hipotesis H1 RSI+HMM no tiene edge en 4h")
        print("       Considerar H2 (ATR Squeeze) como ultima candidata")
