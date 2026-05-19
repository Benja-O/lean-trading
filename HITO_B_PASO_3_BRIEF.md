# HITO B — Paso 3: HMM real con Accord.NET, trainer offline y modelo entrenado de BTCUSDT

> **Brief ejecutable para Claude Code CLI.** Tercer y último paso del Hito B. Reemplaza el classifier fake del Paso 2 por un clasificador HMM real entrenado con datos históricos reales de BTCUSDT perpetual de Binance. Introduce un proyecto standalone para el entrenamiento offline. El modelo resultante se commitea al repo como artefacto versionado.
>
> **Pre-requisitos:** Pasos 1 y 2 commiteados y verdes. El brief asume que existen: el contrato `IMarketRegimeClassifier`, el registry `MarketRegimeRegistry`, el `ConfigurableMarketRegimeClassifier` (fake), el filtro pre-orden en `BarProcessingService`, el wiring inicial en `TradingAlgorithmHost`. El `strategies.json` tiene `"CompatibleRegimes": ["Trend"]` en la entrada de EmaCrossStrategy.

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio de los puntos críticos:

- **Cero comandos `git` de cualquier tipo.** Lista exhaustiva en `AI.md`. No worktrees, no ramas, no checkouts, no commits.
- **No compilar.** El usuario compila.
- **No correr tests.** El usuario corre tests.
- **Excepción autorizada para este brief:** ejecutar el `HmmTrainer` standalone (proyecto nuevo de consola) **es necesario** para producir el modelo serializado que se commitea al repo. Esta es la única ejecución de código permitida en este paso. Si el trainer falla, reportar el error y detenerse; no intentar "corregir" la lógica del modelo, las features o las reglas del mapper sin consultar.
- **Si Claude Code detecta una inconsistencia entre el código actual y este brief**, detenerse y reportar. NO improvisar.
- **Al final del trabajo, proponer el mensaje de commit sugerido** según la política del `AI.md`.

---

## Contexto y motivación del Paso 3

Hoy el sistema tiene un classifier fake (`ConfigurableMarketRegimeClassifier`) que devuelve siempre `RegimeLabel.Trend` para BTCUSDT. El filtro de régimen funciona correctamente (validado empíricamente en el Paso 2 cambiando temporalmente la etiqueta a `HighVolatility`: cero entradas), pero la "inteligencia" detrás de la clasificación es una constante hardcodeada, no análisis estadístico real del mercado.

El Paso 3 reemplaza esa constante por un **Hidden Markov Model (HMM)** entrenado con 5 años de datos históricos de BTCUSDT perpetual de Binance. El sistema queda con clasificación de régimen genuina, basada en propiedades estadísticas observadas del mercado.

El alcance del Paso 3 incluye:

1. **Implementación del adaptador HMM real** que consume Accord.NET.
2. **Creación de un proyecto standalone para entrenamiento offline** (no parte del runtime).
3. **Entrenamiento del modelo** con datos históricos y generación del archivo serializado.
4. **Refactor del wiring** de `TradingAlgorithmHost` para que sea agnóstico al instrumento (resuelve el hardcoding actual de `btcInstrumentId`).
5. **Ajuste del warm-up** para que el HMM se entrene durante el período de warm-up de QuantConnect.
6. **Documentación**: cierre del ADR-017 (estado "Aceptada"), agregado de ADR-019 con detalles específicos del HMM, actualización del ROADMAP (Hito B completado).

---

## Decisiones técnicas aplicadas (no discutir, aplicar)

