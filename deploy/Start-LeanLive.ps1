<#
.SYNOPSIS
    Pre-flight idempotente + arranque de LeanLive en el VPS.

.DESCRIPCION
    Valida, EN ORDEN y con fail-fast, cada precondicion que el arranque de
    LeanLive necesita (aprendidas a los golpes durante el primer deploy):
      1. Binarios del bundle presentes (incluye los DLLs divergentes del fork).
      2. data-folder con los archivos base de Lean (market-hours, symbol-properties).
         Si faltan, los copia desde la data de LeanPaper.
      3. config.json sin PLACEHOLDER_, environment correcto, minimal-position-mode.
      4. strategies.json presente.
      5. Reloj sincronizado con Binance (< umbral). Previene el -1021 que mata el
         Initialize en LoadCashBalance. Intenta w32tm /resync si hay drift y se
         corre como admin; si no puede, FALLA con instruccion (no arranca a loop).
      6. Store de microestructura fresco (advertencia, no bloquea).
      7. Servicio NSSM configurado (stdout/stderr, restart, env vars). Idempotente.
    Solo si TODOS los gates duros pasan, (re)arranca el servicio y muestra el log.

    Es idempotente: re-correrlo re-valida y re-aplica la config NSSM sin efectos
    duplicados; copia la data base solo si falta.

.EJEMPLO
    # Como administrador (necesario para corregir reloj y tocar el servicio):
    .\Start-LeanLive.ps1 -HealthchecksUrl "https://hc-ping.com/<UUID-LeanLive>"

    # Solo validar, sin arrancar:
    .\Start-LeanLive.ps1 -SkipStart

.NOTAS
    Las API keys de Binance van en config.json (no las maneja este script).
    Requiere PowerShell elevado para el gate de reloj y la config del servicio.
#>
param(
    [string]$LiveDir         = 'C:\Lean\Live',
    [string]$StoreDir        = 'C:\Lean\MicrostructureStore',
    [string]$PaperDataDir    = 'C:\Lean\Paper\data',
    [string]$Nssm            = 'C:\nssm-2.24-101-g897c7ad\win64\nssm.exe',
    [string]$ServiceName     = 'LeanLive',
    [string]$HealthchecksUrl = '',
    [string[]]$Symbols       = @('BTCUSDT', 'ETHUSDT', 'SOLUSDT'),
    [int]$MaxClockOffsetMs   = 500,
    [int]$StoreStaleHours    = 3,
    [switch]$SkipStart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# PS 5.1 (default en Windows Server) negocia TLS 1.0/1.1; Binance exige TLS 1.2+.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

# --- Helpers de salida ---------------------------------------------------------
function Write-Ok    { param([string]$m) Write-Host "[ OK ] $m"   -ForegroundColor Green }
function Write-Warn2 { param([string]$m) Write-Host "[WARN] $m"   -ForegroundColor Yellow }
function Write-Step  { param([string]$m) Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Fail {
    param([string]$m, [string]$fix)
    Write-Host "[FAIL] $m" -ForegroundColor Red
    if ($fix) { Write-Host "       -> $fix" -ForegroundColor Red }
    Write-Host "`nArranque ABORTADO. Corregi lo de arriba y volve a correr este script." -ForegroundColor Red
    exit 1
}

function Test-IsAdmin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$id).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
}

# Mide (local - Binance) en ms compensando RTT. Devuelve $null si no pudo consultar.
function Get-BinanceOffsetMs {
    try {
        $t0 = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        $resp = Invoke-RestMethod -Uri 'https://fapi.binance.com/fapi/v1/time' -TimeoutSec 10
        $t1 = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        $localMidpoint = $t0 + [long](($t1 - $t0) / 2)
        return [long]($localMidpoint - [long]$resp.serverTime)
    } catch {
        return $null
    }
}

Write-Host "LeanLive pre-flight  |  $LiveDir  |  servicio '$ServiceName'" -ForegroundColor White

# --- Gate 0: privilegios -------------------------------------------------------
$isAdmin = Test-IsAdmin
if (-not $isAdmin) {
    Write-Warn2 "No estas como administrador. El gate de reloj y la config del servicio pueden fallar. Recomendado: PowerShell elevado."
}

# --- Gate 1: binarios ----------------------------------------------------------
Write-Step "1/7 Binarios del bundle"
$requiredBinaries = @(
    'QuantConnect.Lean.Launcher.exe',
    'Trading.Strategies.dll',
    'Trading.Application.dll',
    'Trading.Domain.dll',
    'QuantConnect.Common.dll',               # fork: BinanceOrderProperties.ReduceOnly (ADR-044)
    'QuantConnect.Brokerages.Binance.dll'    # fork: reduceOnly en CreateOrderBody (ADR-044)
)
foreach ($bin in $requiredBinaries) {
    $path = Join-Path $LiveDir $bin
    if (-not (Test-Path $path)) {
        Fail "Falta binario: $bin" "Copia el publish completo (publish\live) a $LiveDir."
    }
}
Write-Ok "Binarios presentes (incluye los DLLs divergentes del fork)."

