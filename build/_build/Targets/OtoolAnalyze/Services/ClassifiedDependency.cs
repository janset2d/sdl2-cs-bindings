
namespace Build.Targets.OtoolAnalyze.Services;
public sealed record ClassifiedDependency(string Name, string Path, LibraryKind Kind, bool IsSystem);
