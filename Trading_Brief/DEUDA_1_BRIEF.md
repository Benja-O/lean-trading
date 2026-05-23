# DEUDA-1 — Diagnóstico y cierre del test `AccordHmmClassifierReferenceTests` skipeado

> **Brief ejecutable para Claude Code CLI.** Cierre completo de la deuda técnica DEUDA-1 documentada en ADR-020: el test de referencia `AccordHmmClassifierReferenceTests.Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente` está marcado `[Fact(Skip = "...")]` por convergencia degenerada del pipeline HMM con K=3 sobre serie sintética. El modelo de producción (BTCUSDT, K=4) aparentemente no presenta el síntoma. Este brief diagnostica la causa raíz, aplica el fix correspondiente, valida que el modelo de producción no comparte el defecto, y cierra la deuda con ADR nuevo.
>
> **Pre-requisitos:** DEUDA-2 cerrada (logs del JSONL no duplicados, ambiente de diagnóstico limpio). INFRA-2, OPS-1 y OPS-2 cerrados. El sistema está en Bloque 3 cerrado a falta de DEUDA-1. Este es el último refactor del Bloque 3.
>
> **Cierre completo (no mínimo):** este brief NO se limita a hacer pasar el test sintético. Incluye validación cruzada del modelo de producción de BTCUSDT contra realidad histórica conocida del mercado, mitigando el riesgo explícito asumido en ADR-020. Si la validación revela que el modelo de producción comparte un defecto análogo, el brief NO ejecuta el re-entrenamiento (decisión del operador), pero deja preparado el camino.

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio de los puntos críticos:

- **Cero comandos `git` de cualquier tipo.** Lista exhaustiva en `AI.md`.
- **Compilación permitida** apuntando a `.csproj` específicos de `Trading.*`.
- **Ejecución de tests permitida** apuntando a `.csproj` específicos de `Trading.*.Tests`. En este brief la ejecución del test sintético `AccordHmmClassifierReferenceTests` es **necesaria** para capturar el diagnóstico.
- **Ejecución del `HmmTrainer` NO permitida en este brief.** Re-entrenar el modelo de producción es una decisión operativa del operador. Si el diagnóstico revela que el modelo de producción comparte el defecto, Claude Code **se detiene y reporta**; el operador decide si y cuándo re-entrenar.
- **El test sintético se ejecuta vía bypass del `Skip`** (variante A confirmada por el operador): manteniendo el atributo `[Fact(Skip = "...")]` en CI pero ejecutando explícitamente vía `dotnet test --filter` con el flag que ignora el skip (ver Fase 1). El `Skip` se remueve solo cuando el test efectivamente pasa.
- **Validación cruzada manual de regímenes históricos = tarea del operador.** Claude Code prepara la evidencia (extracto de logs por ventana temporal); el operador la confronta con su lectura visual del mercado.
- **Si Claude Code detecta una inconsistencia entre el código actual y este brief**, detenerse y reportar. NO improvisar.
- **Al final del trabajo, proponer el mensaje de commit sugerido.**

---

## Contexto y motivación de DEUDA-1

ADR-020 enumeró tres hipótesis para el fallo del test sintético con K=3:

- **Hipótesis A:** Convergencia a óptimo local malo de Baum-Welch (estados colapsados a parámetros casi idénticos).
- **Hipótesis B:** Bug en `SemanticStateMapper.Build` al calcular cuartiles con K pequeño.
- **Hipótesis C:** El `FeatureScaler` lava las diferencias del segmento HighVolatility por dominancia de los segmentos tranquilos en la varianza global.

**Análisis estático del código previo a la ejecución (importante):**

Inspeccionando `SemanticStateMapper.Build` en el código actual:

```csharp
int stateCount = sortedBySigma.Count;
int topQuartileThreshold = (int)Math.Ceiling(stateCount * 0.75);

for (int positionInSorted = 0; positionInSorted < stateCount; positionInSorted++)
{
    bool isTopQuartile = positionInSorted >= topQuartileThreshold;
    if (isTopQuartile)
        highVolatilityStateIndices.Add(stat.StateIndex);
    ...
}
```

Con K=3: `topQuartileThreshold = Ceiling(3 * 0.75) = Ceiling(2.25) = 3`. La condición `positionInSorted >= 3` **nunca se cumple** porque las posiciones válidas en un array de 3 elementos son {0, 1, 2}. Resultado: **con K=3, ningún estado puede ser asignado a `HighVolatility` por la regla principal.** Existe el fallback "caso degenerado" (`if !anyTrend && !anyHighVol`), pero ese fallback solo se dispara si **además** ningún estado fue asignado a `Trend` — y un estado con `|μ| > 0.001` y `ρ > 0.6` sí se asigna a Trend, deshabilitando el fallback.

**Esto confirma Hipótesis B como causa raíz prácticamente sin necesidad de ejecución.** La Fase 1 del brief ejecuta el test instrumentado de todas formas porque:

1. Es buena práctica de ingeniería confirmar la hipótesis con evidencia ejecutada, no solo con análisis estático.
2. Hipótesis A (convergencia degenerada del Baum-Welch) puede ser un **defecto adicional** que se manifiesta en paralelo a Hipótesis B, no necesariamente excluyente.
3. La instrumentación produce evidencia necesaria para el ADR nuevo (BICs, matriz de transición, medias por estado, mapeo resultante).

Con la causa raíz identificada estáticamente, el fix arquitectónico está claro: hacer las reglas del `SemanticStateMapper` **adaptativas a K**. Pero la decisión de cómo se hacen adaptativas se toma con la evidencia de la Fase 1 sobre la mesa, no antes (porque puede revelar que Hipótesis A también está presente).

**Validación cruzada del modelo de producción:** independientemente de la causa raíz del test sintético, hay que verificar que el modelo `BTCUSDT-perp-binance.hmm.json` actualmente en producción no esté afectado por el mismo defecto. El modelo tiene K=4 (no K=3), donde el `topQuartileThreshold = Ceiling(4 * 0.75) = 3`, y posiciones {0,1,2,3} → solo la posición 3 entra en el top cuartil. Esto significa que **exactamente un estado** se asigna a `HighVolatility` por la regla principal con K=4. Funciona, pero está al borde del caso degenerado. Esto refuerza la importancia de la validación cruzada.

---

## Decisiones técnicas aplicadas (no discutir, aplicar)

