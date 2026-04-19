namespace Build.Domain.Strategy.Models;

/// <summary>
/// The packaging model that determines how native transitive dependencies are handled.
/// </summary>
public enum PackagingModel
{
    /// <summary>
    /// All transitive dependencies are shipped as separate dynamic libraries.
    /// Legacy model — used when hybrid overlay triplets are not available.
    /// </summary>
    PureDynamic,

    /// <summary>
    /// Transitive dependencies are statically baked into satellite DLLs.
    /// Only the core SDL library remains as an external dynamic dependency.
    /// This is the default and recommended model.
    /// </summary>
    HybridStatic,
}

/// <summary>
/// Controls how dependency policy violations are handled during harvest.
/// </summary>
public enum ValidationMode
{
    /// <summary>Validation is disabled. All closures pass regardless of content.</summary>
    Off,

    /// <summary>Violations are reported but do not block the build. IsValid remains true.</summary>
    Warn,

    /// <summary>Violations cause validation failure. IsValid is false.</summary>
    Strict,
}
