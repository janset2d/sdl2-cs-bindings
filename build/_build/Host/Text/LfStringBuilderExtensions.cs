using System.Text;

namespace Build.Host.Text;

/// <summary>
/// Deterministic LF-terminated string-builder helpers. Mirrors
/// <see cref="StringBuilder.AppendLine()"/> shape but always emits <c>'\n'</c>
/// regardless of the host OS (Windows would otherwise emit CRLF via
/// <see cref="Environment.NewLine"/>, breaking byte-identity for generated
/// artifacts and content hashes consumed cross-host).
///
/// Used by the header-set fingerprint calculator for stable SHA-256 input.
/// </summary>
internal static class LfStringBuilderExtensions
{
    public static StringBuilder AppendLf(this StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Append('\n');
    }

    public static StringBuilder AppendLf(this StringBuilder builder, string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Append(value).Append('\n');
    }

    public static StringBuilder AppendLf(this StringBuilder builder, char value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Append(value).Append('\n');
    }
}
