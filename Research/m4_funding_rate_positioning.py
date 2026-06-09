# -*- coding: utf-8 -*-
"""
M4 validation: Funding Rate Positioning — SHORT only (FRP-S).

Hipotesis refinada: cuando el z-score del funding rate es extremo positivo,
el mercado esta overcrowded en longs. Los longs pagan carry alto, los arbs
institucionales ejecutan cash-and-carry (short perp + long spot), y la presion
estructural resultante genera un edge SHORT en los dias siguientes.

Se testea solo el lado SHORT porque:
- El mecanismo de desapalancamiento es asimetrico: funding positivo extremo
  crea presion vendedora concreta (carry cost + arbitraje). Funding negativo
  extremo no tiene el mismo mecanismo estructural (shorts no pagan carry en
  el mismo sentido sistematico).
- El test bidireccional mostro que el edge se concentra en la zona SHORT
  (BTC y SOL con MA filter). ETH no muestra edge en ningun sentido.

Diseno:
- Signal: z > +umbral AND precio < MA_P  -> SHORT
- Activos:   BTCUSDT, ETHUSDT, SOLUSDT (futuros perpetuos Binance)
- Timeframe: diario (funding rate = promedio de las 3 liquidaciones UTC/dia)
- Parametros testeados:
    z_threshold : [1.5, 2.0, 2.5]
    z_window    : [14, 30, 60]  dias
    ma_window   : [20, 50, None]  dias (None = sin filtro de tendencia)
    hold_days   : [3, 7]
- Umbral M4: Sharpe >= 0.5 en >= 50% de configs, en >= 2/3 activos
"""

import time
import numpy as np
import pandas as pd
from binance.client import Client


# ─── Data download ────────────────────────────────────────────────────────────

def download_funding_rates(symbol, start_ms, end_ms):
    """Descarga historico completo de funding rate con paginacion."""
    client = Client()
    all_records = []
    current = start_ms

    while current < end_ms:
        records = client.futures_funding_rate(
            symbol=symbol,
            startTime=current,
            endTime=end_ms,
            limit=1000
        )
        if not records:
            break
        all_records.extend(records)
        last_ts = records[-1]['fundingTime']
        if last_ts >= end_ms or len(records) < 1000:
            break
        current = last_ts + 1
        time.sleep(0.1)

    if not all_records:
        return pd.DataFrame(columns=['time', 'rate']).set_index('time')

    df = pd.DataFrame(all_records)
    df['time'] = pd.to_datetime(df['fundingTime'], unit='ms', utc=True)
    df['rate'] = df['fundingRate'].astype(float)
    return df[['time', 'rate']].set_index('time').sort_index()


def download_daily_closes(symbol, start_ms, end_ms):
    """Descarga cierres diarios de futuros perpetuos con paginacion."""
    client = Client()
    all_klines = []
    current = start_ms
    DAY_MS = 86_400_000

    while current < end_ms:
        klines = client.futures_klines(
            symbol=symbol,
            interval='1d',
            startTime=current,
            endTime=end_ms,
            limit=1500
        )
        if not klines:
            break
        all_klines.extend(klines)
        if len(klines) < 1500:
            break
        current = klines[-1][0] + DAY_MS

    df = pd.DataFrame(all_klines, columns=[
        'time', 'open', 'high', 'low', 'close', 'volume',
        'close_time', 'quote_volume', 'trades',
        'taker_buy', 'taker_buy_quote', 'ignore'
    ])
    df['time'] = pd.to_datetime(df['time'], unit='ms', utc=True)
    df['close'] = df['close'].astype(float)
    return df[['time', 'close']].set_index('time').sort_index()['close']


# ─── Signal computation ───────────────────────────────────────────────────────

def aggregate_funding_daily(fr_df):
    """Promedio de las liquidaciones 00:00, 08:00, 16:00 UTC por dia."""
    return fr_df['rate'].resample('1D').mean()


def compute_zscore(series, window):
    mean = series.rolling(window, min_periods=window).mean()
    std = series.rolling(window, min_periods=window).std(ddof=1)
    return (series - mean) / std


# ─── M4 signal test ───────────────────────────────────────────────────────────

