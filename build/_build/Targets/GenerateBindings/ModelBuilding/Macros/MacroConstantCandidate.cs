using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.Macros;

internal sealed record MacroConstantCandidate(
    string Name,
    string Value,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<CppToken> Tokens,
    string SourceFile,
    string SourceHeader,
    string ParseViewName)
{
    public bool IsFunctionLike => Parameters.Count > 0;
}
