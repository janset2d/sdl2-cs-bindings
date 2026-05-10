using System.Text.Json;
using Build.Results;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Data.ProjectMetadata;

public interface IProjectMetadataReader
{
    /// <summary>
    /// Resolves MSBuild-evaluated properties (<c>TargetFrameworks</c>, <c>Authors</c>,
    /// <c>PackageLicenseFile</c>, <c>PackageIcon</c>) for the supplied csproj. Returns a typed
    /// <see cref="Result{TValue,TError}"/> carrying either the resolved <see cref="EvaluatedProjectMetadata"/>
    /// or a <see cref="ProjectMetadataError"/> describing the MSBuild or parse failure.
    /// </summary>
    Result<EvaluatedProjectMetadata, ProjectMetadataError> Read(FilePath projectPath);
}

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

    /// <inheritdoc />
    public Result<EvaluatedProjectMetadata, ProjectMetadataError> Read(FilePath projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

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
        try
        {
            _cakeContext.DotNetMSBuild(projectPath.FullPath, settings, capturedLines.AddRange);
        }
        catch (CakeException ex)
        {
            var message = $"dotnet msbuild -getProperty failed for '{projectPath.FullPath}': {ex.Message}";
            return Result<EvaluatedProjectMetadata, ProjectMetadataError>.Failure(new ProjectMetadataError(message, projectPath.FullPath, ex));
        }

        if (!TryParseProperties(capturedLines, projectPath, out var properties, out var parseError))
        {
            return Result<EvaluatedProjectMetadata, ProjectMetadataError>.Failure(parseError);
        }

        if (!TryResolveTargetFrameworks(properties, projectPath, out var targetFrameworks, out var tfmError))
        {
            return Result<EvaluatedProjectMetadata, ProjectMetadataError>.Failure(tfmError);
        }

        var authors = GetPropertyOrEmpty(properties, "Authors");
        var licenseFile = GetPropertyOrEmpty(properties, "PackageLicenseFile");
        var icon = GetPropertyOrEmpty(properties, "PackageIcon");

        var metadata = new EvaluatedProjectMetadata(
            TargetFrameworks: targetFrameworks,
            Authors: authors,
            PackageLicenseFile: licenseFile,
            PackageIcon: icon);

        return Result<EvaluatedProjectMetadata, ProjectMetadataError>.Success(metadata);
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
            error = new ProjectMetadataError($"dotnet msbuild -getProperty returned empty output for '{projectPath.FullPath}'.", projectPath.FullPath);
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(joined);
        }
        catch (JsonException ex)
        {
            var message = $"Could not parse dotnet msbuild -getProperty output for '{projectPath.FullPath}': {ex.Message}. Raw output: {joined}";

            properties = [];
            error = new ProjectMetadataError(message, projectPath.FullPath, ex);
            return false;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("Properties", out var propertiesElement) || propertiesElement.ValueKind != JsonValueKind.Object)
            {
                var message = $"dotnet msbuild -getProperty output for '{projectPath.FullPath}' is missing the 'Properties' object. Raw output: {joined}";

                properties = [];
                error = new ProjectMetadataError(message, projectPath.FullPath);
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
