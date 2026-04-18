namespace Build.Tests.Fixtures.Seeders;

/// <summary>
/// Writes a fake <c>vcpkg_installed/{triplet}/</c> layout: <c>info/*.list</c> package-ownership
/// records, plus <c>bin/</c> / <c>lib/</c> / <c>share/</c> payload files. Mirrors the real
/// vcpkg manifest-mode output closely enough for <c>VcpkgCliProvider</c> consumers and the
/// binary-closure walker to operate against fakes.
/// </summary>
public sealed class VcpkgInstalledSeeder : IFixtureSeeder
{
    private readonly string _triplet;
    private readonly List<VcpkgInstalledPackage> _packages = new();

    public VcpkgInstalledSeeder(string triplet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        _triplet = triplet;
    }

    public VcpkgInstalledSeeder WithPackage(string name, Action<VcpkgInstalledPackageBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var packageBuilder = new VcpkgInstalledPackageBuilder(name);
        configure(packageBuilder);
        _packages.Add(packageBuilder.Build());
        return this;
    }

    public void Apply(FakeRepoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var package in _packages)
        {
            foreach (var binFile in package.BinFiles)
            {
                builder.WithTextFile($"vcpkg_installed/{_triplet}/bin/{binFile.FileName}", binFile.Content);
            }

            foreach (var libFile in package.LibFiles)
            {
                builder.WithTextFile($"vcpkg_installed/{_triplet}/lib/{libFile.FileName}", libFile.Content);
            }

            foreach (var shareFile in package.ShareFiles)
            {
                builder.WithTextFile($"vcpkg_installed/{_triplet}/share/{package.Name}/{shareFile.RelativePath}", shareFile.Content);
            }

            if (package.OwnedFiles.Count > 0)
            {
                var listContent = string.Join('\n', package.OwnedFiles);
                builder.WithTextFile($"vcpkg_installed/{_triplet}/info/{package.Name}_{package.Version}.list", listContent);
            }

            if (!string.IsNullOrWhiteSpace(package.Copyright))
            {
                builder.WithTextFile($"vcpkg_installed/{_triplet}/share/{package.Name}/copyright", package.Copyright);
            }
        }
    }
}

public sealed class VcpkgInstalledPackageBuilder
{
    private readonly List<VcpkgInstalledFileEntry> _binFiles = new();
    private readonly List<VcpkgInstalledFileEntry> _libFiles = new();
    private readonly List<VcpkgInstalledShareEntry> _shareFiles = new();
    private readonly List<string> _ownedFiles = new();

    internal VcpkgInstalledPackageBuilder(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public string Version { get; private set; } = "1.0.0";

    public string? Copyright { get; private set; }

    public VcpkgInstalledPackageBuilder WithVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        Version = version;
        return this;
    }

    public VcpkgInstalledPackageBuilder WithBinFile(string fileName, string content = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _binFiles.Add(new VcpkgInstalledFileEntry(fileName, content));
        return this;
    }

    public VcpkgInstalledPackageBuilder WithLibFile(string fileName, string content = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        _libFiles.Add(new VcpkgInstalledFileEntry(fileName, content));
        return this;
    }

    public VcpkgInstalledPackageBuilder WithShareFile(string relativePath, string content = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        _shareFiles.Add(new VcpkgInstalledShareEntry(relativePath, content));
        return this;
    }

    public VcpkgInstalledPackageBuilder WithOwnedFiles(params string[] ownedFiles)
    {
        ArgumentNullException.ThrowIfNull(ownedFiles);
        _ownedFiles.AddRange(ownedFiles);
        return this;
    }

    public VcpkgInstalledPackageBuilder WithCopyright(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Copyright = content;
        return this;
    }

    internal VcpkgInstalledPackage Build()
    {
        return new VcpkgInstalledPackage(
            Name,
            Version,
            _binFiles,
            _libFiles,
            _shareFiles,
            _ownedFiles,
            Copyright);
    }
}

internal sealed record VcpkgInstalledPackage(
    string Name,
    string Version,
    IReadOnlyList<VcpkgInstalledFileEntry> BinFiles,
    IReadOnlyList<VcpkgInstalledFileEntry> LibFiles,
    IReadOnlyList<VcpkgInstalledShareEntry> ShareFiles,
    IReadOnlyList<string> OwnedFiles,
    string? Copyright);

internal sealed record VcpkgInstalledFileEntry(string FileName, string Content);

internal sealed record VcpkgInstalledShareEntry(string RelativePath, string Content);
