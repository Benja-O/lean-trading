# -*- coding: utf-8 -*-
"""
M4 validation: H2 — ATR Compression Breakout.

Hipotesis: el mercado alterna entre fases de compresion (ATR bajo, coiling)
y expansion (ATR alto). Un rompimiento de rango durante compresion ATR
predice un movimiento direccional significativo.

Diseno M4:
- Compresion: ATR(14) < percentil P del ATR rolling 100 barras (P = 25, 35)
- Senal Long:  cierre > max(close[-N:]) AND en compresion -> entrada Long
- Senal Short: cierre < min(close[-N:]) AND en compresion -> entrada Short
- N (lookback precio): 10, 20 barras
- Hold: 4, 8 barras 4h
- Timeframe: 4h, activos: BTC, ETH, BNB, periodo 2020-2025
- Gate M4: >= 5/8 configs Sharpe >= 0.5 en >= 2/3 activos

Diferencia con Donchian (candidata 1):
  Donchian se dispara ante cualquier nuevo maximo/minimo de lookback largo (20-126 barras).
  H2 solo se dispara cuando el ATR esta comprimido (fase de coiling), usando un
  lookback de precio mucho mas corto. El filtro ATR elimina el ruido de rupturas
  durante periodos de alta volatilidad donde las rupturas son menos predecibles.
"""

import numpy as np
import pandas as pd
from binance.client import Client


# ─── ATR(14) ─────────────────────────────────────────────────────────────────

def compute_atr(highs: np.ndarray, lows: np.ndarray, closes: np.ndarray,
                period: int = 14) -> np.ndarray:
    """
    Average True Range de Wilder (smoothing exponencial).
    Retorna array de misma longitud, con nan en los primeros `period` indices.
    """
    n = len(closes)
    tr = np.full(n, np.nan)
    atr = np.full(n, np.nan)

    for i in range(1, n):
        hl = highs[i] - lows[i]
        hc = abs(highs[i] - closes[i - 1])
        lc = abs(lows[i] - closes[i - 1])
        tr[i] = max(hl, hc, lc)

    # Seed: SMA simple de los primeros `period` TR
    atr[period] = np.nanmean(tr[1:period + 1])
    for i in range(period + 1, n):
        atr[i] = (atr[i - 1] * (period - 1) + tr[i]) / period

    return atr


# ─── Rolling percentile ───────────────────────────────────────────────────────

def rolling_percentile(values: np.ndarray, window: int,
                        percentile: float) -> np.ndarray:
    """
    Percentil rolling de longitud `window` sobre `values`.
    Retorna array de misma longitud, con nan donde la ventana no esta completa.
    """
    n = len(values)
    result = np.full(n, np.nan)
    for i in range(window - 1, n):
        result[i] = np.percentile(values[i - window + 1:i + 1], percentile)
    return result


# ─── Data download ────────────────────────────────────────────────────────────

def download_4h_ohlcv(symbol: str, start_date: str, end_date: str) -> pd.DataFrame:
    client = Client()
    klines = client.get_historical_klines(symbol, '4h', start_date, end_date)
    df = pd.DataFrame(klines, columns=[
        'time', 'open', 'high', 'low', 'close', 'volume',
        'close_time', 'quote_volume', 'trades', 'taker_buy', 'taker_buy_quote', 'ignore'
    ])
    df['time'] = pd.to_datetime(df['time'], unit='ms', utc=True)
    for col in ['open', 'high', 'low', 'close', 'volume']:
        df[col] = df[col].astype(float)
    return df[['time', 'open', 'high', 'low', 'close', 'volume']].set_index('time').sort_index()


# ─── M4 signal test ───────────────────────────────────────────────────────────