| Decisión | Valor |
|---|---|
| Orden de fases | Fase 1 (instrumentación + ejecución del test sintético) → Fase 2 (fix según evidencia) → Fase 3 (revalidación del test sintético post-fix) → Fase 4 (validación cruzada del modelo de producción) → Fase 5 (decisión sobre re-entrenamiento) → Fase 6 (cierre documental). |
| Bypass del `Skip` durante diagnóstico | `dotnet test Trading.Strategies.Tests/Trading.Strategies.Tests.csproj --filter "FullyQualifiedName~AccordHmmClassifierReferenceTests"` con el flag `/p:VSTestNoSkip=true` o equivalente. El atributo `[Fact(Skip = "...")]` permanece en el código durante todo el diagnóstico. Solo se remueve al final, cuando el test pasa. **El atributo no se modifica en commits intermedios.** |
| Logging del diagnóstico | Vía `Xunit.Abstractions.ITestOutputHelper` (constructor del fixture lo recibe). NO usar `Console.WriteLine` (regla de `AI.md`). NO levantar el `ITradingLogger` del proyecto (es para runtime, no para tests). |
| Output del logging temporal | Capturado por `dotnet test --logger "console;verbosity=detailed"` y por el operador via redirección a archivo si lo desea. |
| Fix esperado (Hipótesis B confirmada por análisis estático) | Hacer `SemanticStateMapper.Build` adaptativo a K: K=2 binario simple, K=3 percentiles 33/66, K≥4 cuartiles. Detalle en sección "Fix para Hipótesis B" más abajo. |
| Fix si además Hipótesis A está presente | Multi-seed Baum-Welch: ejecutar Baum-Welch N veces con seeds distintos y quedarse con el de mayor log-likelihood. Aplicado al **trainer offline** (`HmmTrainer`), NO al runtime. Detalle en sección "Fix para Hipótesis A" más abajo. |
| Validación cruzada del modelo de producción | Ventanas históricas de 2025-2026 con régimen de consenso humano. Operador identifica 3-5 ventanas a ojo (`yo creo que esto fue trend / vol alta / squeeze`), Claude Code extrae las clasificaciones del HMM en esas ventanas del JSONL del último backtest. Si coinciden con el consenso → modelo de producción OK. Si no coinciden → re-entrenamiento requerido. |
| Decisión sobre re-entrenamiento | Si la validación cruzada revela que el modelo de producción comparte un defecto, Claude Code **se detiene y reporta**. NO ejecuta `HmmTrainer`. El operador decide. |
| ADR nuevo | ADR-024 documentando: causa raíz identificada, decisión del fix, alternativas consideradas, resultado de la validación cruzada del modelo de producción, decisión final sobre re-entrenamiento. **Estado:** Aceptada. ADR-020 pasa a estado "Resuelta en ADR-024". |
| Estructura del baseline post-fix | Si NO se re-entrena: baseline de 6 órdenes según ADR-023 se mantiene. Si SÍ se re-entrena (decisión del operador post-brief): nuevo baseline a documentar en ADR separado, no en ADR-024. |

---

## Fase 1: instrumentación y ejecución del test sintético

### Paso 1.1: instrumentación del test

Modificar `Trading.Strategies.Tests/Regimes/AccordHmmClassifierReferenceTests.cs`:

1. **Constructor del fixture recibe `ITestOutputHelper`:**

```csharp
private readonly ITestOutputHelper _output;

public AccordHmmClassifierReferenceTests(ITestOutputHelper output)
{
    _output = output;
}
```

2. **El método del test recibe logging temporal exhaustivo entre cada paso clave.** Agregar los logs en este orden a `Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente`:

```csharp
// --- TEMP DEUDA-1: logging de diagnóstico ---
_output.WriteLine("=== DIAGNÓSTICO DEUDA-1 ===");
_output.WriteLine($"Serie sintética: {bars.Count} barras, semilla={RandomSeed}");
_output.WriteLine($"Features extraídas: {featureMatrix.Rows.Length} × {FeatureExtractor.FeatureCount}");

// Después del cálculo de candidates:
foreach (var candidate in candidates)
{
    _output.WriteLine($"  K={candidate.K} → BIC={candidate.Bic:F4}, logL={candidate.Model.LogLikelihood(normalized):F4}");
}
_output.WriteLine($"K elegido por BIC mínimo: {chosen.K}");

// Después de Decode:
_output.WriteLine($"--- Matriz de transición del modelo K={chosen.K} ---");
for (int i = 0; i < chosen.K; i++)
{
    var row = string.Join(", ", Enumerable.Range(0, chosen.K).Select(j => chosen.Model.LogTransitions[i][j].ToString("F4")));
    _output.WriteLine($"  Fila {i}: [{row}]  (log-prob; exp para probabilidad real)");
}

// Después de ComputeStateStatistics:
_output.WriteLine($"--- Estadísticas por estado ---");
foreach (var stat in stats)
{
    _output.WriteLine($"  Estado {stat.StateIndex}: μ={stat.MeanReturn:F6}, σ={stat.StdDevReturn:F6}, ρ_self={stat.SelfTransitionProbability:F4}, N_observado={stat.ObservationCount}");
}

// Después de SemanticStateMapper.Build:
_output.WriteLine($"--- Mapeo semántico estado → etiqueta ---");
foreach (var stat in stats)
{
    _output.WriteLine($"  Estado {stat.StateIndex} → {mapper.GetLabel(stat.StateIndex)}");
}

// Antes de las aserciones de segmentos:
var allLabelCounts = classifications
    .Where(c => c.Label != RegimeLabel.Unknown)
    .GroupBy(c => c.Label)
    .OrderByDescending(g => g.Count())
    .ToDictionary(g => g.Key, g => g.Count());
_output.WriteLine($"--- Distribución global de etiquetas (post-warmup, {classifications.Count - 150} barras) ---");
foreach (var (label, count) in allLabelCounts)
{
    _output.WriteLine($"  {label}: {count} barras ({100.0 * count / (classifications.Count - 150):F1}%)");
}

// Distribución por segmento:
var segmentTrendCounts = segmentTrendLabels.GroupBy(l => l).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count());
var segmentHighVolCounts = segmentHighVolLabels.GroupBy(l => l).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count());
var segmentMeanRevCounts = segmentMeanRevLabels.GroupBy(l => l).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count());

_output.WriteLine($"--- Segmento Trend alcista (barras 150-499, esperado: Trend) ---");
foreach (var (label, count) in segmentTrendCounts)
    _output.WriteLine($"  {label}: {count} ({100.0 * count / segmentTrendLabels.Count:F1}%)");

_output.WriteLine($"--- Segmento HighVolatility (barras 650-999, esperado: HighVolatility) ---");
foreach (var (label, count) in segmentHighVolCounts)
    _output.WriteLine($"  {label}: {count} ({100.0 * count / segmentHighVolLabels.Count:F1}%)");

_output.WriteLine($"--- Segmento MeanReverting (barras 1150-1499, esperado: MeanReverting) ---");
foreach (var (label, count) in segmentMeanRevCounts)
    _output.WriteLine($"  {label}: {count} ({100.0 * count / segmentMeanRevLabels.Count:F1}%)");

_output.WriteLine("=== FIN DIAGNÓSTICO ===");
// --- FIN TEMP DEUDA-1 ---
```

**Importante:** todos los bloques marcados `// --- TEMP DEUDA-1 ---` se remueven al final (Fase 6). El test queda con aserciones limpias.

3. **Mantener el `[Fact(Skip = "...")]`** durante toda esta fase. NO modificar el atributo en este paso.

