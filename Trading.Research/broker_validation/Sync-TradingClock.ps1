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
#   2. Si |offset| supera el umbral, intenta primero un resync NTP de Windows (w32tm /resync).
#      Si tras el resync el offset sigue fuera de tolerancia (típico cuando UDP 123 / NTP está
#      bloqueado por la red), hace fallback: setea el reloj del SO directamente al tiempo del
#      servidor de Binance vía Set-Date. Ambas correcciones requieren admin o ejecución como
#      SYSTEM (así corre la tarea programada de Install-TradingClockSync.ps1).
#   3. Vuelve a medir y reporta. Exit code 0 si el offset final <= umbral, 1 si lo supera.
#
# POR QUÉ:
#   Binance rechaza requests con timestamp > 1000ms adelantado del server (error -1021).
#   recvWindow NO ayuda: solo tolera timestamps atrasados. La única defensa contra un reloj
#   adelantado es mantenerlo sincronizado. NTP es el camino "correcto" pero si la red bloquea
#   UDP 123 es inútil; para trading lo que importa es coincidir con Binance, así que alinear el
#   reloj a su server time (por HTTPS, que ya está abierto) es la defensa más confiable.
#   Este guard se usa como pre-flight antes de F5 y como tarea de fondo (Install-TradingClockSync.ps1).
#
# CRITERIO:
#   Umbral por defecto 500ms — la mitad de la tolerancia de Binance (1000ms), con margen
#   para drift entre el resync y el primer request del algoritmo.

param(
    [int]$MaxOffsetMs = 500,
    [switch]$CheckOnly,
    [switch]$Force,       # saltea el intento NTP y setea el reloj directo desde Binance
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

# Setea el reloj del SO directamente al tiempo del servidor de Binance, compensando el RTT.
# Requiere admin/SYSTEM. Es el fallback cuando NTP no está disponible (UDP 123 bloqueado).
function Set-ClockFromBinance {
    $t0 = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $response = Invoke-WebRequest -Uri "https://fapi.binance.com/fapi/v1/time" `
        -UseBasicParsing -TimeoutSec 10
    $t1 = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

    $serverMs = [long]($response.Content | ConvertFrom-Json).serverTime
    $rttMs = $t1 - $t0
    # El server reflejó su reloj ~a la mitad del round-trip; al aplicar Set-Date ya pasó
    # otra media vuelta, así que el objetivo es serverTime + RTT/2.
    $targetMs = $serverMs + [long]($rttMs / 2)
    $targetLocal = [DateTimeOffset]::FromUnixTimeMilliseconds($targetMs).LocalDateTime
    Set-Date -Date $targetLocal | Out-Null
}

try {
    $offset = Measure-BinanceOffsetMs
    Write-Line "Offset inicial (local - Binance): $offset ms (umbral +-$MaxOffsetMs ms)"

    if (-not $Force -and [Math]::Abs($offset) -le $MaxOffsetMs) {
        Write-Line "Reloj dentro de tolerancia. OK."
        exit 0
    }

    if ($CheckOnly) {
        Write-Line "DRIFT fuera de tolerancia ($offset ms). -CheckOnly: no se corrige."
        exit 1
    }

    if (-not (Test-IsAdministrator)) {
        Write-Line "DRIFT fuera de tolerancia ($offset ms) pero el proceso no es administrador."
        Write-Line "Correr este script en una terminal elevada, o instalar la tarea programada"
        Write-Line "con Install-TradingClockSync.ps1 (corre como SYSTEM)."
        exit 1
    }

    # Paso 1: intento NTP (salvo -Force). Es preferible si funciona: mantiene el reloj
    # sobre el sync nativo de Windows.
    if (-not $Force) {
        Write-Line "DRIFT fuera de tolerancia ($offset ms). Intentando resync NTP..."
        $svc = Get-Service w32time
        if ($svc.Status -ne 'Running') { Start-Service w32time }
        & w32tm /resync /force | Out-Null
        Start-Sleep -Seconds 2

        $offset = Measure-BinanceOffsetMs
        Write-Line "Offset post-resync NTP (local - Binance): $offset ms"
        if ([Math]::Abs($offset) -le $MaxOffsetMs) {
            Write-Line "Reloj corregido por NTP dentro de tolerancia. OK."
            exit 0
        }
        Write-Line "NTP no alcanzó (probable UDP 123 bloqueado). Fallback: set directo desde Binance."
    }

    # Paso 2: fallback / -Force — setear el reloj directo al server time de Binance.
    Set-ClockFromBinance
    Start-Sleep -Milliseconds 200
    $offsetAfter = Measure-BinanceOffsetMs
    Write-Line "Offset post-set Binance (local - Binance): $offsetAfter ms"

    if ([Math]::Abs($offsetAfter) -le $MaxOffsetMs) {
        Write-Line "Reloj alineado a Binance dentro de tolerancia. OK."
        exit 0
    }

    Write-Line "El set directo no llevó el offset bajo el umbral ($offsetAfter ms). Revisar."
    exit 1
}
catch {
    Write-Line "ERROR midiendo/sincronizando el reloj: $($_.Exception.Message)"
    exit 1
}
