# INFRA_3 — Aceleración del ciclo de iteración: solution filter, gates ejecutables, test de paridad de backtest y autonomía controlada de build/test (brief v2 — solo código)

> Reducir el ciclo build+test del orden de 15 minutos al orden de 2-4 minutos, ejecutarlo automáticamente sin operador humano en el medio, y proteger las invariantes arquitectónicas y la paridad del backtest con checks automáticos en lugar de revisión manual del diff.

---

## 0. Estado de los archivos `.md` versionados (importante)

**Los cambios a `AI.md`, `ROADMAP.md` y `DECISIONS.md` que este refactor requiere YA FUERON APLICADOS por el operador antes de pasarte este brief.** Concretamente:

- `AI.md` ya incluye la sección nueva "🛠️ Comandos de Build", el bloque "Cómo opera Claude Code en un brief con build/test habilitado", la actualización de §"Testing" punto 3 (referenciando `Trading.IntegrationTests`), y la reescritura de §"Límites de Ejecución del Asistente" puntos 2 y 3 con la nueva política de build/test.
- `ROADMAP.md` ya tiene `INFRA-3 ✅` en el diagrama del Plan general, en la tabla de pendientes del Bloque 3 (antes de OPS-2), y la entrada de cierre en "Historial completado".
- `DECISIONS.md` ya tiene `ADR-023` al inicio cubriendo las 13 decisiones técnicas y las 5 alternativas descartadas.

**Tu trabajo en este brief es SOLO el código y los archivos asociados.** No modifiques los `.md` versionados (`AI.md`, `ROADMAP.md`, `DECISIONS.md`, `POLICY.md`). Si detectás que alguno necesita un ajuste menor que el operador omitió, **detenete y reportá**; no edites por iniciativa propia.

Sí debés **leer y respetar** los `.md` actualizados: ellos contienen las reglas vigentes para tu ejecución (especialmente `AI.md` §"Límites de Ejecución del Asistente" reescrito). Sin embargo, durante la ejecución de este brief específico aplican las restricciones del §2 abajo, que son más estrictas que la política nueva de AI.md.

---

## 1. Pre-requisitos

- Rama de trabajo limpia, sin cambios sin commitear.
- Bloque 3 en progreso, con INFRA-1, INFRA-2 y OPS-1 ya completados.
- Los `.md` versionados (`AI.md`, `ROADMAP.md`, `DECISIONS.md`) ya están actualizados (ver §0).
- Estado del repo verde: la solución compila (aun lentamente) y todos los tests actuales pasan.
- El operador conoce las métricas baseline del backtest BTCUSDT 2025-01-01 a 2026-03-31 con la configuración actual: **225 órdenes** post-filtro HMM (documentado en ADR-021 e historial INFRA-2). Los valores exactos de P&L final y máximo drawdown te los confirma el operador en chat durante la Sub-pieza C (paso 1), porque no están en los `.md` y deben venir de la última corrida real.
- El operador tiene una sesión limpia del IDE/editor (no hay otro proceso compilando `QuantConnect.Lean.sln` en paralelo).

---

## 2. Reglas operativas (inquebrantables) durante este brief

**Atención: las reglas de este §2 son ESPECÍFICAS para la ejecución de este brief y son más estrictas que la política nueva ya escrita en `AI.md` §"Límites de Ejecución del Asistente". Durante INFRA-3 priman las reglas de este §2.** La política nueva de `AI.md` aplica desde el próximo brief en adelante.

Aplican las reglas generales de `AI.md`. Recordatorios críticos:

- **No ejecutar `git` de ningún tipo** salvo lectura pura (`git status`, `git log`, `git diff`) y solo si es estrictamente necesario para diagnosticar. Lista exhaustiva en `AI.md` §"Límites de Ejecución del Asistente" punto 1.
- **No compilar ni ejecutar tests durante este brief**, con UNA excepción específica: el comando `dotnet sln add Trading.IntegrationTests/Trading.IntegrationTests.csproj` en Sub-pieza C paso 5 está autorizado explícitamente porque registrar el proyecto en el `.sln` es la única manera práctica de hacerlo sin que el operador edite el `.sln` a mano (formato complejo, propenso a corrupción). Ningún otro comando de `dotnet` está autorizado durante este brief.
- **No tocar archivos `.md` versionados** (`AI.md`, `ROADMAP.md`, `DECISIONS.md`, `POLICY.md`). Ya fueron actualizados por el operador (§0).
- **No tocar archivos bajo `*Tests.cs`, `*Tests.csproj` ni directorios `*Tests/`** salvo en Sub-pieza C donde está explícitamente autorizado crear el proyecto nuevo `Trading.IntegrationTests` y sus archivos asociados.
- **Detención obligatoria al final de cada sub-pieza.** Te detenés, reportás lo hecho, y esperás que el operador valide y dé OK explícito antes de avanzar a la siguiente sub-pieza. No encadenar A→B→C→D sin checkpoints intermedios.
- **Mensaje de commit sugerido por sub-pieza:** al final de cada sub-pieza proponés el mensaje de commit correspondiente (ver §6). El operador commitea cada sub-pieza por separado (4 commits) o las agrupa según prefiera.

