using System.Reflection;
using Assembly = System.Reflection.Assembly;

namespace Build.Tests.Fixtures;

internal static class FixtureLoader
{
    public static string Load(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var assembly = Assembly.GetExecutingAssembly();
        var assemblyName = assembly.GetName().Name
            ?? throw new InvalidOperationException("Assembly name not found.");

        var resourceName = assemblyName + ".Fixtures.Data." + relativePath.Replace('/', '.').Replace('\\', '.');

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                "Embedded resource '" + resourceName + "' not found. " +
                "Ensure the file is included as <EmbeddedResource> in the csproj.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
