internal static class ManifestRequiredSurfaceReader
{
    public static RequiredSurface Read(string repoRoot)
    {
        var manifestPath = Path.Combine(repoRoot, "build", "manifest.json");
        return File.Exists(manifestPath)
            ? ReadJson(File.ReadAllText(manifestPath))
            : new RequiredSurface(SourceStatus.Missing, EmptySet(), EmptySet());
    }

    public static RequiredSurface ReadJson(string text)
    {
        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.TryGetProperty("library_manifests", out var libraryManifests)
            || libraryManifests.ValueKind is not JsonValueKind.Array)
        {
            return new RequiredSurface(SourceStatus.Missing, EmptySet(), EmptySet());
        }

        foreach (var libraryManifest in libraryManifests.EnumerateArray())
        {
            if (!IsSdl2Core(libraryManifest)
                || !libraryManifest.TryGetProperty("binding_generation", out var bindingGeneration))
            {
                continue;
            }

            return new RequiredSurface(
                SourceStatus.Present,
                ReadRequiredNames(bindingGeneration, "required_functions"),
                ReadRequiredNames(bindingGeneration, "required_constants"));
        }

        return new RequiredSurface(SourceStatus.Missing, EmptySet(), EmptySet());
    }

    private static bool IsSdl2Core(JsonElement libraryManifest)
    {
        var nameMatches = TryGetString(libraryManifest, "name", out var name)
            && name.Equals("SDL2", StringComparison.OrdinalIgnoreCase);
        var vcpkgMatches = TryGetString(libraryManifest, "vcpkg_name", out var vcpkgName)
            && vcpkgName.Equals("sdl2", StringComparison.OrdinalIgnoreCase);
        var idMatches = TryGetString(libraryManifest, "id", out var id)
            && id.Equals("sdl2", StringComparison.OrdinalIgnoreCase);
        var coreMatches = (libraryManifest.TryGetProperty("core_lib", out var coreLib) && coreLib.ValueKind is JsonValueKind.True)
            || (TryGetString(libraryManifest, "role", out var role) && role.Equals("core", StringComparison.OrdinalIgnoreCase));

        return (nameMatches || vcpkgMatches || idMatches) && coreMatches;
    }

    private static IReadOnlySet<string> ReadRequiredNames(JsonElement bindingGeneration, string propertyName)
    {
        if (!bindingGeneration.TryGetProperty(propertyName, out var property))
        {
            return EmptySet();
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        if (property.ValueKind is JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind is JsonValueKind.Object && TryGetString(item, "name", out var objectName))
                {
                    names.Add(objectName);
                }
                else if (item.ValueKind is JsonValueKind.String && item.GetString() is { } stringName)
                {
                    names.Add(stringName);
                }
            }
        }
        else if (property.ValueKind is JsonValueKind.Object)
        {
            foreach (var header in property.EnumerateObject())
            {
                if (header.Value.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in header.Value.EnumerateArray())
                {
                    if (item.ValueKind is JsonValueKind.String && item.GetString() is { } name)
                    {
                        names.Add(name);
                    }
                }
            }
        }

        return names;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.GetString() is { } text)
        {
            value = text;
            return true;
        }

        value = "";
        return false;
    }

    private static IReadOnlySet<string> EmptySet()
        => new HashSet<string>(StringComparer.Ordinal);
}
