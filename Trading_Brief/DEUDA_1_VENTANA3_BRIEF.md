# DEUDA-1 — Mini-sesión: desglose granular barra-a-barra del HMM en ventana 3

> **Brief ejecutable para Claude Code CLI.** Mini-sesión de apoyo al brief principal `DEUDA_1_BRIEF.md` (que está en su Fase 4 — validación cruzada del modelo de producción). Las 5 ventanas inspeccionadas visualmente por el operador en TradingView 4h arrojaron 4 de 5 OK/Ambiguas y 1 ventana (la #3, 2025-01-26 → 2025-02-10) con duda visual fuerte sobre si el modelo etiqueta `Squeeze` un período que contiene un crash con velas grandes (2-3 feb 2025).
>
> **Problema:** El JSONL del backtest solo registra eventos donde una señal fue filtrada por régimen. Durante el crash del 2-3 feb 2025 la `EmaCrossStrategy` no disparó ninguna señal (las EMAs no cruzaron en un movimiento unidireccional), por lo que NO hay log directo del régimen clasificado por el HMM en esas barras específicas. Hay un "punto ciego observacional" exactamente en el período visualmente más informativo para detectar mismatch HighVolatility-vs-Squeeze.
>
> **Objetivo:** producir el desglose **barra-a-barra (4h)** de la clasificación del modelo HMM serializado en producción, sobre las barras históricas del rango 2025-01-22 → 2025-02-15. Para eso, invocar el `AccordHmmClassifier` standalone (fuera de Lean) cargando el modelo serializado actual y reproduciendo el flujo de clasificación contra las barras 4h históricas. **No se modifica producción ni el modelo.** Es una consulta directa al artefacto serializado para tapar el punto ciego del log.
>
> **Resultado esperado:** archivo `briefs/DEUDA_1_ventana3_granular.md` con tabla cronológica timestamp/etiqueta/probabilidades, y un resumen del comportamiento del modelo durante el crash del 2-3 feb 2025. La decisión OK vs FALLA de la validación cruzada la toma el operador con el asistente principal con esa evidencia sobre la mesa.

---

## Reglas operativas (inquebrantables)

Leer y respetar literalmente la sección **"🚦 Límites de Ejecución del Asistente"** de `AI.md`. Recordatorio de los puntos críticos:

- **Cero comandos `git` de cualquier tipo.**
- **Compilación permitida** apuntando a `.csproj` específicos de `Trading.*`.
- **Ejecución de tests permitida** apuntando a `.csproj` específicos de `Trading.*.Tests`.
- **NO se ejecuta `HmmTrainer`.** Esto no es re-entrenamiento. Es una **consulta de lectura** al modelo serializado actual. El archivo `models/regime/BTCUSDT-perp-binance.hmm.json` no se modifica.
- **NO se modifica código de producción** (`Trading.Strategies/Regimes/AccordHmmClassifier.cs`, `HmmModelSerializer.cs`, `PersistedHmmModel.cs`, `FeatureExtractor.cs`, `FeatureScaler.cs`). La consulta se hace via un **test ad-hoc** que usa la API pública de los componentes existentes.
- **Si Claude Code detecta una inconsistencia entre el código actual y este brief**, detenerse y reportar. NO improvisar.
- **Al final del trabajo, NO proponer commit todavía.** El test ad-hoc producido en esta mini-sesión es transitorio (vive hasta que se cierre DEUDA-1) y la decisión de commitearlo o no se toma con la evidencia en mano. El operador decide al final.

---

## Alcance preciso

Una sola acción operativa: producir el desglose granular de etiquetas del HMM para 2025-01-22 → 2025-02-15 (24 días = 144 barras 4h aproximadamente, contando warm-up necesario).

**Por qué empezar antes del 2025-01-26** (la fecha real de inicio de la ventana 3): el `AccordHmmClassifier` requiere warm-up. El warm-up del modelo serializado actual está en `persisted.WarmUpBars` y es relativamente largo (cientos de barras 4h según ADR-019). Para que la clasificación del 2025-01-26 sea válida, el clasificador debe haber procesado suficientes barras previas. **Procesar el clasificador desde el inicio del histórico disponible** es la solución correcta — el classifier internamente buffers lo necesario y arroja `Unknown` hasta calentar. Mantener todas las clasificaciones (incluidas las `Unknown`) y filtrar el reporte por el rango 2025-01-22 → 2025-02-15 al producir la salida.

**Por qué terminar el 2025-02-15** (5 días después del cierre nominal de la ventana 3): para dar contexto post-crash. Si el modelo está en `Squeeze` antes y vuelve a `Squeeze` después, eso refuerza el patrón. Si transita por otras etiquetas, también es información.

---

## Diseño técnico (aplicar directamente)

### Decisión de implementación: test ad-hoc, no script standalone

Crear un test en `Trading.Strategies.Tests/Regimes/ProductionHmmGranularQueryTests.cs`. Razones:

1. **Acceso natural a `Trading.Strategies` y sus dependencias** sin tener que armar `.csproj` standalone nuevo.
2. **Ejecutable con `dotnet test`** (permitido por `AI.md`).
3. El test es transitorio: vive hasta el cierre de DEUDA-1, después se decide si se commitea o se descarta.
4. **Output via `ITestOutputHelper`** capturable con `--logger "console;verbosity=detailed"` o por redirección.

El test NO va a tener aserciones de pass/fail estricto (no es un test de regresión). Su único trabajo es:
- Cargar el modelo serializado de producción.
- Cargar las barras históricas 4h de BTCUSDT desde el archivo Binance.
- Iterar `Classify(bar)` sobre cada barra.
- Emitir el output tabulado al `ITestOutputHelper` Y a un archivo Markdown en `briefs/`.

El test **siempre pasa** (con un `Assert.True(true)` al final), porque su valor es el output, no el veredicto.

### Esqueleto del test

```csharp
// Path: Trading.Strategies.Tests/Regimes/ProductionHmmGranularQueryTests.cs
// PROPÓSITO TRANSITORIO: consulta granular al modelo HMM de producción para
// resolver el punto ciego del log en la ventana 3 de DEUDA-1.
// Se decide si se commitea o se descarta al cerrar DEUDA-1.

using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Trading.Domain.Instruments;
using Trading.Domain.Marketdata;
using Trading.Strategies.Regimes;
using Xunit;
using Xunit.Abstractions;

namespace Trading.Strategies.Tests.Regimes
{
    public class ProductionHmmGranularQueryTests
    {
        private readonly ITestOutputHelper _output;

        public ProductionHmmGranularQueryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void QueryGranularProductionHmm_ForDeuda1Window3()
        {
            // === Configuración ===
            const string ModelJsonPath = @"<RUTA AL MODELO — ver Paso 1 más abajo>";
            const string BinanceKlinesPath = @"<RUTA AL CSV/ARCHIVO DE BARRAS 4H — ver Paso 1 más abajo>";

            var rangeStart = new DateTime(2025, 1, 22, 0, 0, 0, DateTimeKind.Utc);
            var rangeEnd = new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc);
            string outputMarkdownPath = @"<RUTA DE SALIDA — ver Paso 1 más abajo>";

            // === Cargar modelo HMM serializado de producción ===
            var classifier = AccordHmmClassifierFactory.Load(ModelJsonPath);
            _output.WriteLine($"Modelo cargado desde: {ModelJsonPath}");
            _output.WriteLine($"Instrumento: {classifier.Instrument}");

            // === Cargar barras 4h históricas ===
            var allBars = LoadBarsFromBinanceKlines(BinanceKlinesPath, classifier.Instrument);
            _output.WriteLine($"Barras 4h cargadas: {allBars.Count}");
            _output.WriteLine($"Rango temporal: {allBars.First().TimestampUtc:yyyy-MM-dd HH:mm} → {allBars.Last().TimestampUtc:yyyy-MM-dd HH:mm}");

            // === Iterar Classify(bar) sobre TODAS las barras (para que el warm-up suceda
            //     naturalmente). Capturar solo las que caen en el rango de interés para reporte. ===
            var classificationsInRange = new List<RegimeClassification>();

            foreach (var bar in allBars)
            {
                var classification = classifier.Classify(bar);

                if (bar.TimestampUtc >= rangeStart && bar.TimestampUtc <= rangeEnd)
                {
                    classificationsInRange.Add(classification);
                }

                // Optimización: si ya pasamos el rangeEnd, salir
                if (bar.TimestampUtc > rangeEnd)
                {
                    break;
                }
            }

            _output.WriteLine($"\nClasificaciones en rango {rangeStart:yyyy-MM-dd} → {rangeEnd:yyyy-MM-dd}: {classificationsInRange.Count}");

            // === Producir tabla cronológica al test output ===
            _output.WriteLine("\n=== TABLA CRONOLÓGICA (4h) ===");
            _output.WriteLine("| Timestamp UTC          | Etiqueta dominante  | Trend  | Squeeze | HighVol | MeanRev | Unknown |");
            _output.WriteLine("|------------------------|---------------------|--------|---------|---------|---------|---------|");

            foreach (var c in classificationsInRange)
            {
                string row = FormatRow(c);
                _output.WriteLine(row);
            }

            // === Producir archivo Markdown con el reporte completo ===
            WriteMarkdownReport(outputMarkdownPath, classificationsInRange, rangeStart, rangeEnd, classifier);
            _output.WriteLine($"\nReporte Markdown escrito en: {outputMarkdownPath}");

            // === Resumen estadístico al test output ===
            var labelCounts = classificationsInRange
                .GroupBy(c => c.DominantLabel)
                .ToDictionary(g => g.Key, g => g.Count());

            _output.WriteLine("\n=== RESUMEN ===");
            foreach (var (label, count) in labelCounts.OrderByDescending(kv => kv.Value))
            {
                double pct = 100.0 * count / classificationsInRange.Count;
                _output.WriteLine($"  {label}: {count} barras ({pct:F1}%)");
            }

            // === Crash detection: foco específico en 2025-02-02 → 2025-02-04 ===
            var crashWindowStart = new DateTime(2025, 2, 2, 0, 0, 0, DateTimeKind.Utc);
            var crashWindowEnd = new DateTime(2025, 2, 4, 0, 0, 0, DateTimeKind.Utc);
            var crashClassifications = classificationsInRange
                .Where(c => c.TimestampUtc >= crashWindowStart && c.TimestampUtc <= crashWindowEnd)
                .ToList();

            _output.WriteLine($"\n=== FOCO CRASH 2025-02-02 → 2025-02-04 ({crashClassifications.Count} barras 4h) ===");
            foreach (var c in crashClassifications)
            {
                _output.WriteLine($"  {c.TimestampUtc:yyyy-MM-dd HH:mm} → {c.DominantLabel}");
            }

            // Test pasa siempre — su valor es el output, no la aserción.
            Assert.True(true);
        }

        // === Helpers ===

        private static List<MarketBar> LoadBarsFromBinanceKlines(string path, InstrumentId instrument)
        {
            // FORMATO ESPERADO: ver Paso 1 más abajo. Adaptar según el formato real.
            // Implementación de referencia para CSV Binance Klines:
            //   columnas: open_time, open, high, low, close, volume, close_time, ...
            //   open_time en milliseconds since Unix epoch
            //   timeframe: 4h → cada fila es una barra de 4h
            var bars = new List<MarketBar>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("open_time", StringComparison.OrdinalIgnoreCase)) continue; // header

                var parts = line.Split(',');
                long openTimeMs = long.Parse(parts[0], CultureInfo.InvariantCulture);
                decimal open = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
                decimal high = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
                decimal low = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
                decimal close = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
                decimal volume = decimal.Parse(parts[5], CultureInfo.InvariantCulture);

                var timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs).UtcDateTime;

                // ATENCIÓN: la signatura de MarketBar puede variar. Verificar en el código actual
                // y adaptar. Si MarketBar requiere otros campos, completar con valores razonables.
                var bar = new MarketBar(instrument, timestampUtc, open, high, low, close, volume);
                bars.Add(bar);
            }
            return bars;
        }

        private static string FormatRow(RegimeClassification c)
        {
            string ts = c.TimestampUtc.ToString("yyyy-MM-dd HH:mm");
            string label = c.DominantLabel.ToString();

            string trend   = c.LabelProbabilities.TryGetValue(RegimeLabel.Trend, out var pt) ? pt.ToString("F3") : "—";
            string squeeze = c.LabelProbabilities.TryGetValue(RegimeLabel.Squeeze, out var ps) ? ps.ToString("F3") : "—";
            string highVol = c.LabelProbabilities.TryGetValue(RegimeLabel.HighVolatility, out var ph) ? ph.ToString("F3") : "—";
            string meanRev = c.LabelProbabilities.TryGetValue(RegimeLabel.MeanReverting, out var pm) ? pm.ToString("F3") : "—";
            string unknown = c.LabelProbabilities.TryGetValue(RegimeLabel.Unknown, out var pu) ? pu.ToString("F3") : "—";

            return $"| {ts,-22} | {label,-19} | {trend,-6} | {squeeze,-7} | {highVol,-7} | {meanRev,-7} | {unknown,-7} |";
        }

        private static void WriteMarkdownReport(
            string outputPath,
            List<RegimeClassification> classifications,
            DateTime rangeStart,
            DateTime rangeEnd,
            AccordHmmClassifier classifier)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DEUDA-1 — Desglose granular del HMM en ventana 3");
            sb.AppendLine();
            sb.AppendLine($"**Modelo:** {classifier.Instrument}");
            sb.AppendLine($"**Rango analizado:** {rangeStart:yyyy-MM-dd} → {rangeEnd:yyyy-MM-dd}");
            sb.AppendLine($"**Barras totales en rango:** {classifications.Count}");
            sb.AppendLine();
            sb.AppendLine("> **Origen:** consulta directa al modelo serializado de producción");
            sb.AppendLine("> (`BTCUSDT-perp-binance.hmm.json`), sin pasar por Lean. Cada barra 4h del");
            sb.AppendLine("> rango es procesada por `AccordHmmClassifier.Classify(bar)` y se reporta");
            sb.AppendLine("> la etiqueta dominante + probabilidades por etiqueta.");
            sb.AppendLine();

            sb.AppendLine("## Resumen de distribución");
            sb.AppendLine();
            var labelCounts = classifications.GroupBy(c => c.DominantLabel)
                .OrderByDescending(g => g.Count())
                .ToList();
            sb.AppendLine("| Etiqueta | Barras 4h | % |");
            sb.AppendLine("|---|---|---|");
            foreach (var g in labelCounts)
            {
                double pct = 100.0 * g.Count() / classifications.Count;
                sb.AppendLine($"| {g.Key} | {g.Count()} | {pct:F1}% |");
            }
            sb.AppendLine();

            sb.AppendLine("## Foco: crash del 2025-02-02 al 2025-02-04");
            sb.AppendLine();
            var crashStart = new DateTime(2025, 2, 2, 0, 0, 0, DateTimeKind.Utc);
            var crashEnd = new DateTime(2025, 2, 4, 23, 59, 59, DateTimeKind.Utc);
            var crashBars = classifications.Where(c => c.TimestampUtc >= crashStart && c.TimestampUtc <= crashEnd).ToList();
            sb.AppendLine($"**Barras 4h del crash:** {crashBars.Count}");
            sb.AppendLine();
            sb.AppendLine("| Timestamp UTC | Etiqueta dominante | Probabilidades |");
            sb.AppendLine("|---|---|---|");
            foreach (var c in crashBars)
            {
                string probs = string.Join(", ", c.LabelProbabilities
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}={kv.Value:F3}"));
                sb.AppendLine($"| {c.TimestampUtc:yyyy-MM-dd HH:mm} | **{c.DominantLabel}** | {probs} |");
            }
            sb.AppendLine();

            sb.AppendLine("## Tabla cronológica completa");
            sb.AppendLine();
            sb.AppendLine("| Timestamp UTC | Etiqueta dominante | Trend | Squeeze | HighVol | MeanRev | Unknown |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var c in classifications)
            {
                sb.AppendLine(FormatRow(c));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, sb.ToString());
        }
    }
}
```

**Notas sobre el esqueleto:**

- Es código de partida, no necesariamente compilable en el primer intento. Claude Code debe **adaptarlo a las signaturas reales** de `MarketBar`, `RegimeClassification`, `InstrumentId`, etc. Si alguna API difiere de lo que el esqueleto asume, ajustar al constructor/propiedad real.
- El esqueleto asume que `RegimeClassification` tiene una propiedad `LabelProbabilities` de tipo diccionario. Verificar en `Trading.Strategies/Regimes/RegimeClassification.cs`. Si la propiedad real tiene otro nombre o no expone las probabilidades agregadas, adaptar el formato del reporte para usar solo `DominantLabel`.
- El `LoadBarsFromBinanceKlines` asume formato CSV de klines de Binance. Verificar en el Paso 1 cuál es el formato real del archivo histórico que tiene el operador.

---

## Paso 1: validación de rutas y formato del archivo histórico

**Antes de escribir código, Claude Code debe confirmar:**

### 1.1 Path del modelo serializado

Path esperado (según ADR-019 y la convención del proyecto):

```
F:\DesarrolloTrading\QuantConnect\Lean\Launcher\bin\Debug\models\regime\BTCUSDT-perp-binance.hmm.json
```

O alternativamente puede estar en la raíz del proyecto:

```
{ProjectRoot}\models\regime\BTCUSDT-perp-binance.hmm.json
```

**Comprobar con:**

```bash
find . -name "BTCUSDT-perp-binance.hmm.json" -type f 2>/dev/null
```

(o `dir /s /b BTCUSDT-perp-binance.hmm.json` desde la raíz si es PowerShell). Si encuentra múltiples, usar el más reciente (`Get-ChildItem ... | Sort-Object LastWriteTime -Descending`).

**Si no se encuentra el archivo, DETENERSE y reportar.** El test no se puede ejecutar sin el modelo.

### 1.2 Path del archivo de barras 4h históricas

Path declarado en sesiones anteriores: `F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\4h\BTCUSDT\`

**Comprobar el contenido del directorio:**

```bash
ls -la "F:/Mis Documentos/Cripto monedas/Trading/Data/Velas/4h/BTCUSDT/" 2>/dev/null
```

(O `dir "F:\Mis Documentos\Cripto monedas\Trading\Data\Velas\4h\BTCUSDT\"` en cmd/PowerShell). Si no es accesible, **DETENERSE y reportar al operador**. Posibles formatos esperados según convenciones Binance:

- Un archivo CSV por mes (`BTCUSDT-4h-2025-01.csv`, `BTCUSDT-4h-2025-02.csv`).
- Un archivo CSV único con todo el histórico.
- Archivos JSON o Parquet (menos probable).

**Si hay múltiples archivos por mes**, el test debe leer al menos los meses que cubren el rango 2025-01-22 → 2025-02-15 más el warm-up. Como el warm-up del HMM es de cientos de barras 4h (`persisted.WarmUpBars`), conviene cargar también todo el histórico de 2024 disponible, o lo que cubra `WarmUpBars + FeatureExtractor.FeatureWarmUpBars + 1` barras antes del 2025-01-22. **Si el archivo histórico no tiene barras suficientes pre-2025-01-22, reportar y detenerse** — sin warm-up el clasificador devuelve `Unknown` y el reporte no tiene valor.

### 1.3 Formato CSV de Binance Klines

El formato oficial de Binance Klines tiene 12 columnas:

```
open_time, open, high, low, close, volume, close_time,
quote_asset_volume, number_of_trades, taker_buy_base_asset_volume,
taker_buy_quote_asset_volume, ignore
```

`open_time` en **milliseconds since Unix epoch** (entero). Para 4h, el step entre `open_time` consecutivos es `4 * 3600 * 1000 = 14_400_000` ms.

**Si el archivo real tiene otro formato** (ej. JSON, columnas distintas, otro orden), Claude Code adapta el parser. Si el formato es ambiguo, **inspeccionar las primeras 5 líneas y reportar al operador** antes de implementar el parser completo.

### 1.4 Path de salida del reporte

Crear el reporte en:

```
briefs/DEUDA_1_ventana3_granular.md
```

(Relativo a la raíz del repo). Si el directorio `briefs/` no existe en la raíz, crearlo. **El reporte se mantiene en el repo durante el ciclo de DEUDA-1**; la decisión de commitearlo o no la toma el operador al cerrar DEUDA-1.

---

## Paso 2: implementar el test ad-hoc

Una vez confirmados los paths del Paso 1, crear el archivo:

```
Trading.Strategies.Tests/Regimes/ProductionHmmGranularQueryTests.cs
```

Con el esqueleto de la sección "Diseño técnico" adaptado a las APIs reales del proyecto:

- Verificar la signatura del constructor de `MarketBar` (puede tener más o menos campos que el esqueleto asume).
- Verificar la propiedad de `RegimeClassification` que expone las probabilidades agregadas por label (`LabelProbabilities`, `AggregatedProbabilities`, `Probabilities`, etc.). Si la propiedad real solo expone la dominante, simplificar el reporte para usar solo `DominantLabel`.
- Verificar la signatura del constructor de `InstrumentId` (probablemente acepta un string como el símbolo `BTCUSDT` o requiere un objeto compuesto). Adaptar la inicialización.

Si Claude Code no logra encontrar la signatura correcta de algún tipo en 2 intentos de compilación, **detenerse y reportar al operador** con el error de compilación específico. No improvisar tipos.

### Compilación

```bash
dotnet build Trading.Strategies.Tests/Trading.Strategies.Tests.csproj
```

Debe compilar verde.

---

## Paso 3: ejecutar el test y producir el reporte

```bash
dotnet test Trading.Strategies.Tests/Trading.Strategies.Tests.csproj \
    --filter "FullyQualifiedName~QueryGranularProductionHmm_ForDeuda1Window3" \
    --logger "console;verbosity=detailed"
```

**Output esperado:**

1. **Console stdout via `ITestOutputHelper`:** la tabla cronológica completa, el resumen de distribución, y el foco específico del crash 2-4 feb.
2. **Archivo Markdown:** `briefs/DEUDA_1_ventana3_granular.md` con el mismo contenido en formato Markdown amigable para revisión humana.
3. **Test pasa verde** (`Assert.True(true)` al final). Si el test falla por excepción, hay un bug en el código del test ad-hoc, **detenerse y reportar**.

---

## Paso 4: verificación de cordura del reporte

Antes de devolver al operador, Claude Code verifica que el reporte tenga sentido:

1. **El reporte tiene al menos 130 filas en la tabla cronológica** (24 días × 6 barras 4h por día = 144 esperadas; algunas pueden faltar si el archivo histórico tiene gaps, pero el orden debe ser ese).
2. **El warm-up se respetó:** las primeras barras del reporte pueden ser `Unknown`, pero a partir de cierto punto las clasificaciones deben mostrar etiquetas reales (Trend/Squeeze/HighVolatility/MeanReverting). **Si TODAS las filas son `Unknown`**, el warm-up no se cargó: probablemente el archivo histórico no tiene barras previas al 2025-01-22 suficientes. Reportar al operador.
3. **El foco del crash (2-4 feb) tiene barras con etiquetas reales** (no `Unknown`). Si están `Unknown`, mismo problema de warm-up: reportar.
4. **Las probabilidades por etiqueta suman aproximadamente 1.0** en cada fila (tolerancia 0.01 por redondeo). Si suman muy distinto de 1, hay un problema con `RegimeClassification.LabelProbabilities` o con el formato del reporte.

Si las 4 verificaciones pasan, el reporte es válido y se entrega al operador. Si alguna falla, **detenerse y reportar la falla específica**.

---

## Paso 5: reporte al operador (al final)

Producir un mensaje resumen para el operador con:

1. **Path del reporte:** `briefs/DEUDA_1_ventana3_granular.md`.
2. **Distribución global de etiquetas** en el rango (tabla resumen).
3. **Específicamente, las clasificaciones del crash 2025-02-02 → 2025-02-04** (las ~18 barras 4h del crash), listadas con timestamp y etiqueta.
4. **Pregunta directa al operador**: *"El modelo clasificó el crash del 2-3 feb 2025 mayoritariamente como {X}. ¿Esto cambia tu lectura de Ventana 3 (Validación cruzada OK / FALLA / sigue AMBIGUA)?"*. Donde `{X}` es la etiqueta dominante observada en el período del crash.

**Claude Code NO toma la decisión OK / FALLA.** Esa la toma el operador con el asistente principal.

---

## Estructura final de archivos

### Archivos creados (transitorios, decisión de commit pendiente)

```
Trading.Strategies.Tests/Regimes/ProductionHmmGranularQueryTests.cs   ← test ad-hoc, decisión de commit al cierre de DEUDA-1
briefs/DEUDA_1_ventana3_granular.md                                    ← reporte de evidencia, queda referenciado en ADR-024
```

### Archivos NO modificados

```
models/regime/BTCUSDT-perp-binance.hmm.json                            ← modelo intacto, solo lectura
Trading.Strategies/Regimes/AccordHmmClassifier.cs                       ← intacto
Trading.Strategies/Regimes/HmmModelSerializer.cs                        ← intacto
Trading.Strategies/Regimes/SemanticStateMapper.cs                       ← intacto (el fix se aplicará después según resultado)
Trading.Strategies/**                                                   ← cero cambios de producción
Trading.Domain/**, Trading.Application/**                               ← intactos
ROADMAP.md, DECISIONS.md, AI.md, POLICY.md                             ← intactos (se actualizan al cierre formal de DEUDA-1 en sesión separada)
```

---

## Riesgos conocidos y cómo el asistente debe manejarlos

1. **El archivo del modelo no se encuentra en los paths esperados.** Detenerse y pedir al operador la ruta absoluta exacta. NO instanciar un modelo de placeholder.

2. **El archivo de barras 4h no se encuentra o el directorio está vacío.** Detenerse y pedir al operador la ruta absoluta y formato. NO generar barras sintéticas.

3. **El formato del archivo de barras no es Binance Klines estándar.** Inspeccionar las primeras líneas, reportar el formato observado al operador, y pedir confirmación del parser antes de implementarlo completo.

4. **El warm-up del modelo es mayor a las barras disponibles pre-2025-01-22.** El reporte va a tener todas las filas en `Unknown` para el rango de interés. Reportar al operador: "el archivo histórico solo tiene N barras antes del 2025-01-22, insuficientes para el warm-up de M barras del modelo. ¿Tenés histórico más profundo?".

5. **La API de `MarketBar`, `RegimeClassification`, o `InstrumentId` difiere del esqueleto del brief.** Adaptar a las signaturas reales en 2 intentos máximo de compilación. Si tras 2 intentos no compila, detenerse y reportar el error específico.

6. **`AccordHmmClassifierFactory.Load(...)` lanza excepción al cargar el modelo.** Probablemente path o JSON corrupto. Detenerse y reportar, no parchar.

7. **El test compila pero falla por `OutOfMemoryException` u otra excepción de runtime** al iterar sobre muchas barras. Probablemente el `_normalizedFeatureBuffer` del clasificador o el `_rawCloseBuffer` no rotan correctamente (deberían — son `LinkedList` con poda interna). Reportar al operador con el stack trace.

8. **El reporte se produce pero la verificación de cordura del Paso 4 falla.** Reportar al operador con la verificación específica que falló. No "ajustar" el código para que pase.

9. **Si Claude Code encuentra inconsistencias entre este brief y el código real**, detenerse y reportar. NO improvisar.

---

## Mensaje al cierre (NO commit)

**Claude Code NO propone mensaje de commit al final de esta mini-sesión.**

El test ad-hoc es transitorio. La decisión de commitearlo (junto con el reporte Markdown) se toma al cerrar DEUDA-1 formalmente, cuando el operador y el asistente principal hayan revisado el reporte y emitido el veredicto OK/FALLA. En esa sesión posterior, el test ad-hoc:

- **Se commitea junto con `DEUDA_1_BRIEF.md`** si tiene valor durable como test de no-regresión del modelo de producción (consulta granular puede reutilizarse en futuros Hito G).
- **Se descarta** (se elimina antes de committear) si fue puramente diagnóstico transitorio sin valor durable.

Esa decisión se toma con la evidencia en mano, no ahora.

---

## Resumen para el operador al cierre de esta mini-sesión

Al final de esta mini-sesión, el operador tendrá en sus manos:

- **Archivo `briefs/DEUDA_1_ventana3_granular.md`** con la tabla cronológica completa de etiquetas barra-a-barra del modelo de producción sobre el rango 2025-01-22 → 2025-02-15.
- **Foco específico del crash 2025-02-02 → 2025-02-04** con la etiqueta del HMM por cada barra 4h.
- **Distribución global de etiquetas** en el rango.

Con esa evidencia sobre la mesa, el operador y el asistente principal toman en sesión separada la decisión final: **Validación cruzada OK** o **FALLA**. Esa decisión cierra la Fase 4 del brief principal `DEUDA_1_BRIEF.md` y permite avanzar a Fase 5 (decisión sobre re-entrenamiento) y Fase 6 (cierre documental con ADR-024).

**El reporte producido en esta mini-sesión es la pieza que faltaba para mitigar el riesgo asumido de ADR-020 sin punto ciego.**
