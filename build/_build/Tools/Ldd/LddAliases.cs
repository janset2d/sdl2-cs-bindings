using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.IO;

namespace Build.Tools.Ldd;

/// <summary>
/// Contains functionality for working with the ldd command line utility.
/// </summary>
[CakeAliasCategory("Ldd")]
public static class LddAliases
{
    /// <summary>
    /// Runs ldd on a file to determine its shared library dependencies.
    /// </summary>
    /// <param name="context">The cake context.</param>
    /// <param name="filePath">The file to analyze.</param>
    /// <returns>The ldd output.</returns>
    /// <example>
    /// <code>
    /// var dependencies = Ldd("./bin/myprogram");
    /// Information("Dependencies: {0}", dependencies);
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static string Ldd(this ICakeContext context, FilePath filePath)
    {
        return Ldd(context, new LddSettings(filePath));
    }

    /// <summary>
    /// Runs ldd on a file to determine its shared library dependencies using the specified settings.
    /// </summary>
    /// <param name="context">The cake context.</param>
    /// <param name="settings">The ldd settings.</param>
    /// <returns>The ldd output.</returns>
    /// <example>
    /// <code>
    /// var settings = new LddSettings("./bin/myprogram") { Verbose = true };
    /// var dependencies = Ldd("./bin/myprogram", settings);
    /// Information("Dependencies: {0}", dependencies);
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static string Ldd(this ICakeContext context, LddSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);

        var runner = new LddRunner(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools);

        return runner.GetDependencies(settings);
    }

    /// <summary>
    /// Runs ldd on a file and returns a dictionary of library names to their resolved paths.
    /// </summary>
    /// <param name="context">The cake context.</param>
    /// <param name="filePath">The file to analyze.</param>
    /// <returns>A dictionary of library names to their resolved paths.</returns>
    /// <example>
    /// <code>
    /// var dependencies = LddDependencies("./bin/myprogram");
    /// foreach (var dep in dependencies)
    /// {
    ///     Information("Library: {0} => Path: {1}", dep.Key, dep.Value);
    /// }
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static IReadOnlyDictionary<string, string> LddDependencies(this ICakeContext context, FilePath filePath)
    {
        return LddDependencies(context, new LddSettings(filePath));
    }

    /// <summary>
    /// Runs ldd on a file and returns a dictionary of library names to their resolved paths using specified settings.
    /// </summary>
    /// <param name="context">The cake context.</param>
    /// <param name="settings">The ldd settings.</param>
    /// <returns>A dictionary of library names to their resolved paths.</returns>
    /// <example>
    /// <code>
    /// var settings = new LddSettings("./bin/myprogram") { Verbose = true };
    /// var dependencies = LddDependencies("./bin/myprogram", settings);
    /// foreach (var dep in dependencies)
    /// {
    ///     Information("Library: {0} => Path: {1}", dep.Key, dep.Value);
    /// }
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static IReadOnlyDictionary<string, string> LddDependencies(this ICakeContext context, LddSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);

        var runner = new LddRunner(
            context.FileSystem,
            context.Environment,
            context.ProcessRunner,
            context.Tools);

        return runner.GetDependenciesAsDictionary(settings);
    }
}
