internal sealed record SourceReport(
    string Id,
    string Name,
    string Path,
    SourceStatus Status,
    int FunctionCount,
    int ConstantCount,
    int TypeCount)
{
    public static SourceReport FromCSharp(string id, string name, string path, SourceEvidence source)
        => new(id, name, path, source.Status, source.Evidence.Functions.Count, source.Evidence.Constants.Count, source.Evidence.Types.Count);

    public static SourceReport FromRequiredSurface(string id, string name, string path, RequiredSurface surface)
        => new(id, name, path, surface.Status, surface.RequiredFunctions.Count, surface.RequiredConstants.Count, 0);

    public static SourceReport FromDynapi(string id, string name, string path, DynapiEvidence evidence)
        => new(id, name, path, evidence.Status, evidence.Exports.Count, 0, 0);
}
