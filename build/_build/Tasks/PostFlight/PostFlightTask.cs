using Build.Context;
using Build.Tasks.Packaging;
using Cake.Frosting;

namespace Build.Tasks.PostFlight;

/// <summary>
/// Umbrella target for post-pipeline integrity checks. Symmetric to
/// <see cref="Build.Tasks.Preflight.PreFlightCheckTask"/> — PreFlight
/// validates repo shape before work begins; PostFlight validates runtime
/// behaviour after build artifacts and packages exist.
///
/// Today's dependency chain wires package-smoke (PackageReference consumer
/// restoration + runtime SDL_Init). Future additions (native-smoke .NET
/// bindings, compile sanity across TFMs) should be added as <see cref="IsDependentOnAttribute"/>
/// entries on this task, not as separate top-level targets.
///
/// See tests/smoke-tests/README.md for the overall section narrative.
/// </summary>
[TaskName("PostFlight")]
[TaskDescription("Runs post-pipeline integrity checks (smoke-tests). Symmetric counterpart to PreFlightCheck.")]
[IsDependentOn(typeof(PackageConsumerSmokeTask))]
public sealed class PostFlightTask : FrostingTask<BuildContext>
{
}
