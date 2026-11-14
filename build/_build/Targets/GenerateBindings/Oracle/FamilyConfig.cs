using Build.Data.BindingGeneration.Models;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record FamilyConfig(
    BindingGenerationConfig Profile,
    string DisplayName,
    string Sdl2CsRelativePath)
{
    private static readonly Dictionary<string, (string DisplayName, string PeerFile)> PeerEvidence =
        new(StringComparer.Ordinal)
        {
            ["sdl2-core"] = ("SDL2 Core", "SDL2.cs"),
            ["sdl2-image"] = ("SDL2 Image", "SDL2_image.cs"),
            ["sdl2-ttf"] = ("SDL2 TTF", "SDL2_ttf.cs"),
            ["sdl2-mixer"] = ("SDL2 Mixer", "SDL2_mixer.cs"),
            ["sdl2-gfx"] = ("SDL2 GFX", "SDL2_gfx.cs")
        };

    public string FamilyId => Profile.FamilyId;
    public string ExpectedNamespace => Profile.ManagedNamespace;
    public string ExpectedRawClassName => Profile.RawClassName;
    public string ClangSharpCompatRelativePath => new DirectoryPath(Profile.GeneratedRoot).Combine("Compat").FullPath;
    public string ClangSharpModernRelativePath => new DirectoryPath(Profile.GeneratedRoot).Combine("Modern").FullPath;
    public bool UsesSdl2Dynapi => Profile.Dynapi is not null;

    public static FamilyConfig FromProfile(BindingGenerationConfig profile)
    {
        var peer = PeerEvidence[profile.FamilyId];
        return new FamilyConfig(profile, peer.DisplayName, "external/sdl2-cs/src/" + peer.PeerFile);
    }
}
