using Cake.Core.IO;

namespace Build.Domain.Publishing.Models;

/// <summary>
/// Request model for the <c>Publish</c> stage.
/// The record is stable even though publish execution is still stubbed.
/// </summary>
/// <param name="PackagesDir">Directory containing the <c>.nupkg</c> + <c>.snupkg</c> set to
/// push.</param>
/// <param name="FeedUrl">Target feed URL (GitHub Packages staging, nuget.org public, etc.).</param>
/// <param name="AuthToken">Bearer token or API key for the target feed. Supplied via CI secret
/// or environment variable at invocation time; never persisted.</param>
public sealed record PublishRequest(
    DirectoryPath PackagesDir,
    string FeedUrl,
    string AuthToken);