def test_frp(closes, funding_daily, z_threshold, z_window, ma_window, hold_days):
    """
    Testea la senal de Funding Rate Positioning.

    Entrada: cierre del dia t.
    Salida:  cierre del dia t + hold_days.
    No re-entrada mientras la posicion anterior no se cierre.
    """
    combined = pd.DataFrame({
        'close': closes,
        'funding': funding_daily,
    }).dropna(subset=['close', 'funding'])

    fr = combined['funding']
    cl = combined['close']

    z = compute_zscore(fr, z_window)

    if ma_window is not None:
        ma = cl.rolling(ma_window, min_periods=ma_window).mean()
    else:
        ma = pd.Series(np.nan, index=cl.index)

    z_arr  = z.values
    cl_arr = cl.values
    ma_arr = ma.values
    n = len(cl_arr)

    trades = []
    i = 0
    while i < n - hold_days:
        zi  = z_arr[i]
        ci  = cl_arr[i]
        mai = ma_arr[i]

        if np.isnan(zi):
            i += 1
            continue

        # Filtro de tendencia: nan => sin filtro => siempre True
        ok_long  = np.isnan(mai) or ci > mai
        ok_short = np.isnan(mai) or ci < mai

        direction = 0
        if zi > z_threshold and ok_short:
            direction = -1   # SHORT — longs overcrowded

        if direction != 0:
            entry = cl_arr[i]
            exit_ = cl_arr[i + hold_days]
            ret = direction * (exit_ - entry) / entry
            trades.append(ret)
            i += hold_days + 1
        else:
            i += 1

    if len(trades) < 5:
        return {
            'sharpe': np.nan, 'win_rate': np.nan,
            'mean_return': np.nan, 'trades': len(trades),
        }

    returns = np.array(trades)
    mean_ret = returns.mean()
    std_ret  = returns.std(ddof=1)
    win_rate = (returns > 0).mean()

    years           = n / 365.0
    trades_per_year = len(trades) / years
    sharpe = (mean_ret / std_ret * np.sqrt(trades_per_year)) if std_ret > 0 else 0.0

    return {
        'sharpe':      sharpe,
        'win_rate':    win_rate,
        'mean_return': mean_ret,
        'std_return':  std_ret,
        'trades':      len(trades),
    }


# ─── Main ─────────────────────────────────────────────────────────────────────

if __name__ == '__main__':
    print("M4: Funding Rate Positioning SHORT-only (FRP-S) — diario")
    print("=" * 65)

    START_DATE = '2020-01-01'
    END_DATE   = '2025-12-31'
    START_MS   = int(pd.Timestamp(START_DATE, tz='UTC').timestamp() * 1000)
    END_MS     = int(pd.Timestamp(END_DATE,   tz='UTC').timestamp() * 1000)

    SYMBOLS = ['BTCUSDT', 'ETHUSDT', 'SOLUSDT']

    Z_THRESHOLDS = [1.5, 2.0, 2.5]
    Z_WINDOWS    = [14, 30, 60]
    MA_WINDOWS   = [20, 50, None]
    HOLD_DAYS    = [3, 7]

    TOTAL_CONFIGS  = len(Z_THRESHOLDS) * len(Z_WINDOWS) * len(MA_WINDOWS) * len(HOLD_DAYS)
    PASS_THRESHOLD = TOTAL_CONFIGS / 2

    # Descargar datos
    print()
    price_data   = {}
    funding_data = {}

    for sym in SYMBOLS:
        print(f"Descargando {sym}...")
        price_data[sym] = download_daily_closes(sym, START_MS, END_MS)
        fr_raw = download_funding_rates(sym, START_MS, END_MS)
        funding_data[sym] = aggregate_funding_daily(fr_raw)
        print(f"  Precios: {len(price_data[sym])} dias  "
              f"Funding: {len(funding_data[sym])} dias  "
              f"FR medio: {funding_data[sym].mean():.5f}  "
              f"FR max: {funding_data[sym].max():.5f}  "
              f"FR min: {funding_data[sym].min():.5f}")

    # Test de senal
    print()
    print("-" * 65)
    results = {}

    for sym in SYMBOLS:
        results[sym] = {}
        print(f"\n{sym}:")

        for z_thr in Z_THRESHOLDS:
            for z_win in Z_WINDOWS:
                for ma_win in MA_WINDOWS:
                    for hold in HOLD_DAYS:
                        res = test_frp(
                            price_data[sym],
                            funding_data[sym],
                            z_thr, z_win, ma_win, hold
                        )
                        key = (z_thr, z_win, ma_win, hold)
                        results[sym][key] = res

                        s  = res['sharpe']
                        ok = not np.isnan(s) and s >= 0.5
                        status   = '[PASS]' if ok else '[FAIL]'
                        wr_val   = res.get('win_rate', np.nan)
                        wr       = f"{wr_val:.1%}" if not np.isnan(wr_val) else 'n/a'
                        ma_label = f"MA{ma_win}" if ma_win else "noMA"
                        print(
                            f"  z>{z_thr:.1f} win={z_win:2d}d {ma_label:5s} "
                            f"hold={hold}d: "
                            f"Sharpe={s:+.3f}  Win={wr}  "
                            f"Trades={res['trades']:3d}  {status}"
                        )

    # Resumen M4
    print()
    print("=" * 65)
    print("RESUMEN M4:")
    print()

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
        print("\n[OK] M4 PASADO — FRP SHORT-only tiene edge estadistico")
        print("     Proceder a implementacion C# FundingRatePositioningStrategy")
    else:
        print(f"Activos pasando: {assets_passing}/{total_assets}")
        print("\n[FAIL] M4 RECHAZADO — FRP SHORT-only sin edge generalizable cross-asset")
        print("       Explorar taker flow imbalance como alternativa")
