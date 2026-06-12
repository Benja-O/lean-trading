# -*- coding: utf-8 -*-
"""
M4 validation: Lead-lag BTC -> ETH/BNB cross-asset signal.

Hipotesis: El retorno de BTC en la barra t-1 predice la direccion de
ETH y BNB en la barra t. Flujos institucionales entran por BTC primero
y se propagan a alts con un lag de 1 barra.

Diseno M4:
- Senal: BTC_return(t-1) > +threshold -> Long ETH/BNB en barra t
         BTC_return(t-1) < -threshold -> Short ETH/BNB en barra t
- Thresholds: 0.5%, 1.0%, 1.5% (filtro de ruido — solo movimientos fuertes)
- Hold: 4 barras 1h fijo (sin SL/TP — test de senal pura)
- Retorno: (close[t+4] - close[t]) / close[t]
- Timeframe: 1h
- Activos: BTC = senyal, ETH + BNB = activos operados
- Umbral M4: Sharpe >= 0.5 en >= 2/2 activos seguidos (ETH, BNB)
"""

import pandas as pd
import numpy as np
from binance.client import Client
from datetime import datetime


def download_1h_data(symbol: str, start_date: str, end_date: str) -> pd.DataFrame:
    """Descargar datos OHLCV 1h de Binance sin API key (datos publicos)."""
    client = Client()
    klines = client.get_historical_klines(symbol, '1h', start_date, end_date)
    df = pd.DataFrame(klines, columns=[
        'time', 'open', 'high', 'low', 'close', 'volume',
        'close_time', 'quote_volume', 'trades', 'taker_buy', 'taker_buy_quote', 'ignore'
    ])
    df['time'] = pd.to_datetime(df['time'], unit='ms', utc=True)
    df = df[['time', 'open', 'high', 'low', 'close', 'volume']].astype({
        'open': float, 'high': float, 'low': float, 'close': float, 'volume': float
    })
    return df.set_index('time').sort_index()


def align_on_btc(btc: pd.DataFrame, follower: pd.DataFrame) -> pd.DataFrame:
    """
    Alinear follower con BTC por timestamp (inner join).
    Retorna DataFrame con columnas: btc_close, follower_close.
    """
    merged = btc[['close']].rename(columns={'close': 'btc_close'}).join(
        follower[['close']].rename(columns={'close': 'follower_close'}),
        how='inner'
    )
    return merged.dropna()


def test_lead_lag(aligned: pd.DataFrame, threshold_pct: float, hold_bars: int) -> dict:
    """
    Testear senal lead-lag en modo puro (hold fijo, tamaño fijo, sin SL/TP).

    Logica:
    - btc_return[t-1] > +threshold -> Long follower en barra t
    - btc_return[t-1] < -threshold -> Short follower en barra t
    - Retorno: close[t+hold_bars] vs close[t]

    Args:
        aligned: DataFrame con columnas btc_close, follower_close
        threshold_pct: umbral de retorno BTC para activar senal (ej: 0.01 = 1%)
        hold_bars: cuantas barras mantener la posicion

    Returns:
        dict con sharpe, win_rate, mean_return, std_return, trades, direction_breakdown
    """
    closes_btc = aligned['btc_close'].values
    closes_follower = aligned['follower_close'].values
    n = len(closes_btc)

    trades = []

    # Necesitamos: barra t-2 y t-1 para calcular retorno BTC, y t+hold_bars para exit
    for t in range(2, n - hold_bars):
        btc_ret_prev = (closes_btc[t - 1] - closes_btc[t - 2]) / closes_btc[t - 2]

        direction = None
        if btc_ret_prev > threshold_pct:
            direction = 1   # Long
        elif btc_ret_prev < -threshold_pct:
            direction = -1  # Short

        if direction is None:
            continue

        entry_price = closes_follower[t]
        exit_price = closes_follower[t + hold_bars]
        raw_ret = (exit_price - entry_price) / entry_price
        trade_ret = direction * raw_ret  # ajustar por direccion

        trades.append({
            'return': trade_ret,
            'direction': direction,
            'btc_signal': btc_ret_prev
        })

    if len(trades) < 10:
        return {'sharpe': np.nan, 'win_rate': np.nan, 'mean_return': np.nan,
                'std_return': np.nan, 'trades': len(trades)}

    returns = np.array([t['return'] for t in trades])
    directions = np.array([t['direction'] for t in trades])

    win_rate = (returns > 0).sum() / len(returns)
    mean_return = returns.mean()
    std_return = returns.std()

    # Sharpe anualizado por conteo de trades
    # 8760 horas/anio / hold_bars = trades potenciales/anio si senyal siempre activa
    # Usamos sqrt(trades_per_year) donde trades_per_year se estima del sample
    years_in_sample = len(aligned) / 8760
    trades_per_year = len(trades) / years_in_sample
    sharpe = (mean_return / std_return * np.sqrt(trades_per_year)) if std_return > 0 else 0.0

    longs = returns[directions == 1]
    shorts = returns[directions == -1]

    return {
        'sharpe': sharpe,
        'win_rate': win_rate,
        'mean_return': mean_return,
        'std_return': std_return,
        'trades': len(trades),
        'longs': len(longs),
        'shorts': len(shorts),
        'long_mean': longs.mean() if len(longs) > 0 else np.nan,
        'short_mean': shorts.mean() if len(shorts) > 0 else np.nan,
    }


