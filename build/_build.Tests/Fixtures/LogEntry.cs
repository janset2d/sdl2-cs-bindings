using Cake.Core.Diagnostics;

namespace Build.Tests.Fixtures;

public sealed record LogEntry(LogLevel Level, string Message, Verbosity Verbosity);
