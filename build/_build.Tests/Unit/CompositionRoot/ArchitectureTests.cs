using System.Reflection;
using Build.Host;

namespace Build.Tests.Unit.CompositionRoot;

/// <summary>
/// Architecture-level direction-of-dependency invariants per ADR-004 §2.13.
/// <para>
/// Five invariants are asserted against the production <c>Build</c> assembly:
/// </para>
/// <list type="number">
///   <item><description><c>Shared</c> has no outward dependencies on the build host or on Cake.</description></item>
///   <item><description><c>Tools</c> have no Feature dependencies (Cake framework + <c>Build.Shared.*</c> only).</description></item>
///   <item><description><c>Integrations</c> have no Feature dependencies (Cake framework + <c>Build.Shared.*</c> only).</description></item>
///   <item><description><c>Features</c> do not cross-reference each other in code. Cross-feature
///       data sharing flows through <c>Build.Shared.*</c> exclusively. Phase Y (2026-05-03) retired
///       the LocalDev orchestration-feature carve-out; multi-feature compose now lives
///       in repo-root <c>tools.cs</c>, not in Cake.</description></item>
///   <item><description><c>Host</c> is free — the composition site (Program.cs, BuildContext, CompositionRoot, paths) may
///       reference any layer, and is excluded from every other invariant's source set.</description></item>
/// </list>
/// <para>
/// Renamed from <c>LayerDependencyTests</c> at the P2 wave per phase-x §6.4. The ADR-002 three-invariant
/// shape (Domain / Application / Infrastructure / Tasks) is retired in this rewrite; ADR-002 namespaces are
/// no longer present in the codebase post-P1, so checking against them would assert against an empty source set.
/// </para>
/// </summary>
public sealed class ArchitectureTests
{
    private const string SharedPrefix = "Build.Shared.";
    private const string ToolsPrefix = "Build.Tools.";
    private const string IntegrationsPrefix = "Build.Integrations.";
    private const string FeaturesPrefix = "Build.Features.";
    private const string HostPrefix = "Build.Host.";

    private static readonly Assembly BuildAssembly = typeof(BuildContext).Assembly;

    [Test]
    public async Task Shared_Should_Have_No_Outward_Or_Cake_Dependencies()
    {
        // Build.Shared.* may reference pure-domain libraries only (NuGet.Versioning, OneOf, etc.)
        // — not Cake.* framework types, not Build.Host.*, not Build.Features.*, not Build.Tools.*,
        // not Build.Integrations.*. Per ADR-004 §2.6 the Shared layer is the build host's
        // vocabulary surface, decoupled from every other namespace.
        var violations = FindViolations(
            sourcePrefix: SharedPrefix,
            forbiddenPrefixes: [HostPrefix, FeaturesPrefix, ToolsPrefix, IntegrationsPrefix],
            forbidCakeReferences: true);

        await Assert.That(violations)
            .IsEmpty()
            .Because(FormatViolations(violations));
    }

    [Test]
    public async Task Tools_Should_Have_No_Feature_Dependencies()
    {
        // Build.Tools.* may depend on Cake framework + Build.Shared.* only. Cake Tool<TSettings>
        // wrappers stay generic to the build host; coupling them to a specific Feature would
        // re-create the Application/Infrastructure mixed-concern shape ADR-002 §1.1.4 flagged.
        var violations = FindViolations(
            sourcePrefix: ToolsPrefix,
            forbiddenPrefixes: [FeaturesPrefix, HostPrefix, IntegrationsPrefix],
            forbidCakeReferences: false);

        await Assert.That(violations)
            .IsEmpty()
            .Because(FormatViolations(violations));
    }

    /// <summary>
    /// Named exception per phase-x §14.5 IPathService Host-coupling risk:
    /// 2 violations (<c>Integrations.{DotNet,Vcpkg} → Host.Paths.IPathService</c>) are
    /// permanently tolerated. <see cref="Build.Host.Paths.IPathService"/> is the canonical
    /// Host-tier path abstraction that Integrations adapters may consume — the
    /// BuildPaths fluent split originally scoped to P4 §8.3 was discarded on
    /// 2026-05-02 (the 50+-member interface + hundreds-of-callsite rewrite didn't
    /// justify its cost). Decoupling at Adım 13.5 was also rejected because the
    /// P4 wave would have immediately re-touched these classes — see phase-x §14.5
    /// risk #3. The allowlist entries are permanent; do not add new
    /// Integrations→IPathService couplings without explicit approval.
    /// </summary>
    [Test]
    public async Task Integrations_Should_Have_No_Feature_Dependencies()
    {
        // Build.Integrations.* may depend on Cake framework + Build.Shared.* only. Same
        // cohesion rule as Tools — non-Cake-Tool external adapters (NuGet client, dotnet
        // pack invoker, MSVC env resolver, …) stay feature-agnostic.
        var violations = FindViolations(
            sourcePrefix: IntegrationsPrefix,
            forbiddenPrefixes: [FeaturesPrefix, HostPrefix, ToolsPrefix],
            forbidCakeReferences: false);

        var permanentIntegrationsAllowlist = new HashSet<string>(StringComparer.Ordinal)
        {
            "  Build.Integrations.DotNet.DotNetPackInvoker -> Build.Host.Paths.IPathService",
            "  Build.Integrations.Vcpkg.VcpkgCliProvider -> Build.Host.Paths.IPathService",
        };

        var unexpectedViolations = violations
            .Where(violation => !permanentIntegrationsAllowlist.Contains(violation))
            .ToList();

        await Assert.That(unexpectedViolations)
            .IsEmpty()
            .Because(FormatViolations(unexpectedViolations));
    }

