using System.Text;
using CppAst;

namespace Janset.Spike.SDL2.Platform.Generator;

internal sealed record PlatformSpec(
    string Name,
    string? SupportedOsPlatform,
    string TargetSystem,
    string[] Defines,
    string[] Undefines);

internal sealed record GeneratedFunction(
    string Name,
    string ReturnType,
    string Parameters,
    string SourceHeader,
    string Platform);

internal sealed record PassReport(
    string Platform,
    string[] Defines,
    string[] Undefines,
    string[] EmittedFunctions,
    string[] ExcludedAsNeutral,
    string[] DeferredConcerns);

internal static class Program
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string VcpkgIncludeRoot = Path.Combine(RepoRoot, "vcpkg_installed", "x64-windows-hybrid", "include");
    private static readonly string SDL2IncludeDir = Path.Combine(VcpkgIncludeRoot, "SDL2");
    private static readonly string OutputDir = Path.Combine(RepoRoot, "tools", "binding-spike", "cppast-platform", "bindings", "Generated");
    private static readonly string StubIncludeDir = Path.Combine(RepoRoot, "tools", "binding-spike", "cppast-platform", "generator", "include");

    private static readonly string[] HeaderFiles =
    [
        "SDL_system.h",
        "SDL_main.h"
    ];

    private static readonly PlatformSpec Neutral = new(
        "Neutral",
        null,
        "windows",
        [],
        ["_WIN32", "WIN32", "__WIN32__", "__WINDOWS__", "linux", "__linux", "__linux__", "__LINUX__", "__APPLE__", "__MACOSX__"]);

    private static readonly PlatformSpec[] PlatformPasses =
    [
        new("Windows", "windows", "windows", ["_WIN32=1", "WIN32=1", "__WIN32__=1", "__WINDOWS__=1"], ["linux", "__linux", "__linux__", "__LINUX__", "__APPLE__", "__MACOSX__"]),
        new("Linux", "linux", "linux", ["linux=1", "__linux=1", "__linux__=1", "__LINUX__=1"], ["_WIN32", "WIN32", "__WIN32__", "__WINDOWS__", "__APPLE__", "__MACOSX__"]),
        new("OSX", "macos", "macos", ["__MACOSX__=1"], ["_WIN32", "WIN32", "__WIN32__", "__WINDOWS__", "linux", "__linux", "__linux__", "__LINUX__", "__APPLE__"])
    ];

    public static async Task<int> Main()
    {
        var reports = new List<PassReport>();

        var neutralCompilation = Parse(Neutral);
        ThrowIfParseFailed(Neutral.Name, neutralCompilation);
        var neutralFunctions = ExtractFunctions(neutralCompilation, Neutral.Name);
        var neutralNames = neutralFunctions.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);

        await EmitFileAsync("SDL2.Platform.Neutral.g.cs", Neutral.Name, null, neutralFunctions, emitLibName: true).ConfigureAwait(false);
        reports.Add(new PassReport(
            Neutral.Name,
            Neutral.Defines,
            Neutral.Undefines,
            neutralFunctions.Select(f => f.Name).ToArray(),
            [],
            ["SDL_syswm.h platform-specific struct/union layout is intentionally deferred from this function-only spike."]));

        foreach (var platform in PlatformPasses)
        {
            var compilation = Parse(platform);
            ThrowIfParseFailed(platform.Name, compilation);
            var allFunctions = ExtractFunctions(compilation, platform.Name);
            var emittedFunctions = ExcludeNeutralSymbols(allFunctions, neutralNames);
            var excluded = allFunctions
                .Select(f => f.Name)
                .Where(neutralNames.Contains)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            await EmitFileAsync($"SDL2.Platform.{platform.Name}.g.cs", platform.Name, platform.SupportedOsPlatform, emittedFunctions, emitLibName: false).ConfigureAwait(false);
            reports.Add(new PassReport(
                platform.Name,
                platform.Defines,
                platform.Undefines,
                emittedFunctions.Select(f => f.Name).ToArray(),
                excluded,
                []));
        }

        await EmitReportAsync(reports).ConfigureAwait(false);
        Console.WriteLine($"Generated platform-pass spike output under {OutputDir}");
        return 0;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "Janset.SDL2.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repository root containing Janset.SDL2.sln.");
    }

    private static CppCompilation Parse(PlatformSpec spec)
    {
        var options = new CppParserOptions
        {
            ParseMacros = false,
            TargetSystem = spec.TargetSystem,
            SystemIncludeFolders = { StubIncludeDir, VcpkgIncludeRoot },
            Defines = { "SDL_DECLSPEC=", "__PRFCHWINTRIN_H=1" }
        };

        foreach (var define in spec.Defines)
        {
            options.Defines.Add(define);
        }

        foreach (var undefine in spec.Undefines)
        {
            options.AdditionalArguments.Add($"-U{undefine}");
        }

        var headerPaths = HeaderFiles.Select(h => Path.Combine(SDL2IncludeDir, h)).ToList();
        return CppParser.ParseFiles(headerPaths, options);
    }

    private static void ThrowIfParseFailed(string passName, CppCompilation compilation)
    {
        if (!compilation.HasErrors)
        {
            return;
        }

        var messages = compilation.Diagnostics.Messages
            .Where(m => m.Type == CppLogMessageType.Error)
            .Select(m => m.ToString())
            .ToArray();

        throw new InvalidOperationException($"CppAst parse failed for {passName}:{Environment.NewLine}{string.Join(Environment.NewLine, messages)}");
    }

    private static bool IsTargetHeader(CppElement element)
    {
        if (element.SourceFile is null)
        {
            return false;
        }

        var normalized = element.SourceFile.Replace('\\', '/');
        return HeaderFiles.Any(h => normalized.EndsWith("/" + h, StringComparison.OrdinalIgnoreCase));
    }

    private static List<GeneratedFunction> ExtractFunctions(CppCompilation compilation, string platform)
    {
        return compilation.Functions
            .Where(f => IsTargetHeader(f) && !string.IsNullOrWhiteSpace(f.Name))
            .Select(f => new GeneratedFunction(
                f.Name,
                MapType(f.ReturnType),
                string.Join(", ", f.Parameters.Select(p => $"{MapType(p.Type)} {SafeIdentifier(p.Name)}")),
                Path.GetFileName(f.SourceFile ?? string.Empty),
                platform))
            .OrderBy(f => f.SourceHeader, StringComparer.Ordinal)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<GeneratedFunction> ExcludeNeutralSymbols(
        List<GeneratedFunction> platformFunctions,
        HashSet<string> neutralNames)
    {
        return platformFunctions
            .Where(f => !neutralNames.Contains(f.Name))
            .ToList();
    }

    private static async Task EmitFileAsync(
        string fileName,
        string platform,
        string? supportedOsPlatform,
        List<GeneratedFunction> functions,
        bool emitLibName)
    {
        Directory.CreateDirectory(OutputDir);
        var path = Path.Combine(OutputDir, fileName);
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("// CppAst platform-pass spike output.");
        builder.Append("// Pass: ").AppendLine(platform);
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Runtime.InteropServices;");
        if (supportedOsPlatform is not null)
        {
            builder.AppendLine("using System.Runtime.Versioning;");
        }

        builder.AppendLine();
        builder.AppendLine("namespace Janset.Spike.SDL2.Platform;");
        builder.AppendLine();
        builder.AppendLine("internal static unsafe partial class SDL2Platform");
        builder.AppendLine("{");
        if (emitLibName)
        {
            builder.AppendLine("    private const string LibName = \"SDL2\";");
        }

        foreach (var function in functions)
        {
            builder.AppendLine();
            if (supportedOsPlatform is not null)
            {
                builder.Append("    [SupportedOSPlatform(\"").Append(supportedOsPlatform).AppendLine("\")]");
            }

            builder.AppendLine("    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]");
            builder.AppendLine("    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
            builder
                .Append("    internal static extern ")
                .Append(function.ReturnType)
                .Append(' ')
                .Append(function.Name)
                .Append('(')
                .Append(function.Parameters)
                .AppendLine(");");
        }

        builder.AppendLine("}");
        await File.WriteAllTextAsync(path, builder.ToString()).ConfigureAwait(false);
    }

    private static async Task EmitReportAsync(List<PassReport> reports)
    {
        Directory.CreateDirectory(OutputDir);
        var path = Path.Combine(OutputDir, "platform-pass-report.json");
        await File.WriteAllTextAsync(path, BuildReportJson(reports)).ConfigureAwait(false);
    }

    private static string BuildReportJson(List<PassReport> reports)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[");
        for (var i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            builder.AppendLine("  {");
            AppendJsonProperty(builder, "Platform", report.Platform, trailingComma: true, indent: "    ");
            AppendJsonArrayProperty(builder, "Defines", report.Defines, trailingComma: true, indent: "    ");
            AppendJsonArrayProperty(builder, "Undefines", report.Undefines, trailingComma: true, indent: "    ");
            AppendJsonArrayProperty(builder, "EmittedFunctions", report.EmittedFunctions, trailingComma: true, indent: "    ");
            AppendJsonArrayProperty(builder, "ExcludedAsNeutral", report.ExcludedAsNeutral, trailingComma: true, indent: "    ");
            AppendJsonArrayProperty(builder, "DeferredConcerns", report.DeferredConcerns, trailingComma: false, indent: "    ");
            builder.Append("  }");
            if (i < reports.Count - 1)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        builder.AppendLine("]");
        return builder.ToString();
    }

    private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool trailingComma, string indent)
    {
        builder
            .Append(indent)
            .Append('"')
            .Append(name)
            .Append("\": \"")
            .Append(EscapeJson(value))
            .Append('"');
        if (trailingComma)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    private static void AppendJsonArrayProperty(StringBuilder builder, string name, string[] values, bool trailingComma, string indent)
    {
        builder
            .Append(indent)
            .Append('"')
            .Append(name)
            .AppendLine("\": [");

        for (var i = 0; i < values.Length; i++)
        {
            builder
                .Append(indent)
                .Append("  \"")
                .Append(EscapeJson(values[i]))
                .Append('"');
            if (i < values.Length - 1)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        builder.Append(indent).Append(']');
        if (trailingComma)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    private static string EscapeJson(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string SafeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return "@_";
        return name switch
        {
            "ref" or "out" or "in" or "params" or "object" or "string" or "event" => "@" + name,
            _ => name
        };
    }

    private static string MapType(CppType type)
    {
        while (type is CppQualifiedType qt) type = qt.ElementType;

        return type switch
        {
            CppPrimitiveType prim => MapPrimitive(prim),
            CppPointerType ptr => MapPointer(ptr),
            CppTypedef td => MapTypedef(td),
            CppArrayType arr => MapPointer(new CppPointerType(arr.ElementType)),
            CppEnum => "int",
            CppClass cls => cls.Name,
            _ => "IntPtr"
        };
    }

    private static string MapPrimitive(CppPrimitiveType prim) => prim.Kind switch
    {
        CppPrimitiveKind.Void => "void",
        CppPrimitiveKind.Bool => "byte",
        CppPrimitiveKind.Char => "sbyte",
        CppPrimitiveKind.WChar => "char",
        CppPrimitiveKind.Short => "short",
        CppPrimitiveKind.Int => "int",
        CppPrimitiveKind.LongLong => "long",
        CppPrimitiveKind.UnsignedChar => "byte",
        CppPrimitiveKind.UnsignedShort => "ushort",
        CppPrimitiveKind.UnsignedInt => "uint",
        CppPrimitiveKind.UnsignedLongLong => "ulong",
        CppPrimitiveKind.Float => "float",
        CppPrimitiveKind.Double => "double",
        CppPrimitiveKind.Long => "int",
        CppPrimitiveKind.UnsignedLong => "uint",
        _ => "IntPtr"
    };

    private static string MapPointer(CppPointerType ptr)
    {
        var element = ptr.ElementType;
        while (element is CppQualifiedType qt) element = qt.ElementType;

        return element switch
        {
            CppPrimitiveType { Kind: CppPrimitiveKind.Void } => "IntPtr",
            CppPrimitiveType prim => MapPrimitive(prim) + "*",
            CppTypedef td when td.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
            CppTypedef td => MapTypedefPointer(td),
            CppClass cls when cls.Name.StartsWith("ID", StringComparison.Ordinal) => "IntPtr",
            CppClass cls when cls.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
            CppClass cls => cls.Name + "*",
            _ => "IntPtr"
        };
    }

    private static string MapTypedefPointer(CppTypedef td)
    {
        var element = td.ElementType;
        while (element is CppQualifiedType qt) element = qt.ElementType;

        if (element is CppPrimitiveType prim) return MapPrimitive(prim) + "*";
        if (element is CppTypedef nested) return MapTypedefPointer(nested);
        return td.Name + "*";
    }

    private static string MapTypedef(CppTypedef td) => td.Name switch
    {
        "Sint8" => "sbyte",
        "Uint8" => "byte",
        "Sint16" => "short",
        "Uint16" => "ushort",
        "Sint32" => "int",
        "Uint32" => "uint",
        "Sint64" => "long",
        "Uint64" => "ulong",
        "SDL_bool" => "byte",
        "size_t" => "nuint",
        "ptrdiff_t" => "nint",
        _ when td.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
        _ => MapType(td.ElementType)
    };
}
