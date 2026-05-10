namespace Build.Tests.Fixtures;

internal static class VcpkgPackageInfoFixture
{
    public static string EmptyResults => Load("empty-results.json");

    public static string InvalidJson => Load("invalid-json.json");

    public static string OtherPackageWindows => Load("other-package-x64-windows-hybrid.json");

    public static string Sdl2ImageWindows => Load("sdl2-image-x64-windows-hybrid.json");

    public static string Sdl2MinimalWindows => Load("sdl2-minimal-x64-windows-hybrid.json");

    private static string Load(string fileName)
    {
        return FixtureLoader.Load($"VcpkgPackageInfo/{fileName}");
    }
}
