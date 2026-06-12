"""
Descarga mensual de klines 1m de SOLUSDT Futures (UM) desde Binance Vision.

Output: F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\1m\SOLUSDT\
Formato: SOLUSDT-1m-YYYY-MM.zip (igual que BTC/ETH ya descargados)

SOLUSDT perp listado en Binance ~2020-09-10 -> primer mes: 2020-09
"""

import os
import time
import requests
from datetime import date

OUTPUT_DIR = r"F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\1m\SOLUSDT"
BASE_URL   = "https://data.binance.vision/data/futures/um/monthly/klines/SOLUSDT/1m"

# Primer mes con datos (SOLUSDT perp Binance: sep 2020)
START_YEAR, START_MONTH = 2020, 9

# Ultimo mes completo
today = date.today()
end_year  = today.year  if today.month > 1 else today.year  - 1
end_month = today.month - 1 if today.month > 1 else 12

os.makedirs(OUTPUT_DIR, exist_ok=True)

year, month = START_YEAR, START_MONTH
downloaded = skipped = failed = 0

while (year, month) <= (end_year, end_month):
    filename = f"SOLUSDT-1m-{year:04d}-{month:02d}.zip"
    url      = f"{BASE_URL}/{filename}"
    dest     = os.path.join(OUTPUT_DIR, filename)

    if os.path.exists(dest):
        print(f"  [skip]  {filename}")
        skipped += 1
    else:
        print(f"  [down]  {filename} ...", end=" ", flush=True)
        try:
            resp = requests.get(url, timeout=60)
            if resp.status_code == 200:
                with open(dest, "wb") as f:
                    f.write(resp.content)
                size_kb = len(resp.content) // 1024
                print(f"OK ({size_kb} KB)")
                downloaded += 1
                time.sleep(0.3)
            elif resp.status_code == 404:
                print("404 - no disponible todavia")
                skipped += 1
            else:
                print(f"ERROR {resp.status_code}")
                failed += 1
        except Exception as e:
            print(f"EXCEPCION: {e}")
            failed += 1

    month += 1
    if month > 12:
        month = 1
        year += 1

print(f"\nListo: {downloaded} descargados, {skipped} omitidos, {failed} errores.")
print(f"Archivos en: {OUTPUT_DIR}")
