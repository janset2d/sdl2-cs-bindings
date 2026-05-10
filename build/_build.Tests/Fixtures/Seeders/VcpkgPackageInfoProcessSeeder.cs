using Build.Host.Cake;
using Cake.Core;

namespace Build.Tests.Fixtures.Seeders;

public static class VcpkgPackageInfoProcessSeeder
{
    public static FakeCakeWorldV2 WithVcpkgPackageInfo(
        this FakeCakeWorldV2 world,
        string packageName,
        string triplet,
        IReadOnlyList<string> ownedFiles,
        IReadOnlyList<string> dependencies,
        bool transitive = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        ArgumentNullException.ThrowIfNull(ownedFiles);
        ArgumentNullException.ThrowIfNull(dependencies);

        var packageKey = $"{packageName}:{triplet}";
        var ownedFilesWithTriplet = ownedFiles
            .Select(path => $"{triplet}/{path}")
            .ToArray();

        var packageInfo = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["version-string"] = "1.0.0",
            ["port-version"] = 0,
            ["triplet"] = triplet,
            ["dependencies"] = dependencies,
            ["owns"] = ownedFilesWithTriplet,
        };

        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["results"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [packageKey] = packageInfo,
            },
        };

        return world.WithProcessResult(
            GetVcpkgToolName(world),
            BuildPackageInfoArguments(packageKey, transitive),
            exitCode: 0,
            stdOut: world.CakeContext.SerializeJson(payload));
    }

    public static FakeCakeWorldV2 WithMissingVcpkgPackageInfo(this FakeCakeWorldV2 world, string packageName, string triplet)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);

        return world.WithProcessResult(
            GetVcpkgToolName(world),
            BuildPackageInfoArguments($"{packageName}:{triplet}", transitive: false),
            exitCode: 0,
            stdOut: "");
    }

    private static string GetVcpkgToolName(FakeCakeWorldV2 world)
    {
        return world.Environment.Platform.Family == PlatformFamily.Windows ? "vcpkg.exe" : "vcpkg";
    }

    private static string BuildPackageInfoArguments(string packageKey, bool transitive)
    {
        var transitiveArgument = transitive ? " --x-transitive" : "";
        return $"x-package-info \"{packageKey}\" --x-installed{transitiveArgument} --x-json";
    }
}
