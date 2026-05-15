using CppAst;

namespace Build.Targets.GenerateBindings.Parsing;

internal sealed record CppAstParseResult(
    PlatformParseView ParseView,
    CppCompilation Compilation);
