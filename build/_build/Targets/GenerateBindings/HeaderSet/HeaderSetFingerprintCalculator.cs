using System.Security.Cryptography;
using System.Text;
using Build.Host.Cake;
using Cake.Core;
using static System.Convert;

namespace Build.Targets.GenerateBindings.HeaderSet;

internal sealed class HeaderSetFingerprintCalculator(ICakeContext context)
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<HeaderSetFingerprint> ComputeAsync(
        ResolvedHeaderSet headerSet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headerSet);

        var builder = new StringBuilder();
        foreach (var header in headerSet.Headers.OrderBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = headerSet.IncludeRoot.GetRelativePath(header).FullPath.Replace('\\', '/');
            builder.Append(relativePath).Append(Environment.NewLine);

            var content = await _context.ReadAllTextAsync(header).ConfigureAwait(false);
            builder.Append(NormalizeLineEndings(content)).Append(Environment.NewLine);
        }

        var hash = ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
        return new HeaderSetFingerprint($"sha256:{hash}", headerSet.Headers.Count);
    }

    private static string NormalizeLineEndings(string content) =>
        content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
}
