# Install-TradingClockSync.ps1 — Instala el guard de reloj como tarea de fondo
#
# USO (UNA VEZ, en PowerShell ELEVADO / como administrador):
#   .\Install-TradingClockSync.ps1
#   .\Install-TradingClockSync.ps1 -IntervalMinutes 30   # cadencia de resync (default 60)
#   .\Install-TradingClockSync.ps1 -Uninstall            # elimina la tarea
#
# QUÉ HACE:
#   1. Configura el servicio de tiempo de Windows (w32time) con peers NTP confiables,
#      sincronización manual, polling frecuente y arranque automático.
#   2. Registra una tarea programada que ejecuta Sync-TradingClock.ps1 como SYSTEM
#      al inicio del sistema y luego cada N minutos. SYSTEM tiene permiso para resync,
#      así no hay que elevar nada en cada corrida del algoritmo.
#   3. Fuerza un resync inicial.
#
# POR QUÉ:
#   El drift observado (~2s en pocos dias) indica que el sync nativo de Windows no corre
#   con frecuencia suficiente para la tolerancia de Binance (1000ms). Esta tarea mantiene
#   el reloj alineado de forma desatendida y evita que el error -1021 frene las corridas live.

param(
    [int]$IntervalMinutes = 60,
    [int]$MaxOffsetMs = 500,
    [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$taskName = "TradingClockSync"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workerPath = Join-Path $scriptDir "Sync-TradingClock.ps1"

function Test-IsAdministrator {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    Write-Host "ERROR: este instalador requiere privilegios de administrador."
    Write-Host "Abrir PowerShell como administrador y volver a ejecutar."
    exit 1
}

if ($Uninstall) {
    if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        Write-Host "Tarea '$taskName' eliminada."
    } else {
        Write-Host "La tarea '$taskName' no existe; nada que eliminar."
    }
    exit 0
}

if (-not (Test-Path $workerPath)) {
    Write-Host "ERROR: no se encuentra el worker en '$workerPath'."
    exit 1
}

# ===== 1. Configurar w32time =====
Write-Host "Configurando w32time (peers NTP, polling frecuente, arranque automatico)..."
& w32tm /config /manualpeerlist:"time.windows.com,0x9 pool.ntp.org,0x9 time.google.com,0x9" `
    /syncfromflags:manual /reliable:yes /update | Out-Null
Set-Service -Name w32time -StartupType Automatic
Restart-Service w32time

# ===== 2. Registrar la tarea programada =====
Write-Host "Registrando tarea programada '$taskName' (cada $IntervalMinutes min, como SYSTEM)..."

$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$workerPath`" -MaxOffsetMs $MaxOffsetMs -Quiet"

$triggerAtStartup = New-ScheduledTaskTrigger -AtStartup
$triggerRepeat = New-ScheduledTaskTrigger -Once -At (Get-Date) `
    -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes)

$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount `
    -RunLevel Highest

$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable `
    -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Minutes 5)

if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

Register-ScheduledTask -TaskName $taskName `
    -Action $action `
    -Trigger @($triggerAtStartup, $triggerRepeat) `
    -Principal $principal `
    -Settings $settings `
    -Description "Mantiene el reloj sincronizado con NTP para evitar el error -1021 de Binance en trading live." | Out-Null

# ===== 3. Resync inicial =====
Write-Host "Forzando resync inicial..."
& w32tm /resync /force | Out-Null

Write-Host ""
Write-Host "Instalacion completa. La tarea '$taskName' corre al inicio y cada $IntervalMinutes min."
Write-Host "Verificar offset actual con: .\Sync-TradingClock.ps1 -CheckOnly"
exit 0