| Decisión | Valor |
|---|---|
| Librería HMM | **Accord.NET** (`Accord.MachineLearning`, `Accord.Statistics`). Versión última estable disponible para .NET 10. Si no compila en .NET 10 por dependencias deprecated, reportar y detenerse. |
| Algoritmo de entrenamiento | Baum-Welch (provisto por Accord). |
| Algoritmo de decodificación en runtime | Viterbi para estado más probable + forward filtering para distribución de probabilidades posterior. |
| Topología del HMM | Ergodic (todos los estados pueden transicionar a todos los demás). |
| Emisiones | Multivariate Gaussian (cada estado tiene su propia media y matriz de covarianza sobre el vector de features). |
| Number of states (K) | Probar K ∈ {2, 3, 4} durante entrenamiento. Elegir el de **BIC mínimo**. Guardar el K elegido en el modelo serializado. |
| Features por barra | Tres features: (1) retornos logarítmicos = `ln(close[t] / close[t-1])`, (2) volatilidad rolling 20 períodos = `std(returns[t-19..t])`, (3) momentum ratio = `SMA(close, 20)[t] / SMA(close, 50)[t] - 1`. Las primeras 50 barras del training set se descartan (warm-up de features). |
| Normalización de features | Standardization (z-score): cada feature se transforma a `(x - μ_train) / σ_train` usando media y desvío del training set. Los parámetros del scaler se serializan junto al modelo para poder normalizar barras nuevas en runtime con los mismos parámetros del entrenamiento. |
| Warm-up del HMM en runtime | **100 barras 4h** acumuladas en buffer rolling antes de empezar a clasificar. Durante warm-up, el classifier devuelve `RegimeClassification.UnknownFor(...)`. Coordinación con el `SetWarmUp` de QuantConnect: ver sección "Coordinación de warm-ups" más abajo. |
| Semilla random | 42 (Accord respeta seed para Baum-Welch). Reproducibilidad garantizada. |
| Mapeo semántico de estados a `RegimeLabel` | Calculado offline durante el entrenamiento (`SemanticStateMapper`). Se serializa junto al modelo. Reglas: ver sección "Mapeo semántico" más abajo. |
| Ventana de entrenamiento | **2020-01-01 a 2024-12-31 UTC.** ~10950 barras 4h. Estrictamente anterior al período del backtest (2025-01-01 a 2026-03-31). Cero overlap, cero lookahead bias. |
| Instrumento entrenado en este paso | **Solo BTCUSDT perpetual de Binance.** El "instrumento" incluye exchange y tipo de contrato; el modelo entrenado para BTCUSDT perpetual NO debe usarse para BTCUSDT spot ni para otros exchanges sin re-entrenamiento. |
| Fuente de datos | Archivos zip mensuales en `F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\4h\BTCUSDT\` (formato Binance Klines: 12 columnas, primeras 6 son OpenTime, Open, High, Low, Close, Volume). |
| Formato del modelo serializado | JSON (`System.Text.Json`, `JsonSerializerOptions.WriteIndented = true`). Se commitea al repo en `models/regime/BTCUSDT-perp-binance.hmm.json`. Legible en code review. |
| Wiring del registry | **Refactor importante:** el `TradingAlgorithmHost` debe extraer dinámicamente los instrumentos únicos del `strategies.json` y crear un classifier por cada instrumento que (a) tenga al menos una estrategia con `CompatibleRegimes` declarado, y (b) tenga modelo serializado disponible en `models/regime/`. Si una estrategia declara `CompatibleRegimes` pero el modelo del instrumento no existe, **fallar loud al boot** con mensaje claro. |

---

## Coordinación de warm-ups: detalle crítico

Hay **dos warm-ups distintos** que coordinar:

**Warm-up de QuantConnect (`SetWarmUp`):** período donde QC procesa barras históricas para que el algoritmo tenga estado al empezar a operar. Durante este período, las estrategias e indicadores se "calientan" pero **no se envían órdenes** al broker.

**Warm-up del HMM (las primeras 100 barras procesadas):** período donde el HMM acumula barras en su buffer interno antes de poder clasificar con confianza. Mientras esté en warm-up, devuelve `UnknownFor`.

**El bug actual en el código de `TradingAlgorithmHost`** (Paso 2, línea aproximada 5501):

```csharp
regimeConsolidator.DataConsolidated += (sender, tradeBarData) =>
{
    if (IsWarmingUp) return;  // ← ESTO ES UN BUG PARA EL PASO 3
    var marketBar = MarketBarMapper.ToMarketBar((TradeBar)tradeBarData, _instrumentResolver);
    regimeRegistry.ClassifyBar(marketBar);
};
```

El `if (IsWarmingUp) return;` evita que las barras lleguen al registry durante el warm-up de QC. Con el fake del Paso 2 esto no importaba (el fake siempre devuelve `Trend` instantáneamente). Con el HMM real, **es crítico que las barras del warm-up SÍ lleguen al registry** para que el HMM se vaya calentando durante ese período.

**Cambio requerido:** quitar el `if (IsWarmingUp) return;` del handler del consolidator de régimen. El HMM ahora procesa todas las barras desde el inicio.

**Coordinación operativa resultante:**

- `SetWarmUp` de QC se extiende para cubrir al menos 100 barras 4h = **17 días de calendario** antes del `SetStartDate`. Concretamente: `SetWarmUp(TimeSpan.FromDays(17))` o equivalente con margen (recomiendo `TimeSpan.FromDays(20)` para tener buffer).
- Durante esos 17-20 días, QC envía las barras 4h al consolidator de régimen, el consolidator se las pasa al registry, el registry se las pasa al `AccordHmmClassifier`, el classifier las acumula en su buffer rolling.
- Las primeras 100 barras del classifier devuelven `UnknownFor`. A partir de la barra 101, el classifier devuelve clasificaciones reales.
- Cuando QC dispara `OnWarmupFinished` y empieza la fase de operación real, el HMM ya está warmed-up y las consultas al registry devuelven clasificaciones genuinas.

**Verificación operativa:** después de implementar el Paso 3, el log de arranque del backtest debe mostrar el período de warm-up de QC procesando barras (sin emitir órdenes), y la primera orden enviada debe coincidir aproximadamente con el `SetStartDate` original (1/1/2025).

---

## Mapeo semántico de estados a `RegimeLabel`

El HMM produce estados crudos (índices 0..K-1). El sistema necesita etiquetas semánticas (`Trend`, `MeanReverting`, `HighVolatility`, `Squeeze`). El mapeo se calcula **offline durante el entrenamiento** (no en runtime) y se serializa junto al modelo.

**Reglas de mapeo:**

Para cada estado i ∈ {0..K-1}, calcular en el training set:
- `μᵢ` = media de retornos cuando el HMM está en estado i (calculada con Viterbi sobre el training set).
- `σᵢ` = desvío estándar de retornos del estado i.
- `ρᵢ` = probabilidad de auto-transición (matriz transición[i, i]).

Etiquetado (aplicado en orden):
1. Si `σᵢ` está en el cuartil superior de los K estados → `HighVolatility`.
2. Si no, y `σᵢ` está en el cuartil inferior y `ρᵢ > 0.7` → `Squeeze`.
3. Si no, y `|μᵢ| > 0.001` (en escala de retornos 4h) y `ρᵢ > 0.6` → `Trend`.
4. Si no → `MeanReverting` (default).

**Casos especiales:**

- Si la regla produce dos estados con la misma etiqueta, está permitido. El diccionario de probabilidades del `RegimeClassification` suma probabilidades por etiqueta antes de devolverlas.
- Si la regla deja todos los estados sin "Trend" o "HighVolatility" claros (caso degenerado), forzar al menos uno por cuartil. El sistema NO debe quedar sin ningún estado `Trend` mapeado.

**Decisión arquitectónica:** este mapeo es deliberadamente simple. La sofisticación futura (ej. usar BIC para descubrir naturalmente cuántos estados hay vs forzar K=3, o agregar features regime-specific) queda para iteraciones posteriores. Lo importante en este paso es que el mapeo sea determinista, reproducible y razonable.

---

## Estructura final de archivos

### Archivos nuevos

```
Trading.Strategies/Regimes/
  AccordHmmClassifier.cs          (implementación de IMarketRegimeClassifier con Accord)
  HmmModelSerializer.cs           (Load/Save del PersistedHmmModel a/desde JSON)
  PersistedHmmModel.cs            (DTO de serialización: parámetros del HMM + scaler + mapper + metadata)
  SemanticStateMapper.cs          (encapsula el mapeo estado → RegimeLabel; constructor recibe el dict serializado)
  BinanceKlinesParser.cs          (parsea archivos zip mensuales de Binance Klines a IReadOnlyList<MarketBar>)
  AccordHmmClassifierFactory.cs   (helper estático: dado un path a JSON, construye un AccordHmmClassifier listo para usar)

