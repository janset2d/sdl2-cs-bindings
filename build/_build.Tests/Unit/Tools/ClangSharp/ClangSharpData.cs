using Build.Tools.ClangSharp;
using Cake.Core.IO;

namespace Build.Tests.Unit.Tools.ClangSharp;

internal static class ClangSharpData
{
    public static ClangSharpSettings SettingsFrom(string[] tokens)
    {
        var responseStart = Array.FindIndex(tokens, token => token.StartsWith('@', StringComparison.Ordinal));
        var methodStart = Array.IndexOf(tokens, "--methodClassName");
        var includeStart = Array.IndexOf(tokens, "--include-directory");
        var fileStart = Array.IndexOf(tokens, "--file");
        var defineStart = Array.IndexOf(tokens, "--define-macro");
        var undefineStart = Array.FindIndex(tokens, token => token.StartsWith("--additional=", StringComparison.Ordinal));
        var excludeStart = Array.IndexOf(tokens, "--exclude");
        var trailingStart = excludeStart >= 0 ? excludeStart : tokens.Length;
        var defineEnd = undefineStart >= 0 ? undefineStart : trailingStart;

        return new ClangSharpSettings
        {
            WorkingDirectory = new DirectoryPath("{REPO}/spikes/binding-generators"),
            Config = tokens[4..responseStart],
            ResponseFiles = [.. tokens[responseStart..methodStart].Select(token => new FilePath(token[1..]))],
            MethodClassName = tokens[methodStart + 1],
            LibraryPath = tokens[methodStart + 3],
            Namespace = tokens[methodStart + 5],
            AccessSpecifier = tokens[methodStart + 7],
            IncludeDirectories = [.. tokens[includeStart..fileStart].Where((_, index) => index % 2 == 1).Select(path => new DirectoryPath(path))],
            File = new FilePath(tokens[fileStart + 1]),
            Output = new FilePath(tokens[fileStart + 3]),
            Defines = defineStart < 0 ? [] : tokens[(defineStart + 1)..defineEnd],
            Undefines = [.. tokens
                .Where(token => token.StartsWith("--additional=--undefine-macro=", StringComparison.Ordinal))
                .Select(token => token["--additional=--undefine-macro=".Length..])],
            Exclusions = excludeStart < 0 ? [] : tokens[(excludeStart + 1)..],
        };
    }
}
