namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record CSharpEvidence(
    IReadOnlyList<FunctionEvidence> Functions,
    IReadOnlyList<ConstantEvidence> Constants,
    IReadOnlyList<TypeEvidence> Types);
