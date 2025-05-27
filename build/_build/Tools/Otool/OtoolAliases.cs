using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;

namespace Build.Tools.Otool;

/// <summary>
/// Contains functionality for working with the otool command line utility on macOS.
/// </summary>
[CakeAliasCategory("Otool")]
public static class OtoolAliases
{
    /// <summary>
    /// Runs otool on a file to determine its shared library dependencies.
    /// </summary>
    /// <param name="context">The cake context.</param>
    /// <param name="filePath">The file to analyze.</param>
    /// <returns>The otool output.</returns>
    /// <example>
    /// <code>
    /// var dependencies = Otool("./bin/myprogram");
    /// Information("Dependencies: {0}", dependencies);
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static string Otool(this ICakeContext context, FilePath filePath)
    {
        return Otool(context, new OtoolSettings(filePath));
    }

    /// <summary>
    /// Runs otool on a file using the specified settings.
    /// </summary>
    /// <param name="context">The cake context.</param>
    /// <param name="settings">The otool settings.</param>
    /// <returns>The otool output.</returns>
    /// <example>
    /// <code>
    /// var settings = new OtoolSettings("./bin/myprogram") { Verbose = true };
    /// var dependencies = Otool(settings);
    /// Information("Dependencies: {0}", dependencies);
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static string Otool(this ICakeContext context, OtoolSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);

        var runner = new OtoolRunner(context);

        return runner.GetOutput(settings);
    }

    /// <summary>
    /// Runs otool -L on a file and returns a dictionary of library names to their resolved paths.
    /// </summary>
    /// <param name="context">The cake context.</param>
    /// <param name="filePath">The file to analyze.</param>
    /// <returns>A dictionary of library names to their resolved paths.</returns>
    /// <example>
    /// <code>
    /// var dependencies = OtoolDependencies("./bin/myprogram");
    /// foreach (var dep in dependencies)
    /// {
    ///     Information("Library: {0} => Path: {1}", dep.Key, dep.Value);
    /// }
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static IReadOnlyDictionary<string, string> OtoolDependencies(this ICakeContext context, FilePath filePath)
    {
        return OtoolDependencies(context, new OtoolSettings(filePath));
    }

    /// <summary>
    /// Runs otool -L on a file and returns a dictionary of library names to their resolved paths using specified settings.
    /// </summary>
    /// <param name="context">The cake context.</param>
    /// <param name="settings">The otool settings.</param>
    /// <returns>A dictionary of library names to their resolved paths.</returns>
    /// <example>
    /// <code>
    /// var settings = new OtoolSettings("./bin/myprogram") { Verbose = true };
    /// var dependencies = OtoolDependencies(settings);
    /// foreach (var dep in dependencies)
    /// {
    ///     Information("Library: {0} => Path: {1}", dep.Key, dep.Value);
    /// }
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static IReadOnlyDictionary<string, string> OtoolDependencies(this ICakeContext context, OtoolSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);

        var runner = new OtoolRunner(context);

        return runner.GetDependenciesAsDictionary(settings);
    }
}
