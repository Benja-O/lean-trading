# Install-TradingService.ps1 - Instala el Launcher de Lean como servicio Windows via NSSM
#
# USO (en PowerShell ELEVADO, en el VPS):
#   .\Install-TradingService.ps1 -LauncherDir "C:\trading\Lean\Launcher\bin\Release" `
#       -NssmPath "C:\tools\nssm\nssm.exe" -HealthchecksPingUrl "https://hc-ping.com/<uuid>"
#   .\Install-TradingService.ps1 -LauncherDir "..." -Uninstall
#
# QUE HACE:
#   Registra QuantConnect.Lean.Launcher.exe como servicio Windows con auto-restart. Cuando el
#   dead-man's switch del feed llama Environment.Exit(1) (ADR-042), NSSM relanza el proceso con
#   un socket WebSocket limpio. Redirige stdout/stderr a archivos y arranca automatico al boot.
#
# POR QUE NSSM:
#   El codigo asume un supervisor que reinicia ante Exit(1) ("NSSM reconecta con socket limpio").
#   NSSM (Non-Sucking Service Manager) envuelve un exe de consola como servicio con politica de
#   restart configurable. Descargar de https://nssm.cc/ y pasar la ruta con -NssmPath.
#
# NOTA: script en ASCII puro a proposito (PowerShell 5.1 sin BOM mal-interpreta caracteres no-ASCII).

param(
    [string]$ServiceName = "LeanTrading",
    [Parameter(Mandatory = $true)][string]$LauncherDir,
    [string]$NssmPath = "nssm",
    [string]$HealthchecksPingUrl = "",
    [int]$RestartDelayMs = 5000,
    [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object System.Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    Write-Host "ERROR: requiere privilegios de administrador."
    exit 1
}

# Resolver nssm.exe
$nssm = (Get-Command $NssmPath -ErrorAction SilentlyContinue)
if ($null -eq $nssm) {
    Write-Host "ERROR: no se encontro nssm en '$NssmPath'. Descargar de https://nssm.cc/ y pasar -NssmPath."
    exit 1
}
$nssmExe = $nssm.Source

if ($Uninstall) {
    & $nssmExe stop $ServiceName confirm
    & $nssmExe remove $ServiceName confirm
    Write-Host "Servicio '$ServiceName' removido."
    exit 0
}

$launcherExe = Join-Path $LauncherDir "QuantConnect.Lean.Launcher.exe"
if (-not (Test-Path $launcherExe)) {
    Write-Host "ERROR: no se encuentra el Launcher en '$launcherExe'. Revisar -LauncherDir."
    exit 1
}

$logDir = Join-Path $LauncherDir "service-logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }

# Si ya existe, lo removemos para reconfigurar limpio.
$existing = & $nssmExe status $ServiceName 2>$null
if ($LASTEXITCODE -eq 0) {
    & $nssmExe stop $ServiceName confirm 2>$null
    & $nssmExe remove $ServiceName confirm
}

Write-Host "Instalando servicio '$ServiceName' -> $launcherExe"
& $nssmExe install $ServiceName $launcherExe
& $nssmExe set $ServiceName AppDirectory $LauncherDir
& $nssmExe set $ServiceName AppStdout (Join-Path $logDir "service-stdout.log")
& $nssmExe set $ServiceName AppStderr (Join-Path $logDir "service-stderr.log")

# Rotacion de logs por NSSM (evita que crezcan sin limite).
& $nssmExe set $ServiceName AppRotateFiles 1
& $nssmExe set $ServiceName AppRotateBytes 10485760   # 10 MB

# Auto-restart ante cualquier salida (incluye Environment.Exit(1) del dead-man's switch).
& $nssmExe set $ServiceName AppExit Default Restart
& $nssmExe set $ServiceName AppRestartDelay $RestartDelayMs
# Throttle: si el proceso muere antes de N ms, NSSM espera mas para evitar loops de restart.
& $nssmExe set $ServiceName AppThrottle 10000

& $nssmExe set $ServiceName Start SERVICE_AUTO_START
& $nssmExe set $ServiceName DisplayName "Lean Trading (live)"
& $nssmExe set $ServiceName Description "QuantConnect Lean - trading live Binance USDT-M. Auto-restart ante feed stall (ADR-042)."

if (-not [string]::IsNullOrWhiteSpace($HealthchecksPingUrl)) {
    & $nssmExe set $ServiceName AppEnvironmentExtra "HEALTHCHECKS_PING_URL=$HealthchecksPingUrl"
    Write-Host "HEALTHCHECKS_PING_URL configurado en el entorno del servicio."
}

Write-Host "Arrancando servicio..."
& $nssmExe start $ServiceName

Write-Host ""
Write-Host "Servicio '$ServiceName' instalado y arrancado."
Write-Host "  Logs servicio: $logDir"
Write-Host "  Estado:        nssm status $ServiceName  (o services.msc)"
Write-Host "  Heartbeat:     [LauncherDir]\health\heartbeat.json"
Write-Host "  Detener:       nssm stop $ServiceName"
exit 0
