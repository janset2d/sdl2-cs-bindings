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
    /// Convenience constructor for Stage 1 call sites (translator output, test
    /// fixtures) that produce a function-only model. Phase 3D translator rewrite
    /// populates the new category collections directly; this overload retires
    /// when no call sites depend on the function-only shape.
    /// </summary>
    public BindingModel(IReadOnlyList<BindingParseView> Views)
        : this(Views, [], [], [], [], [])
    {
    }
}