---

## 3. Decisiones técnicas aplicadas

Las decisiones D1-D13 que rigen este refactor están todas documentadas en `ADR-023` (que ya está en `DECISIONS.md`). Leelo antes de empezar; resume las decisiones cerradas que NO se discuten durante la ejecución. Resumen rápido para tener a mano:

- D1-D2: solution filter `QuantConnect.Lean.slnf` en la raíz, con los 6 proyectos `Trading.*` (incluido `Trading.IntegrationTests` que vas a crear en Sub-pieza C). NO incluye `HmmTrainer`.
- D3-D4: script en PowerShell, ubicación `scripts/check.ps1`.
- D5: tests lentos marcados `[Trait("Category", "Slow")]`, categoría única.
- D6-D7: comandos para iteración rápida y verificación completa.
- D8: test de paridad en proyecto nuevo `Trading.IntegrationTests/Backtests/BacktestParityTests.cs`.
- D9: backtest invocado in-process; si no es trivial, detenerse y reportar (NO improvisar con proceso hijo).
- D10: tolerancia `1e-6` relativa para decimales, igualdad estricta para enteros.
- D11-D13: salvaguardas operativas (reglas de la política nueva ya en `AI.md`).

---

## 4. Estructura del trabajo (sub-piezas y orden)

Cuatro sub-piezas con dependencia secuencial estricta:

```
Sub-pieza A: crear QuantConnect.Lean.slnf
    ↓ (operador valida que dotnet build sobre el .slnf compila más rápido que sobre el .sln)
Sub-pieza B: crear scripts/check.ps1
    ↓ (operador valida que el script corre y reporta correctamente)
Sub-pieza C: crear proyecto Trading.IntegrationTests con BacktestParityTests
    ↓ (operador valida que el test compila; si la API in-process de Lean funcionó, valida también que el test pasa con baseline)
Sub-pieza D: cierre
    ↓
Mensaje de commit unificado o por sub-pieza, listo para el operador
```

Razón del orden: cada sub-pieza prepara la pieza que la siguiente necesita. C no se puede crear sin que A esté funcionando (el `.slnf` debe incluir desde el inicio a `Trading.IntegrationTests`); B se beneficia de A pero no la requiere estrictamente (igual se hace después de A para mantener el orden mental "primero acelero, después agrego gates").

---

## 5. Alcance detallado por sub-pieza

### 5.A — Sub-pieza A: Solution filter `QuantConnect.Lean.slnf`

**Objetivo:** que `dotnet build QuantConnect.Lean.slnf` compile solo los proyectos del operador + el subset transitivo de Lean que efectivamente referencian, en lugar de toda la solución.

**Trabajo:**

1. **Crear archivo `QuantConnect.Lean.slnf` en la raíz del repo** (al lado de `QuantConnect.Lean.sln`). Contenido literal:

```json
{
  "solution": {
    "path": "QuantConnect.Lean.sln",
    "projects": [
      "Trading.Domain\\Trading.Domain.csproj",
      "Trading.Application\\Trading.Application.csproj",
      "Trading.Strategies\\Trading.Strategies.csproj",
      "Trading.Domain.Tests\\Trading.Domain.Tests.csproj",
      "Trading.Application.Tests\\Trading.Application.Tests.csproj",
      "Trading.Strategies.Tests\\Trading.Strategies.Tests.csproj"
    ]
  }
}
```

Notas:
- Backslashes escapados (`\\`) porque es JSON y el repo es Windows. Es el formato canónico de los `.slnf` generados por Visual Studio.
- `Trading.IntegrationTests` NO está acá todavía (se agrega en Sub-pieza C como modificación a este mismo archivo).
- `HmmTrainer` NO está en la lista por decisión D2 del ADR-023.

**Detención al final de Sub-pieza A:** reportar al operador:
- Archivo creado (`QuantConnect.Lean.slnf`).
- Comando sugerido para validar: `dotnet build QuantConnect.Lean.slnf` debe completar en 2-4 min en frío, segundos en incremental (vs ~15 min sobre el `.sln` completo).
- Mensaje de commit sugerido (ver §6, commit 1).

Esperar OK explícito del operador antes de avanzar a Sub-pieza B.

---

### 5.B — Sub-pieza B: Script `scripts/check.ps1`

**Objetivo:** combinar invariantes arquitectónicos (grep ejecutable de los anti-patrones del `AI.md` §"Anti-patrones Prohibidos") + build + suite rápida de tests en un único comando, como gate único de aceptación.

**Trabajo:**

1. **Crear directorio nuevo `scripts/` en la raíz del repo** (al lado de `QuantConnect.Lean.sln`, `QuantConnect.Lean.slnf`, `AI.md`, etc.).

