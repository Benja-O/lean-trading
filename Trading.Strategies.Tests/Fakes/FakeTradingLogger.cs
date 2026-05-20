using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;

namespace Trading.Strategies.Tests.Fakes
{
    // Usa Trading.Domain.Abstractions.LogLevel directamente para evitar conflicto
    // con el enum homónimo definido en Trading.Application.Tests.Fakes.
    public sealed record CapturedLogEntry(
        LogLevel Level,
        string MessageTemplate,
        IReadOnlyList<object> Arguments);

    public sealed class FakeTradingLogger : ITradingLogger
    {
        private readonly List<CapturedLogEntry> _entries = new();

        public IReadOnlyList<CapturedLogEntry> Entries => _entries;

        public IEnumerable<CapturedLogEntry> EntriesAtLevel(LogLevel level)
            => _entries.Where(e => e.Level == level);

        public IReadOnlyList<CapturedLogEntry> WarningEntries
            => EntriesAtLevel(LogLevel.Warning).ToList();

        public void Debug(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Debug, messageTemplate, arguments));
        public void Info(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Info, messageTemplate, arguments));
        public void Warning(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Warning, messageTemplate, arguments));
        public void Error(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Error, messageTemplate, arguments));
        public void Critical(string messageTemplate, params object[] arguments)
            => _entries.Add(new CapturedLogEntry(LogLevel.Critical, messageTemplate, arguments));
    }
}