Trading.Strategies/Tools/HmmTrainer/
  HmmTrainer.csproj               (proyecto de consola standalone, target net10.0)
  Program.cs                      (entry point: descomprime CSVs, calcula features, entrena, serializa)
  FeatureExtractor.cs             (lógica de cálculo de features: retornos log, vol rolling, momentum ratio)
  FeatureScaler.cs                (z-score con medias y desvíos del training set)
  BicCalculator.cs                (BIC = ln(N)·p − 2·logL)

models/regime/
  BTCUSDT-perp-binance.hmm.json   (generado por el HmmTrainer en este paso; se commitea al repo)

Trading.Strategies.Tests/         (si el proyecto no existe, crear siguiendo el patrón de Trading.Application.Tests)
  Regimes/
    AccordHmmClassifierReferenceTests.cs   (test de referencia con serie sintética conocida)
    SemanticStateMapperTests.cs            (tests del mapeo estado → label)
    BinanceKlinesParserTests.cs            (tests del parser)
```

### Archivos a modificar

```
Trading.Strategies/Trading.Strategies.csproj
  → Agregar PackageReference a Accord.MachineLearning (última versión estable).
  → Si no compila en .NET 10, REPORTAR Y DETENERSE.

Trading.Strategies/TradingAlgorithmHost.cs
  → REFACTOR del bloque "===== Régimen de mercado =====" (líneas aprox. 5374-5384):
    Reemplazar el bloque hardcoded de BTCUSDT por:
    1. Extracción dinámica de instrumentos únicos desde rootConfiguration.Timeframes[*].Strategies[*].Symbol
       que tengan al menos una estrategia con CompatibleRegimes != null.
    2. Por cada instrumento único: buscar el modelo en
       Path.Combine(AppContext.BaseDirectory, "models", "regime", $"{instrumentId}-perp-binance.hmm.json").
       Si existe, construir AccordHmmClassifier via AccordHmmClassifierFactory.Load(path).
       Si NO existe pero hay estrategias que dependen del régimen para ese instrumento, fallar loud
       con InvalidOperationException: "El instrumento {X} tiene estrategias con CompatibleRegimes
       declarado pero no existe el modelo entrenado en {path}. Ejecutá HmmTrainer para generarlo."
    3. Construir el MarketRegimeRegistry con la colección completa de classifiers.

  → REFACTOR del bloque "===== Consolidator dedicado para el régimen de mercado (4h) =====" (líneas aprox. 5489-5507):
    1. Iterar sobre regimeRegistry.GetRegisteredInstruments() (método nuevo a agregar en el registry,
       o exponer el HashSet internamente) en lugar de "new[] { btcInstrumentId }".
    2. QUITAR el "if (IsWarmingUp) return;" del handler. El HMM debe procesar barras durante el warm-up.

  → AJUSTAR SetWarmUp: la línea actual de SetWarmUp (si existe) debe extenderse a TimeSpan.FromDays(20)
    como mínimo para cubrir las 100 barras 4h de warm-up del HMM con margen.
    Si no hay SetWarmUp todavía, agregarlo después de SetStartDate/SetEndDate.