2. **Crear archivo `scripts/check.ps1`** con el contenido literal siguiente:

```powershell
#requires -Version 5.1
<#
.SYNOPSIS
    Gate único de aceptación para cambios en proyectos Trading.*.

.DESCRIPTION
    Ejecuta en orden: (1) invariantes arquitectónicas como grep ejecutable,
    (2) build incremental del .slnf, (3) suite rápida de tests (excluye Category=Slow).

    Si cualquiera de los tres pasos falla, el script termina con exit code != 0
    y reporta cuál fue el fallo. Diseñado para ser invocado por Claude Code y
    por el operador antes de commitear.

    Para incluir el test de paridad de backtest (~100s adicionales), invocar
    con el switch -IncludeSlow.

.PARAMETER IncludeSlow
    Si está presente, corre la suite completa de tests (incluye Category=Slow).
    Sin este switch, excluye los tests Slow para mantener el ciclo rápido.

.PARAMETER SkipBuild
    Si está presente, omite el paso de dotnet build. Útil si el caller acaba
    de compilar y solo quiere re-correr invariantes + tests. Uso poco frecuente.

.EXAMPLE
    .\scripts\check.ps1
    # Ciclo rápido: invariantes + build + tests rápidos. Típico 1-3 min.

.EXAMPLE
    .\scripts\check.ps1 -IncludeSlow
    # Ciclo completo: invariantes + build + todos los tests. Típico 3-5 min.

.NOTES
    Las reglas de invariantes acá codificadas son un espejo de la sección
    "🚫 Anti-patrones Prohibidos" del AI.md. Si esa sección cambia, este
    script debe actualizarse en el mismo refactor.
#>

[CmdletBinding()]
param(
    [switch]$IncludeSlow,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host ""
Write-Host "===== check.ps1 — gate único de aceptación =====" -ForegroundColor Cyan
Write-Host "Repo root: $repoRoot"
Write-Host ""

# ---------------------------------------------------------------------------
# Paso 1: Invariantes arquitectónicas (grep ejecutable)
# ---------------------------------------------------------------------------

Write-Host "[1/3] Verificando invariantes arquitectónicas..." -ForegroundColor Yellow

$invariantFailures = @()

function Test-Invariant {
    param(
        [string]$Description,
        [string]$Pattern,
        [string[]]$Paths,
        [string[]]$ExcludePatterns = @()
    )

    $matches = @()
    foreach ($path in $Paths) {
        $fullPath = Join-Path $repoRoot $path
        if (-not (Test-Path $fullPath)) { continue }

        $found = Get-ChildItem -Path $fullPath -Recurse -Include *.cs -ErrorAction SilentlyContinue |
                 Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
                 Select-String -Pattern $Pattern -ErrorAction SilentlyContinue

        if ($ExcludePatterns.Count -gt 0) {
            $found = $found | Where-Object {
                $line = $_.Line
                -not ($ExcludePatterns | Where-Object { $line -match $_ })
            }
        }

        $matches += $found
    }

    if ($matches.Count -gt 0) {
        $script:invariantFailures += [PSCustomObject]@{
            Description = $Description
            Matches     = $matches
        }
        Write-Host "  ✗ $Description ($($matches.Count) violaciones)" -ForegroundColor Red
    }
    else {
        Write-Host "  ✓ $Description" -ForegroundColor Green
    }
}

# Invariante 1: cero `using QuantConnect` en Domain/Application
Test-Invariant `
    -Description "Sin 'using QuantConnect' en Trading.Domain y Trading.Application" `
    -Pattern '^\s*using\s+QuantConnect' `
    -Paths @('Trading.Domain', 'Trading.Application')

# Invariante 2: cero `DateTime.Now`/`DateTime.UtcNow` en Domain/Application
# (la versión wall clock real en Trading.Strategies es legítima — ADR-021 — y
#  está fuera del scope de este chequeo).
Test-Invariant `
    -Description "Sin DateTime.Now/UtcNow en Trading.Domain y Trading.Application" `
    -Pattern 'DateTime\.(UtcNow|Now)|DateTimeOffset\.Now' `
    -Paths @('Trading.Domain', 'Trading.Application')

# Invariante 3: cero `Console.WriteLine` (logging va por ITradingLogger)
Test-Invariant `
    -Description "Sin Console.WriteLine en código de producción" `
    -Pattern 'Console\.WriteLine' `
    -Paths @('Trading.Domain', 'Trading.Application', 'Trading.Strategies')

# Invariante 4: cero `throw new Exception` o `throw new ApplicationException`
Test-Invariant `
    -Description "Sin 'throw new Exception/ApplicationException' (usar excepciones de dominio)" `
    -Pattern 'throw\s+new\s+(Exception|ApplicationException|SystemException)\s*\(' `
    -Paths @('Trading.Domain', 'Trading.Application', 'Trading.Strategies')

# Invariante 5: cero `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` en producción
Test-Invariant `
    -Description "Sin .Result / .Wait() / .GetAwaiter().GetResult() en producción" `
    -Pattern '\.(Result|Wait\(\)|GetAwaiter\(\)\.GetResult\(\))' `
    -Paths @('Trading.Domain', 'Trading.Application', 'Trading.Strategies')

