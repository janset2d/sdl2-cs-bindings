using Build.Tests.Fixtures;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding;
using Build.Targets.GenerateBindings.Parse;
using Build.Targets.GenerateBindings.PlatformViews;
using CppAst;

namespace Build.Tests.Fixtures.GenerateBindings;

internal static class SemanticFixtureParser
{
    public static BindingModel TranslateSdl2CoreFixture(
        string fixturePath,
        bool parseMacros = false,
        IReadOnlyList<BindingFunction>? requiredFunctions = null,
        IReadOnlyList<string>? defines = null,
        IReadOnlyList<string>? undefines = null)
    {
        var compilation = ParseFixture(fixturePath, parseMacros, parseAsSdl2Header: true, defines, undefines);
        return new BindingModelBuilder().Build(
            [ParseResult("Neutral", compilation, defines, undefines)],
            BindingGenerationFixture.Sdl2CoreConfig(),
            requiredFunctions ?? []);
    }

    public static CppCompilation ParseFixture(
        string fixturePath,
        bool parseMacros = false,
        bool parseAsSdl2Header = false,
        IReadOnlyList<string>? defines = null,
        IReadOnlyList<string>? undefines = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "janset-semantic-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var headerDirectory = parseAsSdl2Header
            ? Path.Combine(directory, "include", "SDL2")
            : directory;
        Directory.CreateDirectory(headerDirectory);
        var header = Path.Combine(headerDirectory, Path.GetFileName(fixturePath));

        try
        {
            File.WriteAllText(header, FixtureLoader.Load(fixturePath));
            var options = new CppParserOptions
            {
                ParserKind = CppParserKind.C,
                TargetSystem = "linux",
                ParseMacros = parseMacros,
            };
            options.Defines.AddRange(defines ?? []);
            foreach (var undefine in undefines ?? [])
            {
                options.AdditionalArguments.Add($"-U{undefine}");
            }

            var compilation = CppParser.ParseFile(header, options);
            if (compilation.HasErrors)
            {
                throw new InvalidOperationException(compilation.Diagnostics.ToString());
            }

            return compilation;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public static CppAstParseResult ParseResult(
        string viewName,
        CppCompilation compilation,
        IReadOnlyList<string>? defines = null,
        IReadOnlyList<string>? undefines = null)
    {
        return new CppAstParseResult(
            new PlatformParseView(
                Name: viewName,
                Kind: string.Equals(viewName, "Neutral", StringComparison.Ordinal)
                    ? PlatformConditionKind.Neutral
                    : PlatformConditionKind.OperatingSystem,
                SupportedOsPlatform: string.Equals(viewName, "Neutral", StringComparison.Ordinal) ? null : viewName.ToLowerInvariant(),
                Defines: defines ?? [],
                Undefines: undefines ?? []),
            [compilation]);
    }
}
