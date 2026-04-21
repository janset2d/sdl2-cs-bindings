using Cake.CMake;

namespace Build.Infrastructure.Tools.CMake;

/// <summary>
/// Slice A binding spike — verifies <c>Cake.CMake 1.4.0</c> resolves under Cake.Frosting 6.1.0
/// + net9.0. Merely references the addin's public surface (<see cref="CMakeSettings"/>) so the
/// assembly is loaded at compile time and any binary-compat break would surface as a build
/// error. Slice D either consumes the addin directly from <c>NativeSmokeTaskRunner</c> or
/// replaces this placeholder with a repo-local <c>CMakeTool</c> / <c>CMakeAliases</c> /
/// <c>CMakeSettings</c> triad following the <see cref="Vcpkg.VcpkgTool{TSettings}"/> template.
/// </summary>
internal static class CakeCmakeSpike
{
    public static CMakeSettings CreateSettings() => new();
}