### Paso 1.2: compilación del proyecto de tests

```bash
dotnet build Trading.Strategies.Tests/Trading.Strategies.Tests.csproj
```

Debe compilar verde. Si falla, reportar y detenerse.

### Paso 1.3: ejecución del test bypassando el `Skip`

```bash
dotnet test Trading.Strategies.Tests/Trading.Strategies.Tests.csproj \
    --filter "FullyQualifiedName~Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente" \
    --logger "console;verbosity=detailed" \
    /p:VSTestNoSkip=true
```

**Notas operativas:**

- El flag `/p:VSTestNoSkip=true` puede no funcionar en todas las versiones de xUnit/dotnet. Si no funciona, alternativa: agregar **temporalmente** un segundo `[Fact]` (sin `Skip`) con un nombre distinto (`Pipeline_DiagnosticoTemporal_NoCommitear`) que llame internamente al método del test. Documentar en el código con un comentario explícito que ese `[Fact]` se remueve antes de commitear.
- Si ninguna de las dos vías funciona, la última alternativa es **comentar el `[Fact(Skip = "...")]`** y agregar `[Fact]` solo, ejecutar, y volver a poner el `Skip` antes de cualquier `git add`. El brief recuerda explícitamente que el atributo no debe quedar en estado intermedio.

### Paso 1.4: análisis del output

Claude Code analiza el output capturado. Las observaciones críticas a registrar:

1. **¿K=3 minimiza el BIC?** Lo esperado por la lógica del test es sí. Si no, hay un problema adicional con la métrica de selección.
2. **¿La matriz de transición muestra estados colapsados?** Si dos filas son casi idénticas, Hipótesis A confirmada en paralelo a B.
3. **¿Las estadísticas por estado discriminan los tres segmentos?** Concretamente: ¿hay un estado con σ visiblemente mayor que los otros dos (esperado: el del segmento HighVolatility)? Si la σ máxima es solo 1.5x la mínima (en lugar de ~5x como diseña la serie sintética), el HMM no separó los regímenes — Hipótesis A confirmada con fuerza.
4. **¿El mapeo semántico asigna `HighVolatility` a algún estado?** Confirmación operativa de Hipótesis B: con K=3, ningún estado debería estar mapeado a `HighVolatility` (por la lógica del cuartil) salvo que se dispare el fallback degenerado.
5. **¿Cuál es el `MostFrequent` de cada segmento?** Confirma cuántas etiquetas distintas dominan los tres segmentos.

### Paso 1.5: clasificación del resultado

Producir un resumen interno (no aplicar fix aún) con la conclusión:

- **"Hipótesis B confirmada (causa única o dominante)":** las estadísticas por estado discriminan los tres segmentos (σ máxima ≥3x mínima), pero el mapeo no asigna `HighVolatility` por el bug del cuartil. → Fase 2 = solo fix de `SemanticStateMapper`.
- **"Hipótesis A + B confirmadas":** las estadísticas por estado **no** discriminan los segmentos (σ máxima < 2x mínima o matriz de transición con estados colapsados), Y además el mapeo es degenerado. → Fase 2 = fix de `SemanticStateMapper` + multi-seed Baum-Welch en el trainer.
- **"Hipótesis C confirmada":** las estadísticas por estado discriminan parcialmente pero las features escaladas no separan los segmentos (verificable inspeccionando las features escaladas del segmento HighVolatility vs los otros). → Fase 2 = revisar el scaling, posiblemente cambiar a robust scaling. **Detenerse y reportar al operador antes de aplicar fix de Hipótesis C** porque cambiar el scaling afecta el contrato entre trainer offline y runtime y obliga a re-entrenar.
- **"Resultado inesperado":** ninguna de las hipótesis explica el output (ej. el test pasa en este run, o falla por otra razón distinta a la documentada). → **Detenerse y reportar al operador con el output completo.** No improvisar fixes.

---

## Fase 2: aplicación del fix

### Fix para Hipótesis B (caso esperado, basado en análisis estático)

Modificar `Trading.Strategies/Regimes/SemanticStateMapper.cs`. Reemplazar la lógica de cuartiles por una **adaptativa a K**:

```csharp
public static SemanticStateMapper Build(IReadOnlyList<StateStatistics> statistics)
{
    if (statistics is null) throw new ArgumentNullException(nameof(statistics));
    if (statistics.Count == 0)
        throw new ArgumentException("Se requieren estadísticas para al menos un estado.", nameof(statistics));

    var sortedBySigma = statistics
        .Select((stat, index) => (stat, index))
        .OrderBy(tuple => tuple.stat.StdDevReturn)
        .ToList();

    int stateCount = sortedBySigma.Count;

    // ADAPTATIVO A K:
    //   K=2: el estado de σ mayor es HighVolatility, el otro evalúa Trend/MeanReverting/Squeeze por su μ y ρ.
    //   K=3: percentiles 33/66 — el último tercio es HighVolatility, el primer tercio (si ρ > 0.7) es Squeeze.
    //   K>=4: cuartiles tradicionales — top cuartil HighVolatility, bottom cuartil (con ρ > 0.7) Squeeze.
    int topThreshold;
    int bottomThreshold;
    if (stateCount == 2)
    {
        topThreshold = 1;       // posición 1 (la única de σ mayor) → HighVolatility candidato
        bottomThreshold = 1;    // posición 0 → bottom (si cumple ρ, será Squeeze)
    }
    else if (stateCount == 3)
    {
        topThreshold = 2;       // posiciones >= 2 (solo la última, la de σ mayor) → HighVolatility
        bottomThreshold = 1;    // posiciones < 1 (solo la primera, la de σ menor) → bottom para Squeeze
    }
    else
    {
        // K >= 4: cuartiles
        topThreshold = (int)Math.Ceiling(stateCount * 0.75);
        bottomThreshold = (int)Math.Floor(stateCount * 0.25);
    }

    var highVolatilityStateIndices = new HashSet<int>();
    var squeezeStateIndices = new HashSet<int>();

    for (int positionInSorted = 0; positionInSorted < stateCount; positionInSorted++)
    {
        var (stat, _) = sortedBySigma[positionInSorted];
        bool isTopBracket = positionInSorted >= topThreshold;
        bool isBottomBracket = positionInSorted < bottomThreshold;

        if (isTopBracket)
            highVolatilityStateIndices.Add(stat.StateIndex);
        else if (isBottomBracket && stat.SelfTransitionProbability > 0.7)
            squeezeStateIndices.Add(stat.StateIndex);
    }

    var mapping = new Dictionary<int, RegimeLabel>();
    bool anyTrend = false;
    foreach (var stat in statistics)
    {
        if (highVolatilityStateIndices.Contains(stat.StateIndex))
        {
            mapping[stat.StateIndex] = RegimeLabel.HighVolatility;
        }
        else if (squeezeStateIndices.Contains(stat.StateIndex))
        {
            mapping[stat.StateIndex] = RegimeLabel.Squeeze;
        }
        else if (Math.Abs(stat.MeanReturn) > 0.001 && stat.SelfTransitionProbability > 0.6)
        {
            mapping[stat.StateIndex] = RegimeLabel.Trend;
            anyTrend = true;
        }
        else
        {
            mapping[stat.StateIndex] = RegimeLabel.MeanReverting;
        }
    }

    // Caso degenerado: si no quedó ningún estado HighVolatility ni Trend, forzar al menos uno.
    bool anyHighVol = highVolatilityStateIndices.Count > 0;
    if (!anyTrend && !anyHighVol)
    {
        var maxSigmaState = statistics.OrderByDescending(stat => stat.StdDevReturn).First();
        mapping[maxSigmaState.StateIndex] = RegimeLabel.HighVolatility;
    }

    return new SemanticStateMapper(mapping);
}
```

