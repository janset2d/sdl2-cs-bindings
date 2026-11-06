namespace Build.Tools.Vcpkg;
internal static class VcpkgPackageKey
{
    public static string Create(string packageName, string triplet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);

        return $"{packageName}:{triplet}";
    }
}
