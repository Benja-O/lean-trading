# reconcile.ps1 — Hito D-prev: reconciliación de balance Lean vs Binance USDT-M
#
# USO:
#   $env:BINANCE_API_KEY    = "tu-api-key-con-lectura-futures"
#   $env:BINANCE_API_SECRET = "tu-api-secret"
#   .\reconcile.ps1 [-HeartbeatPath "ruta\al\heartbeat.json"]
#
# QUÉ HACE:
#   1. Lee el balance USDT que Lean reporta en heartbeat.json (HealthHeartbeatTracker).
#   2. Consulta la API de Binance USDT-M Futures para obtener el balance real.
#   3. Calcula la discrepancia absoluta y porcentual.
#   4. Imprime un resumen y retorna exit code 0 si discrepancia <= 0.5%, 1 si supera.
#
# CRITERIO DE ACEPTACIÓN (Hito D-prev):
#   Discrepancia <= 0.5% del balance real.
#   Una discrepancia dentro de este rango se explica por comisiones/funding no modelados.
#   Una discrepancia mayor indica un bug en LeanPortfolioAdapter o en el brokerage adapter.

param(
    [string]$HeartbeatPath = "F:\Lean\data\results\heartbeat.json",
    [decimal]$MaxDiscrepancyPct = 0.5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-HmacSha256 {
    param([string]$Secret, [string]$Message)
    $keyBytes  = [System.Text.Encoding]::UTF8.GetBytes($Secret)
    $msgBytes  = [System.Text.Encoding]::UTF8.GetBytes($Message)
    $hmac      = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key  = $keyBytes
    $hashBytes = $hmac.ComputeHash($msgBytes)
    return ($hashBytes | ForEach-Object { $_.ToString("x2") }) -join ""
}

# ── 1. Balance Lean (heartbeat.json) ──────────────────────────────────────────

if (-not (Test-Path $HeartbeatPath)) {
    Write-Error "heartbeat.json no encontrado en '$HeartbeatPath'. Asegúrate de que el algoritmo corrió en live mode."
    exit 2
}

$heartbeat     = Get-Content $HeartbeatPath -Raw | ConvertFrom-Json
$leanBalanceRaw = $heartbeat.PortfolioValueUsdt
if ($null -eq $leanBalanceRaw) {
    Write-Error "Campo 'PortfolioValueUsdt' ausente en heartbeat.json. Verificar HeartbeatFileWriter."
    exit 2
}
$leanBalance = [decimal]$leanBalanceRaw

# ── 2. Balance real Binance USDT-M (REST API) ─────────────────────────────────

$apiKey    = $env:BINANCE_API_KEY
$apiSecret = $env:BINANCE_API_SECRET

if ([string]::IsNullOrEmpty($apiKey) -or [string]::IsNullOrEmpty($apiSecret)) {
    Write-Error "Variables de entorno BINANCE_API_KEY y BINANCE_API_SECRET no configuradas."
    exit 2
}

$timestamp  = [long]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())
$queryStr   = "timestamp=$timestamp"
$signature  = Get-HmacSha256 -Secret $apiSecret -Message $queryStr
$url        = "https://fapi.binance.com/fapi/v2/balance?$queryStr&signature=$signature"

$headers = @{ "X-MBX-APIKEY" = $apiKey }
$response = Invoke-RestMethod -Uri $url -Headers $headers -Method GET

$usdtAsset = $response | Where-Object { $_.asset -eq "USDT" }
if ($null -eq $usdtAsset) {
    Write-Error "No se encontró el asset USDT en la respuesta de Binance Futures."
    exit 2
}

$binanceBalance = [decimal]$usdtAsset.balance

# ── 3. Cálculo de discrepancia ────────────────────────────────────────────────

$discrepancyAbs = [Math]::Abs($leanBalance - $binanceBalance)
$discrepancyPct = if ($binanceBalance -ne 0) {
    [Math]::Round(($discrepancyAbs / $binanceBalance) * 100, 4)
} else { 0 }

$status = if ($discrepancyPct -le $MaxDiscrepancyPct) { "OK" } else { "FAIL" }

# ── 4. Resumen ────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "════════════════════════════════════════════"
Write-Host "  Hito D-prev — Reconciliación de balance"
Write-Host "════════════════════════════════════════════"
Write-Host ("  Lean (heartbeat.json) : {0:F2} USDT" -f $leanBalance)
Write-Host ("  Binance Futures (API) : {0:F2} USDT" -f $binanceBalance)
Write-Host ("  Discrepancia          : {0:F4} USDT  ({1:F4}%)" -f $discrepancyAbs, $discrepancyPct)
Write-Host ("  Umbral aceptación     : <= {0}%" -f $MaxDiscrepancyPct)
Write-Host ("  Resultado             : {0}" -f $status)
Write-Host "════════════════════════════════════════════"
Write-Host ""

if ($status -eq "OK") {
    Write-Host "RECONCILIACIÓN APROBADA — balance dentro del margen de tolerancia." -ForegroundColor Green
    exit 0
} else {
    Write-Host "RECONCILIACIÓN FALLIDA — discrepancia supera el umbral." -ForegroundColor Red
    Write-Host "Verificar LeanPortfolioAdapter y el log JSONL para órdenes no registradas."
    exit 1
}