**Actualizar XML doc del método** para documentar la adaptación por K:

```csharp
/// <summary>
/// Construye un SemanticStateMapper a partir de las estadísticas por estado.
///
/// Reglas adaptativas según el número de estados K:
/// - K=2: estado de σ mayor → HighVolatility candidato; el otro evalúa Trend/Squeeze/MeanReverting.
/// - K=3: tercios — última posición ordenada por σ → HighVolatility, primera posición (si ρ > 0.7) → Squeeze.
/// - K>=4: cuartiles tradicionales — top cuartil HighVolatility, bottom cuartil (con ρ > 0.7) Squeeze.
///
/// Reglas comunes a todos los K:
/// - Estados no clasificados como HighVolatility ni Squeeze evalúan |μ| > 0.001 y ρ > 0.6 → Trend.
/// - Resto → MeanReverting.
/// - Caso degenerado (ningún estado HighVolatility ni Trend): forzar HighVolatility al de σ máxima.
///
/// La adaptación por K es necesaria porque Math.Ceiling(K * 0.75) con K=3 produce 3, lo cual deja la
/// condición "positionInSorted >= 3" insatisfacible en un array de 3 elementos (ver ADR-024).
/// </summary>
```

### Fix adicional para Hipótesis A (solo si confirmada en Fase 1)

**Solo aplicar si la Fase 1 confirmó que las estadísticas por estado no discriminan los segmentos.**

Modificar el `HmmTrainer/Program.cs` (proyecto standalone offline) para implementar **multi-seed Baum-Welch**:

```csharp
private const int MultiSeedAttempts = 10;
private static readonly int[] RandomSeeds = Enumerable.Range(1, MultiSeedAttempts)
    .Select(i => 42 * i + 17).ToArray();

private static HiddenMarkovModel<MultivariateNormalDistribution, double[]> TrainHmmMultiSeed(
    int numberOfStates, double[][] observations)
{
    HiddenMarkovModel<MultivariateNormalDistribution, double[]> bestModel = null;
    double bestLogLikelihood = double.NegativeInfinity;

    foreach (int seed in RandomSeeds)
    {
        Accord.Math.Random.Generator.Seed = seed;
        var model = TrainHmmSingleSeed(numberOfStates, observations);
        double logLikelihood = model.LogLikelihood(observations);

        if (logLikelihood > bestLogLikelihood)
        {
            bestLogLikelihood = logLikelihood;
            bestModel = model;
        }
    }

    return bestModel;
}
```

Donde `TrainHmmSingleSeed` es el método actual `TrainHmm` renombrado. El método público que se invoca desde el `Main` del trainer ahora es `TrainHmmMultiSeed`.

**Aplicar el mismo fix al método `TrainHmm` del test sintético** (`AccordHmmClassifierReferenceTests.cs`), para que el test sintético también se beneficie del multi-seed.

**Si la Fase 1 NO confirmó Hipótesis A, NO aplicar este fix.** Es overhead innecesario al trainer.

### Fix para Hipótesis C (solo si confirmada en Fase 1)

**No aplicar este fix sin reportar primero al operador.** Cambiar el `FeatureScaler` afecta el contrato serializado entre trainer offline y runtime, lo cual obliga a re-entrenar el modelo de producción. Esto excede el alcance de un fix automático.

Si la Fase 1 confirmó Hipótesis C, detenerse y reportar al operador con la evidencia. El operador decide.

### Paso 2.X: compilación

```bash
dotnet build Trading.Strategies/Trading.Strategies.csproj
dotnet build Trading.Strategies.Tests/Trading.Strategies.Tests.csproj
```

Si el trainer también se modificó (Hipótesis A):

```bash
dotnet build Trading.Strategies/Tools/HmmTrainer/HmmTrainer.csproj
```

---

## Fase 3: revalidación del test sintético post-fix

### Paso 3.1: re-ejecución del test instrumentado

Misma invocación que en Paso 1.3, **sin** quitar todavía el `[Fact(Skip = "...")]` ni los logs temporales:

```bash
dotnet test Trading.Strategies.Tests/Trading.Strategies.Tests.csproj \
    --filter "FullyQualifiedName~Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente" \
    --logger "console;verbosity=detailed" \
    /p:VSTestNoSkip=true
```

### Paso 3.2: verificación del éxito

El test debe pasar verde. Las aserciones críticas:

1. `chosen.K.Should().Be(3)` — el BIC mínimo se da en K=3 (esto era cierto antes y debe seguir siéndolo).
2. `dominantLabels.Count.Should().BeGreaterThanOrEqualTo(2)` — los tres segmentos están dominados por al menos dos etiquetas distintas.
3. `(highVolCount / totalSegmentHighVol).Should().BeGreaterThan(0.5)` — el segmento HighVolatility está dominado por la etiqueta `HighVolatility` en >50% de las barras.

**Si el test pasa:** proceder a Fase 4.

**Si el test sigue fallando:** detenerse y reportar al operador. La hipótesis identificada en Fase 1 puede haber estado incompleta. Compartir los nuevos logs y el delta vs los originales.

### Paso 3.3: NO quitar el `Skip` ni los logs temporales todavía

Aún falta la validación cruzada del modelo de producción (Fase 4). Si la validación cruzada revela problemas en el modelo de producción, podría requerir re-trabajo. El `Skip` y los logs se remueven solo al final, en Fase 6.

---

## Fase 4: validación cruzada del modelo de producción contra realidad histórica

**Esta fase tiene dos partes: una mecánica (Claude Code) y una humana (operador).** Es el sanity check humano que ADR-020 documentó como "riesgo asumido" pendiente de mitigar.

### Paso 4.1 (Claude Code): preparar la evidencia

1. **Identificar el JSONL del último backtest exitoso del operador.** Path esperado:
   `Trading.Strategies/bin/Debug/net10.0/logs/trading-{fecha}.jsonl` o equivalente según `JsonlFileLogSink` (rotación diaria, retención 30 días).
   Si Claude Code no puede ubicar el JSONL del backtest del período 2025-01-01 a 2026-03-31, **detenerse y pedirle al operador** que confirme el path exacto.