def test_atr_compression_breakout(
    closes: np.ndarray,
    highs: np.ndarray,
    lows: np.ndarray,
    atr_percentile: float,
    price_lookback: int,
    hold_bars: int,
    atr_window: int = 100,
) -> dict:
    """
    Test senal: ATR comprimido AND rompimiento de rango de precio.

    Compresion: ATR(14)[i] < percentil(atr_percentile) del ATR rolling 100 barras
    Long:  close[i] > max(close[i-price_lookback : i]) AND compresion
    Short: close[i] < min(close[i-price_lookback : i]) AND compresion

    Nota: la condicion de rompimiento usa i (barra actual) contra la ventana
    [i-lookback, i-1] para evitar look-ahead (se excluye la barra actual del rango).
    """
    atr = compute_atr(highs, lows, closes)
    atr_pct_threshold = rolling_percentile(atr, atr_window, atr_percentile)

    n = len(closes)
    trades = []

    i = price_lookback  # warm-up minimo para la ventana de precio
    while i < n - hold_bars:
        if np.isnan(atr[i]) or np.isnan(atr_pct_threshold[i]):
            i += 1
            continue

        in_compression = atr[i] < atr_pct_threshold[i]
        if not in_compression:
            i += 1
            continue

        # Rango de precio ANTES de la barra i (sin look-ahead)
        price_range = closes[i - price_lookback: i]
        range_high = np.max(price_range)
        range_low  = np.min(price_range)

        entry = closes[i]
        exit_ = closes[i + hold_bars]

        if entry > range_high:
            # Rompimiento alcista — Long
            ret = (exit_ - entry) / entry
            trades.append(('long', ret))
            i += hold_bars + 1
        elif entry < range_low:
            # Rompimiento bajista — Short
            ret = (entry - exit_) / entry
            trades.append(('short', ret))
            i += hold_bars + 1
        else:
            i += 1

    if len(trades) < 5:
        return {'sharpe': np.nan, 'win_rate': np.nan,
                'mean_return': np.nan, 'trades': len(trades)}

    returns = np.array([r for _, r in trades])
    mean_ret = returns.mean()
    std_ret  = returns.std(ddof=1)
    win_rate = (returns > 0).mean()

    years = n / (6 * 365)  # 6 barras 4h/dia
    trades_per_year = len(trades) / years
    sharpe = (mean_ret / std_ret * np.sqrt(trades_per_year)) if std_ret > 0 else 0.0

    long_count  = sum(1 for d, _ in trades if d == 'long')
    short_count = sum(1 for d, _ in trades if d == 'short')

    return {
        'sharpe':       sharpe,
        'win_rate':     win_rate,
        'mean_return':  mean_ret,
        'std_return':   std_ret,
        'trades':       len(trades),
        'long_trades':  long_count,
        'short_trades': short_count,
    }


# ─── Main ─────────────────────────────────────────────────────────────────────

if __name__ == '__main__':
    print("M4: H2 — ATR Compression Breakout (4h, bidireccional)")
    print("=" * 65)

    START   = '2020-01-01'
    END     = '2025-12-31'
    SYMBOLS = ['BTCUSDT', 'ETHUSDT', 'BNBUSDT']

    ATR_PERCENTILES  = [25, 35]   # nivel de compresion (mas bajo = mas comprimido)
    PRICE_LOOKBACKS  = [10, 20]   # barras para definir el rango de precio
    HOLD_CONFIGS     = [4, 8]     # barras de hold
    TOTAL_CONFIGS    = len(ATR_PERCENTILES) * len(PRICE_LOOKBACKS) * len(HOLD_CONFIGS)  # 8

    # Descargar datos
    print()
    ohlcv_data = {}
    for sym in SYMBOLS:
        print(f"Descargando {sym} 4h OHLCV...")
        ohlcv_data[sym] = download_4h_ohlcv(sym, START, END)
        print(f"  {len(ohlcv_data[sym])} barras")

    # Test de senal
    print()
    print("-" * 65)
    results = {}
    for sym in SYMBOLS:
        df = ohlcv_data[sym]
        closes = df['close'].values
        highs  = df['high'].values
        lows   = df['low'].values

        results[sym] = {}
        print(f"\n{sym}:")
        for pct in ATR_PERCENTILES:
            for lookback in PRICE_LOOKBACKS:
                for hold in HOLD_CONFIGS:
                    res = test_atr_compression_breakout(
                        closes, highs, lows,
                        atr_percentile=pct,
                        price_lookback=lookback,
                        hold_bars=hold,
                    )
                    key = (pct, lookback, hold)
                    results[sym][key] = res
                    s   = res['sharpe']
                    wr  = res['win_rate']
                    status = '[PASS]' if (not np.isnan(s) and s >= 0.5) else '[FAIL]'
                    wr_str = f"{wr:.1%}" if not np.isnan(wr) else 'n/a'
                    print(
                        f"  ATR<P{pct} look={lookback}b hold={hold}b: "
                        f"Sharpe={s:+.3f}  Win={wr_str}  "
                        f"Trades={res['trades']:3d}  "
                        f"(L={res.get('long_trades','?')} S={res.get('short_trades','?')})  "
                        f"{status}"
                    )

    # Resumen M4
    print()
    print("=" * 65)
    print("RESUMEN M4:")
    print()

    PASS_THRESHOLD = 5  # >= 5/8 configs

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
    total_assets   = len(asset_pass)

    if assets_passing >= 2:
        print(f"Activos pasando: {assets_passing}/{total_assets}")
        print("\n[OK] M4 PASADO — proceder a implementacion C# AtrCompressionBreakoutStrategy")
        print("     ATR Compression Breakout tiene edge estadistico en 4h")
    else:
        print(f"Activos pasando: {assets_passing}/{total_assets}")
        print("\n[FAIL] M4 RECHAZADO — hipotesis H2 ATR Compression Breakout sin edge en 4h")
        print("       Considerar cambio de espacio de hipotesis (ver notas)")
        print()
        print("  Diagnostico adicional — breakdown por direccion:")
        for sym in results:
            long_sharpes  = []
            short_sharpes = []
            for key, res in results[sym].items():
                if not np.isnan(res['sharpe']):
                    pass  # breakdown por dir requeriria tests separados
            print(f"  {sym}: revisar distribucion Long vs Short en la salida de arriba")
