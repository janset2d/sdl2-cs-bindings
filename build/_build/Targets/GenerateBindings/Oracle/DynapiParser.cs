namespace Build.Targets.GenerateBindings.Oracle;

internal static class DynapiParser
{
    public static IReadOnlySet<string> Parse(string text)
    {
        const char quote = (char)39;
        var exports = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = line.Split(quote);

            if (fields.Length >= 6)
            {
                exports.Add(fields[5]);
            }
        }

        return exports;
    }
}
