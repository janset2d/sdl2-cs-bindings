using Build.Data.NativePackageMetadata;

namespace Build.Tests.Fixtures;
internal static class NativeMetadataFixture
{
    public static NativePackageMetadataDocument Create() => new()
    {
        JansetFamilyVersion = "2.32.0",
        FamilyIdentifier = "sdl2-core",
        UpstreamLibrary = "sdl2",
        UpstreamVersion = "2.32.8",
        VcpkgPortVersion = 0,
        TripletSet = ["x64-windows-hybrid", "x64-linux-hybrid"],
        BuildCommit = "0123456789abcdef0123456789abcdef01234567",
    };
}
