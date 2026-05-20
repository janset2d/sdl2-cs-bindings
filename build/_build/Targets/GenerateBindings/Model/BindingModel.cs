namespace Build.Targets.GenerateBindings.Model;

// Public because Build.Validation.BindingGeneration.IBindingFamilyValidator
// takes BindingModel in its method signature, and the validator instances
// are registered as IEnumerable<IBindingFamilyValidator> consumed by the
// public GenerateBindingsTask ctor — CS0051 cascade through the public Task
// convention applies. BindingParseView / BindingFunction / BindingParameter
// follow the same cascade. The cascade-lock retires when the Task-visibility
// convention slice flips all 10 sibling tasks to internal sealed class
// together (out of unified-slice scope).
//
// Categories outside the per-view Functions split (Structs / Enums / Constants /
// Handles / Callbacks) are flat collections — they have no platform-conditioned
// variants at the SDL2 surface in Stage 1. If a future header bump introduces
// one (e.g. a platform-only enum), the model gains a view dimension on that
// category at that time, not pre-emptively.
public sealed record BindingModel(
    IReadOnlyList<BindingParseView> Views,
    IReadOnlyList<BindingStruct> Structs,
    IReadOnlyList<BindingEnumeration> Enums,
    IReadOnlyList<BindingConstant> Constants,
    IReadOnlyList<BindingHandle> Handles,
    IReadOnlyList<BindingCallback> Callbacks)
{
    /// <summary>
    /// Report produced by <see cref="Build.Targets.GenerateBindings.ModelBuilding.Macros.BindingConstantTranslator"/>
    /// covering all macro candidates seen during the parse phase and the outcome of each.
    /// Defaults to <see cref="MacroConstantReport.Empty"/> so validators and tests that
    /// construct <see cref="BindingModel"/> without a full translation pass compile cleanly.
    /// </summary>
    public MacroConstantReport MacroReport { get; init; } = MacroConstantReport.Empty;

    /// <summary>
    /// Convenience constructor for tests and narrow validators that only need the
    /// per-view function surface. Production translation populates the semantic
    /// category collections directly.
    /// </summary>
    public BindingModel(IReadOnlyList<BindingParseView> Views)
        : this(Views, [], [], [], [], [])
    {
    }
}
