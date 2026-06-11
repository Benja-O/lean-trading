<#
.SYNOPSIS
    Genera el esqueleto de una nueva IStrategy: clase, tests y snippet JSON.

.PARAMETER Name
    Nombre base en PascalCase (ej: RsiMeanReversion). El sufijo "Strategy" se agrega
    automaticamente. Si lo pasas incluido, se normaliza igual.

.EXAMPLE
    .\New-Strategy.ps1 -Name RsiMeanReversion
    .\New-Strategy.ps1 -Name RsiMeanReversionStrategy
#>
param(
    [Parameter(Mandatory)]
    [string]$Name
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Normalizacion
$BaseName = $Name -replace 'Strategy$', ''

if ($BaseName -notmatch '^[A-Z][A-Za-z0-9]+$') {
    Write-Error "El nombre debe ser PascalCase sin espacios (ej: RsiMeanReversion). Recibido: '$BaseName'"
    exit 1
}

$ClassName    = $BaseName + 'Strategy'
$RepoRoot     = $PSScriptRoot
$StrategyPath = Join-Path $RepoRoot ('Trading.Strategies\Implementations\' + $ClassName + '.cs')
$TestsPath    = Join-Path $RepoRoot ('Trading.Application.Tests\Strategies\' + $ClassName + 'Tests.cs')

# Guard: no sobreescribir trabajo existente
foreach ($path in @($StrategyPath, $TestsPath)) {
    if (Test-Path $path) {
        Write-Error ('Ya existe: ' + $path + ' -- eliminalo manualmente si queres regenerar.')
        exit 1
    }
}

# Templates (single-quoted: sin expansion de PS, seguro para codigo C# con $ y {})

$StrategyTemplate = @'
using System.Collections.Generic;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Implementations
{
    public sealed class __CLASS__ : IStrategy
    {
        private readonly Dictionary<string, object> _stateBySymbol = new();

        // TODO: ajustar al periodo del indicador mas lento
        public int WarmUpBars => 1;

        public SignalDirection EvaluateSignal(MarketBar marketBar)
        {
            string ticker = marketBar.InstrumentId.Ticker;
            // TODO: implementar logica de senal
            return SignalDirection.Flat;
        }
    }
}
'@

$TestsTemplate = @'
using FluentAssertions;
using System;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Trading.Strategies.Implementations;
using Xunit;

namespace Trading.Application.Tests.Strategies
{
    public class __CLASS__Tests
    {
        private static readonly InstrumentId BtcUsdt = new("BTCUSDT");

        private static MarketBar BuildBar(decimal close, int barIndex) =>
            new(BtcUsdt, close,
                new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(barIndex));

        [Fact]
        public void WarmUpBars_ReturnsExpectedValue()
        {
            var strategy = new __CLASS__();
            strategy.WarmUpBars.Should().BeGreaterThan(0);
        }

        [Fact]
        public void EvaluateSignal_DuringWarmUp_ReturnsFlat()
        {
            var strategy = new __CLASS__();
            for (int i = 0; i < strategy.WarmUpBars - 1; i++)
            {
                strategy.EvaluateSignal(BuildBar(100m, i))
                    .Should().Be(SignalDirection.Flat,
                        because: $"Barra {i} esta en periodo de warm-up.");
            }
        }

        [Fact]
        public void EvaluateSignal_TODO_DescribeScenario()
        {
            // TODO: implementar test para el escenario principal de senal
            var strategy = new __CLASS__();
            Assert.True(false, "Test pendiente de implementacion.");
        }
    }
}
'@

# Sustitucion y escritura
$StrategyContent = $StrategyTemplate -replace '__CLASS__', $ClassName
$TestsContent    = $TestsTemplate    -replace '__CLASS__', $ClassName

[System.IO.File]::WriteAllText($StrategyPath, $StrategyContent)
[System.IO.File]::WriteAllText($TestsPath,    $TestsContent)

# Salida
$lowerBase = $BaseName.ToLower()

$factoryLine = '    "' + $lowerBase + 'strategy" or "' + $lowerBase + '" => new ' + $ClassName + '(),'

$jsonLines = @(
    '    {',
    ('      "StrategyName": "' + $ClassName + '",'),
    '      "Symbol":       "BTCUSDT",',
    '      "StopLossPercentage":    5.0,',
    '      "TakeProfitPercentage": 10.0,',
    '      "RiskPerTradePercentage": 1.0,',
    '      "MaxBars": 8',
    '    }'
)

Write-Host ''
Write-Host ('  ' + $StrategyPath) -ForegroundColor Green
Write-Host ('  ' + $TestsPath)    -ForegroundColor Green
Write-Host ''
Write-Host 'Proximos pasos:' -ForegroundColor Cyan
Write-Host ''
Write-Host ('1. Implementar la logica en ' + $ClassName + '.cs (buscar los TODO).')
Write-Host ''
Write-Host '2. Registrar en StrategyFactory.cs:'
Write-Host $factoryLine -ForegroundColor Yellow
Write-Host ''
Write-Host '3. Agregar a strategies.json bajo el timeframe correspondiente:'
foreach ($line in $jsonLines) { Write-Host $line -ForegroundColor Yellow }
Write-Host ''
Write-Host ('4. Completar los TODO en ' + $ClassName + 'Tests.cs antes del M4.')
Write-Host ''