# --- Gate 2: data-folder base --------------------------------------------------
Write-Step "2/7 Archivos base de Lean (data-folder)"
$dataDir = Join-Path $LiveDir 'data'
$marketHours = Join-Path $dataDir 'market-hours\market-hours-database.json'
$symbolProps = Join-Path $dataDir 'symbol-properties\symbol-properties-database.csv'

if (-not (Test-Path $marketHours) -or -not (Test-Path $symbolProps)) {
    Write-Warn2 "Faltan archivos base en $dataDir. Intentando copiar desde $PaperDataDir ..."
    if (-not (Test-Path $PaperDataDir)) {
        Fail "No existe $PaperDataDir para copiar la data base." "Copia data\market-hours y data\symbol-properties desde la carpeta Data\ del repo Lean."
    }
    # robocopy: exit code < 8 = exito.
    & robocopy $PaperDataDir $dataDir /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        Fail "robocopy fallo (exit $LASTEXITCODE) copiando la data base." "Copia manualmente $PaperDataDir -> $dataDir."
    }
}
if (-not (Test-Path $marketHours)) { Fail "Sigue faltando market-hours-database.json en $dataDir." "Conseguilo de Data\market-hours del repo Lean." }
if (-not (Test-Path $symbolProps)) { Fail "Sigue faltando symbol-properties-database.csv en $dataDir." "Conseguilo de Data\symbol-properties del repo Lean." }
Write-Ok "market-hours-database.json y symbol-properties-database.csv presentes."

# --- Gate 3: config.json -------------------------------------------------------
Write-Step "3/7 config.json"
$configPath = Join-Path $LiveDir 'config.json'
if (-not (Test-Path $configPath)) {
    Fail "No existe config.json en $LiveDir." "Copia config.live.template.json a config.json y completa las API keys."
}
$configText = Get-Content $configPath -Raw
# Chequear el VALOR de los campos, no la palabra suelta: el comentario de cabecera
# del template menciona "PLACEHOLDER_*" y daria un falso positivo.
if ($configText -match '"binance-api-(key|secret)"\s*:\s*"(\s*PLACEHOLDER[^"]*|\s*)"') {
    Fail "config.json tiene binance-api-key/secret sin completar (placeholder o vacio)." "Completa binance-api-key / binance-api-secret con las keys reales (Futures + IP whitelist)."
}
if ($configText -notmatch '"environment"\s*:\s*"live-futures-binance"') {
    Fail "config.json no tiene environment 'live-futures-binance'." 'Setea "environment": "live-futures-binance".'
}
if ($configText -notmatch '"minimal-position-mode"\s*:\s*true') {
    Fail "config.json no tiene minimal-position-mode en true (top-level)." 'Agrega "minimal-position-mode": true al nivel raiz (NO en strategies.json).'
}
Write-Ok "config.json: environment correcto, minimal-position-mode true, sin placeholders."

# --- Gate 4: strategies.json ---------------------------------------------------
Write-Step "4/7 strategies.json"
$strategiesPath = Join-Path $LiveDir 'strategies.json'
if (-not (Test-Path $strategiesPath)) {
    Fail "No existe strategies.json en $LiveDir." "Copia el strategies.json del bundle (2 estrategias de microestructura)."
}
Write-Ok "strategies.json presente."

# --- Gate 5: reloj (anti -1021) ------------------------------------------------
Write-Step "5/7 Reloj vs Binance (anti -1021)"
$offset = Get-BinanceOffsetMs
if ($null -eq $offset) {
    Write-Warn2 "No se pudo consultar la hora de Binance (red/TLS). No se pudo verificar el reloj - verificalo a mano antes de confiar en el arranque."
} else {
    Write-Host "       offset (local - Binance) = $offset ms (umbral +-$MaxClockOffsetMs)"
    if ([Math]::Abs($offset) -gt $MaxClockOffsetMs) {
        if (-not $isAdmin) {
            Fail "Reloj fuera de tolerancia ($offset ms) y no sos admin." "Abri PowerShell elevado y corre Sync-TradingClock.ps1 -Force (o este script)."
        }
        Write-Warn2 "Drift de $offset ms. Intentando w32tm /resync ..."
        try { Start-Service w32time -ErrorAction SilentlyContinue } catch {}
        & w32tm /resync /force | Out-Null
        Start-Sleep -Seconds 2
        $offset = Get-BinanceOffsetMs
        if ($null -ne $offset -and [Math]::Abs($offset) -le $MaxClockOffsetMs) {
            Write-Ok "Reloj corregido por NTP. offset = $offset ms."
        } else {
            Fail "El reloj sigue desviado tras w32tm (offset = $offset ms)." "NTP probablemente bloqueado. Corre Sync-TradingClock.ps1 -Force (setea el reloj a hora de Binance por HTTPS) e instala la tarea con Install-TradingClockSync.ps1."
        }
    } else {
        Write-Ok "Reloj dentro de tolerancia ($offset ms)."
    }
}

