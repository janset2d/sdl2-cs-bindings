internal sealed record CSharpEvidence(
    IReadOnlyList<FunctionEvidence> Functions,
    IReadOnlyList<ConstantEvidence> Constants,
    IReadOnlyList<TypeEvidence> Types);
