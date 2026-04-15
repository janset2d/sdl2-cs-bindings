#pragma warning disable MA0045

using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Build.Modules.Contracts;
using Build.Modules.Coverage.Models;
using Cake.Core.IO;

namespace Build.Modules.Coverage;

/// <summary>
/// Parses cobertura XML coverage reports — the format emitted by Microsoft.Testing.Platform's
/// built-in <c>--coverage --coverage-output-format cobertura</c> pipeline. Only the root
/// <c>&lt;coverage&gt;</c> element's aggregate attributes are consumed; per-file detail is
/// intentionally ignored (kept out of scope for the ratchet policy).
/// </summary>
/// <remarks>
/// File access goes through Cake's <see cref="IFileSystem"/> abstraction so the reader is
/// mockable in tests (<c>FakeFileSystem</c>) and plays nicely with Cake's path handling.
/// </remarks>
public sealed class CoberturaReader(IFileSystem fileSystem) : ICoberturaReader
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public CoverageMetrics Parse(string xmlContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlContent);

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlContent);
        }
        catch (XmlException ex)
        {
            throw new ArgumentException($"Invalid cobertura XML: {ex.Message}", nameof(xmlContent), ex);
        }

        var root = doc.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "coverage", StringComparison.Ordinal))
        {
            throw new ArgumentException("Cobertura XML must have <coverage> as the root element.", nameof(xmlContent));
        }

        return new CoverageMetrics
        {
            LineRate = RequireDoubleAttribute(root, "line-rate"),
            BranchRate = RequireDoubleAttribute(root, "branch-rate"),
            LinesCovered = RequireIntAttribute(root, "lines-covered"),
            LinesValid = RequireIntAttribute(root, "lines-valid"),
            BranchesCovered = RequireIntAttribute(root, "branches-covered"),
            BranchesValid = RequireIntAttribute(root, "branches-valid"),
        };
    }

    public CoverageMetrics ParseFile(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var file = _fileSystem.GetFile(path);
        using var stream = file.OpenRead();
        using var reader = new StreamReader(stream);
        var xml = reader.ReadToEnd();

        return Parse(xml);
    }

    private static double RequireDoubleAttribute(XElement element, string name)
    {
        var attr = element.Attribute(name)
            ?? throw new ArgumentException($"Cobertura root element is missing required attribute '{name}'.", nameof(element));

        if (!double.TryParse(attr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"Cobertura attribute '{name}' has invalid double value '{attr.Value}'.", nameof(element));
        }

        return value;
    }

    private static int RequireIntAttribute(XElement element, string name)
    {
        var attr = element.Attribute(name)
            ?? throw new ArgumentException($"Cobertura root element is missing required attribute '{name}'.", nameof(element));

        if (!int.TryParse(attr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new ArgumentException($"Cobertura attribute '{name}' has invalid int value '{attr.Value}'.", nameof(element));
        }

        return value;
    }
}