# --- Gate 6: store de microestructura (advertencia) ----------------------------
Write-Step "6/7 Store de microestructura ($StoreDir)"
if (-not (Test-Path $StoreDir)) {
    Write-Warn2 "No existe $StoreDir. El warmup no tendra datos del Recorder - verifica que LeanRecorder este corriendo."
} else {
    foreach ($sym in $Symbols) {
        $csv = Join-Path $StoreDir "$($sym)_1h_live.csv"
        if (-not (Test-Path $csv)) {
            Write-Warn2 "${sym}: falta $($sym)_1h_live.csv en el store."
            continue
        }
        $ageHours = ([DateTime]::UtcNow - (Get-Item $csv).LastWriteTimeUtc).TotalHours
        if ($ageHours -gt $StoreStaleHours) {
            Write-Warn2 ("{0}: store stale (ultima escritura hace {1:N1} h > {2} h). El Recorder puede estar caido." -f $sym, $ageHours, $StoreStaleHours)
        } else {
            Write-Ok ("{0}: store fresco (ultima escritura hace {1:N1} h)." -f $sym, $ageHours)
        }
    }
}

# --- Gate 7: servicio NSSM (idempotente) ---------------------------------------
Write-Step "7/7 Servicio NSSM '$ServiceName'"
if (-not (Test-Path $Nssm)) {
    Fail "No se encontro nssm.exe en $Nssm." "Pasa la ruta correcta con -Nssm."
}
$status = (& $Nssm status $ServiceName 2>&1) | Out-String
if ($status -match 'does not exist' -or $status -match 'OpenService') {
    Write-Warn2 "El servicio no existe. Instalandolo..."
    & $Nssm install $ServiceName (Join-Path $LiveDir 'QuantConnect.Lean.Launcher.exe') | Out-Null
}
# Config idempotente:
& $Nssm set $ServiceName AppDirectory   $LiveDir | Out-Null
& $Nssm set $ServiceName AppStdout      (Join-Path $LiveDir 'service-out.log') | Out-Null
& $Nssm set $ServiceName AppStderr      (Join-Path $LiveDir 'service-err.log') | Out-Null
& $Nssm set $ServiceName AppRotateFiles 1 | Out-Null
& $Nssm set $ServiceName AppRotateOnline 1 | Out-Null
& $Nssm set $ServiceName AppRotateBytes 10485760 | Out-Null
& $Nssm set $ServiceName AppExit Default Restart | Out-Null

# Env vars: AppEnvironmentExtra reemplaza el entorno; van TODAS juntas.
$envVars = @("MICROSTRUCTURE_STORE_DIR=$StoreDir")
if ([string]::IsNullOrWhiteSpace($HealthchecksUrl)) {
    Write-Warn2 "Sin -HealthchecksUrl: el ping a Healthchecks.io queda deshabilitado (no fatal, pero sin dead-man's switch externo)."
} else {
    $envVars += "HEALTHCHECKS_PING_URL=$HealthchecksUrl"
}
& $Nssm set $ServiceName AppEnvironmentExtra $envVars | Out-Null
Write-Ok "Servicio configurado (stdout/stderr, restart, env: $($envVars -join ' ; '))."

# --- Arranque ------------------------------------------------------------------
if ($SkipStart) {
    Write-Host "`n-SkipStart: validacion OK, no se arranca el servicio." -ForegroundColor Green
    exit 0
}

Write-Step "Arrancando '$ServiceName'"
& $Nssm restart $ServiceName | Out-Null
Start-Sleep -Seconds 8

$outLog = Join-Path $LiveDir 'service-out.log'
$errLog = Join-Path $LiveDir 'service-err.log'

Write-Host "`n----- ultimas lineas de service-err.log -----" -ForegroundColor Cyan
if (Test-Path $errLog) { Get-Content $errLog -Tail 15 } else { Write-Host "(sin stderr aun)" }

Write-Host "`n----- senales de arranque en service-out.log -----" -ForegroundColor Cyan
if (Test-Path $outLog) {
    Get-Content $outLog |
        Where-Object {
            $_ -match 'Initialize completed|LoadCashBalance|Warmup|WarmupFinished|-1021|-2015|-4061|RuntimeError|Error getting cash' -and
            $_ -notmatch 'BadImageFormat'
        } |
        Select-Object -Last 20
} else {
    Write-Host "(sin stdout aun)"
}

Write-Host "`nSeguimiento en vivo:  Get-Content '$outLog' -Wait -Tail 60" -ForegroundColor White
Write-Host "Verde si ves 'Initialize completed' sin -1021/-2015/-4061 y el warmup cargando el store." -ForegroundColor White
