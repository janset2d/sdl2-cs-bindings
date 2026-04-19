using Build.Domain.Coverage.Models;
using Cake.Core.IO;

namespace Build.Infrastructure.Coverage;

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
