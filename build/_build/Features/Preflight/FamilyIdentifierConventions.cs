using System.Globalization;

namespace Build.Features.Preflight;

/// <summary>
/// Canonical family-identifier naming conventions per
/// <c>docs/knowledge-base/release-lifecycle-direction.md §1</c>.
/// </summary>
/// <remarks>
/// Family identifier format: <c>sdl&lt;major&gt;-&lt;role&gt;</c> (lowercase, kebab).
/// Examples: <c>sdl2-core</c>, <c>sdl2-image</c>, <c>sdl3-core</c>.
///
/// Derived names per family identifier:
/// <list type="bullet">
/// <item>Managed PackageId: <c>Janset.SDL{Major}.{Role}</c> (e.g. <c>Janset.SDL2.Core</c>)</item>
/// <item>Native PackageId: <c>Janset.SDL{Major}.{Role}.Native</c> (e.g. <c>Janset.SDL2.Core.Native</c>)</item>
/// <item>MinVerTagPrefix: <c>{family}-</c> (e.g. <c>sdl2-core-</c>)</item>
/// </list>
/// </remarks>
public static class FamilyIdentifierConventions
{
    /// <summary>
    /// Parses a family identifier into its SDL major version and role components.
    /// </summary>
    /// <param name="familyIdentifier">Family identifier in form <c>sdl&lt;major&gt;-&lt;role&gt;</c>.</param>
    /// <returns>SDL major version (digits string, e.g. <c>"2"</c>) and PascalCase role (e.g. <c>"Core"</c>).</returns>
    public static (string SdlMajor, string Role) Parse(string familyIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyIdentifier);

        var dashIndex = familyIdentifier.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex <= 0 || dashIndex >= familyIdentifier.Length - 1)
        {
            throw new ArgumentException(
                $"Family identifier must follow 'sdl<major>-<role>' format. Got: '{familyIdentifier}'.",
                nameof(familyIdentifier));
        }

        var sdlPart = familyIdentifier[..dashIndex];
        var role = familyIdentifier[(dashIndex + 1)..];

        if (!sdlPart.StartsWith("sdl", StringComparison.OrdinalIgnoreCase) || sdlPart.Length <= 3)
        {
            throw new ArgumentException($"Family identifier prefix must be 'sdl<major>'. Got: '{sdlPart}'.", nameof(familyIdentifier));
        }

        var majorPart = sdlPart[3..];
        if (!majorPart.All(char.IsDigit))
        {
            throw new ArgumentException($"Family identifier SDL major must be all digits. Got: '{majorPart}'.", nameof(familyIdentifier));
        }

        return (majorPart, ToPascalCase(role));
    }

    /// <summary>
    /// Returns the canonical managed PackageId for a family identifier.
    /// </summary>
    public static string ManagedPackageId(string familyIdentifier)
    {
        var (sdlMajor, role) = Parse(familyIdentifier);
        return string.Create(CultureInfo.InvariantCulture, $"Janset.SDL{sdlMajor}.{role}");
    }

    /// <summary>
    /// Returns the canonical native PackageId for a family identifier.
    /// </summary>
    public static string NativePackageId(string familyIdentifier)
    {
        var (sdlMajor, role) = Parse(familyIdentifier);
        return string.Create(CultureInfo.InvariantCulture, $"Janset.SDL{sdlMajor}.{role}.Native");
    }

    /// <summary>
    /// Returns the expected MinVerTagPrefix for a manifest tag prefix string.
    /// MinVer requires a trailing dash to separate prefix from SemVer.
    /// </summary>
    public static string MinVerTagPrefix(string manifestTagPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestTagPrefix);
        return manifestTagPrefix + "-";
    }

    /// <summary>
    /// Returns the canonical MSBuild property name carrying the consumer-side version of a
    /// family's managed package. Consumed by the shared smoke-test MSBuild props + the
    /// <c>PackageConsumerSmokeRunner</c> when injecting <c>-p:...=&lt;version&gt;</c> into
    /// dotnet invocations.
    /// </summary>
    /// <remarks>
    /// Convention: <c>Janset.SDL&lt;Major&gt;.&lt;Role&gt;</c> → <c>JansetSdl&lt;Major&gt;&lt;Role&gt;PackageVersion</c>.
    /// Example: <c>sdl2-core</c> → <c>JansetSdl2CorePackageVersion</c>.
    /// </remarks>
    public static string VersionPropertyName(string familyIdentifier)
    {
        var (sdlMajor, role) = Parse(familyIdentifier);
        return string.Create(CultureInfo.InvariantCulture, $"JansetSdl{sdlMajor}{role}PackageVersion");
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return string.Create(value.Length, value, static (span, source) =>
        {
            source.AsSpan().CopyTo(span);
            span[0] = char.ToUpperInvariant(span[0]);
            for (var i = 1; i < span.Length; i++)
            {
                span[i] = char.ToLowerInvariant(span[i]);
            }
        });
    }
}
