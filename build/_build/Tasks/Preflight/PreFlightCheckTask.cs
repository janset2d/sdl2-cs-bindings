/*
 * SPDX-FileCopyrightText: 2025 James Williamson <james@semick.dev>
 * SPDX-License-Identifier: MIT
 */

using Build.Application.Preflight;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Preflight;

/// <summary>
/// Pre-flight validation task that checks version consistency between manifest.json and vcpkg.json,
/// and validates strategy coherence for runtime entries in manifest.json.
/// This task ensures that the intended native library versions in manifest.json match
/// the actual vcpkg overrides before starting any build operations.
/// </summary>
[TaskName("PreFlightCheck")]
[TaskDescription("Validates manifest-vcpkg version consistency and runtime strategy coherence (partial gate)")]
public sealed class PreFlightCheckTask : FrostingTask<BuildContext>
{
    private readonly PreflightTaskRunner _preflightTaskRunner;

    public PreFlightCheckTask(PreflightTaskRunner preflightTaskRunner)
    {
        _preflightTaskRunner = preflightTaskRunner ?? throw new ArgumentNullException(nameof(preflightTaskRunner));
    }

    public override void Run(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _preflightTaskRunner.Run(context);
    }
}
