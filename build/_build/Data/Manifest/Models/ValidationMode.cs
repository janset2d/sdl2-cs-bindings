namespace Build.Data.Manifest.Models;

/// <summary>
/// Controls how dependency policy violations are handled during harvest.
/// Driven by <c>manifest.json packaging_config.validation_mode</c>.
/// </summary>
public enum ValidationMode
{
    /// <summary>Validation is disabled. All closures pass regardless of content.</summary>
    Off,

    /// <summary>Violations are reported but do not block the build (warnings).</summary>
    Warn,

    /// <summary>Violations cause validation failure (errors).</summary>
    Strict,
}
