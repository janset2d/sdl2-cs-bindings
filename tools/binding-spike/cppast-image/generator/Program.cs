using System.Globalization;
using System.Text;
using CppAst;

namespace Janset.Spike.SDL2.Image.Generator;

internal sealed record GeneratedFunction(
    string Name,
    string ReturnType,
    string Parameters,
    string SourceHeader);

internal sealed record GeneratedEnum(
    string Name,
    string[] Members);

internal sealed record ImageReport(
    string[] EmittedFunctions,
    string[] EmittedImageTypes,
    string[] ReusedCoreTypes,
    string[] DeferredDeclarations);

internal static class Program
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string VcpkgIncludeRoot = Path.Combine(RepoRoot, "vcpkg_installed", "x64-windows-hybrid", "include");
    private static readonly string Sdl2IncludeDir = Path.Combine(VcpkgIncludeRoot, "SDL2");
    private static readonly string OutputDir = Path.Combine(RepoRoot, "tools", "binding-spike", "cppast-image", "bindings", "Generated");

    private static readonly Dictionary<string, string> CoreTypeMap = new(StringComparer.Ordinal)
    {
        ["SDL_version"] = "SdlVersion",
        ["SDL_Surface"] = "SdlSurface",
        ["SDL_Texture"] = "SdlTexture",
        ["SDL_Renderer"] = "SdlRenderer",
        ["SDL_RWops"] = "SdlRWops"
    };

    private static readonly Dictionary<string, string> ImageTypeMap = new(StringComparer.Ordinal)
    {
        ["IMG_Animation"] = "ImgAnimation"
    };

    private static readonly Dictionary<string, string> ImageEnumMap = new(StringComparer.Ordinal)
    {
        ["IMG_InitFlags"] = "ImgInitFlags"
    };

    public static async Task<int> Main()
    {
        var compilation = ParseImageHeader();
        ThrowIfParseFailed(compilation);

        var functions = ExtractFunctions(compilation);
        var enums = ExtractEnums(compilation);
        var imageTypes = CollectImageTypes(functions);
        var reusedCoreTypes = CollectReusedCoreTypes(functions);

        await EmitBindingsAsync(functions, enums, imageTypes).ConfigureAwait(false);
        await EmitReportAsync(new ImageReport(
            functions.Select(f => f.Name).ToArray(),
            enums.Select(e => e.Name).Concat(imageTypes).ToArray(),
            reusedCoreTypes,
            ["ImgAnimation is emitted as an opaque image-owned type; field-level IMG_Animation layout remains deferred for the production generator."])).ConfigureAwait(false);

        Console.WriteLine($"Generated SDL2_image shared-type spike output under {OutputDir}");
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

    private static CppCompilation ParseImageHeader()
    {
        var options = new CppParserOptions
        {
            ParseMacros = true,
            TargetSystem = "windows",
            SystemIncludeFolders = { VcpkgIncludeRoot },
            Defines = { "SDL_DECLSPEC=", "__PRFCHWINTRIN_H=1" }
        };

        return CppParser.ParseFiles([Path.Combine(Sdl2IncludeDir, "SDL_image.h")], options);
    }

    private static void ThrowIfParseFailed(CppCompilation compilation)
    {
        if (!compilation.HasErrors)
        {
            return;
        }

        var messages = compilation.Diagnostics.Messages
            .Where(m => m.Type == CppLogMessageType.Error)
            .Select(m => m.ToString())
            .ToArray();

        throw new InvalidOperationException($"CppAst parse failed for SDL_image.h:{Environment.NewLine}{string.Join(Environment.NewLine, messages)}");
    }

    private static bool IsImageHeader(CppElement element)
    {
        if (element.SourceFile is null)
        {
            return false;
        }

        return element.SourceFile.Replace('\\', '/').EndsWith("/SDL_image.h", StringComparison.OrdinalIgnoreCase);
    }

    private static List<GeneratedFunction> ExtractFunctions(CppCompilation compilation)
    {
        return compilation.Functions
            .Where(f => IsImageHeader(f) && !string.IsNullOrWhiteSpace(f.Name))
            .Select(f => new GeneratedFunction(
                f.Name,
                MapType(f.ReturnType),
                string.Join(", ", f.Parameters.Select(p => MapParameter(p))),
                Path.GetFileName(f.SourceFile ?? string.Empty)))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string MapParameter(CppParameter parameter)
    {
        return string.Concat(MapType(parameter.Type), " ", SafeIdentifier(parameter.Name));
    }

    private static List<GeneratedEnum> ExtractEnums(CppCompilation compilation)
    {
        return compilation.Enums
            .Where(e => IsImageHeader(e) && ImageEnumMap.ContainsKey(e.Name))
            .Select(e => new GeneratedEnum(
                ImageEnumMap[e.Name],
                e.Items
                    .Select(item => string.Concat("        ", MapEnumMemberName(item.Name), " = ", item.Value.ToString(CultureInfo.InvariantCulture), ","))
                    .ToArray()))
            .ToList();
    }

    private static string[] CollectImageTypes(List<GeneratedFunction> functions)
    {
        return ImageTypeMap.Values
            .Where(typeName => functions.Any(function =>
                function.ReturnType.Contains(typeName, StringComparison.Ordinal) ||
                function.Parameters.Contains(typeName, StringComparison.Ordinal)))
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] CollectReusedCoreTypes(List<GeneratedFunction> functions)
    {
        return CoreTypeMap
            .Where(pair => functions.Any(function =>
                function.ReturnType.Contains(pair.Value, StringComparison.Ordinal) ||
                function.Parameters.Contains(pair.Value, StringComparison.Ordinal)))
            .Select(pair => pair.Key)
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task EmitBindingsAsync(
        List<GeneratedFunction> functions,
        List<GeneratedEnum> enums,
        string[] imageTypes)
    {
        Directory.CreateDirectory(OutputDir);
        var path = Path.Combine(OutputDir, "SDL2.Image.g.cs");
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("// CppAst SDL2_image shared-type spike output.");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Runtime.InteropServices;");
        builder.AppendLine("using Janset.Spike.SDL2.Core;");
        builder.AppendLine();
        builder.AppendLine("namespace Janset.Spike.SDL2.Image;");
        builder.AppendLine();

        foreach (var imageType in imageTypes)
        {
            builder.Append("public readonly partial struct ").Append(imageType).AppendLine(";");
            builder.AppendLine();
        }

        foreach (var generatedEnum in enums)
        {
            builder.AppendLine("[Flags]");
            builder.Append("public enum ").Append(generatedEnum.Name).AppendLine(" : int");
            builder.AppendLine("{");
            foreach (var member in generatedEnum.Members)
            {
                builder.AppendLine(member);
            }

            builder.AppendLine("}");
            builder.AppendLine();
        }

        builder.AppendLine("internal static unsafe partial class Sdl2Image");
        builder.AppendLine("{");
        builder.AppendLine("    private const string LibName = \"SDL2_image\";");

        foreach (var function in functions)
        {
            builder.AppendLine();
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

    private static async Task EmitReportAsync(ImageReport report)
    {
        Directory.CreateDirectory(OutputDir);
        var path = Path.Combine(OutputDir, "image-shared-type-report.json");
        await File.WriteAllTextAsync(path, BuildReportJson(report)).ConfigureAwait(false);
    }

    private static string BuildReportJson(ImageReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        AppendJsonArrayProperty(builder, "EmittedFunctions", report.EmittedFunctions, trailingComma: true, indent: "  ");
        AppendJsonArrayProperty(builder, "EmittedImageTypes", report.EmittedImageTypes, trailingComma: true, indent: "  ");
        AppendJsonArrayProperty(builder, "ReusedCoreTypes", report.ReusedCoreTypes, trailingComma: true, indent: "  ");
        AppendJsonArrayProperty(builder, "DeferredDeclarations", report.DeferredDeclarations, trailingComma: false, indent: "  ");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendJsonArrayProperty(StringBuilder builder, string name, string[] values, bool trailingComma, string indent)
    {
        builder.Append(indent).Append('"').Append(name).AppendLine("\": [");
        for (var i = 0; i < values.Length; i++)
        {
            builder.Append(indent).Append("  \"").Append(EscapeJson(values[i])).Append('"');
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
        if (string.IsNullOrEmpty(name))
        {
            return "@_";
        }

        return name switch
        {
            "ref" or "out" or "in" or "params" or "object" or "string" or "event" => "@" + name,
            _ => name
        };
    }

    private static string MapEnumMemberName(string name)
    {
        return name switch
        {
            "IMG_INIT_JPG" => "Jpg",
            "IMG_INIT_PNG" => "Png",
            "IMG_INIT_TIF" => "Tif",
            "IMG_INIT_WEBP" => "Webp",
            "IMG_INIT_JXL" => "Jxl",
            "IMG_INIT_AVIF" => "Avif",
            _ => name
        };
    }

    private static string MapType(CppType type)
    {
        while (type is CppQualifiedType qualified)
        {
            type = qualified.ElementType;
        }

        return type switch
        {
            CppPrimitiveType primitive => MapPrimitive(primitive),
            CppPointerType pointer => MapPointer(pointer),
            CppTypedef typedef => MapTypedef(typedef),
            CppArrayType array => MapPointer(new CppPointerType(array.ElementType)),
            CppEnum cppEnum => MapEnum(cppEnum),
            CppClass cppClass => MapNamedType(cppClass.Name),
            _ => "IntPtr"
        };
    }

    private static string MapPrimitive(CppPrimitiveType primitive) => primitive.Kind switch
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

    private static string MapPointer(CppPointerType pointer)
    {
        var element = pointer.ElementType;
        while (element is CppQualifiedType qualified)
        {
            element = qualified.ElementType;
        }

        return element switch
        {
            CppPrimitiveType { Kind: CppPrimitiveKind.Void } => "IntPtr",
            CppPrimitiveType primitive => string.Concat(MapPrimitive(primitive), "*"),
            CppPointerType => "IntPtr",
            CppTypedef typedef => string.Concat(MapTypedef(typedef), "*"),
            CppClass cppClass => string.Concat(MapNamedType(cppClass.Name), "*"),
            _ => "IntPtr"
        };
    }

    private static string MapTypedef(CppTypedef typedef)
    {
        if (CoreTypeMap.TryGetValue(typedef.Name, out var coreType))
        {
            return coreType;
        }

        if (ImageTypeMap.TryGetValue(typedef.Name, out var imageType))
        {
            return imageType;
        }

        return typedef.Name switch
        {
            "Sint8" => "sbyte",
            "Uint8" => "byte",
            "Sint16" => "short",
            "Uint16" => "ushort",
            "Sint32" => "int",
            "Uint32" => "uint",
            "Sint64" => "long",
            "Uint64" => "ulong",
            "size_t" => "nuint",
            "ptrdiff_t" => "nint",
            _ => MapType(typedef.ElementType)
        };
    }

    private static string MapEnum(CppEnum cppEnum)
    {
        return ImageEnumMap.TryGetValue(cppEnum.Name, out var imageEnum)
            ? imageEnum
            : "int";
    }

    private static string MapNamedType(string name)
    {
        if (CoreTypeMap.TryGetValue(name, out var coreType))
        {
            return coreType;
        }

        return ImageTypeMap.TryGetValue(name, out var imageType) ? imageType : name;
    }
}
