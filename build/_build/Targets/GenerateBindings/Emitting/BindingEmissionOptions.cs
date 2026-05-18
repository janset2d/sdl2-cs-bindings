using Build.Data.BindingGeneration.Models;

namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record BindingEmissionOptions
{
    public BindingEmissionOptions(string managedNamespace, string primaryClassName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryClassName);

        ManagedNamespace = managedNamespace;
        PrimaryClassName = primaryClassName;
    }

    public string ManagedNamespace { get; }

    public string PrimaryClassName { get; }

    public string RawClassName => PrimaryClassName + "Native";

    public static BindingEmissionOptions FromConfig(BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new BindingEmissionOptions(config.ManagedNamespace, config.PrimaryClassName);
    }
}
