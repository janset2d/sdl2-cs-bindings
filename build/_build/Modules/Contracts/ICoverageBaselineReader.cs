using Build.Modules.Coverage.Models;
using Cake.Core.IO;

namespace Build.Modules.Contracts;

/// <summary>
/// Reads <c>build/coverage-baseline.json</c> — the ratchet floor committed to the repo.
/// Separated from <see cref="ICoberturaReader"/> so consumers and tests can substitute either
/// side independently.
/// </summary>
public interface ICoverageBaselineReader
{
    CoverageBaseline Parse(string jsonContent);

    CoverageBaseline ParseFile(FilePath path);
}
