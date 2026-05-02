/*
 * SPDX-FileCopyrightText: 2025 James Williamson <james@semick.dev>
 * SPDX-License-Identifier: MIT
 */

using Build.Host;
using Cake.Frosting;

namespace Build.Features.Coverage;

/// <summary>
/// Static-floor coverage ratchet: compares the cobertura report against a baseline file
/// committed in the repo. Fails the build if line or branch coverage drops below the floor.
/// </summary>
/// <remarks>
/// <para>The coverage report is expected to come from
/// <c>dotnet test -- --coverage --coverage-output-format cobertura</c>.</para>
/// <para>Default coverage file location: <c>artifacts/test-results/build-tests/coverage.cobertura.xml</c>.
/// Override with <c>--coverage-file=&lt;path&gt;</c>. Baseline always lives at
/// <c>build/coverage-baseline.json</c>.</para>
/// </remarks>
[TaskName("Coverage-Check")]
[TaskDescription("Validates test coverage against the baseline floor in build/coverage-baseline.json (ratchet policy)")]
public sealed class CoverageCheckTask(
    CoverageCheckTaskRunner coverageCheckTaskRunner) : FrostingTask<BuildContext>
{
    private readonly CoverageCheckTaskRunner _coverageCheckTaskRunner = coverageCheckTaskRunner ?? throw new ArgumentNullException(nameof(coverageCheckTaskRunner));

    public override void Run(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _coverageCheckTaskRunner.Run(context);
    }
}
