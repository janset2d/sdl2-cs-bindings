/*
 * SPDX-FileCopyrightText: 2025 James Williamson <james@semick.dev>
 * SPDX-License-Identifier: MIT
 */

using Build.Host;
using Build.Host.Configuration;
using Cake.Frosting;

namespace Build.Features.Preflight;

/// <summary>
/// Pre-flight validation task that checks version consistency between manifest.json and vcpkg.json,
/// and validates strategy coherence for runtime entries in manifest.json.
/// This task ensures that the intended native library versions in manifest.json match
/// the actual vcpkg overrides before starting any build operations.
/// </summary>
[TaskName("PreFlightCheck")]
[TaskDescription("Validates manifest-vcpkg version consistency and runtime strategy coherence (partial gate)")]
public sealed class PreFlightCheckTask(
    PreflightTaskRunner preflightTaskRunner,
    PackageBuildConfiguration packageBuildConfiguration) : AsyncFrostingTask<BuildContext>
{
    private readonly PreflightTaskRunner _preflightTaskRunner = preflightTaskRunner ?? throw new ArgumentNullException(nameof(preflightTaskRunner));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new PreflightRequest(_packageBuildConfiguration.ExplicitVersions);
        return _preflightTaskRunner.RunAsync(context, request);
    }
}
