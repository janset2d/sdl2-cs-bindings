using Build.Data.BindingGeneration.Models;
using Build.Host.Paths;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record FamilyConfig(
    BindingGenerationConfig Profile,
    string DisplayName,
    string Sdl2CsRelativePath)
{
    public string FamilyId => Profile.FamilyId;
    public string ExpectedNamespace => Profile.ManagedNamespace;
    public string ExpectedRawClassName => Profile.RawClassName;
    public string ClangSharpCompatRelativePath => new DirectoryPath(Profile.GeneratedRoot).Combine("Compat").FullPath;
    public string ClangSharpModernRelativePath => new DirectoryPath(Profile.GeneratedRoot).Combine("Modern").FullPath;
    public bool UsesSdl2Dynapi => Profile.Dynapi is not null;

    public static FamilyConfig FromProfile(BindingGenerationConfig profile, IPathService paths)
    {
        var peer = profile.PeerEvidence;
        return new FamilyConfig(profile, peer.DisplayName, paths.GetRepoRelativePath(paths.Sdl2CsSourceDir.CombineWithFilePath(peer.Sdl2CsFile)));
    }
}