2. **Extraer los eventos de clasificación de régimen del JSONL.** El `AccordHmmClassifier` debería estar logueando cada clasificación (verificar en el código de `AccordHmmClassifier` si emite log; si no, el cambio de etiqueta sí se loguea cuando el filtro de régimen descarta una señal en `BarProcessingService`).

   Concretamente, buscar entradas del estilo:
   - `"señal {SignalDirection} descartada... Régimen actual de {InstrumentId} es {RegimeLabel}..."` — cada uno de estos eventos identifica un timestamp y una etiqueta de régimen.

3. **Producir un archivo de evidencia** en `briefs/DEUDA_1_diagnostico_evidencia.md` (proyecto raíz, no commiteable a `models/`) con el siguiente contenido:

```markdown
# DEUDA-1 — Evidencia de regímenes detectados por el modelo de producción

> **Origen:** JSONL del backtest del período 2025-01-01 a 2026-03-31 (path: ...).
> **Modelo:** BTCUSDT-perp-binance.hmm.json (K=4 según ADR-019).

## Distribución global de etiquetas en el período

| Etiqueta | Cantidad de barras 4h | % del total |
|---|---|---|
| Trend | ... | ...% |
| HighVolatility | ... | ...% |
| Squeeze | ... | ...% |
| MeanReverting | ... | ...% |
| Unknown | ... | ...% (espera ~20 días de warm-up) |

## Líneas temporales por etiqueta

(Listar bloques de fechas continuos en los que el régimen fue cada etiqueta.
Ejemplo: "Trend: 2025-01-22 04:00 a 2025-02-15 12:00", etc. Si los bloques son
muy fragmentados, mostrar solo los más largos — los que duran ≥3 días).

## Sugerencias de ventanas para validación humana

(Claude Code propone 5 ventanas de 5-10 días con régimen estable según el HMM,
distribuidas a lo largo de 2025-2026, mezclando las 4 etiquetas. El operador
mira el gráfico de BTCUSDT en TradingView para esas ventanas y confirma o
refuta visualmente.)
```

### Paso 4.2 (Operador): validación visual

El operador toma el archivo de evidencia, abre TradingView (o equivalente) en el gráfico de BTCUSDT 4h, y para cada una de las 5 ventanas sugeridas evalúa:

- **¿La etiqueta asignada por el HMM coincide con lo que un trader humano llamaría ese régimen?**
- Por ejemplo:
  - Ventana etiquetada `Trend` por el HMM → ¿el chart muestra una tendencia visualmente clara y sostenida?
  - Ventana etiquetada `HighVolatility` → ¿el chart muestra velas grandes, ATR elevado, rangos amplios?
  - Ventana etiquetada `Squeeze` → ¿el chart muestra rango estrecho, baja volatilidad, consolidación?
  - Ventana etiquetada `MeanReverting` → ¿el chart muestra movimientos cortos en ambas direcciones sin tendencia clara?

El operador no necesita ser cuantitativo: una respuesta cualitativa "sí coincide" / "no coincide" / "ambiguo" por ventana es suficiente.

**Criterio de éxito:** al menos 4 de 5 ventanas coinciden o son ambiguas (no contradicen el modelo). Si 2 o más ventanas contradicen frontalmente el modelo (ej. ventana etiquetada `Trend` en un período visualmente lateral), el modelo de producción está defectuoso.

### Paso 4.3 (Operador → Claude Code): reporte de resultado

El operador reporta a Claude Code uno de tres resultados:

- **"Validación cruzada OK":** el modelo de producción refleja razonablemente los regímenes históricos. → Proceder a Fase 5 con decisión "NO re-entrenar".
- **"Validación cruzada FALLA":** 2 o más ventanas contradicen frontalmente el modelo. → Proceder a Fase 5 con decisión "SÍ re-entrenar" (que NO ejecuta Claude Code, sino que prepara el camino).
- **"Validación cruzada AMBIGUA":** los resultados son intermedios (ej. 3 ventanas OK, 2 ambiguas), el operador no está seguro. → Repetir Paso 4.1-4.2 con más ventanas, o consultar al asistente principal para una segunda opinión sobre los datos antes de decidir.

---

## Fase 5: decisión sobre re-entrenamiento del modelo de producción

### Caso A: validación cruzada OK (modelo de producción NO requiere re-entrenamiento)

Proceder directamente a Fase 6. El modelo de producción es válido y se mantiene. El fix del `SemanticStateMapper` se aplicó preventivamente para el caso K=3 (que la producción no usa hoy, pero podría usar en el futuro si se decide entrenar con otro K). La regla institucional "el código robusto se mantiene en producción aunque no se use hoy" justifica el fix incluso si el modelo actual no se beneficia directamente.

### Caso B: validación cruzada FALLA (modelo de producción SÍ requiere re-entrenamiento)

**Claude Code NO ejecuta el `HmmTrainer`.** Esto es decisión del operador.

Claude Code prepara la evidencia para que el operador pueda re-entrenar fácilmente cuando decida:

1. Verificar que el código del `HmmTrainer` ahora incluye el fix de multi-seed (si la Fase 1 confirmó Hipótesis A) y el del `SemanticStateMapper` adaptativo (siempre).
2. Verificar que los datos de Binance Klines siguen disponibles en `F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\4h\BTCUSDT\`.
3. Producir un script o nota en `briefs/DEUDA_1_reentrenamiento_pendiente.md` documentando:
   - El comando exacto a ejecutar (`dotnet run --project Trading.Strategies/Tools/HmmTrainer/HmmTrainer.csproj` con args si aplica).
   - El path del modelo actual que se reemplazará (`models/regime/BTCUSDT-perp-binance.hmm.json`).
   - La recomendación de mover el modelo actual a `models/regime/archive/BTCUSDT-perp-binance.hmm.{fecha}.json` antes de re-entrenar (preservar histórico).
   - La advertencia de que post-re-entrenamiento puede cambiar el baseline de no-regresión (6 órdenes según ADR-023). Si cambia, hay que documentar el nuevo baseline en un ADR separado.
   - El recordatorio de que la POLICY no se modifica durante un drawdown (POLICY 6.2). Si el sistema está en operación cuando se decide re-entrenar, **detener primero el sistema** antes de cambiar el modelo.

4. **Detenerse y reportar al operador** con un mensaje claro: *"Validación cruzada reveló defecto en modelo de producción. El fix arquitectónico está aplicado (`SemanticStateMapper` adaptativo a K, multi-seed Baum-Welch si aplica). El re-entrenamiento del modelo de producción es decisión operativa del operador y está documentado en `briefs/DEUDA_1_reentrenamiento_pendiente.md`. Esperando instrucción."*

El operador puede:
- Aprobar el re-entrenamiento inmediato → en una sesión posterior, ejecuta el `HmmTrainer` él mismo.
- Postergar el re-entrenamiento → DEUDA-1 se cierra parcialmente; queda nueva deuda explícita "REENT-1: re-entrenar modelo HMM con fix arquitectónico aplicado". Se documenta en ROADMAP.

---

## Fase 6: cierre documental y limpieza

### Paso 6.1: remover instrumentación temporal

1. En `AccordHmmClassifierReferenceTests.cs`:
   - Remover el campo `_output` y el constructor que lo recibe.
   - Remover todos los bloques `// --- TEMP DEUDA-1 ---` con sus llamadas a `_output.WriteLine`.
   - **Quitar el atributo `[Fact(Skip = "...")]`** y reemplazarlo por `[Fact]` simple.