Trading.Application/Regimes/MarketRegimeRegistry.cs
  → Agregar método público: IReadOnlySet<InstrumentId> GetRegisteredInstruments()
    que devuelve los keys del _classifiers diccionario.
  → No tocar nada más.

DECISIONS.md
  → Actualizar ADR-017: estado pasa de "Parcialmente aceptada" a "Aceptada".
    Agregar al final de la sección "Consecuencias" una nota:
    "Paso 3 completado el {FECHA}. Modelo BTCUSDT-perp-binance entrenado con ventana
    2020-01-01 a 2024-12-31. K elegido por BIC: {K}. Ver ADR-019 para detalles del HMM."
  → Agregar ADR-019 al inicio del archivo (antes de ADR-018):
    Título: "ADR-019 — Implementación específica del HMM en Paso 3 del Hito B"
    Contenido: ver sección "Contenido del ADR-019" más abajo.

ROADMAP.md
  → Diagrama del Plan general: cambiar "Paso 3: ⬜" a "Paso 3: ✅".
    Cambiar "🔄 HITO B" a "✅ HITO B: Clasificación de regímenes de mercado (HMM)".
  → Tabla "🔄 HITO B — En progreso": cambiar a "✅ HITO B — Completado".
    Marcar Paso 3 con ✅ y fecha.
  → Agregar entrada al "Historial completado" describiendo el cierre del Hito B con resumen.
