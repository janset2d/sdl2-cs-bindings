using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.PlatformViews;
using CppAst;

namespace Build.Targets.GenerateBindings.Parse;

// Public because GenerateBindingsTask (sibling Cake Frosting Task convention is
// `public sealed class XxxTask`) takes ICppAstParseRunner in its ctor; CS0051
// would block the flip otherwise. Cascade: ResolvedHeaderSet / CppAstParseResult /
// PlatformParseView (+ PlatformConditionKind enum member) / ParseDiagnosticFormatter
// all stay public because they appear in this interface's public signature or in
// the public CppAstParseRunner ctor. Until the Task-visibility convention shifts
// (10 sibling tasks all `public sealed class XxxTask`), the rollback to internal
// described in the Stage 1 plan §P2.6 cannot land for this surface — only types
// off the public signature graph (currently none in this folder) can flip.
public interface ICppAstParseRunner
{
    CppAstParseResult Parse(BindingGenerationConfig config, ResolvedHeaderSet headerSet, PlatformParseView parseView);
}

public sealed class CppAstParseRunner(ParseDiagnosticFormatter diagnosticFormatter) : ICppAstParseRunner
{
    private readonly ParseDiagnosticFormatter _diagnosticFormatter = diagnosticFormatter ?? throw new ArgumentNullException(nameof(diagnosticFormatter));

    /// <summary>
    /// Builds the per-view <see cref="CppParserOptions"/> from manifest config + view
    /// macro state. <see cref="BindingGenerationConfig.ParseDefines"/> +
    /// <see cref="BindingGenerationConfig.ClangArgs"/> supply the family-level baseline;
    /// rationale for each entry lives in
    /// <c>docs/playbook/binding-generator-maintenance.md</c> §"Parse-time configuration
    /// surface (per family)" (paragraph-length context that doesn't fit in JSON).
    /// <see cref="PlatformParseView.Defines"/> + <see cref="PlatformParseView.Undefines"/>
    /// supply the per-view platform macro group; the master undefine-all-platform-macros
    /// hygiene is computed by <see cref="PlatformCatalog"/> when each view is built.
    /// </summary>
    public static CppParserOptions CreateOptions(BindingGenerationConfig config, ResolvedHeaderSet headerSet, PlatformParseView parseView)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(headerSet);
        ArgumentNullException.ThrowIfNull(parseView);

        // TargetSystem = "linux" pins libclang to the actual host target (the
        // binding-generator container runs Linux x86_64). CppAst defaults to "windows"
        // even on a Linux host (CppParserOptions ctor); without override, the libclang
        // triple becomes x86_64-pc-windows- and SDL_stdinc.h activates its _MSC_VER
        // branch at line 357 (`#include <sal.h>`), which fails inside our container.
        //
        // SystemIncludeFolders carries only the vcpkg family header set. Compiler-shipped
        // headers (stddef.h, stdint.h, stdarg.h) and glibc headers (sys/types.h, etc.)
        // are resolved by libclang via the CPATH env var contract in
        // docker/binding-generator.Dockerfile — NuGet libclang.runtime.* does not ship
        // a clang resource directory (verified by package contents; documented by
        // ClangSharp issue #414).
        // SyntheticIncludeRoot must come FIRST so libclang resolves stub stand-ins
        // (process.h, windows.h, etc.) instead of failing-closed on the real Windows/
        // Apple system headers that don't exist inside the Linux container.
        var options = new CppParserOptions
        {
            ParseMacros = true,
            ParserKind = CppParserKind.C,
            TargetSystem = "linux",
            SystemIncludeFolders =
            {
                headerSet.SyntheticIncludeRoot.FullPath,
                headerSet.IncludeRoot.FullPath,
            },
        };

        options.Defines.AddRange(config.ParseDefines);
        options.Defines.AddRange(parseView.Defines);

        options.AdditionalArguments.AddRange(config.ClangArgs);

        foreach (var undefine in parseView.Undefines)
        {
            options.AdditionalArguments.Add($"-U{undefine}");
        }

        return options;
    }

    // Parses every header in the per-family header set in its own translation unit and
    // returns the per-header compilations bundled under the parse view. Each ParseFile
    // call is an isolated libclang TU: a parse failure in one header does not poison
    // the others, and transitive include chains stay narrow per header.
    //
    // This is the peer-validated pattern: amerkoleci/Alimer.Bindings.SDL (CppAst, SDL3)
    // and ppy/SDL3-CS (ClangSharp) both loop per header rather than batching headers
    // into one TU. CppAst's CppParser.ParseFile internally delegates to ParseFiles
    // with a singleton list, so the loop — not the API choice — is what isolates.
    public CppAstParseResult Parse(BindingGenerationConfig config, ResolvedHeaderSet headerSet, PlatformParseView parseView)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(headerSet);
        ArgumentNullException.ThrowIfNull(parseView);

        var options = CreateOptions(config, headerSet, parseView);
        var compilations = new List<CppCompilation>(headerSet.Headers.Count);

        foreach (var header in headerSet.Headers)
        {
            var compilation = CppParser.ParseFile(header.FullPath, options);
            if (compilation.HasErrors)
            {
                throw new InvalidOperationException(_diagnosticFormatter.FormatErrors($"{parseView.Name} [{header.GetFilename()}]", compilation));
            }

            compilations.Add(compilation);
        }

        return new CppAstParseResult(parseView, compilations);
    }
}