2. Verificar que `using Xunit.Abstractions;` se remueve si ya no es necesario.

3. Compilar y re-ejecutar el test (sin `--filter` ahora, como parte de la suite completa):

```bash
dotnet test Trading.Strategies.Tests/Trading.Strategies.Tests.csproj
```

Todos los tests del proyecto deben pasar verde, incluido el que estaba skipeado.

### Paso 6.2: ADR-024 nuevo

Agregar al inicio de `DECISIONS.md` (antes de ADR-023):

```markdown
## ADR-024 — SemanticStateMapper adaptativo a K + multi-seed Baum-Welch (resuelve ADR-020)
**Fecha:** {FECHA}
**Estado:** Aceptada

### Contexto

ADR-020 documentó como deuda técnica el test `AccordHmmClassifierReferenceTests.Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente` skipeado por convergencia degenerada con K=3 sobre serie sintética. ADR-020 enumeró tres hipótesis de causa raíz y un plan de diagnóstico. Este ADR documenta la resolución.

### Causa raíz identificada

{Resumen de los hallazgos de la Fase 1 del brief: cuál hipótesis se confirmó.
Concretamente: bug en `SemanticStateMapper.Build` al calcular cuartiles con K
pequeño. Con K=3, `Ceiling(3 * 0.75) = 3`, lo cual hace la condición
`positionInSorted >= 3` insatisfacible en un array de 3 elementos. Resultado:
ningún estado mapeado a HighVolatility. Si además Hipótesis A se confirmó:
agregar "Adicionalmente, Baum-Welch convergía a un óptimo local malo con
seed=42, donde dos estados colapsaban a medias casi idénticas."}

### Decisión

**Fix 1 — `SemanticStateMapper.Build` adaptativo a K:**
- K=2: estado de σ mayor → HighVolatility candidato; el otro evalúa Trend/Squeeze/MeanReverting por sus reglas estándar de μ y ρ.
- K=3: tercios — última posición ordenada por σ → HighVolatility, primera posición (si ρ > 0.7) → Squeeze.
- K>=4: cuartiles tradicionales (sin cambios respecto a la versión anterior).
- Reglas comunes y caso degenerado: sin cambios.

**Fix 2 (solo si Hipótesis A se confirmó) — Multi-seed Baum-Welch en `HmmTrainer`:**
- Entrenar el HMM N veces con seeds distintos, quedarse con el modelo de mayor log-likelihood.
- Aplicado al trainer offline; el runtime carga el modelo serializado sin cambios.
- N=10 seeds, valores `42 * i + 17` para i ∈ {1..10}.

**Decisión sobre el modelo de producción:** {Una de dos opciones según el resultado de la Fase 4:
- "Validación cruzada del modelo de producción contra ventanas históricas conocidas de 2025-2026 fue OK: el modelo refleja razonablemente los regímenes históricos. No requiere re-entrenamiento. El fix se aplica preventivamente para K=3 (que el modelo actual con K=4 no usa, pero podría usar en re-entrenamientos futuros)."
- "Validación cruzada FALLÓ en {N} de 5 ventanas. El modelo de producción requiere re-entrenamiento con el fix arquitectónico aplicado. Re-entrenamiento NO se ejecuta en este ADR (decisión operativa pendiente del operador). Se documenta como deuda técnica REENT-1 en ROADMAP."}

### Alternativas consideradas

**A — Eliminar el test sintético en lugar de hacerlo pasar.** Descartada: el test detectó un bug arquitectónico real (`SemanticStateMapper` no adaptativo a K). Eliminar el test enmascararía el problema en lugar de resolverlo.

**B — Forzar K=4 mínimo en todos los entrenamientos para evitar el caso degenerado de K=3.** Descartada: K se elige por BIC sobre los datos. Forzar K mínimo sería sobreajustar a la heurística del cuartil en lugar de corregir la heurística. El BIC es metodológicamente correcto; las reglas del mapper deben adaptarse.

**C — Refactor profundo de `SemanticStateMapper` con clustering jerárquico sobre las estadísticas de estados.** Descartada por overengineering: el mapper actual con reglas adaptativas a K es suficiente. Una mejora más sofisticada solo se justificaría si en el futuro se observa que las reglas siguen produciendo mapeos contraintuitivos en producción.

**D (elegida) — Adaptación de la heurística existente a K.** Mínimo cambio que resuelve el bug sin alterar el contrato serializado entre trainer y runtime ni la arquitectura del pipeline.

### Consecuencias

- El test `Pipeline_SerieSinteticaConTresRegimenes_ClasificaCorrectamente` pasa verde.
- `SemanticStateMapperTests` recibe casos de prueba adicionales por K (K=2, K=3, K=4, K=5) para cubrir las nuevas ramas. {Especificar cuántos tests nuevos.}
- {Si Hipótesis A se confirmó} El trainer offline ahora ejecuta 10 pasadas de Baum-Welch en lugar de 1. El tiempo de entrenamiento se multiplica por ~10 (de ~minutos a ~decenas de minutos para datasets grandes), aceptable porque el trainer es offline y se ejecuta poco frecuentemente.
- {Si validación cruzada OK} El modelo de producción actual (K=4) se mantiene; el baseline de no-regresión de 6 órdenes (ADR-023) se preserva.
- {Si validación cruzada FALLÓ} El modelo actual queda marcado como "a re-entrenar" en ROADMAP (REENT-1). El operador decide cuándo ejecutar. El baseline puede cambiar tras re-entrenar; se documentará en ADR separado.
- ADR-020 pasa a estado "Resuelta en ADR-024".

### Validaciones ejecutadas

1. **Test sintético post-fix:** los tres segmentos están dominados por etiquetas distintas; el segmento HighVolatility tiene >50% de barras clasificadas como HighVolatility. {Reportar % real observado en el test.}
2. **Validación cruzada del modelo de producción:** {detalle de las 5 ventanas evaluadas, resultado por ventana, conclusión global.}
3. **Backtest de no-regresión:** {Si NO se re-entrenó: el backtest produce 6 órdenes como antes (baseline preservado). Si SÍ se re-entrenó: ADR aparte con el nuevo baseline.}

### Riesgo residual

- La validación cruzada se hizo con 5 ventanas seleccionadas; un modelo defectuoso podría coincidir por casualidad en esas ventanas y diferir en otras. **Mitigación:** la inspección humana semanal (POLICY sección 4) durante paper trading va a producir señales si el modelo se comporta de forma incoherente en operación real.
- Las reglas adaptativas por K son heurísticas, no derivan de un criterio teórico unificado. Si en el futuro se entrenan modelos con K>=5 o con dimensionalidad de features muy distinta, las reglas pueden requerir re-calibración. Trigger sugerido: si la diversidad de etiquetas asignadas en un modelo nuevo es <2, revisar el mapper.
```

