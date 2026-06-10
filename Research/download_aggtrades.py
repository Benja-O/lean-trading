#!/usr/bin/env python3
"""
Binance Futures AggTrades — Descargador + Extractor de Features 1h
Fuente : data.binance.vision  (USDT-M perpetuos)
Salida :
  raw/{SYMBOL}/YYYY/  → ZIPs diarios
  features/           → {SYMBOL}_1h_features.parquet
"""

import time
import zipfile
import logging
from datetime import date, timedelta
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed

import requests
import pandas as pd

# ─── Configuración ────────────────────────────────────────────────────────────
BASE_PATH   = Path(r"F:\Mis Documentos\Cripto monedas\Trading\Data\AggTrades")
SYMBOLS     = ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
START_DATE  = date(2021, 1, 1)
END_DATE    = date.today() - timedelta(days=1)
BASE_URL    = "https://data.binance.vision/data/futures/um/daily/aggTrades"
MAX_WORKERS = 4
RETRIES     = 3
RETRY_WAIT  = 5    # segundos entre reintentos
CHUNK_BYTES = 1 << 20   # 1 MB por chunk de descarga

# Columnas del CSV de Binance Vision (sin header)
CSV_COLS   = ["agg_id", "price", "qty", "first_id", "last_id", "ts_ms", "is_buyer_maker"]
CSV_DTYPES = {
    "agg_id":   "int64",
    "price":    "float64",
    "qty":      "float64",
    "first_id": "int64",
    "last_id":  "int64",
    "ts_ms":    "int64",
}
# ─────────────────────────────────────────────────────────────────────────────

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s  %(levelname)-8s %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)


# ── Rutas ─────────────────────────────────────────────────────────────────────

def raw_zip(symbol, d):
    return BASE_PATH / "raw" / symbol / f"{d.year:04d}" / f"{symbol}-aggTrades-{d}.zip"

def feat_file(symbol):
    return BASE_PATH / "features" / f"{symbol}_1h_features.parquet"


# ── Descarga ──────────────────────────────────────────────────────────────────

def _download_one(symbol, d):
    dest = raw_zip(symbol, d)
    if dest.exists():
        return (symbol, d, "skip")

    dest.parent.mkdir(parents=True, exist_ok=True)
    url = f"{BASE_URL}/{symbol}/{symbol}-aggTrades-{d}.zip"

    for attempt in range(1, RETRIES + 1):
        try:
            r = requests.get(url, stream=True, timeout=30)
            if r.status_code == 404:
                return (symbol, d, "notfound")
            r.raise_for_status()
            tmp = dest.with_suffix(".tmp")
            with open(tmp, "wb") as f:
                for chunk in r.iter_content(CHUNK_BYTES):
                    f.write(chunk)
            tmp.rename(dest)
            return (symbol, d, "ok")
        except Exception as exc:
            if attempt < RETRIES:
                time.sleep(RETRY_WAIT)
            else:
                log.warning(f"  ERROR {symbol} {d}: {exc}")
                return (symbol, d, "error")


def download_symbol(symbol):
    days = list(_iter_dates(START_DATE, END_DATE))
    counters = {"ok": 0, "skip": 0, "notfound": 0, "error": 0}

    log.info(f"[{symbol}] Descarga — {len(days)} días, {MAX_WORKERS} workers paralelos")
    with ThreadPoolExecutor(max_workers=MAX_WORKERS) as pool:
        futures = {pool.submit(_download_one, symbol, d): d for d in days}
        for i, fut in enumerate(as_completed(futures), 1):
            _, _, status = fut.result()
            counters[status] += 1
            if i % 300 == 0 or i == len(days):
                log.info(
                    f"  [{symbol}] {i}/{len(days)} — "
                    f"ok:{counters['ok']}  skip:{counters['skip']}  "
                    f"404:{counters['notfound']}  err:{counters['error']}"
                )

    log.info(
        f"[{symbol}] Descarga completada — "
        f"ok:{counters['ok']}  skip:{counters['skip']}  "
        f"404:{counters['notfound']}  err:{counters['error']}"
    )
    return counters["notfound"]


# ── Procesamiento → features 1h ───────────────────────────────────────────────

def _parse_bool(v):
    return str(v).strip().lower() in ("true", "1")


def _read_zip(path):
    try:
        with zipfile.ZipFile(path) as zf:
            with zf.open(zf.namelist()[0]) as f:
                df = pd.read_csv(
                    f,
                    header=None,
                    names=CSV_COLS,
                    dtype=CSV_DTYPES,
                    converters={"is_buyer_maker": _parse_bool},
                )
        return df if not df.empty else None
    except Exception as exc:
        log.warning(f"  Error leyendo {path.name}: {exc}")
        return None


