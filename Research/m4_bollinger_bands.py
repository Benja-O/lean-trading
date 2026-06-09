# -*- coding: utf-8 -*-
"""
M4 validation: Bollinger Bands %b oversold signal.

Hipótesis: Cuando %b (percentage B) cae bajo 0 (precio en o bajo banda inferior),
la reversión a la media tiende a producir un retorno positivo en las siguientes
barras, especialmente en régimen de volatilidad alta (ADX > 30).

Diseño M4:
- Señal pura: Long cuando %b < 0 durante N barras consecutivas (testear N=1,2,4)
- Hold fijo: 10 barras (sin SL/TP)
- Tamaño: fijo, sin vol-scaling
- Retorno: cierre en la barra 10 al precio de close de esa barra
- Timeframe: 4h (compatible con implementación)
"""

import pandas as pd
import numpy as np
from binance.client import Client
from datetime import datetime, timedelta

def download_4h_data(symbol: str, start_date: str, end_date: str) -> pd.DataFrame:
    """Descargar datos OHLCV 4h de Binance sin API key (datos públicos)."""
    client = Client()
    klines = client.get_historical_klines(symbol, '4h', start_date, end_date)
    df = pd.DataFrame(klines, columns=[
        'time', 'open', 'high', 'low', 'close', 'volume',
        'close_time', 'quote_volume', 'trades', 'taker_buy', 'taker_buy_quote', 'ignore'
    ])
    df['time'] = pd.to_datetime(df['time'], unit='ms')
    df = df[['time', 'open', 'high', 'low', 'close', 'volume']].astype({
        'open': float, 'high': float, 'low': float, 'close': float, 'volume': float
    })
    return df.reset_index(drop=True)

def calculate_bollinger_bands(closes: np.array, period: int = 5, std_dev: float = 1.0) -> tuple:
    """Calcular bandas de Bollinger: (upper, lower, middle, %b)."""
    if len(closes) < period:
        return None, None, None, None

    recent = closes[-period:]
    sma = recent.mean()
    std = recent.std()
    upper = sma + std_dev * std
    lower = sma - std_dev * std

    if upper == lower:
        pb = 0
    else:
        pb = (closes[-1] - lower) / (upper - lower)

    return upper, lower, sma, pb

def calculate_adx(highs: np.array, lows: np.array, closes: np.array, period: int = 10) -> float:
    """ADX simplificado."""
    if len(highs) < period + 1:
        return 0

    recent_h = highs[-(period+1):]
    recent_l = lows[-(period+1):]

    sum_tr = 0
    sum_plus_dm = 0
    sum_minus_dm = 0

    for i in range(1, len(recent_h)):
        tr = max(recent_h[i] - recent_l[i],
                 abs(recent_h[i] - closes[-(period+1-i)]),
                 abs(recent_l[i] - closes[-(period+1-i)]))
        sum_tr += tr

        plus_dm = recent_h[i] - recent_h[i-1] if recent_h[i] > recent_h[i-1] else 0
        minus_dm = recent_l[i-1] - recent_l[i] if recent_l[i] < recent_l[i-1] else 0

        sum_plus_dm += plus_dm
        sum_minus_dm += minus_dm

    if sum_tr == 0:
        return 0

    plus_di = (sum_plus_dm / sum_tr) * 100
    minus_di = (sum_minus_dm / sum_tr) * 100

    if (plus_di + minus_di) == 0:
        return 0

    di = abs(plus_di - minus_di) / (plus_di + minus_di)
    return di * 100

def test_bb_signal(df: pd.DataFrame, oversold_bars: int = 4, hold_bars: int = 10) -> dict:
    """
    Testear señal de Bollinger Bands en modo puro (hold fijo, tamaño fijo).

    Returns: dict con métricas de retorno (Sharpe, win_rate, etc.)
    """
    closes = df['close'].values
    highs = df['high'].values
    lows = df['low'].values

    trades = []
    i = 20  # Necesitamos al menos 20 barras para BB + ADX

    while i < len(closes) - hold_bars:
        # Detectar oversold: N barras consecutivas con %b < 0 y ADX > 30
        oversold_count = 0
        start_idx = i

        for j in range(start_idx, min(start_idx + oversold_bars, len(closes) - hold_bars)):
            _, _, _, pb = calculate_bollinger_bands(closes[:j+1], period=5, std_dev=1.0)
            adx = calculate_adx(highs[:j+1], lows[:j+1], closes[:j+1], period=10)

            if pb is not None and pb < 0 and adx > 30:
                oversold_count += 1
            else:
                break

        if oversold_count >= oversold_bars:
            entry_idx = start_idx + oversold_bars - 1
            exit_idx = entry_idx + hold_bars

            if exit_idx < len(closes):
                entry_price = closes[entry_idx]
                exit_price = closes[exit_idx]
                ret = (exit_price - entry_price) / entry_price
                trades.append({'entry': entry_price, 'exit': exit_price, 'return': ret})
                i = exit_idx + 1
            else:
                break
        else:
            i += 1

    if not trades:
        return {'sharpe': np.nan, 'win_rate': 0, 'mean_return': 0, 'trades': 0}

    returns = np.array([t['return'] for t in trades])
    win_rate = (returns > 0).sum() / len(returns) if len(returns) > 0 else 0
    mean_return = returns.mean()
    std_return = returns.std()

    # 88 barras 4h/año aprox
    sharpe = (mean_return / std_return * np.sqrt(88)) if std_return > 0 else 0

    return {
        'sharpe': sharpe,
        'win_rate': win_rate,
        'mean_return': mean_return,
        'std_return': std_return,
        'trades': len(trades)
    }

if __name__ == '__main__':
    print("M4: Bollinger Bands %b oversold signal")
    print("=" * 60)

    symbols = ['BTCUSDT', 'ETHUSDT', 'BNBUSDT']
    start = '2020-01-01'
    end = '2025-12-31'
    oversold_configs = [1, 2, 4]

    results = {}

    for symbol in symbols:
        print(f"\nDescargando {symbol}...")
        try:
            df = download_4h_data(symbol, start, end)
            print(f"  {len(df)} barras 4h")
            results[symbol] = {}

            for obs_bars in oversold_configs:
                print(f"  Testando oversold={obs_bars} barras...", end='')
                res = test_bb_signal(df, oversold_bars=obs_bars, hold_bars=10)
                results[symbol][obs_bars] = res
                print(f" Sharpe={res['sharpe']:.3f}, Win={res['win_rate']:.1%}, Trades={res['trades']}")

        except Exception as e:
            print(f"  Error: {e}")

    print("\n" + "=" * 60)
    print("RESUMEN M4:")
    for symbol in symbols:
        if symbol in results:
            for obs_bars in oversold_configs:
                if obs_bars in results[symbol]:
                    res = results[symbol][obs_bars]
                    status = "[PASS]" if res['sharpe'] >= 0.5 else "[FAIL]"
                    print(f"{symbol} oversold={obs_bars}: Sharpe={res['sharpe']:.3f} {status}")

    # Umbral M4: Sharpe >= 0.5 en >= 2/3 activos
    total = sum(len(r) for r in results.values())
    passed = sum(1 for symbol in results for obs_bars in results[symbol]
                 if results[symbol][obs_bars]['sharpe'] >= 0.5)
    print(f"\nResultado: {passed}/{total} configs pasan M4")
    if passed >= total * 2 / 3:
        print("[OK] HIPOTESIS CONFIRMADA - proceder a implementacion")
    else:
        print("[FAIL] HIPOTESIS RECHAZADA - descartar estrategia")