### Paso 6.3: actualización de ADR-020

En `DECISIONS.md`, modificar el header de ADR-020:

```markdown
## ADR-020 — Test de referencia AccordHmmClassifierReferenceTests skipeado por convergencia degenerada con datos sintéticos
**Fecha:** 2026-05-19
**Estado:** Resuelta en ADR-024 ({FECHA})
```

No tocar el contenido del ADR-020, solo el estado. ADR-020 sigue siendo el registro histórico de la deuda; ADR-024 es la resolución.

### Paso 6.4: actualización del ROADMAP

En `ROADMAP.md`:

1. Tabla "🔄 BLOQUE 3 — En progreso": marcar DEUDA-1 con ✅ y fecha.
2. **Si la validación cruzada falló y se difiere el re-entrenamiento**, agregar nueva fila ⬜ `REENT-1` con descripción: "Re-entrenamiento del modelo HMM de BTCUSDT con fix arquitectónico aplicado. Detalle en `briefs/DEUDA_1_reentrenamiento_pendiente.md`. No bloquea Hito C **solo si** el operador decide arrancar paper trading con la estrategia EmaCross sin filtro de régimen, o postergando Hito C hasta el re-entrenamiento. La decisión la toma el operador con el asistente principal en sesión separada."
3. **Si DEUDA-2 también está cerrada** (esperado), cambiar el estado del Bloque 3 de "🔄 En progreso" a "✅ Completo".
4. Sección "Historial completado": agregar entrada para DEUDA-1 con resumen siguiendo el formato del proyecto.

### Paso 6.5: tests adicionales en `SemanticStateMapperTests`

Agregar tests al archivo `Trading.Strategies.Tests/Regimes/SemanticStateMapperTests.cs` cubriendo:

- `Build_ConKIgualA2_AsignaHighVolatilityAlEstadoDeSigmaMayor`.
- `Build_ConKIgualA3_AsignaHighVolatilityAlUltimoTercio` (caso que era el bug).
- `Build_ConKIgualA3_AsignaSqueezeAlPrimerTercioSiCumpleRho`.
- `Build_ConKIgualA4_AplicaCuartilesTradicionales` (caso del modelo de producción actual).
- `Build_ConKIgualA5_AplicaCuartilesTradicionales`.

Patrón de los tests: construir un `IReadOnlyList<StateStatistics>` sintético con valores conocidos (medias, desvíos, ρ específicos), invocar `SemanticStateMapper.Build`, hacer assert sobre el mapeo resultante.

Aprox. 5 tests nuevos. El archivo `SemanticStateMapperTests.cs` ya existe con 5 tests previos (según historial completado del Paso 3 de Hito B).

### Paso 6.6: validación final del proyecto completo

```bash
dotnet build Trading.Strategies/Trading.Strategies.csproj
dotnet build Trading.Strategies.Tests/Trading.Strategies.Tests.csproj

dotnet test Trading.Strategies.Tests/Trading.Strategies.Tests.csproj
dotnet test Trading.Application.Tests/Trading.Application.Tests.csproj
dotnet test Trading.Domain.Tests/Trading.Domain.Tests.csproj
```

Todos los tests deben pasar verde, incluido el ex-skipeado.

---

## Estructura final de archivos

### Archivos modificados

```
Trading.Strategies/Regimes/SemanticStateMapper.cs
  → Método Build adaptativo a K (K=2, K=3, K>=4) con XML doc actualizado.

Trading.Strategies/Tools/HmmTrainer/Program.cs   (SOLO si Hipótesis A confirmada)
  → Reemplazar TrainHmm por TrainHmmMultiSeed con 10 seeds.
  → Mantener TrainHmmSingleSeed (ex TrainHmm) como método privado.

Trading.Strategies.Tests/Regimes/AccordHmmClassifierReferenceTests.cs
  → Quitar [Fact(Skip = "...")] y reemplazar por [Fact].
  → Aplicar el mismo cambio multi-seed en su método auxiliar TrainHmm si Hipótesis A confirmada.
  → Sin logging temporal (ya fue removido en Fase 6.1).

Trading.Strategies.Tests/Regimes/SemanticStateMapperTests.cs
  → Agregar 5 tests cubriendo K=2, K=3 (caso bug), K=3 con Squeeze, K=4, K=5.

DECISIONS.md
  → ADR-024 nuevo al inicio del archivo (antes de ADR-023).
  → ADR-020: estado actualizado a "Resuelta en ADR-024 ({FECHA})".

ROADMAP.md
  → Tabla Bloque 3: DEUDA-1 marcada ✅ con fecha.
  → Si validación cruzada falló: nueva fila REENT-1 agregada.
  → Si DEUDA-2 también ✅: Bloque 3 cambia a "✅ Completo".
  → Historial completado: entrada para DEUDA-1.
```

### Archivos creados solo durante el diagnóstico (NO commitear)

```
briefs/DEUDA_1_diagnostico_evidencia.md       (intermedio, se descarta post-cierre)
briefs/DEUDA_1_reentrenamiento_pendiente.md   (SOLO si validación cruzada falló; queda referenciado en ROADMAP REENT-1)
```

### Archivos que NO se tocan

```
Trading.Strategies/Regimes/AccordHmmClassifier.cs    ← clasificador runtime intacto
Trading.Strategies/Regimes/HmmModelSerializer.cs     ← serialización intacta
Trading.Strategies/Regimes/PersistedHmmModel.cs      ← contrato del DTO intacto
Trading.Strategies/Regimes/FeatureExtractor.cs       ← features intactas (a menos que Hipótesis C, en cuyo caso se reportó y detuvo)
Trading.Strategies/Regimes/FeatureScaler.cs          ← scaler intacto (a menos que Hipótesis C)
models/regime/BTCUSDT-perp-binance.hmm.json          ← modelo de producción intacto (a menos que operador decida re-entrenar manualmente fuera del brief)
Trading.Domain/**                                    ← Domain intacto
Trading.Application/**                               ← Application intacto
AI.md                                                ← sin cambios
POLICY.md                                            ← sin cambios
```

---

## Riesgos conocidos y cómo el asistente debe manejarlos

1. **El bypass del `[Fact(Skip = "...")]` no funciona con `/p:VSTestNoSkip=true`.** Alternativas en orden de preferencia: (a) agregar `[Fact]` adicional sin Skip con nombre `_DiagnosticoTemporal_NoCommitear`, (b) comentar el `[Fact(Skip = "...")]` recordando reponerlo antes de cualquier compromiso, (c) consultar al operador. Documentar siempre la vía usada en el output al operador.

