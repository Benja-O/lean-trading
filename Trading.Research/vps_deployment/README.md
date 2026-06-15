# Deployment del sistema de trading a un VPS (corrida continua)

Runbook para correr el sistema Lean de trading live de forma **desatendida** en un VPS Windows,
con auto-restart, reloj sincronizado y monitoreo. Cierra el Hito D (corrida continua con
`minimal-position-mode`).

> **Por qué un VPS.** La máquina de desarrollo mostró tres síntomas de red restrictiva (drift de
> reloj, NTP/UDP 123 bloqueado, flapping del WebSocket de mayor tráfico — ver POLICY 2.4 y
> ADR-043). Un VPS estable con IP fija cerca de Binance elimina esa clase de problemas de raíz.

---

## 0. Recomendación de infraestructura

- **Proveedor/Región:** Windows VPS en **AWS `ap-northeast-1` (Tokio)** o equivalente — el matching
  engine de Binance está ahí; co-ubicar minimiza latencia y desconexiones. Cualquier VPS Windows
  estable sirve; la cercanía es optimización, no requisito.
- **Tamaño:** 2 vCPU / 4 GB RAM es holgado para 6 executors en 1h (el engine es liviano en
  estado estacionario; el warmup carga ~47k barras 1h por símbolo). Disco: 30+ GB (el `Data/` de
  Lean pesa).
- **OS:** Windows Server 2019/2022. El tooling (NSSM, w32tm, los scripts PowerShell) es Windows.

---

## 1. Prerrequisitos en el VPS

1. **.NET SDK 10** (para buildear en el box) — https://dotnet.microsoft.com/download
   - Alternativa: solo el **runtime** si vas a copiar un `dotnet publish` ya compilado.
2. **git** — para clonar/actualizar el repo.
3. **NSSM** — https://nssm.cc/ . Descomprimir y anotar la ruta de `nssm.exe` (ej. `C:\tools\nssm\nssm.exe`).

---

## 2. Código, datos y modelos

El repo trae el código, pero **`Data/` y los modelos/config NO están en git** (gitignored). Hay
que proveerlos aparte.

1. **Código:**
   ```powershell
   git clone https://github.com/Benja-O/lean-trading.git C:\trading\Lean
   cd C:\trading\Lean
   dotnet build Launcher/QuantConnect.Lean.Launcher.csproj -c Release
   ```
