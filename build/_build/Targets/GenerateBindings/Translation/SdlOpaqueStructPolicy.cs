using System.Collections.Frozen;

namespace Build.Targets.GenerateBindings.Translation;

internal static class SdlOpaqueStructPolicy
{
    private static readonly FrozenSet<string> OpaqueStructNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "SDL_RWops",
        }.ToFrozenSet(StringComparer.Ordinal);

    public static bool IsOpaqueStruct(string name) =>
        OpaqueStructNames.Contains(name);
}
