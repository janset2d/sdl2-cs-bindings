using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.ModelBuilding.SdlPolicy;
using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.Declarations;

internal sealed class BindableDeclarationPolicy
{
    private readonly BindingGenerationConfig _config;
    private readonly KnownUnsupportedDeclarationPolicy _unsupportedPolicy;

    public BindableDeclarationPolicy(
        BindingGenerationConfig config,
        KnownUnsupportedDeclarationPolicy unsupportedPolicy)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _unsupportedPolicy = unsupportedPolicy ?? throw new ArgumentNullException(nameof(unsupportedPolicy));
    }

    public static bool IsSdl2Header(string? sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile))
        {
            return false;
        }

        var normalized = sourceFile.Replace('\\', '/');
        return normalized.Contains("/SDL2/", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsBindableFunction(CppFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);

        return IsSdl2Header(function.SourceFile)
            && !string.IsNullOrWhiteSpace(function.Name)
            && !function.Flags.HasFlag(CppFunctionFlags.Inline)
            && !_config.ExcludedFunctions.Contains(function.Name)
            && !_unsupportedPolicy.IsUnsupported(function, out _);
    }

    public bool IsBindableEnum(CppEnum enumeration)
    {
        ArgumentNullException.ThrowIfNull(enumeration);

        return IsBindableOwnedType(enumeration.SourceFile, enumeration.Name);
    }

    public bool IsBindableOwnedType(CppClass cls)
    {
        ArgumentNullException.ThrowIfNull(cls);

        return IsBindableOwnedType(cls.SourceFile, cls.Name);
    }

    public bool IsBindableOwnedType(CppTypedef typedef)
    {
        ArgumentNullException.ThrowIfNull(typedef);

        return IsBindableOwnedType(typedef.SourceFile, typedef.Name);
    }

    public bool IsBindableStruct(CppClass cls)
    {
        ArgumentNullException.ThrowIfNull(cls);

        return IsSdl2Header(cls.SourceFile)
            && !string.IsNullOrWhiteSpace(cls.Name)
            && cls.IsDefinition
            && cls.Fields.Count > 0
            && (cls.ClassKind is CppClassKind.Struct or CppClassKind.Union)
            && !SdlNativeTypeSubstitutionPolicy.IsSubstitutedValueType(cls.Name)
            && !SdlOpaqueStructPolicy.IsOpaqueStruct(cls.Name)
            && _config.OwnedPrefixes.Any(prefix => cls.Name.StartsWith(prefix, StringComparison.Ordinal))
            && !_unsupportedPolicy.IsUnsupported(cls.Name, out _);
    }

    private bool IsBindableOwnedType(string? sourceFile, string? nativeName)
    {
        if (string.IsNullOrWhiteSpace(nativeName))
        {
            return false;
        }

        return IsSdl2Header(sourceFile)
            && _config.OwnedPrefixes.Any(prefix => nativeName.StartsWith(prefix, StringComparison.Ordinal))
            && !_unsupportedPolicy.IsUnsupported(nativeName, out _);
    }
}
