namespace Build.Targets.GenerateBindings.Oracle;

internal static class RawAbiChecks
{
    public static IReadOnlyList<RawAbiCheck> Run(FamilyConfig config, CSharpEvidence evidence, RequiredSurface required)
    {
        var checks = new List<RawAbiCheck>();
        var publicRawContainers = evidence.Types
            .Where(type => IsExpectedRawClass(config, type) && IsEffectivelyPublic(type.Accessibility, type.ContainingTypeAccessibilities))
            .Select(type => (type.NamespaceName, type.ContainingTypePath))
            .ToHashSet();

        foreach (var type in evidence.Types.Where(type => IsExpectedRawClass(config, type) && IsEffectivelyPublic(type.Accessibility, type.ContainingTypeAccessibilities)))
        {
            checks.Add(HardBug("raw-abi-public-class", type.Name, "Raw ABI class is public; generated raw extern containers must be internal.", type.SourcePath));
        }

        foreach (var function in evidence.Functions.Where(function =>
            IsRawImport(config, function, publicRawContainers) && IsEffectivelyPublic(function.Accessibility, function.ContainingTypeAccessibilities)))
        {
            checks.Add(HardBug("raw-abi-public-import", function.ManagedName, "Raw native import method is public; generated raw externs must be internal.", function.SourcePath));
        }

        AddRequiredSurfaceChecks(checks, config, evidence, required);

        AddDeferredLayoutChecks(checks, evidence, "SDL_RWops", "deferred-layout-sdl-rwops", includeNestedFields: true);
        AddDeferredLayoutChecks(checks, evidence, "SDL_SysWMinfo", "deferred-layout-sdl-syswminfo", includeNestedFields: false);
        AddDeferredLayoutChecks(checks, evidence, "SDL_SysWMmsg", "deferred-layout-sdl-syswmmsg", includeNestedFields: false);

        AddDuplicateTagTypedefChecks(checks, evidence);

        foreach (var function in evidence.Functions.Where(IsNativeImport))
        {
            if (!IsClongDualDispatchHelper(function)
                && (IsPlatformSensitiveLong(function.ReturnNativeTypeName, function.ReturnType)
                || function.Parameters.Any(parameter => IsPlatformSensitiveLong(parameter.NativeTypeName, parameter.Type)))
               )
            {
                checks.Add(new RawAbiCheck(
                    "platform-sensitive-long",
                    "warning",
                    "Compatibility Risk",
                    function.ManagedName,
                    "C long/unsigned long is mapped to int/uint, which is width-sensitive across supported platforms.",
                    function.SourcePath));
            }

            if (IsPlatformSensitiveWchar(function.ReturnNativeTypeName, function.ReturnType)
                || function.Parameters.Any(parameter => IsPlatformSensitiveWchar(parameter.NativeTypeName, parameter.Type)))
            {
                checks.Add(new RawAbiCheck(
                    "platform-sensitive-wchar",
                    "warning",
                    "Compatibility Risk",
                    function.ManagedName,
                    "wchar_t is mapped to ushort*, which is platform-sensitive outside Windows.",
                    function.SourcePath));
            }
        }

        foreach (var drift in evidence.Types
            .Select(type => (type.NamespaceName, type.SourcePath))
            .Concat(evidence.Functions.Select(function => (function.NamespaceName, function.SourcePath)))
            .Concat(evidence.Constants.Select(constant => (constant.NamespaceName, constant.SourcePath)))
            .Where(item => item.NamespaceName.Length > 0 && item.NamespaceName != config.ExpectedNamespace)
            .Distinct()
            .OrderBy(item => item.NamespaceName, StringComparer.Ordinal))
        {
            checks.Add(HardBug("family-namespace-drift", drift.NamespaceName,
                $"Generated namespace should be {config.ExpectedNamespace} for {config.FamilyId}.", drift.SourcePath));
        }

        return checks;
    }

    private static void AddRequiredSurfaceChecks(List<RawAbiCheck> checks, FamilyConfig config, CSharpEvidence evidence, RequiredSurface required)
    {
        if (!config.FamilyId.Equals("sdl2-core", StringComparison.OrdinalIgnoreCase) || required.Status != SourceStatus.Present)
        {
            return;
        }

        var generatedFunctions = evidence.Functions
            .Where(IsNativeImport)
            .Select(function => function.ManagedName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var requiredFunction in required.RequiredFunctions.Where(requiredFunction => !generatedFunctions.Contains(requiredFunction)).Order(StringComparer.Ordinal))
        {
            checks.Add(HardBug("required-function-missing", requiredFunction, "Manifest-required SDL2 function is absent from ClangSharp generated evidence.", "manifest"));
        }

        var generatedConstants = evidence.Constants.Select(constant => constant.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var requiredConstant in required.RequiredConstants.Where(requiredConstant => !generatedConstants.Contains(requiredConstant)).Order(StringComparer.Ordinal))
        {
            checks.Add(HardBug("required-constant-missing", requiredConstant, "Manifest-required SDL2 constant is absent from ClangSharp generated evidence.", "manifest"));
        }
    }

