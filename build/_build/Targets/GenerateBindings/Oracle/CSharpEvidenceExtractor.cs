internal static class CSharpEvidenceExtractor
{
    public static CSharpEvidence Extract(string path, string source, string[] preprocessorSymbols)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithPreprocessorSymbols(preprocessorSymbols),
            path: path);
        var root = syntaxTree.GetCompilationUnitRoot();

        var functions = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(CreateFunctionEvidence)
            .ToArray();

        var fields = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(IsConstantLikeField)
            .SelectMany(CreateFieldEvidence);
        var properties = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(property => property.ExpressionBody is not null)
            .Select(CreatePropertyEvidence);
        var constants = fields.Concat(properties).ToArray();

        var types = root.DescendantNodes()
            .Where(node => node is ClassDeclarationSyntax or StructDeclarationSyntax or EnumDeclarationSyntax)
            .Select(CreateTypeEvidence)
            .ToArray();

        return new CSharpEvidence(functions, constants, types);
    }

    private static FunctionEvidence CreateFunctionEvidence(MethodDeclarationSyntax method)
    {
        var import = FindImportAttribute(method);
        return new FunctionEvidence(
            method.Identifier.ValueText,
            import.NativeEntryPoint.Length == 0 ? method.Identifier.ValueText : import.NativeEntryPoint,
            import.NativeLibrary,
            import.Kind,
            GetAccessibility(method.Modifiers),
            GetContainingType(method),
            GetContainingTypePath(method),
            GetContainingTypeAccessibilities(method),
            GetNamespaceName(method),
            method.SyntaxTree.FilePath,
            method.ReturnType.ToString(),
            GetReturnNativeTypeName(method),
            method.ParameterList.Parameters.Select(CreateParameterEvidence).ToArray());
    }

    private static ParameterEvidence CreateParameterEvidence(ParameterSyntax parameter)
        => new(
            parameter.Identifier.ValueText,
            parameter.Type?.ToString() ?? "",
            GetNativeTypeName(parameter.AttributeLists));

    private static ConstantEvidence CreatePropertyEvidence(PropertyDeclarationSyntax property)
        => new(
            property.Identifier.ValueText,
            "Property",
            GetContainingType(property),
            GetNamespaceName(property),
            property.SyntaxTree.FilePath);

    private static IEnumerable<ConstantEvidence> CreateFieldEvidence(FieldDeclarationSyntax field)
    {
        foreach (var variable in field.Declaration.Variables)
        {
            yield return new ConstantEvidence(
                variable.Identifier.ValueText,
                "Field",
                GetContainingType(field),
                GetNamespaceName(field),
                field.SyntaxTree.FilePath);
        }
    }

    private static TypeEvidence CreateTypeEvidence(SyntaxNode type)
    {
        return type switch
        {
            ClassDeclarationSyntax @class => new TypeEvidence(
                @class.Identifier.ValueText,
                "Class",
                GetAccessibility(@class.Modifiers),
                GetContainingTypePath(@class),
                GetContainingTypeAccessibilities(@class),
                GetNamespaceName(@class),
                @class.SyntaxTree.FilePath,
                HasDirectFields(@class),
                HasNestedFields(@class)),
            StructDeclarationSyntax structure => new TypeEvidence(
                structure.Identifier.ValueText,
                "Struct",
                GetAccessibility(structure.Modifiers),
                GetContainingTypePath(structure),
                GetContainingTypeAccessibilities(structure),
                GetNamespaceName(structure),
                structure.SyntaxTree.FilePath,
                HasDirectFields(structure),
                HasNestedFields(structure)),
            EnumDeclarationSyntax enumeration => new TypeEvidence(
                enumeration.Identifier.ValueText,
                "Enum",
                GetAccessibility(enumeration.Modifiers),
                GetContainingTypePath(enumeration),
                GetContainingTypeAccessibilities(enumeration),
                GetNamespaceName(enumeration),
                enumeration.SyntaxTree.FilePath,
                false,
                false),
            _ => throw new InvalidOperationException("Unsupported type syntax node.")
        };
    }

    private static bool HasDirectFields(TypeDeclarationSyntax type)
        => type.Members.OfType<FieldDeclarationSyntax>().Any();

    private static bool HasNestedFields(TypeDeclarationSyntax type)
        => type.Members.OfType<TypeDeclarationSyntax>().Any(nested => HasDirectFields(nested) || HasNestedFields(nested));

    private static bool IsConstantLikeField(FieldDeclarationSyntax field)
    {
        var modifiers = field.Modifiers;
        return modifiers.Any(SyntaxKind.ConstKeyword)
            || (modifiers.Any(SyntaxKind.StaticKeyword) && modifiers.Any(SyntaxKind.ReadOnlyKeyword));
    }

    private static ImportEvidence FindImportAttribute(MethodDeclarationSyntax method)
    {
        foreach (var attribute in method.AttributeLists.SelectMany(list => list.Attributes))
        {
            var attributeName = attribute.Name.ToString();
            if (!IsImportAttribute(attributeName, out var importKind))
            {
                continue;
            }

            return new ImportEvidence(
                importKind,
                GetImportLibraryArgument(attribute, method),
                GetImportEntryPoint(attribute, method.Identifier.ValueText));
        }

        return new ImportEvidence("", "", "");
    }

    private static bool IsImportAttribute(string attributeName, out string importKind)
    {
        if (MatchesAttribute(attributeName, "DllImport"))
        {
            importKind = "DllImport";
            return true;
        }

        if (MatchesAttribute(attributeName, "LibraryImport"))
        {
            importKind = "LibraryImport";
            return true;
        }

        importKind = "";
        return false;
    }

    private static bool MatchesAttribute(string attributeName, string simpleName)
        => attributeName == simpleName
            || attributeName == simpleName + "Attribute"
            || attributeName.EndsWith("." + simpleName, StringComparison.Ordinal)
            || attributeName.EndsWith("." + simpleName + "Attribute", StringComparison.Ordinal);

    private static string GetReturnNativeTypeName(MethodDeclarationSyntax method)
        => GetNativeTypeName(method.AttributeLists.Where(list => list.Target?.Identifier.IsKind(SyntaxKind.ReturnKeyword) == true));

    private static string GetNativeTypeName(IEnumerable<AttributeListSyntax> attributeLists)
    {
        foreach (var attribute in attributeLists.SelectMany(list => list.Attributes))
        {
            if (MatchesAttribute(attribute.Name.ToString(), "NativeTypeName")
                && attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }

        return "";
    }

    private static string GetImportLibraryArgument(AttributeSyntax attribute, MethodDeclarationSyntax method)
    {
        var expression = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            IdentifierNameSyntax identifier => ResolveStringConstant(method, identifier.Identifier.ValueText),
            _ => ""
        };
    }

    private static string GetImportEntryPoint(AttributeSyntax attribute, string managedName)
    {
        foreach (var argument in attribute.ArgumentList?.Arguments ?? [])
        {
            if (argument.NameEquals?.Name.Identifier.ValueText == "EntryPoint"
                && argument.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }

        return managedName;
    }

    private static string ResolveStringConstant(MethodDeclarationSyntax method, string identifierName)
    {
        foreach (var type in method.Ancestors().OfType<TypeDeclarationSyntax>())
        {
            foreach (var field in type.Members.OfType<FieldDeclarationSyntax>())
            {
                if (!field.Modifiers.Any(SyntaxKind.ConstKeyword) || field.Declaration.Type.ToString() != "string")
                {
                    continue;
                }

                foreach (var variable in field.Declaration.Variables)
                {
                    if (variable.Identifier.ValueText == identifierName
                        && variable.Initializer?.Value is LiteralExpressionSyntax literal
                        && literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        return literal.Token.ValueText;
                    }
                }
            }
        }

        return "";
    }

    private static string GetAccessibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return "public";
        }

        if (modifiers.Any(SyntaxKind.InternalKeyword))
        {
            return "internal";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.PrivateKeyword))
        {
            return "private protected";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.InternalKeyword))
        {
            return "protected internal";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword))
        {
            return "protected";
        }

        if (modifiers.Any(SyntaxKind.PrivateKeyword))
        {
            return "private";
        }

        return "";
    }

    private static string GetContainingType(SyntaxNode node)
        => node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "";

    private static string GetContainingTypePath(SyntaxNode node)
    {
        var containingTypes = node.Ancestors().OfType<TypeDeclarationSyntax>().Reverse();
        if (node is TypeDeclarationSyntax type)
        {
            containingTypes = containingTypes.Append(type);
        }

        return string.Join(".", containingTypes.Select(type => type.Identifier.ValueText));
    }

    private static IReadOnlyList<string> GetContainingTypeAccessibilities(SyntaxNode node)
        => node.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(type => GetAccessibility(type.Modifiers)).ToArray();

    private static string GetNamespaceName(SyntaxNode node)
        => string.Join(".", node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(ns => ns.Name.ToString()));

    private readonly record struct ImportEvidence(string Kind, string NativeLibrary, string NativeEntryPoint);
}
