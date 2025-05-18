using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IPathService
{
    DirectoryPath RepoRoot { get; }
    DirectoryPath BuildDir { get; }
    DirectoryPath BuildProjectDir { get; }
    DirectoryPath ArtifactsDir { get; }
    DirectoryPath HarvestOutput { get; }
    DirectoryPath SrcDir { get; }
    DirectoryPath VcpkgRoot { get; }
    DirectoryPath GetVcpkgInstalledDir { get; }
    DirectoryPath GetVcpkgInstalledTripletDir(string triplet);
    DirectoryPath GetVcpkgInstalledBinDir(string triplet);
    DirectoryPath GetVcpkgInstalledLibDir(string triplet);
    DirectoryPath GetVcpkgInstalledShareDir(string triplet);
    DirectoryPath GetVcpkgPackageShareDir(string triplet, string packageName);
    FilePath GetVcpkgPackageCopyrightFile(string triplet, string packageName);
    DirectoryPath GetHarvestStageDir(string libraryName, string rid);
    DirectoryPath GetHarvestStageNativeDir(string libraryName, string rid);
    DirectoryPath GetHarvestStageLicensesDir(string libraryName, string rid);
    FilePath GetHarvestManifestFile(string libraryName, string rid);
    FilePath GetRuntimesFile();
    FilePath GetManifestFile();
    FilePath GetSystemArtifactsFile();
}