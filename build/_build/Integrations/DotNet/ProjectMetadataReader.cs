using System.Text.Json;
using Build.Shared.Packaging;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Integrations.DotNet;

public sealed class ProjectMetadataReader(ICakeContext cakeContext, ICakeLog log) : IProjectMetadataReader
{
    private static readonly string[] QueriedProperties =
    [
        "TargetFrameworks",
        "TargetFramework",
        "Authors",
        "PackageLicenseFile",
        "PackageIcon",
    ];

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public Task<ProjectMetadataResult> ReadAsync(FilePath projectPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        cancellationToken.ThrowIfCancellationRequested();

        var settings = new DotNetMSBuildSettings
        {
            NoLogo = true,
        };

        foreach (var propertyName in QueriedProperties)
        {
            settings.GetProperties.Add(propertyName);
        }

        _log.Verbose("Resolving project metadata for '{0}' via dotnet msbuild -getProperty.", projectPath.FullPath);

        var capturedLines = new List<string>();
        _cakeContext.DotNetMSBuild(
            projectPath.FullPath,
            settings,
            lines => capturedLines.AddRange(lines));

        if (!TryParseProperties(capturedLines, projectPath, out var properties, out var parseError))
        {
            return Task.FromResult<ProjectMetadataResult>(parseError);
        }

        if (!TryResolveTargetFrameworks(properties, projectPath, out var targetFrameworks, out var tfmError))
        {
            return Task.FromResult<ProjectMetadataResult>(tfmError);
        }

        var authors = GetPropertyOrEmpty(properties, "Authors");
        var licenseFile = GetPropertyOrEmpty(properties, "PackageLicenseFile");
        var icon = GetPropertyOrEmpty(properties, "PackageIcon");

        var metadata = new ProjectMetadata(
            TargetFrameworks: targetFrameworks,
            Authors: authors,
            PackageLicenseFile: licenseFile,
            PackageIcon: icon);

        return Task.FromResult<ProjectMetadataResult>(metadata);
    }

    private static bool TryParseProperties(
        IReadOnlyList<string> standardOutputLines,
        FilePath projectPath,
        out Dictionary<string, string> properties,
        out ProjectMetadataError error)
    {
        // With multiple GetProperties entries, dotnet msbuild emits a single JSON document
        // across one or more stdout lines: { "Properties": { "<name>": "<value>", ... } }.
        var joined = string.Concat(standardOutputLines).Trim();
        if (string.IsNullOrWhiteSpace(joined))
        {
            properties = [];
            error = new ProjectMetadataError(
                $"dotnet msbuild -getProperty returned empty output for '{projectPath.FullPath}'.",
                projectPath.FullPath);
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(joined);
        }
        catch (JsonException ex)
        {
            properties = [];
            error = new ProjectMetadataError(
                $"Could not parse dotnet msbuild -getProperty output for '{projectPath.FullPath}': {ex.Message}. Raw output: {joined}",
                projectPath.FullPath,
                ex);
            return false;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("Properties", out var propertiesElement) ||
                propertiesElement.ValueKind != JsonValueKind.Object)
            {
                properties = [];
                error = new ProjectMetadataError(
                    $"dotnet msbuild -getProperty output for '{projectPath.FullPath}' is missing the 'Properties' object. Raw output: {joined}",
                    projectPath.FullPath);
                return false;
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in propertiesElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : string.Empty;
            }

            properties = result;
            error = null!;
            return true;
        }
    }

    private static bool TryResolveTargetFrameworks(
        IReadOnlyDictionary<string, string> properties,
        FilePath projectPath,
        out string[] targetFrameworks,
        out ProjectMetadataError error)
    {
        var multi = GetPropertyOrEmpty(properties, "TargetFrameworks");
        if (!string.IsNullOrWhiteSpace(multi))
        {
            targetFrameworks = multi.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            error = null!;
            return true;
        }

        var single = GetPropertyOrEmpty(properties, "TargetFramework");
        if (!string.IsNullOrWhiteSpace(single))
        {
            targetFrameworks = [single];
            error = null!;
            return true;
        }

        targetFrameworks = [];
        error = new ProjectMetadataError(
            $"Could not resolve target frameworks for '{projectPath.FullPath}'. Neither TargetFrameworks nor TargetFramework were set.",
            projectPath.FullPath);
        return false;
    }

    private static string GetPropertyOrEmpty(IReadOnlyDictionary<string, string> properties, string name)
    {
        return properties.TryGetValue(name, out var value) ? value : string.Empty;
    }
}
