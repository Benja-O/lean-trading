using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Abstractions;

namespace Trading.Application.Tests.Fakes
{
    public enum LogLevel { Debug, Info, Warning, Error, Critical }

    public sealed record CapturedLogEntry(
        LogLevel Level,
        string MessageTemplate,
        IReadOnlyList<object> Arguments);

    public sealed class FakeTradingLogger : ITradingLogger
    {
        private readonly List<CapturedLogEntry> _entries = new();

        public IReadOnlyList<CapturedLogEntry> Entries => _entries;

        public IEnumerable<CapturedLogEntry> EntriesAtLevel(LogLevel level)
            => _entries.Where(entry => entry.Level == level);

        public IReadOnlyList<CapturedLogEntry> DebugEntries
            => EntriesAtLevel(LogLevel.Debug).ToList();
        public IReadOnlyList<CapturedLogEntry> InfoEntries
            => EntriesAtLevel(LogLevel.Info).ToList();
        public IReadOnlyList<CapturedLogEntry> WarningEntries
            => EntriesAtLevel(LogLevel.Warning).ToList();
        public IReadOnlyList<CapturedLogEntry> ErrorEntries
            => EntriesAtLevel(LogLevel.Error).ToList();
        public IReadOnlyList<CapturedLogEntry> CriticalEntries
            => EntriesAtLevel(LogLevel.Critical).ToList();

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
