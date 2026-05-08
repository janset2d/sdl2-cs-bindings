using Build.Versioning;

namespace Build.Features.Publishing;

public sealed record PublishRequest(
    string FeedUrl,
    string AuthToken,
    PackageFamilyVersionSet Versions);
