using System.Collections.Generic;
using Trading.Application.Auditing;
using Trading.Application.Tests.Fakes;
using Trading.Domain.Models;
using Trading.Domain.ValueObjects;
using Xunit;

namespace Trading.Application.Tests.Auditing
{
    public class SignalAuditorTests
    {
        private readonly FakeTradingLogger _logger = new();
        private readonly InstrumentId _btcUsdt = new("BTCUSDT");

        private static MarketBar BuildBar(InstrumentId instrumentId, decimal close, int timestampOffsetMinutes)
        {
            return new MarketBar(
                instrumentId,
                close,
                new System.DateTime(2025, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
                    .AddMinutes(timestampOffsetMinutes));
        }

        private sealed class FakeRecomputer : IIndicatorRecomputer
        {
            private readonly Dictionary<string, decimal> _resultToReturn;

            public FakeRecomputer(string strategyName, Dictionary<string, decimal> resultToReturn)
            {
                StrategyName = strategyName;
                _resultToReturn = resultToReturn;
            }

            public string StrategyName { get; }

            public IReadOnlyDictionary<string, decimal> Recompute(IReadOnlyList<MarketBar> observedBars)
                => _resultToReturn;
        }

        [Fact]
        public void AuditSignal_WhenDirectionIsFlat_DoesNothing()
        {
            var auditor = new SignalAuditor(new List<IIndicatorRecomputer>(), _logger);
            auditor.ObserveBar(BuildBar(_btcUsdt, 100m, 0));

            auditor.AuditSignal(
                strategyName: "EmaCrossStrategy",
                executorIdentifier: "exec1",
                direction: SignalDirection.Flat,
                instrumentId: _btcUsdt,
                declaredDiagnostics: new SignalDiagnostics(new Dictionary<string, decimal> { ["X"] = 1m }));

            Assert.Empty(auditor.AuditResults);
        }

        [Fact]
        public void AuditSignal_WhenDeclaredAndRecomputedMatch_ResultIsConsistent()
        {
            var recomputer = new FakeRecomputer("EmaCrossStrategy",
                new Dictionary<string, decimal> { ["EmaFast"] = 100m, ["EmaSlow"] = 90m });
            var auditor = new SignalAuditor(new[] { recomputer }, _logger);
            auditor.ObserveBar(BuildBar(_btcUsdt, 100m, 0));

            var declared = new SignalDiagnostics(new Dictionary<string, decimal>
            {
                ["EmaFast"] = 100m,
                ["EmaSlow"] = 90m
            });

            auditor.AuditSignal("EmaCrossStrategy", "exec1", SignalDirection.Long, _btcUsdt, declared);

            Assert.Single(auditor.AuditResults);
            Assert.True(auditor.AuditResults[0].IsConsistent);
        }

        [Fact]
        public void AuditSignal_WhenValuesDiverge_RecordsDiscrepancy()
        {
            var recomputer = new FakeRecomputer("EmaCrossStrategy",
                new Dictionary<string, decimal> { ["EmaFast"] = 100m });
            var auditor = new SignalAuditor(new[] { recomputer }, _logger);
            auditor.ObserveBar(BuildBar(_btcUsdt, 100m, 0));

            var declared = new SignalDiagnostics(new Dictionary<string, decimal>
            {
                ["EmaFast"] = 95m // diverge
            });

            auditor.AuditSignal("EmaCrossStrategy", "exec1", SignalDirection.Long, _btcUsdt, declared);

            Assert.Single(auditor.AuditResults);
            Assert.False(auditor.AuditResults[0].IsConsistent);
            Assert.Single(auditor.AuditResults[0].Discrepancies);
            Assert.Equal("EmaFast", auditor.AuditResults[0].Discrepancies[0].Key);
            Assert.Equal(95m, auditor.AuditResults[0].Discrepancies[0].DeclaredValue);
            Assert.Equal(100m, auditor.AuditResults[0].Discrepancies[0].RecomputedValue);
        }

        [Fact]
        public void AuditSignal_ToleranceAbsorbsTinyRoundingErrors()
        {
            var recomputer = new FakeRecomputer("EmaCrossStrategy",
                new Dictionary<string, decimal> { ["EmaFast"] = 100.000000001m });
            var auditor = new SignalAuditor(
                new[] { recomputer }, _logger, maximumBufferSize: 200, comparisonTolerance: 0.0001m);
            auditor.ObserveBar(BuildBar(_btcUsdt, 100m, 0));

            var declared = new SignalDiagnostics(new Dictionary<string, decimal>
            {
                ["EmaFast"] = 100m
            });

            auditor.AuditSignal("EmaCrossStrategy", "exec1", SignalDirection.Long, _btcUsdt, declared);

            Assert.True(auditor.AuditResults[0].IsConsistent);
        }

        [Fact]
        public void AuditSignal_UnknownStrategy_DoesNotAuditAndLogsWarning()
        {
            var auditor = new SignalAuditor(new List<IIndicatorRecomputer>(), _logger);
            auditor.ObserveBar(BuildBar(_btcUsdt, 100m, 0));

            var declared = new SignalDiagnostics(new Dictionary<string, decimal> { ["X"] = 1m });
            auditor.AuditSignal("UnknownStrategy", "exec1", SignalDirection.Long, _btcUsdt, declared);

            Assert.Empty(auditor.AuditResults);
            Assert.NotEmpty(_logger.WarningEntries);
        }

        [Fact]
        public void AuditSignal_NoObservedBars_DoesNotAuditAndLogsWarning()
        {
            var recomputer = new FakeRecomputer("EmaCrossStrategy",
                new Dictionary<string, decimal> { ["EmaFast"] = 100m });
            var auditor = new SignalAuditor(new[] { recomputer }, _logger);
            // NO se llama ObserveBar

            var declared = new SignalDiagnostics(new Dictionary<string, decimal> { ["EmaFast"] = 100m });
            auditor.AuditSignal("EmaCrossStrategy", "exec1", SignalDirection.Long, _btcUsdt, declared);

            Assert.Empty(auditor.AuditResults);
            Assert.NotEmpty(_logger.WarningEntries);
        }

        [Fact]
        public void AuditSignal_DeclaredKeyNotProducedByRecomputer_IsIgnored()
        {
            // Esta es una limitación conocida: si el recomputer omite una clave (ej. PreviousSignal),
            // el auditor debe ignorarla en lugar de marcarla como discrepancia.
            var recomputer = new FakeRecomputer("EmaCrossStrategy",
                new Dictionary<string, decimal> { ["EmaFast"] = 100m }); // omite EmaSlow
            var auditor = new SignalAuditor(new[] { recomputer }, _logger);
            auditor.ObserveBar(BuildBar(_btcUsdt, 100m, 0));

            var declared = new SignalDiagnostics(new Dictionary<string, decimal>
            {
                ["EmaFast"] = 100m,
                ["EmaSlow"] = 90m // declarada pero no producida por el recomputer
            });

            auditor.AuditSignal("EmaCrossStrategy", "exec1", SignalDirection.Long, _btcUsdt, declared);

            Assert.True(auditor.AuditResults[0].IsConsistent);
        }

        [Fact]
        public void ReportSummary_LogsTotals()
        {
            var auditor = new SignalAuditor(new List<IIndicatorRecomputer>(), _logger);
            auditor.ReportSummary();

            Assert.NotEmpty(_logger.InfoEntries);
        }
    }
}
