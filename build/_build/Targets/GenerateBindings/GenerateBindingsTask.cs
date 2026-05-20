using Build.Data.BindingGeneration;
using Build.Host;
using Build.Targets.GenerateBindings.Parse;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Targets.GenerateBindings;

// EnsureVcpkgDependencies must run before GenerateBindings. Cake IsDependentOn
// attributes are not used in this project; sequential dispatch is the caller's
// responsibility (docker/binding-generator-entrypoint.sh runs the two targets
// back to back inside the container, matching the tools.cs setup / ci-sim
// pattern that drives Cake host-side too).
[TaskName("GenerateBindings")]
[TaskDescription("Regenerates SDL2 family bindings driven by manifest.library_manifests[].binding_generation. Default: every family with binding_generation.enabled=true. Linux-canonical, runs inside linux-builder container.")]
public sealed class GenerateBindingsTask(
    IBindingGenerationConfigRepository configRepository,
    BindingFamilyGeneration familyGeneration,
    ILibclangVersionAsserter libclangVersionAsserter,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly IBindingGenerationConfigRepository _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
    private readonly BindingFamilyGeneration _familyGeneration = familyGeneration ?? throw new ArgumentNullException(nameof(familyGeneration));
    private readonly ILibclangVersionAsserter _libclangVersionAsserter = libclangVersionAsserter ?? throw new ArgumentNullException(nameof(libclangVersionAsserter));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AssertLinuxTriplet(context.RuntimeIdentifier, context.Runtime.Triplet);
        _libclangVersionAsserter.Assert();
        LogContainerDigestIfPresent(context.Environment);

        var families = _configRepository.EnumerateEnabledFamilies();
        if (families.Count == 0)
        {
            throw new CakeException(
                "No enabled binding-generation families in manifest. Set binding_generation.enabled=true on at least one library_manifests[] entry.");
        }

        foreach (var familyId in families)
        {
            await _familyGeneration.GenerateAsync(context, familyId, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private static void AssertLinuxTriplet(string rid, string triplet)
    {
        // EndsWith against the manifest-declared canonical Linux triplet suffix.
        // A bare Contains("linux", ...) substring would accept any malformed
        // triplet that mentions "linux" anywhere (e.g. a hypothetical
        // "linux-bridge-windows"); the suffix check pins the actual triplet
        // shape from build/manifest.json runtimes[linux-*].triplet.
        if (!triplet.EndsWith("-linux-hybrid", StringComparison.OrdinalIgnoreCase))
        {
            throw new CakeException(
                "GenerateBindings is Linux-canonical; expected the host triplet to end with '-linux-hybrid' " +
                "(per build/manifest.json runtimes[linux-x64/linux-arm64].triplet) inside the linux-builder " +
                $"container; got RID '{rid}' / triplet '{triplet}'. Invoke via 'tools.cs generate-bindings'.");
        }
    }

    private void LogContainerDigestIfPresent(ICakeEnvironment environment)
    {
        var digest = environment.GetEnvironmentVariable("CONTAINER_DIGEST");
        if (!string.IsNullOrWhiteSpace(digest))
        {
            _log.Information("linux-builder container digest: {0}", digest);
        }
    }

}
