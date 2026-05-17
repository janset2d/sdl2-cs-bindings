namespace Build.Targets.GenerateBindings.Model;

// Public because Build.Validation.BindingGeneration.IBindingFamilyValidator
// takes BindingModel in its method signature, and the validator instances
// are registered as IEnumerable<IBindingFamilyValidator> consumed by the
// public GenerateBindingsTask ctor — CS0051 cascade through the public Task
// convention applies. BindingParseView / BindingFunction / BindingParameter
// follow the same cascade. The cascade-lock retires when the Task-visibility
// convention slice flips all 10 sibling tasks to internal sealed class
// together (out of unified-slice scope).
public sealed record BindingModel(IReadOnlyList<BindingParseView> Views);
