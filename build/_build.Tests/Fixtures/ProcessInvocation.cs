using Cake.Core.IO;

namespace Build.Tests.Fixtures;

public sealed record ProcessInvocation(FilePath Command, string Arguments, bool RedirectStandardOutput, bool RedirectStandardError, bool Silent);
