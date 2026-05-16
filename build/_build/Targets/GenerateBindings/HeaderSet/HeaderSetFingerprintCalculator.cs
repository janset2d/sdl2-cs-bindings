using System.Security.Cryptography;
using System.Text;
using Build.Host.Cake;
using Build.Host.Text;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using static System.Convert;

namespace Build.Targets.GenerateBindings.HeaderSet;

internal sealed class HeaderSetFingerprintCalculator(ICakeContext context)
{
    // AppendLf (Host.Text) forces '\n' regardless of host OS so the SHA-256
    // hash is identical for the same logical SDL2 header input set on Windows
    // dev / Linux container / CI runner. Without LF normalization a Windows-host
    // hash would diverge from a Linux-host hash and break the .generated-stamp
    // cross-host reproducibility contract.

    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<HeaderSetFingerprint> ComputeAsync(ResolvedHeaderSet headerSet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(headerSet);

        var builder = new StringBuilder();

        // Synthetic headers ship under build/_build/Targets/GenerateBindings/SyntheticHeaders/
        // and feed libclang's resolver before vcpkg's include root. They are part
        // of the parse input contract; an edit to a stub changes the AST surface
        // even though the SDL2 headers stay byte-identical. The fingerprint must
        // cover them or stale stamps will say "no regen needed" after a stub
        // change. Order by relative path under SyntheticIncludeRoot for stability.
        var syntheticHeaders = _context.GetFiles($"{headerSet.SyntheticIncludeRoot.FullPath}/*.h")
            .OrderBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var syntheticHeader in syntheticHeaders)
        {
            ct.ThrowIfCancellationRequested();
            await AppendHeaderAsync(builder, headerSet.SyntheticIncludeRoot, syntheticHeader, syntheticPrefix: "synthetic/").ConfigureAwait(false);
        }

        foreach (var header in headerSet.Headers.OrderBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            await AppendHeaderAsync(builder, headerSet.IncludeRoot, header, syntheticPrefix: string.Empty).ConfigureAwait(false);
        }

        var hash = ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
        return new HeaderSetFingerprint($"sha256:{hash}", headerSet.Headers.Count);
    }

    private async Task AppendHeaderAsync(StringBuilder builder, DirectoryPath relativeRoot, FilePath header, string syntheticPrefix)
    {
        var relativePath = syntheticPrefix + relativeRoot.GetRelativePath(header).FullPath.Replace('\\', '/');
        builder.Append(relativePath).AppendLf();

        var content = await _context.ReadAllTextAsync(header).ConfigureAwait(false);
        builder.Append(NormalizeLineEndings(content)).AppendLf();
    }

    private static string NormalizeLineEndings(string content) =>
        content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
