using CppAst;

namespace Build.Targets.GenerateBindings.Parsing;

public sealed record CppAstParseResult(
    PlatformParseView ParseView,
    IReadOnlyList<CppCompilation> Compilations);
