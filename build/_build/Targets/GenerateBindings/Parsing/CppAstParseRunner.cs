using Build.Targets.GenerateBindings.HeaderSet;
using CppAst;

namespace Build.Targets.GenerateBindings.Parsing;

internal sealed class CppAstParseRunner(ParseDiagnosticFormatter diagnosticFormatter)
{
    private readonly IReadOnlyList<string> _baseDefines =
    [
        "SDL_DECLSPEC=",
        "__PRFCHWINTRIN_H=1",
    ];

    private readonly ParseDiagnosticFormatter _diagnosticFormatter = diagnosticFormatter ?? throw new ArgumentNullException(nameof(diagnosticFormatter));

    public CppParserOptions CreateOptions(ResolvedHeaderSet headerSet, PlatformParseView parseView)
    {
        ArgumentNullException.ThrowIfNull(headerSet);
        ArgumentNullException.ThrowIfNull(parseView);

        var options = new CppParserOptions
        {
            ParseMacros = true,
            TargetSystem = string.Empty,
            SystemIncludeFolders = { headerSet.IncludeRoot.FullPath },
        };

        foreach (var define in _baseDefines)
        {
            options.Defines.Add(define);
        }

        foreach (var define in parseView.Defines)
        {
            options.Defines.Add(define);
        }

        foreach (var undefine in parseView.Undefines)
        {
            options.AdditionalArguments.Add($"-U{undefine}");
        }

        return options;
    }

    public CppAstParseResult Parse(ResolvedHeaderSet headerSet, PlatformParseView parseView)
    {
        var options = CreateOptions(headerSet, parseView);
        var compilation = CppParser.ParseFiles(headerSet.Headers.Select(file => file.FullPath).ToList(), options);
        if (compilation.HasErrors)
        {
            throw new InvalidOperationException(_diagnosticFormatter.FormatErrors(parseView.Name, compilation));
        }

        return new CppAstParseResult(parseView, compilation);
    }
}
