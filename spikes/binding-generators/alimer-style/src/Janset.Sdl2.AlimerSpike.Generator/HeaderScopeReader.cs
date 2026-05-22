namespace Janset.Sdl2.AlimerSpike.Generator;

internal static class HeaderScopeReader
{
    public static IReadOnlyList<string> ReadHeaderPaths(string includeRoot, string scopeFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(includeRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeFile);

        if (!Directory.Exists(includeRoot))
        {
            throw new DirectoryNotFoundException($"SDL2 include directory not found: {includeRoot}");
        }

        if (!File.Exists(scopeFile))
        {
            throw new FileNotFoundException("Header scope file not found.", scopeFile);
        }

        var headers = new List<string>();
        foreach (var line in File.ReadLines(scopeFile))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var headerPath = Path.Combine(includeRoot, trimmed);
            if (!File.Exists(headerPath))
            {
                throw new FileNotFoundException($"Scoped header '{trimmed}' was not found under '{includeRoot}'.", headerPath);
            }

            headers.Add(headerPath);
        }

        return headers;
    }
}