2. **El test sigue fallando después del fix.** Si la Fase 3 revela que las aserciones siguen sin pasar, hay un defecto adicional no contemplado en las tres hipótesis del ADR-020. **Detenerse y reportar al operador** con todos los logs. No iterar fixes a ciegas: cuatro iteraciones sin convergencia fue exactamente lo que mató al SignalAuditor (ver ADR-014).

3. **No se puede ubicar el JSONL del backtest reciente para la validación cruzada.** Si el JSONL del período 2025-01-01 a 2026-03-31 no existe o no es accesible, **detenerse y pedirle al operador** que (a) corra el backtest una vez con el fix de DEUDA-2 aplicado para producir un JSONL fresco, (b) confirme el path.

4. **El operador reporta validación cruzada AMBIGUA.** No tomar decisión solo. Reportar de vuelta al asistente principal (no a Claude Code), porque la decisión sobre si el modelo es "aceptable" o no requiere conversación con datos sobre la mesa.

5. **El operador aprueba el re-entrenamiento dentro del brief actual.** Aunque el brief explícitamente prohíbe ejecutar `HmmTrainer`, si el operador autoriza en chat directo (no en mensajes preexistentes del brief), Claude Code todavía no ejecuta automáticamente: pide confirmación de un comando concreto a correr. Re-entrenar implica reemplazar un artefacto versionado del repo; el operador debe ver el comando antes de ejecutarse.

6. **El proyecto `HmmTrainer` no compila tras agregar multi-seed.** Si la modificación al trainer rompe compilación por alguna dependencia que no fue prevista, **detenerse y reportar**. No "ajustar" la lógica del trainer sin diagnóstico.

7. **Cambia el baseline de no-regresión del backtest** por el cambio en `SemanticStateMapper`. Aunque el `SemanticStateMapper` se ejecuta solo durante el entrenamiento offline (al construir el modelo), el archivo `BTCUSDT-perp-binance.hmm.json` actual ya tiene el mapeo serializado de cuando el `SemanticStateMapper` se ejecutó originalmente. Cambiar el código del `SemanticStateMapper` **no cambia** el comportamiento runtime sobre el modelo serializado actual. Por tanto, **el baseline de 6 órdenes se preserva sin re-entrenar**. Si Claude Code observa que cambia, hay un acoplamiento no documentado entre runtime y mapper; reportar al operador.

8. **Si Claude Code encuentra inconsistencias entre este brief y el código real**, detenerse y reportar. NO improvisar.

---

## Mensaje de commit sugerido (al cerrar el trabajo)

```
fix(regimes): SemanticStateMapper adaptativo a K (resuelve ADR-020)

DEUDA-1 cerrada. El test AccordHmmClassifierReferenceTests, skipeado desde
el cierre del Hito B Paso 3, ahora pasa verde.

Causa raíz: SemanticStateMapper.Build calculaba topQuartileThreshold como
Ceiling(K * 0.75). Con K=3, esto da 3, lo cual hace que la condición
"positionInSorted >= 3" sea insatisfacible en un array de 3 elementos.
Resultado: ningún estado se mapeaba a HighVolatility con K=3. El bug solo
era visible con K pequeño; el modelo de producción (K=4) lo evadía por
casualidad (posición 3 sí entra en el cuartil cuando hay 4 elementos).

Cambios:
- Trading.Strategies/Regimes/SemanticStateMapper.cs: lógica adaptativa
  según K (K=2 binaria, K=3 tercios, K>=4 cuartiles tradicionales).
- {Si Hipótesis A confirmada} Trading.Strategies/Tools/HmmTrainer/Program.cs:
  multi-seed Baum-Welch (10 seeds, quedarse con el de mayor log-likelihood)
  para mitigar convergencia a óptimo local malo.
- Trading.Strategies.Tests/Regimes/AccordHmmClassifierReferenceTests.cs:
  quitar [Fact(Skip)], el test ahora pasa.
- Trading.Strategies.Tests/Regimes/SemanticStateMapperTests.cs: 5 tests
  nuevos cubriendo K=2, K=3 (caso del bug), K=3 con Squeeze, K=4, K=5.
- DECISIONS: ADR-024 nuevo documentando la resolución y la validación
  cruzada del modelo de producción contra realidad histórica. ADR-020
  pasa a "Resuelta en ADR-024".
- ROADMAP: DEUDA-1 marcada ✅. {Si DEUDA-2 también ✅: Bloque 3 completo}.
  {Si validación cruzada falló: nueva entrada REENT-1 documentada.}

Validación cruzada del modelo de producción contra realidad histórica
de 2025-2026 (5 ventanas inspeccionadas visualmente por el operador):
{OK / FALLA en N ventanas / AMBIGUA}.

{Si validación cruzada FALLA:} Re-entrenamiento del modelo BTCUSDT
queda como deuda explícita REENT-1, no se ejecuta en este commit
(decisión operativa del operador).

Closes DEUDA-1
Refs ADR-020, ADR-024
```

---

## Resumen para el operador al final de DEUDA-1

Al cerrar DEUDA-1, el sistema queda con:

- **`SemanticStateMapper` robusto** para cualquier K ∈ {2, 3, 4, 5, ...}, con tests que cubren los casos extremos. Bug arquitectónico identificado y resuelto.
- **Test sintético activo** (sin Skip) verificando el pipeline end-to-end del HMM con K=3 sobre serie sintética con tres regímenes claramente diferenciados. Cualquier regresión futura del mapper se detecta automáticamente.
- **{Si Hipótesis A confirmada}** Trainer offline más robusto con multi-seed Baum-Welch.
- **Validación cruzada del modelo de producción contra realidad histórica documentada** en ADR-024 — el riesgo asumido de ADR-020 está mitigado.
- **{Si validación cruzada OK}** Modelo de producción BTCUSDT preservado, baseline de 6 órdenes mantiene.
- **{Si validación cruzada FALLA}** Re-entrenamiento documentado como REENT-1 con instrucciones precisas, esperando decisión del operador.

**Estado del Bloque 3 tras este brief:**

- Si DEUDA-2 también está cerrada (esperado): **Bloque 3 completo.** Sistema técnicamente listo para Hito C (paper trading).
- Si DEUDA-2 sigue abierta: Bloque 3 sigue en progreso.

**Próxima decisión operativa (conversación con asistente principal, no Claude Code):**

- Con qué estrategia arrancar Hito C: ¿EmaCross/BTCUSDT 1h (actualmente único IStrategy implementado, no es estrategia con edge defendible) o adelantar el diseño de una primera estrategia régimen-dependiente real (volatility-targeted trend following con filtro HMM)?
- Esta conversación está abierta desde sesiones previas; ya elegiste "patrones de volatilidad y régimen" como terreno. Lo que queda es: formular la hipótesis económica concreta de la estrategia, papers de respaldo, reglas explícitas, ADR previo a implementación.