2. **Carpeta `Data/`** (gitignored, ~varios GB): copiarla desde tu máquina local a
   `C:\trading\Lean\Data\` (robocopy sobre RDP, o un blob/almacenamiento). Sin ella el engine no
   arranca (symbol properties, market hours, etc.).
3. **Modelos de régimen** (`models/regime/*.hmm.json`) y artefactos ML: copiarlos a la carpeta de
   salida del build (junto al Launcher) tal como están en tu local. El host falla loud al boot si
   falta un modelo requerido por una estrategia con `CompatibleRegimes`.
4. **`strategies.json`**: está en `Trading.Strategies/strategies.json` (versionado) y el build lo
   copia a la carpeta del Launcher. Verificar que quedó junto al exe.

> El payload de runtime vive en `Launcher/bin/Release/` (exe + DLLs + Data + strategies.json +
> models + config.json). Esa es la carpeta que NSSM va a usar como `AppDirectory`.

---

## 3. Config y secretos (NO van a git)

En `Launcher/bin/Release/config.json` (basado en tu config.json local que ya funciona):

- `"environment": "live-futures-binance"`
- **API keys de Binance** con permiso de **Futures trading** (no withdrawal). Las mismas que
  validaste en D-prev, o nuevas.
- `"minimal-position-mode": true` — posiciones al min notional (shakedown). Desactivar cuando se
  pase a sizing real (ADR-045).
- Asegurar que `broker-validation-mode` y `sltp-validation-mode` **no estén** o estén en `false`
  (los hooks ya se removieron del código, pero por las dudas).

> **Nunca** commitear config.json con keys reales. Es un archivo local del VPS.

---

## 4. Reloj sincronizado (obligatorio)

El error -1021 de Binance frena el arranque si el reloj está adelantado >1000ms (ADR-043). En
PowerShell **elevado**:

```powershell
cd C:\trading\Lean\Trading.Research\broker_validation
powershell -ExecutionPolicy Bypass -File .\Install-TradingClockSync.ps1
.\Sync-TradingClock.ps1 -CheckOnly      # confirmar offset < 500ms
```

Esto deja una tarea programada que resincroniza al inicio y cada 60 min (como SYSTEM, con
fallback a server time de Binance si NTP está bloqueado).

---

## 5. Whitelist de IP en Binance

1. Obtener la IP pública del VPS:
   ```powershell
   (Invoke-WebRequest "https://api.ipify.org" -UseBasicParsing).Content
   ```
2. En Binance → API Management → editar la key → restringir a esa IP (trusted IP). El VPS tiene IP
   fija, así que el whitelist no se rompe (a diferencia de la red residencial).

---

## 6. Monitoreo (Healthchecks.io)

1. Crear un check en https://healthchecks.io/ con período acorde a la cadencia de barras (las
   estrategias son 1h; el ping se gatea por frescura de barras con umbral 90 min — POLICY/ADR-021).
2. Copiar la **ping URL** del check. Se pasa al servicio en el paso 7 (`-HealthchecksPingUrl`).
3. Configurar la alerta (email/Telegram) para que avise si el proceso deja de pingear.

---

## 7. Instalar el servicio (NSSM, auto-restart)

En PowerShell **elevado**:

```powershell
cd C:\trading\Lean\Trading.Research\vps_deployment
.\Install-TradingService.ps1 `
    -LauncherDir "C:\trading\Lean\Launcher\bin\Release" `
    -NssmPath "C:\tools\nssm\nssm.exe" `
    -HealthchecksPingUrl "https://hc-ping.com/<tu-uuid>"
```

Configura auto-restart ante `Environment.Exit(1)` del dead-man's switch (ADR-042 — el feed
congelado mata el proceso y NSSM lo relanza con socket limpio), redirige stdout/stderr a
`service-logs/`, y arranca automático al boot.

---

## 8. Verificación post-arranque

1. **Estado del servicio:** `nssm status LeanTrading` → `SERVICE_RUNNING`. O `services.msc`.
2. **Logs:** revisar `Launcher\bin\Release\service-logs\service-stdout.log` y el JSONL estructurado
   en `logs\trading-<fecha>.jsonl`. Confirmar:
   - Balance carga sin -1021.
   - Warmup llega a 100% → "Algorithm finished warming up."
   - El proceso **se mantiene vivo** (sin Exit(1) recurrente).
3. **Heartbeat:** `Launcher\bin\Release\health\heartbeat.json` → `LastDataReceivedUtc` se actualiza
   cada ~minuto (feed vivo, ADR-042) y `LastBarProcessedUtc` al cierre de cada barra 1h.
4. **Healthchecks.io:** el check pasa a verde (recibiendo pings).
5. **Primera orden real:** cuando alguna de las 6 estrategias dispare, verificar en Binance la
   entrada mínima + SL/TP nativos con **Reduce-Only=true** (ADR-044).

> **Verificar (gotcha conocido):** que el Launcher no quede esperando input de consola al final
> de una corrida (el "Press any key" que aparece en debug local). Como servicio no hay consola; si
> el proceso se cierra por esperar un key, NSSM lo reinicia en loop. Si pasa, revisar la config de
> Lean para cierre automático sin key-press.

---

## 9. Actualizaciones (deploy de cambios)

```powershell
nssm stop LeanTrading
cd C:\trading\Lean
git pull
dotnet build Launcher/QuantConnect.Lean.Launcher.csproj -c Release
# (re-copiar strategies.json/models a bin/Release si el build no los copió)
nssm start LeanTrading
```

> `Data/`, `config.json` y modelos persisten entre updates (no los toca git). Solo se recompila el
> código.

---

## 10. Operación y runbooks

- Procedimientos de emergencia (kill switch, discrepancia broker, proceso caído): **POLICY.md §5**.
- Reconciliación de balance Lean vs Binance: `Trading.Research/broker_validation/reconcile.ps1`.
- Cadencia de revisión humana: **POLICY.md §4**.
- Pasar de `minimal-position-mode` a sizing real: requiere allocator multi-estrategia (ROADMAP) y
  cumplir el gate de promoción a live de POLICY §6.3.
