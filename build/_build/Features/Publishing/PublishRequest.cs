using NuGet.Versioning;

namespace Build.Features.Publishing;

public sealed record PublishRequest(
    string FeedUrl,
    string AuthToken,
    IReadOnlyDictionary<string, NuGetVersion> Versions);
