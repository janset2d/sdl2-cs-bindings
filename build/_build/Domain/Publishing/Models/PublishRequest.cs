using Cake.Core.IO;

namespace Build.Domain.Publishing.Models;

/// <summary>
/// ADR-003 §3.2 per-stage request placeholder for the <c>Publish</c> stage. The stage itself
/// is not implemented in this pass — <c>PublishTask</c> and <c>PublishTaskRunner</c> land as
/// stubs in Slice E. The record shape is locked in Slice C so that Phase 2b Stream D-ci can
/// consume the contract without retroactive record-shape churn.
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