```

### Archivos que NO se tocan

```
Trading.Domain/Abstractions/Regimes/*        ← contratos del Domain intactos
Trading.Application/Regimes/ConfigurableMarketRegimeClassifier.cs   ← el fake queda en el repo para tests futuros
Trading.Application/Execution/BarProcessingService.cs   ← el filtro funciona idéntico
Trading.Application/Risk/*                   ← RiskOrchestrator y monitors intactos
strategies.json                              ← la entrada de EmaCross con CompatibleRegimes ya está
EmaCrossStrategy.cs                          ← la estrategia no se entera del cambio
Tests existentes del Paso 2                  ← siguen pasando idénticos
```

---

## Contratos exactos de los componentes nuevos

### `PersistedHmmModel.cs` (DTO de serialización)

```csharp
namespace Trading.Strategies.Regimes
{
    public sealed record PersistedHmmModel(
        string InstrumentIdentifier,
        string Exchange,
        string ContractType,                          // "perpetual" para este paso
        string Timeframe,                             // "4h"
        DateTime TrainedAtUtc,
        DateTime TrainingWindowStartUtc,
        DateTime TrainingWindowEndUtc,
        int NumberOfStates,                           // K elegido por BIC
        double FinalBic,
        double[] InitialProbabilities,                // π del HMM
        double[][] TransitionMatrix,                  // A del HMM
        double[][] EmissionMeans,                     // μᵢ por estado (vector de medias de features)
        double[][][] EmissionCovariances,             // Σᵢ por estado (matriz de covarianza de features)
        double[] FeatureScalerMeans,                  // medias del training set por feature
        double[] FeatureScalerStdDevs,                // desvíos del training set por feature
        Dictionary<int, string> StateToRegimeLabel,   // mapeo crudo → label (RegimeLabel serializado como string)
        int WarmUpBars                                // 100 en este paso
    );
}
```

### `AccordHmmClassifier.cs` (esqueleto a completar por Claude Code)

```csharp
using Accord.Statistics.Models.Markov;
using Accord.Statistics.Distributions.Multivariate;
using Trading.Domain.Abstractions.Regimes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Strategies.Regimes
{
    public sealed class AccordHmmClassifier : IMarketRegimeClassifier
    {
        private readonly HiddenMarkovModel<MultivariateNormalDistribution, double[]> _model;
        private readonly SemanticStateMapper _semanticMapper;
        private readonly FeatureScaler _scaler;
        private readonly int _warmUpBars;
        private readonly Queue<MarketBar> _rawBarBuffer;        // buffer de barras crudas para calcular features
        private readonly Queue<double[]> _normalizedFeatureBuffer;
        private const int FeatureWarmUpBars = 50;               // primeras 50 barras se descartan para calcular features rolling

        public InstrumentId Instrument { get; }
        public bool IsWarmedUp => _normalizedFeatureBuffer.Count >= _warmUpBars;

        // Constructor recibe el modelo deserializado + mapper + scaler + warmup
        public AccordHmmClassifier(
            InstrumentId instrument,
            HiddenMarkovModel<MultivariateNormalDistribution, double[]> model,
            SemanticStateMapper semanticMapper,
            FeatureScaler scaler,
            int warmUpBars)
        {
            // ... asignaciones defensivas ...
        }

        public RegimeClassification Classify(MarketBar bar)
        {
            // 1. Agregar la barra al buffer de barras crudas (mantener tamaño máximo = FeatureWarmUpBars + 50 por seguridad).
            // 2. Si todavía no hay suficientes barras crudas para calcular features (<= FeatureWarmUpBars), devolver UnknownFor.
            // 3. Calcular las 3 features de la barra actual:
            //    - return_log = ln(close[t] / close[t-1])
            //    - vol_20 = std(return_log[t-19..t])
            //    - momentum_ratio = SMA(close, 20)[t] / SMA(close, 50)[t] - 1
            // 4. Normalizar con _scaler.
            // 5. Agregar al _normalizedFeatureBuffer.
            // 6. Si !IsWarmedUp, devolver UnknownFor.
            // 7. Tomar la secuencia completa del buffer y pasarla a _model.Decode(...) para obtener la secuencia de estados (Viterbi).
            // 8. Tomar el último estado de la secuencia decodificada.
            // 9. Llamar a _model.Posterior(secuencia, last_step) para obtener distribución de probabilidades del último paso.
            // 10. Mapear estado crudo → RegimeLabel via _semanticMapper.
            // 11. Construir el diccionario probabilities: para cada estado i, sumar posterior[i] al label correspondiente.
            // 12. Devolver new RegimeClassification(Instrument, label, probabilities, bar.TimestampUtc).
        }
    }
}
```

### `AccordHmmClassifierFactory.cs`

```csharp
namespace Trading.Strategies.Regimes
{
    public static class AccordHmmClassifierFactory
    {
        public static AccordHmmClassifier Load(string modelJsonPath)
        {
            // 1. Verificar que el archivo existe; si no, lanzar FileNotFoundException con path en el mensaje.
            // 2. Deserializar PersistedHmmModel desde el JSON.
            // 3. Reconstruir el HiddenMarkovModel<MultivariateNormalDistribution, double[]> de Accord
            //    a partir de los parámetros persistidos (transition matrix, initial probabilities, emission means/covariances).
            // 4. Reconstruir el FeatureScaler con FeatureScalerMeans y FeatureScalerStdDevs.
            // 5. Reconstruir el SemanticStateMapper con StateToRegimeLabel (parsear strings a RegimeLabel via RegimeLabelParser.Parse).
            // 6. Construir y devolver AccordHmmClassifier(InstrumentId(persisted.InstrumentIdentifier), model, mapper, scaler, persisted.WarmUpBars).
        }
    }
}
```

---

## Detalle del proyecto `HmmTrainer` (standalone)

### `HmmTrainer.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\Trading.Domain\Trading.Domain.csproj" />
    <ProjectReference Include="..\..\..\Trading.Application\Trading.Application.csproj" />
    <ProjectReference Include="..\..\Trading.Strategies.csproj" />
  </ItemGroup>
</Project>
```

### `Program.cs` (esqueleto)

```csharp
// Parámetros hardcoded para este paso (parametrizar después si crece):
//   - Directorio de zips: F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\4h\BTCUSDT\
//   - Ventana training: 2020-01-01 a 2024-12-31 UTC
//   - Output path: {repoRoot}/models/regime/BTCUSDT-perp-binance.hmm.json
//   - InstrumentId: "BTCUSDT"
//   - Exchange: "Binance"
//   - ContractType: "perpetual"
//   - Timeframe: "4h"
//   - K candidatos: {2, 3, 4}
//   - WarmUpBars del modelo: 100
//   - Semilla random: 42

// Pasos:
//   1. Parsear todos los zips del directorio con BinanceKlinesParser, filtrar barras dentro de la ventana.
//   2. Validar que el set tenga >= 10000 barras. Si no, abort con mensaje claro.
//   3. Extraer features con FeatureExtractor (descartar primeras 50 barras de warm-up de features).
//   4. Calcular FeatureScaler (medias y desvíos del training set). Normalizar features.
//   5. Para K ∈ {2, 3, 4}:
//        a. Entrenar HMM con Accord (Gaussian emissions, ergodic topology, Baum-Welch, semilla 42).
//        b. Calcular log-likelihood final del modelo entrenado.
//        c. Calcular BIC con BicCalculator.
//        d. Guardar (K, modelo, BIC, log-likelihood) en lista de candidatos.
//   6. Elegir el modelo con BIC mínimo.
//   7. Aplicar las reglas del SemanticStateMapper al modelo elegido, generar el dict StateToRegimeLabel.
//   8. Construir PersistedHmmModel con todos los datos.
//   9. Serializar a {output_path} con HmmModelSerializer.Save.
//  10. Log estructurado a consola:
//        "Trained HMM: instrument=BTCUSDT, K={K}, BIC={BIC}, training_window=[{start}, {end}], 
//         n_bars={n}, mapping={state_to_label_dict}"
//  11. Output adicional: log de los BIC de todos los K candidatos para auditoría (decisión del operador
//      verificar si la elección fue clara o marginal).
```

---

## Tests obligatorios (política ADR-014)

### `AccordHmmClassifierReferenceTests` — test de referencia con serie sintética

Test exhaustivo:

```
Arrange:
  Generar serie sintética de 1500 barras con tres regímenes claramente diferenciados:
    - Barras 0-499:    media de retornos +0.0015, desvío 0.005   → "Trend alcista calmo"
    - Barras 500-999:  media de retornos 0,      desvío 0.025    → "HighVolatility"
    - Barras 1000-1499: media de retornos 0,     desvío 0.003    → "MeanReverting"
  Random con semilla 42 para reproducibilidad.

Act:
  - Ejecutar el pipeline completo del HmmTrainer en memoria sobre la serie con K ∈ {2,3,4}.
  - Verificar que el BIC mínimo se da en K=3 (los datos prefieren 3 estados claramente diferenciados).
  - Construir AccordHmmClassifier con el modelo entrenado.
  - Pasar las 1500 barras una a una.
  - Recolectar las clasificaciones de las barras 150-499, 650-999, 1150-1499 (post-warm-up de features y post-warm-up del modelo).

Assert:
  - En cada segmento, ≥85% de las clasificaciones tienen la etiqueta esperada (Trend, HighVolatility, MeanReverting respectivamente).
  - Las probabilidades de cada RegimeClassification suman 1.0 ± 1e-9.
  - IsWarmedUp es false antes de 100 barras post-warm-up de features (o sea, antes de la barra ~150) y true después.
```

### `SemanticStateMapperTests`

```
Test 1: estado con σ en cuartil superior → HighVolatility.
Test 2: estado con σ en cuartil inferior y ρ=0.8 → Squeeze.
Test 3: estado con |μ|=0.002 y ρ=0.7 → Trend.
Test 4: estado con μ≈0 y ρ=0.5 → MeanReverting.
Test 5: caso degenerado (todos los estados similares) → al menos un estado mapea a un label no-MeanReverting.
```

### `BinanceKlinesParserTests`

```
Test 1: fila válida con 12 columnas → parsea correctamente OpenTime, OHLC, Volume.
Test 2: archivo CSV con o sin header → detecta automáticamente y skipea header si existe.
Test 3: timestamp en ms epoch → convierte a DateTime UTC correctamente.
Test 4: archivo zip mensual → descomprime en memoria y parsea todas las filas.
Test 5: precio o volumen inválido (string no numérico, vacío, cero) → fail loud con FormatException o equivalente.
Test 6: filtro por rango de fechas → solo devuelve barras dentro de [startUtc, endUtc].
```

---

## Contenido del ADR-019 a agregar a DECISIONS.md

```markdown
## ADR-019 — Implementación específica del HMM en Paso 3 del Hito B
**Fecha:** {FECHA DE EJECUCIÓN DEL PASO 3}
**Estado:** Aceptada

### Contexto
ADR-017 documentó la decisión de implementar clasificación de régimen con HMM (frente a k-means o redes neuronales). Este ADR documenta los parámetros específicos del HMM efectivamente implementado en el Paso 3 del Hito B, así como las decisiones operativas tomadas durante la ejecución concreta.

### Decisión
**Librería y algoritmos:** Accord.NET para implementación de HMM con emisiones Multivariate Gaussian, topología ergódica, entrenamiento con Baum-Welch (semilla 42), decodificación en runtime con Viterbi + forward filtering posterior para probabilidades.

**Features:** Tres features por barra:
1. Retornos logarítmicos: `ln(close[t] / close[t-1])`
2. Volatilidad rolling 20 períodos: desvío estándar de los últimos 20 retornos log.
3. Momentum ratio: `SMA(close, 20)[t] / SMA(close, 50)[t] - 1`

Las primeras 50 barras del training set se descartan para warm-up de features (cálculo de SMAs).

**Normalización:** Z-score con medias y desvíos del training set. Los parámetros del scaler se serializan junto al modelo para garantizar normalización idéntica en runtime.

**Selección de K:** Probar K ∈ {2, 3, 4} y elegir el de BIC mínimo. K elegido en esta ejecución: {K_VALOR}. BICs registrados: K=2 → {BIC_2}, K=3 → {BIC_3}, K=4 → {BIC_4}.

**Mapeo semántico:** Calculado offline durante entrenamiento aplicando reglas deterministas basadas en media de retornos, desvío de retornos, y persistencia (probabilidad de auto-transición). Resultado para este modelo: {DICT_DE_ESTADO_A_LABEL}.

**Warm-up:** 100 barras 4h. Coordinado con `SetWarmUp` de QuantConnect extendido a 20 días de calendario para cubrir las 100 barras 4h con margen. Durante el warm-up de QC, el HMM procesa las barras pero el classifier devuelve `RegimeLabel.Unknown` hasta acumular 100 barras post-feature-warm-up.

**Ventana de entrenamiento:** 2020-01-01 a 2024-12-31 UTC. ~10950 barras 4h. Estrictamente anterior al período del backtest (2025-01-01 a 2026-03-31). Cero lookahead bias.

**Instrumento:** BTCUSDT perpetual de Binance. El modelo NO es transferible a otros instrumentos ni exchanges sin re-entrenamiento.

### Refactor adicional: wiring agnóstico al instrumento
El wiring del régimen en `TradingAlgorithmHost` se refactorizó para extraer dinámicamente los instrumentos únicos del `strategies.json` y crear un classifier por cada instrumento con modelo disponible. El hardcoding previo de `btcInstrumentId` queda eliminado. Cuando se agregue un segundo instrumento al sistema (ej. ETHUSDT en un futuro Hito E), solo será necesario:
1. Entrenar un modelo para ese instrumento con el HmmTrainer.
2. Commitear el JSON a `models/regime/`.
3. Agregar la estrategia correspondiente a `strategies.json` con `CompatibleRegimes`.

El wiring de `TradingAlgorithmHost` no se toca.

### Alternativas consideradas durante la ejecución
- **Re-entrenamiento periódico automático en runtime.** Descartado por ahora: agrega complejidad operativa (qué pasa si el re-entrenamiento falla, cómo se versionan los modelos, cómo se garantiza consistencia entre re-entrenamiento y operación). Si el modelo se degrada, se re-entrena offline corriendo el `HmmTrainer` y se commitea la nueva versión.
- **Multi-feature engineering avanzado (ATR, RSI, volume ratio).** Descartado en este paso: tres features simples son suficientes para arrancar y validar el pipeline. La iteración de features queda como mejora futura cuando el sistema esté operando y haya feedback empírico.
- **Régimen sistémico además de por activo.** Postergado a SYSREG-1 del Bloque 4 del ROADMAP.

### Consecuencias
- Sistema con clasificación de régimen funcionando con inteligencia estadística real basada en 5 años de datos históricos de BTCUSDT.
- Backtest del período 2025-01-01 a 2026-03-31 se ejecuta con el filtro de régimen activo, filtrando señales de EmaCross según el régimen detectado por el HMM en cada momento.
- Deuda técnica documentada: el `AccordHmmClassifier` mantiene buffer en memoria. Si el proceso reinicia en producción, el classifier entra en warm-up nuevamente (resuelto vía `SetWarmUp` de QC con 20 días). Persistencia del buffer entre reinicios queda como mejora si la latencia de warm-up se vuelve problemática.
- El proyecto `Trading.Strategies/Tools/HmmTrainer` queda como herramienta para re-entrenar el modelo cuando sea necesario (degradación detectada, agregado de instrumentos, mejora de features).
- ADR-017 se actualiza a estado "Aceptada" (Hito B completado en todos sus pasos).
```

---

## Validaciones de salida (a ejecutar por el usuario)

```bash
# Invariantes arquitectónicas
grep -rn "^using QuantConnect" Trading.Domain/ Trading.Application/ Trading.Application.Tests/ Trading.Domain.Tests/
# Debe devolver vacío.

grep -rn "^using Accord" Trading.Domain/ Trading.Application/ Trading.Application.Tests/ Trading.Domain.Tests/
# Debe devolver vacío (Accord solo vive en Trading.Strategies y en Tools/HmmTrainer).

# Build
dotnet build

# Tests
dotnet test
```

**Tests esperados después de Paso 3:**
- Todos los tests previos siguen verdes (Pasos 1 y 2, ~82 tests).
- Tests nuevos verdes: aproximadamente 10-15 entre `AccordHmmClassifierReferenceTests`, `SemanticStateMapperTests`, `BinanceKlinesParserTests`.

**Validación operativa del modelo entrenado:**
- El archivo `models/regime/BTCUSDT-perp-binance.hmm.json` existe, es JSON válido, contiene todos los campos de `PersistedHmmModel`.
- El log del trainer reporta el K elegido, los BICs de los tres candidatos, el mapeo semántico resultante.

**Validación empírica del backtest (recomendada):**
- Correr el backtest del período 2025-01-01 a 2026-03-31. Debe completarse sin errores.
- Comparar el número de órdenes vs el backtest anterior (524 órdenes con classifier fake que devolvía siempre `Trend`).
  - Si el HMM clasifica gran parte del período como `Trend`: número de órdenes similar (~400-524).
  - Si el HMM clasifica gran parte como otros regímenes: número de órdenes significativamente menor.
- El log debe mostrar entradas tipo "señal Long descartada... Régimen actual de BTCUSDT es {X}, no está en CompatibleRegimes" cuando el régimen sea distinto de `Trend`.

Cualquiera de los dos resultados es **información operativa válida**. Si el HMM filtra mucho, vale la pena evaluar si la regla `CompatibleRegimes: ["Trend"]` es la correcta para EmaCross o si conviene agregar `Squeeze` también. Pero esa decisión la toma el operador después del primer backtest.

---

## Riesgos conocidos y cómo el asistente debe manejarlos

1. **Accord.NET puede no compilar en .NET 10.** Es la dependencia externa más riesgosa del paso. Si `dotnet add package Accord.MachineLearning` falla por dependencias incompatibles, **reportar y detenerse**. No buscar workarounds creativos sin consultar al operador.

2. **El trainer puede no converger o producir BICs absurdos.** Si los BICs de los tres K candidatos son extremadamente cercanos (diferencia < 1%), reportar al operador antes de finalizar: puede ser señal de features mal calibradas o ventana de datos insuficiente.

3. **El mapeo semántico puede producir un caso degenerado** donde dos estados terminan con la misma etiqueta. Está permitido (el código suma probabilidades), pero loguear el caso para que el operador lo sepa.

4. **Si el `TradingAlgorithmHost` actual NO tiene `SetWarmUp`**, agregarlo después de `SetStartDate`/`SetEndDate` con `SetWarmUp(TimeSpan.FromDays(20))`. Si ya tiene `SetWarmUp` con un valor distinto, modificarlo al nuevo valor reportando el cambio al operador.

5. **Si Claude Code encuentra inconsistencias entre este brief y el código real**, detenerse y reportar. NO improvisar.

---

## Mensaje de commit sugerido (al cerrar el trabajo)

```
feat(regimes): implementar HMM real con Accord.NET, trainer offline y modelo entrenado de BTCUSDT

- Trading.Strategies/Regimes/: AccordHmmClassifier, SemanticStateMapper,
  HmmModelSerializer, BinanceKlinesParser, AccordHmmClassifierFactory.
- Trading.Strategies/Tools/HmmTrainer/: proyecto standalone de entrenamiento
  offline con Accord.NET. Entrena con datos históricos de Binance Klines
  (ventana 2020-01-01 a 2024-12-31), prueba K ∈ {2,3,4} y elige por BIC.
- models/regime/BTCUSDT-perp-binance.hmm.json: modelo entrenado generado por
  el HmmTrainer, K={K} seleccionado por BIC.
- TradingAlgorithmHost: refactor del wiring de régimen para extraer dinámicamente
  instrumentos únicos del strategies.json. Eliminación del hardcoding de
  btcInstrumentId. SetWarmUp extendido a 20 días para cubrir warm-up del HMM.
  Bug fix: quitar "if (IsWarmingUp) return;" del consolidator de régimen
  (el HMM debe procesar barras durante warm-up).
- MarketRegimeRegistry: nuevo método GetRegisteredInstruments para wiring agnóstico.
- Tests nuevos: AccordHmmClassifierReferenceTests (serie sintética con 3 regímenes),
  SemanticStateMapperTests, BinanceKlinesParserTests.
- DECISIONS: ADR-017 pasa a estado "Aceptada" (Hito B completado).
  ADR-019 nuevo documentando parámetros específicos del HMM.
- ROADMAP: Hito B marcado completo. Movido al historial completado.

Closes HITO-B
Refs ADR-017, ADR-019
```

---

## Resumen para el usuario al final del Paso 3

Al cerrar el Paso 3, el sistema queda con:

- **Clasificación de régimen genuina** basada en HMM entrenado con 5 años de datos históricos reales de BTCUSDT perpetual de Binance.
- **Filtro de régimen activo** que discrimina señales de EmaCross según el régimen detectado por el HMM (vs el fake del Paso 2 que devolvía siempre `Trend`).
- **Wiring escalable** para agregar nuevos instrumentos en el futuro sin tocar el wiring del host.
- **Hito B completo.** Tres pasos cerrados, ADRs documentados, ROADMAP actualizado, sistema listo para arrancar el Bloque 3 (INFRA-2, OPS-1, OPS-2) que precede al Hito C (paper trading).

**Próxima decisión operativa del operador después de commitear:**
- Correr el backtest completo y comparar resultados con el backtest del Paso 2 (que tenía el filtro de régimen pero con classifier fake).
- Si el HMM filtra demasiado o demasiado poco, ajustar `CompatibleRegimes` en `strategies.json` (decisión del operador, no del sistema).