if ($invariantFailures.Count -gt 0) {
    Write-Host ""
    Write-Host "Violaciones detectadas:" -ForegroundColor Red
    foreach ($failure in $invariantFailures) {
        Write-Host ""
        Write-Host "  $($failure.Description)" -ForegroundColor Red
        foreach ($match in $failure.Matches) {
            $relativePath = $match.Path.Replace($repoRoot + '\', '')
            Write-Host "    $relativePath`:$($match.LineNumber): $($match.Line.Trim())"
        }
    }
    Write-Host ""
    Write-Host "FAIL: invariantes arquitectónicas violadas. Ver lista arriba." -ForegroundColor Red
    exit 1
}

Write-Host "  ✓ Todas las invariantes pasan." -ForegroundColor Green
Write-Host ""

# ---------------------------------------------------------------------------
# Paso 2: Build incremental del .slnf
# ---------------------------------------------------------------------------

if ($SkipBuild) {
    Write-Host "[2/3] Build SALTEADO (flag -SkipBuild)." -ForegroundColor Yellow
}
else {
    Write-Host "[2/3] Compilando QuantConnect.Lean.slnf..." -ForegroundColor Yellow
    Push-Location $repoRoot
    try {
        & dotnet build QuantConnect.Lean.slnf --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Host "FAIL: dotnet build retornó $LASTEXITCODE." -ForegroundColor Red
            exit 2
        }
        Write-Host "  ✓ Build OK." -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
    Write-Host ""
}

# ---------------------------------------------------------------------------
# Paso 3: Tests
# ---------------------------------------------------------------------------

$testFilter = if ($IncludeSlow) { '' } else { '--filter "Category!=Slow"' }
$testMode = if ($IncludeSlow) { 'completa (incluye Category=Slow)' } else { 'rápida (excluye Category=Slow)' }

Write-Host "[3/3] Corriendo suite $testMode..." -ForegroundColor Yellow
Push-Location $repoRoot
try {
    $testArgs = @('test', 'QuantConnect.Lean.slnf', '--nologo', '--verbosity', 'quiet', '--no-build')
    if ($testFilter) { $testArgs += @('--filter', 'Category!=Slow') }

    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "FAIL: dotnet test retornó $LASTEXITCODE." -ForegroundColor Red
        exit 3
    }
    Write-Host "  ✓ Tests OK." -ForegroundColor Green
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "===== check.ps1 — TODOS LOS GATES PASAN =====" -ForegroundColor Green
Write-Host ""
exit 0
```

**Detención al final de Sub-pieza B:** reportar:
- Archivo creado (`scripts/check.ps1`).
- Comandos sugeridos para validar:
  - `.\scripts\check.ps1` — debe imprimir los tres pasos con check verde, exit code 0, tiempo 1-3 min.
  - (Opcional) introducir una violación deliberada temporal (ej. `DateTime.UtcNow` en algún archivo de `Trading.Application`), correr el script, verificar que falla con exit code 1 y reporta la violación con archivo:línea. Revertir el cambio.
- Mensaje de commit sugerido (ver §6, commit 2).

Esperar OK explícito del operador antes de avanzar a Sub-pieza C.

---

### 5.C — Sub-pieza C: Proyecto `Trading.IntegrationTests` con `BacktestParityTests`

**Objetivo:** convertir POLICY.md §6.1 ("backtest del último mes da resultados idénticos al pre-cambio") de regla operativa cumplida a mano a gate automático ejecutable.

**Trabajo:**

1. **Confirmar las métricas baseline con el operador antes de codear el test.** Reportar al operador: "Necesito los siguientes valores exactos del backtest BTCUSDT 2025-01-01 a 2026-03-31 con la configuración actual de `strategies.json`, para fijarlos como baseline del test de paridad: (a) total de órdenes, (b) P&L final en USDT, (c) máximo drawdown en fracción decimal. Ya sé que el conteo de órdenes es 225 (documentado en ADR-021 e historial INFRA-2). Necesito P&L y DD exactos." El operador corre el backtest una vez, te pasa los tres números en el chat, y vos continuás.

2. **Crear estructura del proyecto nuevo.** Carpeta `Trading.IntegrationTests/` en la raíz del repo, hermana de `Trading.Application.Tests/`, `Trading.Domain.Tests/`, `Trading.Strategies.Tests/`.

3. **Crear `Trading.IntegrationTests/Trading.IntegrationTests.csproj`** con el contenido literal:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Trading.Application\Trading.Application.csproj" />
    <ProjectReference Include="..\Trading.Domain\Trading.Domain.csproj" />
    <ProjectReference Include="..\Trading.Strategies\Trading.Strategies.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

Notas:
- Mismas versiones de packages que los otros `*.Tests.csproj` actuales del repo. NO desviar.
- Referencia los 3 proyectos `Trading.*` igual que `Trading.Strategies.Tests`. Como `Trading.Strategies` arrastra QC, este proyecto va a tener acceso a Lean cuando se invoque el engine.
- NO se agrega `Microsoft.Extensions.Logging` ni otras dependencias del lado QC explícitamente; lo necesario vendrá vía transitivos. Si descubrís que falta algún paquete específico para arrancar el engine, **detenete y reportá** antes de agregar dependencias (ver §7 Riesgo R-C2).

4. **Agregar `Trading.IntegrationTests` al `.slnf` creado en Sub-pieza A.** Modificar `QuantConnect.Lean.slnf` para incluir la nueva entrada en `projects`, manteniendo orden alfabético:

```json
{
  "solution": {
    "path": "QuantConnect.Lean.sln",
    "projects": [
      "Trading.Application\\Trading.Application.csproj",
      "Trading.Application.Tests\\Trading.Application.Tests.csproj",
      "Trading.Domain\\Trading.Domain.csproj",
      "Trading.Domain.Tests\\Trading.Domain.Tests.csproj",
      "Trading.IntegrationTests\\Trading.IntegrationTests.csproj",
      "Trading.Strategies\\Trading.Strategies.csproj",
      "Trading.Strategies.Tests\\Trading.Strategies.Tests.csproj"
    ]
  }
}
```

5. **Agregar el proyecto al `.sln` también.** Sin esto, abrir la solución en Visual Studio no muestra el proyecto nuevo. Ejecutá EXACTAMENTE este comando (única excepción de ejecución autorizada en este brief):

```
dotnet sln QuantConnect.Lean.sln add Trading.IntegrationTests/Trading.IntegrationTests.csproj
```

NO otros sub-comandos de `dotnet sln`, NO `dotnet build`, NO `dotnet test` todavía. Si el comando falla, **detenete y reportá**; no edites el `.sln` a mano (formato complejo, propenso a corrupción).

6. **Crear `Trading.IntegrationTests/Backtests/BacktestParityTests.cs`** con el esqueleto siguiente. El contenido exacto de `RunReferenceBacktest()` depende de la API de Lean (ver R-C2); el esqueleto especifica los asserts y la estructura del test:

```csharp
using FluentAssertions;
using Xunit;

namespace Trading.IntegrationTests.Backtests
{
    /// <summary>
    /// Test de paridad del backtest baseline: BTCUSDT perpetual, 2025-01-01 a 2026-03-31,
    /// configuración actual de strategies.json (EmaCrossStrategy 1h, RiskPerTradePercentage 2.0,
    /// filtro de régimen HMM activo).
    ///
    /// Objetivo:
    /// Detectar automáticamente regresiones de comportamiento introducidas por cualquier cambio
    /// a Trading.Domain, Trading.Application o Trading.Strategies. POLICY.md §6.1 exige
    /// "resultados idénticos al pre-cambio" como condición para mergear cambios; este test
    /// codifica esa exigencia.
    ///
    /// Tolerancia:
    /// - Conteo de órdenes: igualdad estricta (entero).
    /// - P&L final y máximo drawdown: tolerancia relativa 1e-6 (consistente con ADR-012
    ///   y los tests de referencia de indicadores).
    ///
    /// Marcado [Trait("Category", "Slow")]: este test toma ~100 segundos. NO se ejecuta
    /// en la suite rápida de iteración. Se ejecuta antes de commit y antes de cerrar
    /// un brief (gate completo: `scripts\check.ps1 -IncludeSlow` o
    /// `dotnet test QuantConnect.Lean.slnf` sin filtro).
    ///
    /// Recalibración del baseline:
    /// Cuando un cambio LEGÍTIMO modifica las métricas (ej. fix de bug en el sizing que
    /// cambia órdenes generadas, refactor del HMM que altera clasificaciones), el operador
    /// actualiza las constantes BaselineOrderCount/BaselineFinalPnLUsdt/BaselineMaxDrawdown
    /// en este archivo, documenta la razón en el mismo commit, y registra entrada en
    /// DECISIONS.md justificando por qué la regresión esperada no es una regresión.
    /// </summary>
    [Trait("Category", "Slow")]
    public class BacktestParityTests
    {
        // Baseline confirmado por el operador durante la construcción de INFRA-3.
        // Conteo de órdenes documentado en ADR-021 e historial INFRA-2 del ROADMAP.
        // P&L y DD: reemplazar los placeholders con los valores reales que pase el operador
        // en el chat durante Sub-pieza C paso 1.
        private const int BaselineOrderCount = 225;
        private const decimal BaselineFinalPnLUsdt = 0m; // TODO: reemplazar con valor real del operador
        private const decimal BaselineMaxDrawdown = 0m; // TODO: reemplazar con valor real del operador

        // Tolerancia relativa para comparación de decimales (consistente con ADR-012).
        private const decimal RelativeTolerance = 1e-6m;

        [Fact]
        public void Backtest_BTCUSDT_2025_01_to_2026_03_MatchesBaseline()
        {
            // ----- Ejecución del backtest -----
            // PLACEHOLDER: invocar el engine de Lean in-process con la misma configuración
            // que TradingAlgorithmHost (SetStartDate 2025-01-01, SetEndDate 2026-03-31,
            // SetCash USDT 100000, BrokerageModel Binance Margin, fee 0.001, slippage 0.001,
            // strategies.json default del repo).
            //
            // La API exacta depende de cómo Lean expone su engine para invocación programática.
            // Si no existe API in-process trivial, DETENERSE Y REPORTAR al operador (ver brief §7
            // Riesgo R-C2); no improvisar levantando un proceso hijo, no copiar configuración del
            // Launcher sin coordinar.

            BacktestResult result = RunReferenceBacktest();

            // ----- Asserts de paridad -----
            result.OrderCount.Should().Be(
                BaselineOrderCount,
                because: "POLICY.md §6.1 exige paridad estricta del conteo de órdenes contra baseline.");

            AssertRelativeTolerance(
                actual: result.FinalPnLUsdt,
                expected: BaselineFinalPnLUsdt,
                tolerance: RelativeTolerance,
                metricName: "FinalPnLUsdt");

            AssertRelativeTolerance(
                actual: result.MaxDrawdown,
                expected: BaselineMaxDrawdown,
                tolerance: RelativeTolerance,
                metricName: "MaxDrawdown");
        }

        private static BacktestResult RunReferenceBacktest()
        {
            // PLACEHOLDER (ver comentario en el test).
            throw new System.NotImplementedException(
                "Implementación pendiente: investigar la API in-process de Lean y completar este método. " +
                "Ver brief INFRA-3 §5.C paso 7 y §7 Riesgo R-C2.");
        }

        private static void AssertRelativeTolerance(
            decimal actual,
            decimal expected,
            decimal tolerance,
            string metricName)
        {
            decimal denominator = System.Math.Max(System.Math.Max(System.Math.Abs(actual), System.Math.Abs(expected)), 1m);
            decimal relativeError = System.Math.Abs(actual - expected) / denominator;

            relativeError.Should().BeLessThan(
                tolerance,
                because: $"{metricName} debe igualar el baseline dentro de la tolerancia relativa {tolerance}. " +
                         $"Esperado: {expected}, Actual: {actual}, Error relativo: {relativeError}.");
        }

        private sealed record BacktestResult(int OrderCount, decimal FinalPnLUsdt, decimal MaxDrawdown);
    }
}
```

7. **Investigar la API in-process de Lean y completar `RunReferenceBacktest()`**. Opciones probables a evaluar:
   - **`LeanEngineSystemHandlers` + `LeanEngineAlgorithmHandlers` + `Engine.Run`** (la API estándar usada por `QuantConnect.Lean.Launcher`, requiere construir un `BacktestNodePacket` y un `IAlgorithm`).
   - **`AlgorithmRunner`** (clase helper presente en algunos forks de Lean para testing automatizado).
   - **Proceso hijo + parsing de output**: descartable, agrega no-determinismo y dependencia de paths. NO usar.

   Si ninguna opción in-process funciona (incompatibilidad con .NET 10, requiere configuración compleja de servicios estáticos, dependencias no resueltas): **detenete y reportá al operador con detalle de qué se intentó y qué falló**. El operador puede decidir: (a) aceptar el test en estado `[Fact(Skip = "...")]` y registrar `DEUDA-4` en ROADMAP (esto SÍ requeriría que el operador modifique el ROADMAP después; vos no lo tocás), (b) cambiar el enfoque del test, (c) abrir un mini-refactor previo para preparar Lean para invocación in-process.

**Detención al final de Sub-pieza C:** reportar:
- Proyecto creado (`Trading.IntegrationTests/`).
- Archivos creados (`Trading.IntegrationTests.csproj`, `Backtests/BacktestParityTests.cs`).
- Modificación al `.slnf` (proyecto agregado).
- Comando ejecutado `dotnet sln add` (única excepción de ejecución autorizada).
- Estado del test: completo y compilando si pudiste completar la API in-process, o esqueleto con `NotImplementedException` y reporte al operador si no (riesgo R-C2 disparado).
- Comandos sugeridos para el operador validar:
  - `dotnet build Trading.IntegrationTests\Trading.IntegrationTests.csproj` — debe compilar.
  - `dotnet test QuantConnect.Lean.slnf --filter "Category=Slow"` — si la API funcionó, el test corre ~100s y pasa con el baseline. Si quedó skipped, el test reporta como skipped.
  - `dotnet test QuantConnect.Lean.slnf --filter "Category!=Slow"` — la suite rápida sigue siendo rápida y NO incluye el test de paridad.
- Mensaje de commit sugerido (ver §6, commit 3).

Esperar OK explícito del operador antes de avanzar a Sub-pieza D.

---

### 5.D — Sub-pieza D: Cierre

**Objetivo:** consolidar el trabajo, verificar coherencia mínima.

**Trabajo:**

1. **Verificación de coherencia.** Confirmar mentalmente (sin tocar nada):
   - `QuantConnect.Lean.slnf` lista los 7 proyectos (6 originales + `Trading.IntegrationTests`).
   - `scripts/check.ps1` existe y no tiene errores de sintaxis evidentes.
   - `Trading.IntegrationTests/Trading.IntegrationTests.csproj` y `BacktestParityTests.cs` existen y respetan las convenciones del proyecto (mismo `TargetFramework`, mismas versiones de packages, namespace en español o inglés según el resto del repo).
   - El `.sln` tiene la entrada de `Trading.IntegrationTests` (resultado del `dotnet sln add`).
   - NO modificaste ningún `.md` versionado.
   - NO modificaste ningún archivo bajo `Trading.*.Tests/` existentes (solo creaste `Trading.IntegrationTests/` que es proyecto nuevo).

2. **Proponer el mensaje de commit unificado** (si el operador prefiere consolidar todo en un solo commit en vez de los 4 commits sugeridos por sub-pieza). Ver §6.

**Detención al final de Sub-pieza D:** reportar:
- Resumen completo de archivos creados y modificados durante INFRA-3.
- Confirmación explícita de que NO se modificaron los `.md` versionados.
- Confirmación explícita de que NO se modificaron tests existentes.
- Comando final sugerido para el operador validar todo end-to-end: `.\scripts\check.ps1 -IncludeSlow` (debería pasar invariantes verdes + build verde + tests rápidos + Slow verdes).
- Mensaje de commit unificado sugerido (ver §6).

---

## 6. Mensajes de commit sugeridos

Cuatro commits separados, uno por sub-pieza. El operador puede agruparlos.

### Commit 1 (post Sub-pieza A)

```
chore(build): solution filter QuantConnect.Lean.slnf

- Crea QuantConnect.Lean.slnf en la raíz con los 6 proyectos Trading.*.
- Limita el grafo de build al subset relevante (~2-4min en frío vs ~15min del .sln completo).

Refs INFRA-3 sub-pieza A, ADR-023
```

### Commit 2 (post Sub-pieza B)

```
feat(scripts): gate único de aceptación scripts/check.ps1

- Crea scripts/check.ps1 que combina grep ejecutable de invariantes arquitectónicas,
  dotnet build QuantConnect.Lean.slnf y dotnet test --filter "Category!=Slow".
- Switches -IncludeSlow y -SkipBuild para casos específicos.
- Espejo ejecutable de la sección "Anti-patrones Prohibidos" de AI.md.

Refs INFRA-3 sub-pieza B, ADR-023
```

### Commit 3 (post Sub-pieza C)

```
test(integration): proyecto Trading.IntegrationTests y test de paridad de backtest baseline

- Nuevo proyecto Trading.IntegrationTests con xunit + FluentAssertions
  (mismas versiones que los demás *.Tests del repo).
- BacktestParityTests verifica paridad estricta de OrderCount (225) y paridad relativa
  1e-6 de P&L y máximo drawdown contra baseline confirmado.
- Marcado [Trait("Category", "Slow")] — ~100s, fuera del pipeline rápido.
- Agregado al QuantConnect.Lean.sln y al QuantConnect.Lean.slnf.
- Codifica POLICY.md §6.1 ("backtest del último mes da resultados idénticos al pre-cambio")
  como gate ejecutable.

Refs INFRA-3 sub-pieza C, ADR-023, POLICY §6.1
```

### Commit 4 (post Sub-pieza D — cierre, vacío de cambios de código)

Sub-pieza D no introduce cambios de código, solo verificación. Si el operador commitea por sub-pieza, este commit es opcional. Sin él, el último commit es el de Sub-pieza C.

### Mensaje unificado (si el operador prefiere un solo commit)

```
feat(infra): INFRA-3 — solution filter, gates ejecutables, paridad de backtest y autonomía de build/test

Reduce el ciclo build+test de ~15min a ~2-4min en frío (segundos en incremental),
convierte invariantes arquitectónicas y paridad de backtest en gates ejecutables,
y habilita a Claude Code a correr dotnet build/test con salvaguardas explícitas.

Cambios de código:
- A: Solution filter QuantConnect.Lean.slnf con los 6 proyectos Trading.* + Trading.IntegrationTests.
- B: scripts/check.ps1 — gate único (invariantes + build + tests rápidos).
- C: Proyecto Trading.IntegrationTests + BacktestParityTests (Category=Slow) que
     codifica POLICY §6.1 como gate ejecutable.

Cambios documentales (pre-aplicados por el operador antes del brief):
- AI.md §"Comandos de Build", §"Cómo opera Claude Code...", §"Testing" punto 3 actualizado,
  §"Límites de Ejecución" puntos 2 y 3 reescritos para permitir dotnet build/test sobre .slnf
  con salvaguardas; prohibición de git intacta.
- ROADMAP: INFRA-3 ✅ en Bloque 3 antes de OPS-2 y entrada en Historial completado.
- ADR-023 documenta D1-D13 y alternativas descartadas.

Closes INFRA-3
Refs ADR-023, POLICY §6.1
```

---

## 7. Riesgos conocidos y manejo

| ID | Riesgo | Cómo manejarlo |
|---|---|---|
| R-A1 | El `.slnf` tiene paths con backslashes y JSON los interpreta mal en máquinas Linux/Mac. | NO bloquea: el operador es Windows. Si en el futuro aparece un entorno Linux/Mac, se regenera el `.slnf` con `/` en otro refactor. |
| R-A2 | `dotnet build QuantConnect.Lean.slnf` falla la primera vez con error de NuGet restore sobre proyectos de Lean no incluidos en el filter. | Reportar al operador con el error exacto. La solución más probable es ejecutar `dotnet restore QuantConnect.Lean.sln` UNA VEZ manualmente (operador, no vos) para popular `obj/project.assets.json`. Si el problema persiste, el operador evaluará si agregar `dotnet restore QuantConnect.Lean.slnf` al script `check.ps1`. |
| R-B1 | El script `check.ps1` da falso positivo en un invariante por una línea legítima (ej. `Console.WriteLine` dentro de un comment block, o un `DateTime.UtcNow` legítimo de `Trading.Strategies` que el regex incluyó por error). | Refinar el regex de la invariante específica. Si el problema no es trivialmente refinable, reportar al operador antes de modificar el script. |
| R-B2 | El script tarda más de lo esperado por `Get-ChildItem -Recurse` sobre carpetas grandes. | Los filtros `-notmatch '\\(bin|obj)\\'` ya están. Si aparece otra carpeta grande no excluida, agregar al filtro. |
| R-C1 | El operador no recuerda el P&L y DD exactos del backtest baseline. | Detener Sub-pieza C, pedir al operador que corra el backtest una vez, registre los tres números en el chat, y continuar. NO inventar, NO usar 0 como placeholder y "después se ajusta". |
| R-C2 | La API in-process de Lean no es trivialmente invocable desde un test xUnit. | **Detenete y reportá al operador con detalle de qué se intentó.** NO improvisar con proceso hijo. El operador decide: (a) aceptar el test en estado `[Fact(Skip = "...")]` y registrar `DEUDA-4` en ROADMAP (esto lo hace el operador, no vos), (b) cambiar el enfoque, (c) abrir un mini-refactor previo. |
| R-C3 | El `dotnet sln add` falla porque el `.sln` tiene un formato no estándar. | Reportar al operador con el error. Como fallback, el operador agrega el proyecto desde Visual Studio. No modificar el `.sln` directamente con un editor de texto. |
| R-doc | Inadvertidamente modificás un `.md` versionado (`AI.md`, `ROADMAP.md`, `DECISIONS.md`, `POLICY.md`). | **Esto NO debe pasar.** El §0 del brief es explícito: ya están actualizados y no se tocan. Si por error tocás alguno, revertí el cambio sobre ese archivo inmediatamente y reportá. |

---

## 8. Resumen para el operador al cierre

**Qué queda funcionando:**

- `QuantConnect.Lean.slnf` acota el build a los 7 proyectos `Trading.*` + Algorithm + Indicators + transitivos. Build de ~2-4min en frío, segundos en incremental.
- `scripts\check.ps1` es el gate único: invariantes arquitectónicas + build + suite rápida. Switch `-IncludeSlow` para incluir el test de paridad.
- `Trading.IntegrationTests/Backtests/BacktestParityTests` ejecuta el backtest baseline y verifica paridad estricta del conteo de órdenes (225) y paridad relativa 1e-6 de P&L y DD máximo. Cualquier cambio en código que rompa la paridad falla el test.
- Los `.md` versionados (`AI.md`, `ROADMAP.md`, `DECISIONS.md`) ya estaban pre-actualizados y no se tocaron durante este brief.

**Qué decisión operativa toca después:**

- El operador verifica con el siguiente brief no trivial (OPS-2) que el flujo nuevo de Claude Code con build/test habilitado funciona en la práctica.
- Si la regla "máximo 5 ciclos" muestra ser muy restrictiva, evaluar subirla a 8 o 10 en refactor pequeño con su propio ADR.
- Si Sub-pieza C terminó con `BacktestParityTests` en estado skipped (R-C2 disparado), agregar `DEUDA-4` al ROADMAP referenciando el bloqueo, y planificar resolverla antes del Hito C (paper trading).

**Cuál es el siguiente paso en el ROADMAP:**

- `OPS-2` — `StrategyHealthMonitor` (implementación runtime de `POLICY.md`). Primer brief que se beneficia del flujo acelerado por INFRA-3.
