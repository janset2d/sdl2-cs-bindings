using Build.Modules.Coverage.Models;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

/// <summary>
/// Reads cobertura-format coverage reports. Implementations are expected to extract only
/// the aggregate metrics exposed on the root <c>&lt;coverage&gt;</c> element — per-file
/// detail is intentionally out of scope for the ratchet policy.
/// </summary>
public interface ICoberturaReader
{
    CoverageMetrics Parse(string xmlContent);

    CoverageMetrics ParseFile(FilePath path);
}
