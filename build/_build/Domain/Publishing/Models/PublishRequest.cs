using NuGet.Versioning;

namespace Build.Domain.Publishing.Models;

public sealed record PublishRequest(
    string FeedUrl,
    string AuthToken,
    IReadOnlyDictionary<string, NuGetVersion> Versions);
