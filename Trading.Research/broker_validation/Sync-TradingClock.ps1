# Sync-TradingClock.ps1 — Guard de reloj para trading live Binance USDT-M
#
# USO:
#   .\Sync-TradingClock.ps1                 # mide offset y resincroniza si hace falta
#   .\Sync-TradingClock.ps1 -CheckOnly      # solo mide, no toca el reloj (no requiere admin)
#   .\Sync-TradingClock.ps1 -MaxOffsetMs 300
#
# QUÉ HACE:
#   1. Mide el offset entre el reloj local y el servidor de Binance USDT-M
#      (https://fapi.binance.com/fapi/v1/time), compensando el RTT de la medición.
#   2. Si |offset| supera el umbral, fuerza un resync NTP de Windows (w32tm /resync).
#      El resync requiere privilegios de administrador o ejecución como SYSTEM
#      (así corre desde la tarea programada que instala Install-TradingClockSync.ps1).
#   3. Vuelve a medir y reporta. Exit code 0 si el offset final <= umbral, 1 si lo supera.
#
# POR QUÉ:
#   Binance rechaza requests con timestamp > 1000ms adelantado del server (error -1021).
#   recvWindow NO ayuda: solo tolera timestamps atrasados. La única defensa contra un reloj
#   adelantado es mantenerlo sincronizado. Este guard se usa como pre-flight antes de F5 y
#   como tarea de fondo periódica (ver Install-TradingClockSync.ps1).
#
# CRITERIO:
#   Umbral por defecto 500ms — la mitad de la tolerancia de Binance (1000ms), con margen
#   para drift entre el resync y el primer request del algoritmo.

param(
    [int]$MaxOffsetMs = 500,
    [switch]$CheckOnly,
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Line {
    param([string]$Message)
    if (-not $Quiet) { Write-Host $Message }
}

# Mide (local - server) en ms, compensando el RTT: asume que la respuesta del server
# refleja su reloj en el punto medio del round-trip.
function Measure-BinanceOffsetMs {
    $t0 = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $response = Invoke-WebRequest -Uri "https://fapi.binance.com/fapi/v1/time" `
        -UseBasicParsing -TimeoutSec 10
    $t1 = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

    $serverMs = [long]($response.Content | ConvertFrom-Json).serverTime
    $rttMs = $t1 - $t0
    $localMidpointMs = $t0 + [long]($rttMs / 2)
    return [long]($localMidpointMs - $serverMs)
}

function Test-IsAdministrator {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

try {
    $offset = Measure-BinanceOffsetMs
    Write-Line "Offset inicial (local - Binance): $offset ms (umbral +-$MaxOffsetMs ms)"

    if ([Math]::Abs($offset) -le $MaxOffsetMs) {
        Write-Line "Reloj dentro de tolerancia. OK."
        exit 0
    }

    if ($CheckOnly) {
        Write-Line "DRIFT fuera de tolerancia ($offset ms). -CheckOnly: no se resincroniza."
        exit 1
    }

    if (-not (Test-IsAdministrator)) {
        Write-Line "DRIFT fuera de tolerancia ($offset ms) pero el proceso no es administrador."
        Write-Line "Ejecutar 'w32tm /resync /force' en una terminal elevada, o instalar la tarea"
        Write-Line "programada con Install-TradingClockSync.ps1 (corre como SYSTEM)."
        exit 1
    }

    Write-Line "DRIFT fuera de tolerancia ($offset ms). Forzando resync NTP..."
    $svc = Get-Service w32time
    if ($svc.Status -ne 'Running') { Start-Service w32time }
    & w32tm /resync /force | Out-Null

    # Dar un instante al servicio para aplicar la corrección antes de re-medir.
    Start-Sleep -Seconds 2
    $offsetAfter = Measure-BinanceOffsetMs
    Write-Line "Offset post-resync (local - Binance): $offsetAfter ms"

    if ([Math]::Abs($offsetAfter) -le $MaxOffsetMs) {
        Write-Line "Reloj corregido dentro de tolerancia. OK."
        exit 0
    }

    Write-Line "El resync no llevo el offset bajo el umbral ($offsetAfter ms). Revisar w32time."
    exit 1
}
catch {
    Write-Line "ERROR midiendo/sincronizando el reloj: $($_.Exception.Message)"
    exit 1
}
