namespace Build.Shared.Runtime;

/// <summary>
/// Resolves child-process .NET runtime environment overrides needed for RID-specific
/// smoke-test execution when the host runner's default <c>dotnet</c> installation is
/// insufficient for the target apphost architecture.
/// </summary>
/// <remarks>
/// <para>
/// Current concrete use-case: <c>PackageConsumerSmoke</c> on <c>win-x86</c>. GitHub's
/// Windows runners ship x64 .NET on <c>PATH</c>, but a 32-bit apphost resolves its own
/// runtime via <c>DOTNET_ROOT_X86</c> / <c>DOTNET_ROOT(x86)</c>. When those are absent, the
/// x86 smoke executable falls back to the x64 hostfxr and dies with
/// <c>0x800700C1 (BAD_EXE_FORMAT)</c>.
/// </para>
/// <para>
/// Return shape matches <see cref="IMsvcDevEnvironment"/>: callers merge the returned delta
/// into the child process only. The parent Cake host stays on the default x64 SDK / host;
/// no global <c>PATH</c> or <c>DOTNET_ROOT</c> mutation happens.
/// </para>
/// </remarks>
public interface IDotNetRuntimeEnvironment
{
    /// <summary>
    /// Returns the environment-variable delta required for child <c>dotnet</c> invocations
    /// targeting <paramref name="rid"/> and the executable <paramref name="targetFrameworks"/>.
    /// Non-special cases return an empty dictionary. Windows x86 smoke on a non-Windows host
    /// throws <see cref="PlatformNotSupportedException"/> as a defence-in-depth assertion.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string rid,
        IReadOnlyList<string> targetFrameworks,
        CancellationToken cancellationToken = default);
}
