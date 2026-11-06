namespace Build.Targets.Harvest.Models;

/// <summary>
/// Represents the complete plan for deploying artifacts, consisting of a series of actions and metadata.
/// </summary>
/// <param name="Actions">The list of deployment actions to execute.</param>
/// <param name="Statistics">Rich metadata about what's being deployed.</param>
public sealed record DeploymentPlan(IReadOnlyList<DeploymentAction> Actions, DeploymentStatistics Statistics);
