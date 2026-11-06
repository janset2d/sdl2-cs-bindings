using Build.Tools.Vcpkg.Settings;
using Cake.Core.IO;

namespace Build.Tests.Fixtures;

internal static class VcpkgInstallData
{
    public const string ExpectedArguments =
        "install --triplet x64-windows-hybrid --host-triplet arm64-windows-hybrid " +
        "--downloads-root C:/repo/downloads --classic --overlay-ports C:/repo/ports " +
        "--overlay-triplets C:/repo/triplets --binarysource clear --feature-flags manifests,versions " +
        "--x-buildtrees-root C:/repo/buildtrees --x-install-root C:/repo/installed " +
        "--x-manifest-root C:/repo/manifest --x-packages-root C:/repo/packages --x-asset-sources clear " +
        "sdl2 sdl2-image --allow-unsupported --clean-after-build --clean-buildtrees-after-build " +
        "--clean-downloads-after-build --clean-packages-after-build --dry-run --editable --enforce-port-checks " +
        "--x-feature image --x-feature mixer --head --keep-going --x-no-default-features --no-downloads " +
        "--only-downloads --only-binarycaching --recurse --x-write-nuget-packages-config --no-print-usage";

    public static readonly DirectoryPath Root = new("C:/repo/vcpkg");

    public static VcpkgInstallSettings AllSwitches() => new(Root)
    {
        Triplet = "x64-windows-hybrid",
        HostTriplet = "arm64-windows-hybrid",
        DownloadsRoot = new DirectoryPath("C:/repo/downloads"),
        ClassicMode = true,
        OverlayPorts = [new DirectoryPath("C:/repo/ports")],
        OverlayTriplets = [new DirectoryPath("C:/repo/triplets")],
        BinarySources = ["clear"],
        FeatureFlags = ["manifests", "versions"],
        BuildTreesRoot = new DirectoryPath("C:/repo/buildtrees"),
        InstallRoot = new DirectoryPath("C:/repo/installed"),
        ManifestRoot = new DirectoryPath("C:/repo/manifest"),
        PackagesRoot = new DirectoryPath("C:/repo/packages"),
        AssetSources = "clear",
        AllowUnsupported = true,
        CleanAfterBuild = true,
        CleanBuildTreesAfterBuild = true,
        CleanDownloadsAfterBuild = true,
        CleanPackagesAfterBuild = true,
        DryRun = true,
        Editable = true,
        EnforcePortChecks = true,
        Features = ["image", "mixer"],
        Head = true,
        KeepGoing = true,
        NoDefaultFeatures = true,
        NoDownloads = true,
        OnlyDownloads = true,
        OnlyBinaryCaching = true,
        Recurse = true,
        WriteNuGetPackagesConfig = true,
        NoPrintUsage = true,
    };
}