if __name__ == '__main__':
    print("M4: Lead-lag BTC -> ETH/BNB (1h)")
    print("=" * 65)

    start = '2020-01-01'
    end = '2025-12-31'

    thresholds = [0.005, 0.010, 0.015]   # 0.5%, 1.0%, 1.5%
    hold_bars = 4                          # 4h total en 1h TF

    followers = ['ETHUSDT', 'BNBUSDT']

    print(f"\nDescargando BTCUSDT 1h ({start} -> {end})...")
    btc = download_1h_data('BTCUSDT', start, end)
    print(f"  {len(btc)} barras")

    follower_data = {}
    for sym in followers:
        print(f"Descargando {sym} 1h...")
        try:
            follower_data[sym] = download_1h_data(sym, start, end)
            print(f"  {len(follower_data[sym])} barras")
        except Exception as exc:
            print(f"  ERROR: {exc}")

    print()
    results = {}

    for sym in followers:
        if sym not in follower_data:
            continue
        aligned = align_on_btc(btc, follower_data[sym])
        print(f"{sym} — {len(aligned)} barras alineadas con BTC")
        results[sym] = {}

        for thr in thresholds:
            res = test_lead_lag(aligned, threshold_pct=thr, hold_bars=hold_bars)
            results[sym][thr] = res
            status = '[PASS]' if (not np.isnan(res['sharpe']) and res['sharpe'] >= 0.5) else '[FAIL]'
            print(
                f"  threshold={thr*100:.1f}%: "
                f"Sharpe={res['sharpe']:.3f}  "
                f"Win={res['win_rate']:.1%}  "
                f"Mean={res['mean_return']*100:.3f}%  "
                f"Trades={res['trades']} (L={res.get('longs',0)}/S={res.get('shorts',0)})  "
                f"{status}"
            )
        print()

    print("=" * 65)
    print("RESUMEN M4:")
    print()

    asset_pass = {}
    for sym in results:
        configs_pass = sum(
            1 for thr in results[sym]
            if not np.isnan(results[sym][thr]['sharpe']) and results[sym][thr]['sharpe'] >= 0.5
        )
        total_configs = len(thresholds)
        passed = configs_pass >= (total_configs / 2)  # mayoria simple de configs
        asset_pass[sym] = passed
        bar = '[PASS]' if passed else '[FAIL]'
        print(f"  {sym}: {configs_pass}/{total_configs} configs con Sharpe >= 0.5  {bar}")

    print()
    assets_passing = sum(1 for v in asset_pass.values() if v)
    total_assets = len(asset_pass)

    # Umbral M4: ambos activos deben pasar (2/2) dado que no hay tercer activo de control
    m4_pass = assets_passing >= 2
    print(f"Activos con mayoria de configs >= 0.5: {assets_passing}/{total_assets}")
    if m4_pass:
        print("\n[OK] M4 PASADO — proceder a implementacion C# con BTC/ETH")
        print("     Backtest: BTC como senal, ETH como activo operado")
        print("     BNBUSDT confirmado como follower adicional (sin datos locales para backtest)")
    else:
        print("\n[FAIL] M4 RECHAZADO — hipotesis H3 lead-lag no tiene edge estadistico")
        print("       Considerar H1 (RSI + HMM) como siguiente candidata")
