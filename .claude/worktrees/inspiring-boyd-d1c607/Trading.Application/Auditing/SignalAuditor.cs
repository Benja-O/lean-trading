using System;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;

namespace Trading.Application.Auditing
{
    /// <summary>
    /// Auditor de fidelidad de señales. Mantiene un buffer rolling de las últimas barras
    /// observadas por instrumento y, cuando una estrategia emite una señal no-Flat,
    /// recalcula los indicadores independientemente y compara con los SignalDiagnostics
    /// declarados.
    ///
    /// Alcance limitado: detecta bugs de flujo de control y estado interno dentro de la
    /// estrategia. NO detecta bugs en la librería de indicadores subyacente (QuantConnect)
    /// ni sesgos en el feed de datos. Para auditoría verdaderamente independiente, ver TODO
    /// en ROADMAP.md ("Auditor Python con TA-Lib").
    ///
    /// Activación: el SignalAuditor es opcional. Si no se construye y wirea desde el host,
    /// no se ejecuta. Tiene costo (mantiene buffer en memoria + recálculo por señal),
    /// por eso se activa con flag explícito.
    /// </summary>
    public class SignalAuditor
    {
        private readonly Dictionary<string, IIndicatorRecomputer> _recomputersByStrategyName;
        private readonly Dictionary<string, Queue<MarketBar>> _observedBarsBySymbol = new();
        private readonly int _maximumBufferSize;
        private readonly decimal _comparisonTolerance;
        private readonly ITradingLogger _logger;

        private readonly List<SignalAuditResult> _auditResults = new();

        public IReadOnlyList<SignalAuditResult> AuditResults => _auditResults;