    [Test]
    public async Task Features_Should_Not_Cross_Reference()
    {
        // Build.Features.X.* may not reference types in Build.Features.Y.*. Cross-feature
        // data sharing flows through Build.Shared.* exclusively. Phase Y (2026-05-03) retired
        // the LocalDev orchestration-feature carve-out; multi-feature compose now lives
        // in repo-root tools.cs, not in Cake.
        var violations = new List<string>();

        var featureTypes = SafeGetTypes(BuildAssembly)
            .Where(t => IsInNamespace(t, FeaturesPrefix) && !IsCompilerGenerated(t));

        foreach (var sourceType in featureTypes)
        {
            var sourceFeatureRoot = ExtractFeatureRoot(sourceType.Namespace!);
            if (sourceFeatureRoot is null)
            {
                continue;
            }

            foreach (var referenced in GetReferencedTypes(sourceType))
            {
                if (referenced.Namespace is null || !referenced.Namespace.StartsWith(FeaturesPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var referencedFeatureRoot = ExtractFeatureRoot(referenced.Namespace);
                if (referencedFeatureRoot is null || string.Equals(referencedFeatureRoot, sourceFeatureRoot, StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add($"  {sourceType.FullName} -> {referenced.FullName}  (sibling feature: {sourceFeatureRoot} → {referencedFeatureRoot})");
            }
        }

        var deduped = violations.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList();
        await Assert.That(deduped)
            .IsEmpty()
            .Because(FormatViolations(deduped));
    }

    [Test]
    public async Task Host_Is_Free()
    {
        // Build.Host.* may reference any layer — it is the composition site (Program.cs,
        // CompositionRoot, BuildContext bind everything). This invariant exists primarily
        // as a dual-direction sanity check: Host is the only layer not constrained by
        // any of invariants #1-#4. The test trivially passes by definition; it survives in
        // the suite as documentation of the design and as a regression guard if a future
        // refactor accidentally drops Host below another layer.
        var hostTypeCount = SafeGetTypes(BuildAssembly)
            .Count(t => IsInNamespace(t, HostPrefix) && !IsCompilerGenerated(t));

        await Assert.That(hostTypeCount)
            .IsGreaterThan(0)
            .Because("Build.Host.* should contain at least one production type (BuildContext, Program.cs companions, paths, configuration).");
    }

    /// <summary>
    /// Extracts the feature root namespace from a fully-qualified namespace.
    /// E.g. <c>Build.Features.Packaging.ArtifactSourceResolvers</c> → <c>Build.Features.Packaging</c>.
    /// Returns <see langword="null"/> when the namespace is not a Features sub-namespace.
    /// </summary>
    private static string? ExtractFeatureRoot(string ns)
    {
        if (!ns.StartsWith(FeaturesPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var afterPrefix = ns.AsSpan(FeaturesPrefix.Length);
        var dotIndex = afterPrefix.IndexOf('.');
        var featureName = dotIndex == -1 ? afterPrefix.ToString() : afterPrefix[..dotIndex].ToString();

        return $"{FeaturesPrefix}{featureName}";
    }

    private static List<string> FindViolations(
        string sourcePrefix,
        string[] forbiddenPrefixes,
        bool forbidCakeReferences)
    {
        var violations = new List<string>();

        var sourceTypes = SafeGetTypes(BuildAssembly)
            .Where(t => IsInNamespace(t, sourcePrefix) && !IsCompilerGenerated(t));

        foreach (var sourceType in sourceTypes)
        {
            foreach (var referenced in GetReferencedTypes(sourceType))
            {
                if (referenced.Namespace is null)
                {
                    continue;
                }

                if (forbidCakeReferences && referenced.Namespace.StartsWith("Cake.", StringComparison.Ordinal))
                {
                    violations.Add($"  {sourceType.FullName} -> {referenced.FullName}  (Cake reference forbidden)");
                    continue;
                }

                foreach (var forbidden in forbiddenPrefixes)
                {
                    if (referenced.Namespace.StartsWith(forbidden, StringComparison.Ordinal))
                    {
                        violations.Add($"  {sourceType.FullName} -> {referenced.FullName}");
                    }
                }
            }
        }

        return violations.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList();
    }

    private static HashSet<Type> GetReferencedTypes(Type type)
    {
        var types = new HashSet<Type>();

        if (type.BaseType is not null)
        {
            types.Add(type.BaseType);
        }

        foreach (var iface in type.GetInterfaces())
        {
            types.Add(iface);
        }

        const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(AllMembers))
        {
            AddWithGenerics(types, field.FieldType);
        }

        foreach (var prop in type.GetProperties(AllMembers))
        {
            AddWithGenerics(types, prop.PropertyType);
        }

        foreach (var method in type.GetMethods(AllMembers))
        {
            AddWithGenerics(types, method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                AddWithGenerics(types, parameter.ParameterType);
            }
        }

        foreach (var ctor in type.GetConstructors(AllMembers))
        {
            foreach (var parameter in ctor.GetParameters())
            {
                AddWithGenerics(types, parameter.ParameterType);
            }
        }

        return types;
    }

    private static void AddWithGenerics(HashSet<Type> set, Type type)
    {
        set.Add(type);
        if (type.IsGenericType)
        {
            foreach (var genericArg in type.GetGenericArguments())
            {
                set.Add(genericArg);
            }
        }
    }

    private static bool IsInNamespace(Type type, string prefix)
        => type.Namespace is not null && type.Namespace.StartsWith(prefix, StringComparison.Ordinal);

    private static bool IsCompilerGenerated(Type type)
        => type.Name.Contains('<', StringComparison.Ordinal)
        || type.Name.Contains('>', StringComparison.Ordinal)
        || type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static string FormatViolations(List<string> violations)
        => violations.Count == 0
            ? "no violations"
            : $"architecture violations ({violations.Count}):\n" + string.Join('\n', violations);
}