def _agg_1h(df):
    """Agrega AggTrades a barras 1h y calcula features microestructurales."""
    df["bar"]      = pd.to_datetime(df["ts_ms"], unit="ms", utc=True).dt.floor("1h")
    df["buy_qty"]  = df["qty"].where(~df["is_buyer_maker"], 0.0)
    df["sell_qty"] = df["qty"].where( df["is_buyer_maker"], 0.0)

    g = df.groupby("bar", sort=True)

    bars = pd.DataFrame({
        "open":             g["price"].first(),
        "high":             g["price"].max(),
        "low":              g["price"].min(),
        "close":            g["price"].last(),
        "volume":           g["qty"].sum(),
        "buy_volume":       g["buy_qty"].sum(),
        "sell_volume":      g["sell_qty"].sum(),
        "trade_count":      g["qty"].count(),
        "mean_trade_size":  g["qty"].mean(),
    }).reset_index()

    vol = bars["volume"].replace(0.0, float("nan"))
    sv  = bars["sell_volume"].replace(0.0, float("nan"))

    bars["ofi"]            = (bars["buy_volume"] - bars["sell_volume"]) / vol
    bars["buy_sell_ratio"] = bars["buy_volume"] / sv
    bars["cvd_delta"]      = bars["buy_volume"] - bars["sell_volume"]
    bars["arrival_rate"]   = bars["trade_count"]
    bars["price_return"]   = (bars["close"] - bars["open"]) / bars["open"]

    return bars[[
        "bar", "open", "high", "low", "close", "volume",
        "buy_volume", "sell_volume", "trade_count", "mean_trade_size",
        "ofi", "buy_sell_ratio", "cvd_delta", "arrival_rate", "price_return",
    ]]


def process_symbol(symbol):
    raw_root = BASE_PATH / "raw" / symbol
    all_zips = sorted(raw_root.rglob("*.zip")) if raw_root.exists() else []

    if not all_zips:
        log.warning(f"[{symbol}] Sin ZIPs disponibles — saltando procesamiento")
        return

    log.info(f"[{symbol}] Procesando {len(all_zips)} ZIPs → barras 1h")
    frames = []
    for i, zp in enumerate(all_zips, 1):
        raw = _read_zip(zp)
        if raw is not None:
            frames.append(_agg_1h(raw))
        if i % 300 == 0:
            log.info(f"  [{symbol}] {i}/{len(all_zips)} ZIPs procesados")

    if not frames:
        log.warning(f"[{symbol}] Sin datos procesables")
        return

    result = (
        pd.concat(frames, ignore_index=True)
        .sort_values("bar")
        .reset_index(drop=True)
    )
    # CVD acumulativo continuo (no resetea por día)
    result["cvd"] = result["cvd_delta"].cumsum()

    out = feat_file(symbol)
    out.parent.mkdir(parents=True, exist_ok=True)
    result.to_parquet(out, index=False, compression="snappy")

    # CSV para consumo desde C# (MicrostructureRegistry).
    # El C# espera columnas en este orden exacto (índices hardcodeados en ParseLine):
    # bar,open,high,low,close,volume,buy_volume,sell_volume,trade_count,mean_trade_size,
    # ofi,buy_sell_ratio,cvd_delta,arrival_rate,price_return,cvd
    csv_out = out.with_suffix(".csv")
    result.to_csv(csv_out, index=False)

    span = f"{result['bar'].iloc[0].date()} → {result['bar'].iloc[-1].date()}"
    log.info(f"[{symbol}] Guardado {out.name} + {csv_out.name} — {len(result):,} barras  ({span})")


# ── Verificación previa de símbolos ───────────────────────────────────────────

def _symbol_exists(symbol):
    probe = END_DATE - timedelta(days=7)
    url   = f"{BASE_URL}/{symbol}/{symbol}-aggTrades-{probe}.zip"
    try:
        r = requests.head(url, timeout=10)
        return r.status_code == 200
    except Exception:
        return False


def _verify_symbols():
    """
    Verifica qué símbolos están disponibles en Binance Vision.
    Para TRB prueba variantes conocidas del nombre.
    """
    trb_variants = ["TRBUSDTPERP", "TRBUSD"]
    valid = []

    for sym in SYMBOLS:
        candidates = trb_variants if sym.startswith("TRB") else [sym]
        found = None
        for candidate in candidates:
            if _symbol_exists(candidate):
                found = candidate
                break
        if found:
            log.info(f"  {found:20s} ✔ disponible")
            valid.append(found)
        else:
            tried = " / ".join(candidates)
            log.warning(f"  {tried:20s} ✗ NO encontrado en Binance Vision")

    return valid


# ── Utilidades ────────────────────────────────────────────────────────────────

def _iter_dates(start, end):
    d = start
    while d <= end:
        yield d
        d += timedelta(days=1)


# ── Entry point ───────────────────────────────────────────────────────────────

def main():
    log.info("=" * 62)
    log.info("  Binance AggTrades Research Pipeline — Hito E")
    log.info(f"  Período  : {START_DATE}  →  {END_DATE}")
    log.info(f"  Salida   : {BASE_PATH}")
    log.info("=" * 62)

    log.info("\n── Verificando símbolos en Binance Vision ───────────────────")
    valid_symbols = _verify_symbols()
    if not valid_symbols:
        log.error("Ningún símbolo encontrado. Revisar nombres en SYMBOLS.")
        return

    log.info(f"\n  Símbolos confirmados: {valid_symbols}")

    log.info("\n── FASE 1: DESCARGA ─────────────────────────────────────────")
    for sym in valid_symbols:
        download_symbol(sym)

    log.info("\n── FASE 2: PROCESAMIENTO ────────────────────────────────────")
    for sym in valid_symbols:
        process_symbol(sym)

    log.info("\n── RESUMEN ──────────────────────────────────────────────────")
    for sym in valid_symbols:
        fp = feat_file(sym)
        if fp.exists():
            size_mb = fp.stat().st_size / (1 << 20)
            df = pd.read_parquet(fp, columns=["bar"])
            log.info(f"  {sym}: {len(df):,} barras 1h  |  {size_mb:.1f} MB")
        else:
            log.warning(f"  {sym}: parquet no generado")

    log.info("\n✔ Pipeline completado.")


if __name__ == "__main__":
    main()