    private static void AddDeferredLayoutChecks(List<RawAbiCheck> checks, CSharpEvidence evidence, string typeName, string checkId, bool includeNestedFields)
    {
        foreach (var type in evidence.Types.Where(type => type.Kind == "Struct" && type.Name == typeName && (type.HasFields || (includeNestedFields && type.HasNestedFields))))
        {
            checks.Add(HardBug(checkId, type.Name, $"{typeName} layout is deferred but ClangSharp emitted fields.", type.SourcePath));
        }
    }

    private static void AddDuplicateTagTypedefChecks(List<RawAbiCheck> checks, CSharpEvidence evidence)
    {
        var declaredStructNames = evidence.Types
            .Where(type => type.Kind == "Struct")
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var emptyStructs = evidence.Types
            .Where(type => type.Kind == "Struct" && !type.HasFields && !type.HasNestedFields)
            .ToArray();

        foreach (var structName in emptyStructs.Select(structEvidence => structEvidence.Name))
        {
            var reported = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (annotationCanonical, sourcePath) in EnumerateMismatchedReferences(structName, evidence.Functions))
            {
                if (declaredStructNames.Contains(annotationCanonical) || !reported.Add(annotationCanonical))
                {
                    continue;
                }

                checks.Add(new RawAbiCheck(
                    "duplicate-tag-typedef",
                    "warning",
                    "Compatibility Risk",
                    structName,
                    $"Struct '{structName}' is referenced via [NativeTypeName(\"{annotationCanonical} *\")] - tag/typedef pair detected; " +
                    "canonicalize via per-header RSP --remap (Constitution section \"Opaque Handles\").",
                    sourcePath));
            }
        }
    }

    private static IEnumerable<(string AnnotationCanonical, string SourcePath)> EnumerateMismatchedReferences(string structName, IReadOnlyList<FunctionEvidence> functions)
    {
        foreach (var function in functions)
        {
            if (ReferencesManagedType(function.ReturnType, structName))
            {
                var canonical = TryCanonicalizeNativeTypeName(function.ReturnNativeTypeName);

                if (canonical is not null && !canonical.Equals(structName, StringComparison.Ordinal))
                {
                    yield return (canonical, function.SourcePath);
                }
            }

            foreach (var parameter in function.Parameters)
            {
                if (!ReferencesManagedType(parameter.Type, structName))
                {
                    continue;
                }

                var canonical = TryCanonicalizeNativeTypeName(parameter.NativeTypeName);

                if (canonical is not null && !canonical.Equals(structName, StringComparison.Ordinal))
                {
                    yield return (canonical, function.SourcePath);
                }
            }
        }
    }

    private static bool ReferencesManagedType(string managedType, string structName)
        => StripPointerAndWhitespace(managedType).Equals(structName, StringComparison.Ordinal);

    private static string? TryCanonicalizeNativeTypeName(string nativeTypeName)
    {
        if (nativeTypeName.Length == 0)
        {
            return null;
        }

        var canonical = nativeTypeName
            .Replace("const ", "", StringComparison.Ordinal)
            .Replace("struct ", "", StringComparison.Ordinal);
        canonical = StripPointerAndWhitespace(canonical);
        return canonical.Length == 0 ? null : canonical;
    }

    private static string StripPointerAndWhitespace(string text)
        => text.Replace("*", "", StringComparison.Ordinal).Trim();

    private static bool IsExpectedRawClass(FamilyConfig config, TypeEvidence type)
        => type.Kind == "Class"
            && type.Name == config.ExpectedRawClassName;

    private static bool IsRawImport(FamilyConfig config, FunctionEvidence function, HashSet<(string NamespaceName, string ContainingTypePath)> publicRawContainers)
        => IsNativeImport(function)
            && function.ContainingType == config.ExpectedRawClassName
            && publicRawContainers.Contains((function.NamespaceName, function.ContainingTypePath));

    private static bool IsEffectivelyPublic(string accessibility, IReadOnlyList<string> containingTypeAccessibilities)
        => accessibility == "public"
            && containingTypeAccessibilities.All(accessibility => accessibility == "public");

    private static bool IsNativeImport(FunctionEvidence function)
        => function.ImportKind.Length > 0;

    private static bool IsClongDualDispatchHelper(FunctionEvidence function)
        => function.Accessibility == "private"
            && (function.ManagedName.EndsWith("_Win32", StringComparison.Ordinal)
                || function.ManagedName.EndsWith("_Unix64", StringComparison.Ordinal));

    private static bool IsPlatformSensitiveLong(string nativeTypeName, string managedType)
        => (nativeTypeName == "long" && managedType == "int")
            || (nativeTypeName == "unsigned long" && managedType == "uint");

    private static bool IsPlatformSensitiveWchar(string nativeTypeName, string managedType)
        => nativeTypeName.Contains("wchar_t", StringComparison.Ordinal) && managedType == "ushort*";

    private static RawAbiCheck HardBug(string checkId, string symbol, string message, string sourcePath)
        => new(checkId, "error", "Hard Bug", symbol, message, sourcePath);
}
