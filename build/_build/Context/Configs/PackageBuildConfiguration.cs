namespace Build.Context.Configs;

/// <summary>
/// Holds package-task selection and version inputs.
/// </summary>
public sealed class PackageBuildConfiguration(IReadOnlyList<string> families, string? familyVersion)
{
    public IReadOnlyList<string> Families { get; } = families ?? throw new ArgumentNullException(nameof(families));

    public string? FamilyVersion { get; } = familyVersion;
}