        /// <param name="recomputers">Implementaciones de recálculo independiente, una por estrategia auditable.</param>
        /// <param name="logger">Logger para reportar discrepancias en tiempo real (también se reportan al final).</param>
        /// <param name="maximumBufferSize">Cuántas barras retener por símbolo. Debe ser >= al lookback más largo de cualquier indicador auditado.</param>
        /// <param name="comparisonTolerance">Tolerancia absoluta para comparar valores recalculados vs declarados. Default 1e-9.</param>
        public SignalAuditor(
            IEnumerable<IIndicatorRecomputer> recomputers,
            ITradingLogger logger,
            int maximumBufferSize = 200,
            decimal comparisonTolerance = 0.000000001m)
        {
            if (recomputers == null) throw new ArgumentNullException(nameof(recomputers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maximumBufferSize = maximumBufferSize;
            _comparisonTolerance = comparisonTolerance;

            _recomputersByStrategyName = recomputers.ToDictionary(
                recomputer => recomputer.StrategyName,
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Observa una barra recibida. Llamar SIEMPRE antes de auditar señales,
        /// independientemente de si la señal va a ser auditada o no, para mantener el buffer al día.
        /// </summary>
        public void ObserveBar(MarketBar marketBar)
        {
            string ticker = marketBar.InstrumentId.Ticker;
            if (!_observedBarsBySymbol.TryGetValue(ticker, out var buffer))
            {
                buffer = new Queue<MarketBar>(_maximumBufferSize);
                _observedBarsBySymbol[ticker] = buffer;
            }
            buffer.Enqueue(marketBar);
            while (buffer.Count > _maximumBufferSize)
            {
                buffer.Dequeue();
            }
        }

        /// <summary>
        /// Audita una señal. La estrategia declara los valores que vio; el auditor recalcula
        /// independientemente y compara. Resultado se acumula en AuditResults.
        ///
        /// Si no hay recomputer registrado para el strategyName, se registra una advertencia
        /// y NO se audita (no debe hacer fallar al sistema).
        /// </summary>
        public void AuditSignal(
            string strategyName,
            string executorIdentifier,
            SignalDirection direction,
            InstrumentId instrumentId,
            SignalDiagnostics declaredDiagnostics)
        {
            if (direction == SignalDirection.Flat) return;
            if (declaredDiagnostics == null || declaredDiagnostics.IsEmpty) return;

            if (!_recomputersByStrategyName.TryGetValue(strategyName, out var recomputer))
            {
                _logger.Warning(
                    "SignalAuditor: no hay IIndicatorRecomputer registrado para estrategia {StrategyName}. Señal NO auditada.",
                    strategyName);
                return;
            }

            string ticker = instrumentId.Ticker;
            if (!_observedBarsBySymbol.TryGetValue(ticker, out var buffer) || buffer.Count == 0)
            {
                _logger.Warning(
                    "SignalAuditor: no hay barras observadas para {InstrumentId}. Señal NO auditada.",
                    instrumentId);
                return;
            }

            var observedBars = buffer.ToArray();
            IReadOnlyDictionary<string, decimal> recomputedValues;
            try
            {
                recomputedValues = recomputer.Recompute(observedBars);
            }
            catch (Exception recomputeException)
            {
                _logger.Error(
                    "SignalAuditor: recomputer para {StrategyName} lanzó excepción. Detalle: {Detail}.",
                    strategyName, recomputeException.ToString());
                return;
            }

            var discrepancies = new List<SignalDiscrepancy>();
            foreach (var declaredEntry in declaredDiagnostics.Values)
            {
                if (!recomputedValues.TryGetValue(declaredEntry.Key, out var recomputedValue))
                {
                    // Clave no producida por el recomputer (limitación conocida). Se ignora.
                    continue;
                }

                decimal absoluteDifference = Math.Abs(declaredEntry.Value - recomputedValue);
                if (absoluteDifference > _comparisonTolerance)
                {
                    discrepancies.Add(new SignalDiscrepancy(
                        Key: declaredEntry.Key,
                        DeclaredValue: declaredEntry.Value,
                        RecomputedValue: recomputedValue,
                        AbsoluteDifference: absoluteDifference));
                }
            }

            bool isConsistent = discrepancies.Count == 0;
            var auditResult = new SignalAuditResult(executorIdentifier, direction, isConsistent, discrepancies);
            _auditResults.Add(auditResult);

            if (!isConsistent)
            {
                _logger.Warning(
                    "SignalAuditor: DISCREPANCIA en señal de {ExecutorIdentifier} ({Direction}). Claves divergentes: {DiscrepancyCount}.",
                    executorIdentifier, direction, discrepancies.Count);
                foreach (var discrepancy in discrepancies)
                {
                    _logger.Warning(
                        "  Clave '{Key}': declarado={DeclaredValue}, recalculado={RecomputedValue}, diff={AbsoluteDifference}.",
                        discrepancy.Key, discrepancy.DeclaredValue, discrepancy.RecomputedValue, discrepancy.AbsoluteDifference);
                }
            }
        }

        /// <summary>
        /// Reporta a Info un resumen agregado de la auditoría: total auditadas, consistentes, con discrepancia.
        /// Se llama al finalizar el backtest desde el host.
        /// </summary>
        public void ReportSummary()
        {
            int total = _auditResults.Count;
            int consistent = _auditResults.Count(result => result.IsConsistent);
            int withDiscrepancy = total - consistent;

            _logger.Info(
                "SignalAuditor: resumen del backtest. Señales auditadas: {Total}. Consistentes: {Consistent}. Con discrepancia: {WithDiscrepancy}.",
                total, consistent, withDiscrepancy);

            if (withDiscrepancy > 0)
            {
                var groupedByExecutor = _auditResults
                    .Where(result => !result.IsConsistent)
                    .GroupBy(result => result.ExecutorIdentifier);

                foreach (var executorGroup in groupedByExecutor)
                {
                    _logger.Warning(
                        "SignalAuditor: {ExecutorIdentifier} acumuló {Count} señales con discrepancia.",
                        executorGroup.Key, executorGroup.Count());
                }
            }
        }
    }
}
