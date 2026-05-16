using System.Text.RegularExpressions;
using Cake.Core;
using Cake.Core.Diagnostics;
using ClangSharp.Interop;

namespace Build.Targets.GenerateBindings.Parsing;

public interface ILibclangVersionAsserter
{
    void Assert();
}

/// <summary>
/// Resolves the libclang runtime version via ClangSharp.Interop and fails closed
/// when the major.minor does not match the CppAst trio pin (ADR-004). Hosted as
/// a collaborator so scenario tests can mock the version probe — invoking
/// <c>clang.getClangVersion</c> requires libclang native binaries, which the
/// test host (Windows) does not carry; the runtime container does.
/// </summary>
public sealed partial class LibclangVersionAsserter(ICakeLog log) : ILibclangVersionAsserter
{
    // Word-bounded regex — a future libclang 20.10.x release would substring-match
    // "20.1" but not "20\.1\.\d+\b". The trailing build component is required; a
    // bare "20.1" with no third segment is not a version we ship against.
    [GeneratedRegex(@"\b20\.1\.\d+\b", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExpectedVersionPattern();

    private const string ExpectedVersionHumanReadable = "20.1.x";

    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public void Assert()
    {
        var resolved = ResolveVersion();
        _log.Information("libclang resolved version: {0}", resolved);

        if (!ExpectedVersionPattern().IsMatch(resolved))
        {
            throw new CakeException(
                $"libclang version mismatch: expected {ExpectedVersionHumanReadable} (CppAst 0.24.0 trio), " +
                $"resolved '{resolved}'. Check Directory.Packages.props + packages.lock.json.");
        }
    }

    private static unsafe string ResolveVersion()
    {
        var cxString = clang.getClangVersion();
        return cxString.ToString() ?? string.Empty;
    }
}
