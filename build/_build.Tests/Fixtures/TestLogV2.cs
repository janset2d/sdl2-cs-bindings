using System.Globalization;
using Cake.Core.Diagnostics;

namespace Build.Tests.Fixtures;

public sealed class TestLogV2 : ICakeLog
{
    private readonly Lock _lock = new();
    private readonly List<LogEntry> _entries = [];

    public Verbosity Verbosity { get; set; } = Verbosity.Normal;

    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_lock) { return _entries.ToList(); } }
    }

    public int ErrorCount
    {
        get { lock (_lock) { return _entries.Count(e => e.Level == LogLevel.Error); } }
    }

    public int WarningCount
    {
        get { lock (_lock) { return _entries.Count(e => e.Level == LogLevel.Warning); } }
    }

    public int InfoCount
    {
        get { lock (_lock) { return _entries.Count(e => e.Level == LogLevel.Information); } }
    }

    public void Write(Verbosity verbosity, LogLevel level, string format, params object[] args)
    {
        if (verbosity > Verbosity)
        {
            return;
        }

        var message = args.Length > 0
            ? string.Format(CultureInfo.InvariantCulture, format, args)
            : format;

        lock (_lock)
        {
            _entries.Add(new LogEntry(level, message, verbosity));
        }
    }

    public bool HasMessage(LogLevel level, string contains)
    {
        lock (_lock)
        {
            return _entries.Any(e =>
                e.Level == level &&
                e.Message.Contains(contains, StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool HasNoMessages(LogLevel level)
    {
        lock (_lock)
        {
            return _entries.All(e => e.Level != level);
        }
    }
}

public sealed record LogEntry(LogLevel Level, string Message, Verbosity Verbosity);
