using CppAst;
using Build.Targets.GenerateBindings.PlatformViews;

namespace Build.Targets.GenerateBindings.Parse;

public sealed record CppAstParseResult(
    PlatformParseView ParseView,
    IReadOnlyList<CppCompilation> Compilations);
